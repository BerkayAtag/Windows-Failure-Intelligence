using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WFI.Core;
using WFI.Infrastructure;

public static class AgentEndpoints
{
    public static async Task AuthenticateAgentAsync(HttpContext c, RequestDelegate next)
    {
        if(c.Request.Path.StartsWithSegments("/api/v1/agent/me"))
        {
            var header=c.Request.Headers.Authorization.ToString();
            if(!Guid.TryParse(c.Request.Headers["X-WFI-Device"],out var id)||!header.StartsWith("Bearer ",StringComparison.Ordinal))
            {c.Response.StatusCode=401;return;}
            var device=await c.RequestServices.GetRequiredService<AgentCoordinator>().AuthenticateAsync(id,header[7..],c.RequestAborted);
            if(device==null){c.Response.StatusCode=401;return;}
            c.Items["agent-device"]=device;
        }
        await next(c);
    }
    public static void MapAgentEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/devices/{id:guid}/activity",async(Guid id,HttpContext c,WfiDb db)=>
        {
            var domain=c.User.FindFirstValue("domain")!;
            if(!await db.Devices.AnyAsync(d=>d.Id==id&&d.Domain==domain,c.RequestAborted))return Results.NotFound();
            var diagnostics=await db.DiagnosticJobs.AsNoTracking().Where(j=>j.DeviceId==id&&j.Domain==domain).OrderByDescending(j=>j.CreatedAt).Take(25)
                .Select(j=>new{j.Id,j.Status,j.ScanType,j.CreatedAt,j.Error}).ToListAsync(c.RequestAborted);
            var remediations=await db.RemediationExecutions.AsNoTracking().Where(j=>j.DeviceId==id&&j.Domain==domain).OrderByDescending(j=>j.RequestedAt).Take(25)
                .Select(j=>new{j.Id,j.PlaybookCode,j.PlaybookTitle,j.Status,j.Automatic,j.RequestedAt,j.Error,j.ExecutionOutput,j.ValidationResult}).ToListAsync(c.RequestAborted);
            return Results.Ok(new{diagnostics,remediations});
        }).RequireAuthorization();
        api.MapGet("/automation", async (HttpContext c, WfiDb db) =>
        {
            var domain=c.User.FindFirstValue("domain")!;
            return Results.Ok(await db.AutomationPolicies.AsNoTracking().SingleOrDefaultAsync(p=>p.Domain==domain,c.RequestAborted) ?? new AutomationPolicy{Domain=domain});
        }).RequireAuthorization("Admin");
        api.MapPut("/automation", async (AutomationUpdate input, HttpContext c, WfiDb db) =>
        {
            var domain=c.User.FindFirstValue("domain")!;
            await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
            var policy=new AutomationPolicy{Domain=domain,Enabled=input.Enabled,UpdatedAt=DateTime.UtcNow,UpdatedBy=c.User.Identity!.Name!};
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AutomationPolicies" ("Domain", "Enabled", "UpdatedAt", "UpdatedBy")
                VALUES ({policy.Domain}, {policy.Enabled}, {policy.UpdatedAt}, {policy.UpdatedBy})
                ON CONFLICT ("Domain") DO UPDATE SET "Enabled" = excluded."Enabled",
                    "UpdatedAt" = excluded."UpdatedAt", "UpdatedBy" = excluded."UpdatedBy"
                """,c.RequestAborted);
            if(input.ApplyToAll) await db.Devices.Where(d=>d.Domain==domain).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.AutomationMode,"Inherit"),c.RequestAborted);
            db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!,Actor=policy.UpdatedBy,Action=input.ApplyToAll?"automation.all-devices":"automation.default",Target=domain+":"+input.Enabled});
            await db.SaveChangesAsync(c.RequestAborted);await tx.CommitAsync(c.RequestAborted);return Results.Ok(policy);
        }).RequireAuthorization("Admin");
        api.MapPut("/devices/{id:guid}/automation",async(Guid id,DeviceAutomationUpdate input,HttpContext c,WfiDb db)=>
        {
            if(input.Mode is not ("Inherit" or "Enabled" or "Disabled"))return Results.BadRequest();
            var domain=c.User.FindFirstValue("domain")!;
            await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
            var changed=await db.Devices.Where(d=>d.Id==id&&d.Domain==domain).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.AutomationMode,input.Mode),c.RequestAborted);
            if(changed==0)return Results.NotFound();
            db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!,Actor=c.User.Identity!.Name!,Action="automation.device",Target=id+":"+input.Mode});
            await db.SaveChangesAsync(c.RequestAborted);await tx.CommitAsync(c.RequestAborted);return Results.Ok(new{mode=input.Mode});
        }).RequireAuthorization("Admin");
        api.MapPost("/devices/register", async (RegisterDeviceRequest input, HttpContext c, WfiDb db) =>
        {
            var host = input.Hostname?.Trim().ToLowerInvariant() ?? "";
            if (!Regex.IsMatch(host, @"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")) return Results.BadRequest();
            var domain = c.User.FindFirstValue("domain")!;
            await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
            await DiagnosticAdmission.LockAsync(db,domain,c.RequestAborted);
            var token = AgentCoordinator.NewSecret();
            var existing = await db.Devices.AsNoTracking().Where(d => d.Domain == domain && d.Hostname == host).Select(d => new { d.Id, d.Status, d.AgentCredentialHash, d.EnrollmentExpiresAt }).FirstOrDefaultAsync(c.RequestAborted);
            if (existing != null)
            {
                // A revoked device or an expired, never-used enrollment may be re-registered under the same identity row.
                // A fresh pending registration or an enrolled agent is never replaced silently.
                var now = DateTime.UtcNow;
                if (existing.Status != "revoked" && !(existing.Status == "pending" && existing.AgentCredentialHash == null && (existing.EnrollmentExpiresAt == null || existing.EnrollmentExpiresAt <= now)))
                    return Results.Conflict(new { error = "Bu makine zaten kayıtlı. Bekleyen kayıt için konsoldan token yenileyin veya cihazı önce iptal edin." });
                var expiry = now.AddHours(1);
                var reused = await db.Devices.Where(d => d.Id == existing.Id && d.Domain == domain && d.Status == existing.Status && d.AgentCredentialHash == existing.AgentCredentialHash)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, "pending").SetProperty(d => d.AgentCredentialHash, (string?)null)
                        .SetProperty(d => d.EnrollmentToken, AgentCoordinator.Hash(token)).SetProperty(d => d.EnrollmentExpiresAt, expiry).SetProperty(d => d.NextDiagnosticAt, (DateTime?)null), c.RequestAborted);
                if (reused != 1) return Results.Conflict();
                db.Audit.Add(new() { Domain=domain, Actor=c.User.Identity!.Name!, Action="device.reregistered", Target=existing.Id.ToString() });
                await db.SaveChangesAsync(c.RequestAborted);
                await tx.CommitAsync(c.RequestAborted);
                return Results.Ok(new { existing.Id, Hostname=host, Domain=domain, enrollmentToken=token, EnrollmentExpiresAt=expiry });
            }
            var device = new Device { Domain=domain, Hostname=host, Status="pending", OsVersion="", AgentVersion="",
                EnrollmentToken=AgentCoordinator.Hash(token), EnrollmentExpiresAt=DateTime.UtcNow.AddHours(1) };
            db.Devices.Add(device); db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!, Actor=c.User.Identity!.Name!, Action="device.registered", Target=device.Id.ToString() });
            await db.SaveChangesAsync(c.RequestAborted);
            await tx.CommitAsync(c.RequestAborted);
            return Results.Ok(new { device.Id, device.Hostname, device.Domain, enrollmentToken=token, device.EnrollmentExpiresAt });
        }).RequireAuthorization("Admin");

        api.MapPost("/devices/{id:guid}/enrollment",async(Guid id,HttpContext c,WfiDb db)=>
        {
            var domain=c.User.FindFirstValue("domain")!;var token=AgentCoordinator.NewSecret();var expiry=DateTime.UtcNow.AddHours(1);
            // Renewal is for a not-yet-enrolled device only; active credentials cannot be replaced silently.
            var changed=await db.Devices.Where(d=>d.Id==id&&d.Domain==domain&&d.Status=="pending"&&d.AgentCredentialHash==null)
                .ExecuteUpdateAsync(s=>s.SetProperty(d=>d.EnrollmentToken,AgentCoordinator.Hash(token)).SetProperty(d=>d.EnrollmentExpiresAt,expiry),c.RequestAborted);
            if(changed==0)return Results.Conflict();
            db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!,Actor=c.User.Identity!.Name!,Action="device.enrollment.renewed",Target=id.ToString()});await db.SaveChangesAsync(c.RequestAborted);
            return Results.Ok(new{id,enrollmentToken=token,enrollmentExpiresAt=expiry});
        }).RequireAuthorization("Admin");

        api.MapPost("/devices/{id:guid}/revoke", async (Guid id, HttpContext c, WfiDb db) =>
        {
            var domain = c.User.FindFirstValue("domain")!;
            await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
            var changed = await db.Devices.Where(d => d.Id == id && d.Domain == domain)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, "revoked").SetProperty(d => d.AgentCredentialHash, (string?)null)
                    .SetProperty(d => d.EnrollmentToken, (string?)null), c.RequestAborted);
            if (changed == 0) return Results.NotFound();
            // A revoked agent can never deliver evidence or run work: release its queue capacity and unstarted repairs.
            var now=DateTime.UtcNow;
            await db.DiagnosticJobs.Where(j=>j.DeviceId==id&&j.Domain==domain&&(j.Status=="pending"||j.Status=="running"))
                .ExecuteUpdateAsync(s=>s.SetProperty(j=>j.Status,"failed").SetProperty(j=>j.Error,"Cihaz iptal edildi.").SetProperty(j=>j.CompletedAt,now),c.RequestAborted);
            await db.RemediationExecutions.Where(r=>r.DeviceId==id&&r.Domain==domain&&(r.Status=="Approved"||r.Status=="PendingApproval"))
                .ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"Cancelled").SetProperty(r=>r.CompletedAt,now).SetProperty(r=>r.Error,"Cihaz iptal edildiği için başlamadan iptal edildi."),c.RequestAborted);
            db.Audit.Add(new() { Domain=domain, Actor=c.User.Identity!.Name!, Action="device.revoked", Target=id.ToString() });
            await db.SaveChangesAsync(c.RequestAborted); await tx.CommitAsync(c.RequestAborted); return Results.Ok();
        }).RequireAuthorization("Admin");

        api.MapPost("/agent/enroll", async (AgentEnrollment input, HttpContext c, AgentCoordinator coordinator) =>
        {
            var result = await coordinator.EnrollAsync(input, c.RequestAborted);
            return result == null ? Results.Unauthorized() : Results.Ok(result);
        }).RequireRateLimiting("agent");

        var agent = api.MapGroup("/agent/me").RequireRateLimiting("agent");
        agent.MapPost("/heartbeat", async (AgentPulse pulse, HttpContext c, WfiDb db, AgentCoordinator coordinator) =>
        {
            if(!ValidPulse(pulse))return Results.BadRequest();
            var automatic=await PulseAsync(pulse,c,db,coordinator);var d=Device(c);
            return Results.Ok(new{d.Id,d.Domain,d.Hostname,automatic});
        });
        agent.MapPost("/check-in",async(AgentCheckIn input,HttpContext c,WfiDb db,AgentCoordinator coordinator,RemediationCoordinator remediations)=>
        {
            if(input.Pulse==null||!ValidPulse(input.Pulse)||input.Capabilities==null||input.Capabilities.Length>8||
                input.Capabilities.Any(x=>x==null||x.Length>64)||!input.Capabilities.Contains("combined-check-in-v1"))return Results.BadRequest();
            var started=System.Diagnostics.Stopwatch.StartNew();
            using var activity=WfiTelemetry.Traces.StartActivity("agent.checkin");
            try {
            await PulseAsync(input.Pulse,c,db,coordinator);
            var diagnostic=await coordinator.ClaimDiagnosticAsync(Device(c).Id,c.RequestAborted);
            // Lease only one kind of work: collection must not consume a remediation lease's execution budget.
            var remediation=diagnostic==null?await remediations.ClaimAsync(Device(c),c.RequestAborted):null;
            return Results.Ok(new AgentCheckInResponse(1,["combined-check-in-v1"],300,diagnostic,remediation));
            } finally {WfiTelemetry.CheckInSeconds.Record(started.Elapsed.TotalSeconds);}
        });
        agent.MapPost("/remediations/claim",async(HttpContext c,RemediationCoordinator coordinator)=>
        {
            var assignment=await coordinator.ClaimAsync(Device(c),c.RequestAborted);
            return assignment==null?Results.NoContent():Results.Ok(assignment);
        });
        agent.MapPost("/remediations/{id:guid}/result",async(Guid id,RemediationReceipt receipt,HttpContext c,RemediationCoordinator coordinator)=>
            await coordinator.CompleteAsync(Device(c),id,receipt,c.RequestAborted)?Results.Ok():Results.Conflict());
        agent.MapPost("/diagnostics/claim", async (HttpContext c, AgentCoordinator coordinator) =>
        {
            var assignment = await coordinator.ClaimDiagnosticAsync(Device(c).Id, c.RequestAborted);
            return assignment == null ? Results.NoContent() : Results.Ok(assignment);
        });
        agent.MapPost("/diagnostics/{id:guid}/evidence", async (Guid id, EvidenceSubmission submission, HttpContext c, WfiDb db) =>
        {
            if (submission.Evidence == null || submission.Evidence.Count > 50 || submission.Evidence.Any(e => e == null || e.Content == null || e.Source == null || e.Category == null || e.Content.Length > 65536 || e.Source.Length > 180 || e.Category.Length > 80) || submission.Evidence.Sum(e => (long)e.Content.Length) > 524288)
                return Results.BadRequest(new { error="Kanıt boyutu sınırı aşıldı." });
            foreach (var item in submission.Evidence) { item.Content = LogAnalyzer.Redact(item.Content); item.Source = LogAnalyzer.Redact(item.Source); }
            var data = JsonSerializer.Serialize(submission.Evidence, AnalysisWorker.JsonOptions);
            var device = Device(c); var now = DateTime.UtcNow;
            await using var tx = await db.Database.BeginTransactionAsync(c.RequestAborted);
            var changed = await db.DiagnosticJobs.Where(j => j.Id == id && j.DeviceId == device.Id && j.Status == "running" && j.LeaseToken == submission.LeaseToken && j.LeaseUntil > now)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "evidence_received").SetProperty(j => j.EvidenceJson, data), c.RequestAborted);
            if (changed == 0) return Results.Conflict();
            db.Audit.Add(new() { Domain=device.Domain, Actor="agent:"+device.Id, Action="diagnostic.evidence", Target=id.ToString() });
            await db.SaveChangesAsync(c.RequestAborted); await tx.CommitAsync(c.RequestAborted);
            return Results.Accepted();
        });
    }

    private static bool ValidPulse(AgentPulse pulse)=>pulse.AgentVersion!=null&&pulse.OsVersion!=null&&pulse.AgentVersion.Length<=64&&pulse.OsVersion.Length<=256;
    private static async Task<bool> PulseAsync(AgentPulse pulse,HttpContext c,WfiDb db,AgentCoordinator coordinator)
    {
            var d = Device(c);
            await db.Devices.Where(x => x.Id == d.Id && x.Status != "revoked").ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastHeartbeat, DateTime.UtcNow).SetProperty(x => x.Status, "online")
                .SetProperty(x => x.AgentVersion, pulse.AgentVersion).SetProperty(x => x.OsVersion, pulse.OsVersion), c.RequestAborted);
            var automatic=await coordinator.AutomaticEnabledAsync(d,c.RequestAborted);
            if(automatic&&(d.NextDiagnosticAt==null||d.NextDiagnosticAt<=DateTime.UtcNow))
            {
                var now=DateTime.UtcNow;
                await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
                await DiagnosticAdmission.LockAsync(db,d.Domain,c.RequestAborted);
                var capacity=await DiagnosticAdmission.HasCapacityAsync(db,d.Domain,c.RequestServices.GetRequiredService<IConfiguration>(),c.RequestAborted);
                var next=capacity?now.AddMinutes(15):now.AddSeconds(60+Random.Shared.Next(0,60));
                var claimed=await db.Devices.Where(x=>x.Id==d.Id&&x.Status!="revoked"&&(x.NextDiagnosticAt==null||x.NextDiagnosticAt<=now))
                    .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.NextDiagnosticAt,next),c.RequestAborted);
                if(capacity&&claimed==1&&!await db.DiagnosticJobs.AnyAsync(j=>j.DeviceId==d.Id&&(j.Status=="pending"||j.Status=="running"||j.Status=="evidence_received"||j.Status=="analyzing"),c.RequestAborted))
                {
                    db.DiagnosticJobs.Add(new(){DeviceId=d.Id,Domain=d.Domain,Hostname=d.Hostname,RequestedBy="automation",ScanType="FullSystemScan"});
                    await db.SaveChangesAsync(c.RequestAborted);
                }
                await tx.CommitAsync(c.RequestAborted);
            }
            return automatic;
    }

    private static Device Device(HttpContext c) => (Device)c.Items["agent-device"]!;
}

