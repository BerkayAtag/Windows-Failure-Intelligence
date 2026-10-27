using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WFI.Core;
using WFI.Infrastructure;

public static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this RouteGroupBuilder api)
    {
        var group=api.MapGroup("/campaigns").RequireAuthorization("Admin");
        group.MapGet("",async(HttpContext c,WfiDb db)=>
        {
            var domain=c.User.FindFirstValue("domain")!;
            return Results.Ok(await db.RepairCampaigns.AsNoTracking().Where(x=>x.Domain==domain).OrderByDescending(x=>x.CreatedAt).Take(50).ToListAsync(c.RequestAborted));
        });
        group.MapPost("/preview",async(CampaignDraft input,HttpContext c,WfiDb db)=>await DraftAsync(input,c,db,false));
        group.MapPost("",async(CampaignDraft input,HttpContext c,WfiDb db)=>await DraftAsync(input,c,db,true));
        group.MapGet("/{id:guid}",async(Guid id,HttpContext c,WfiDb db)=>
        {
            var domain=c.User.FindFirstValue("domain")!;
            var campaign=await db.RepairCampaigns.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.Domain==domain,c.RequestAborted);
            if(campaign==null)return Results.NotFound();
            var jobs=await db.RemediationExecutions.AsNoTracking().Where(x=>x.CampaignId==id&&x.Domain==domain)
                .OrderBy(x=>x.Hostname).Select(x=>new{x.Id,x.DeviceId,x.Hostname,x.Status,x.CampaignPilot,x.ValidationResult,x.Error}).Take(1000).ToListAsync(c.RequestAborted);
            return Results.Ok(new{campaign,jobs});
        });
        group.MapPost("/{id:guid}/command",async(Guid id,CampaignCommand input,HttpContext c,WfiDb db)=>
        {
            var domain=c.User.FindFirstValue("domain")!;var ct=c.RequestAborted;var actor=c.User.Identity!.Name!;
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            // Scope check precedes the lock and any state refresh.
            if(!await db.RepairCampaigns.AnyAsync(x=>x.Id==id&&x.Domain==domain,ct))return Results.NotFound();
            var campaign=(await CampaignCoordinator.RefreshLockedAsync(db,id,ct))!;
            var book=await db.Playbooks.SingleOrDefaultAsync(p=>p.Id==campaign.PlaybookId&&p.IsApproved&&p.Version==RemediationEngine.Version,ct);
            var status=campaign.Status;
            if(input.Action=="start"&&status=="Draft")
            {
                if(book==null||RemediationEngine.Find(book.Code)==null)return Results.Conflict();
                if(book.RiskLevel=="High"&&!input.HighRiskConfirmed)return Results.BadRequest(new{error="Yüksek risk onayı gerekli."});
                var targets=JsonSerializer.Deserialize<Guid[]>(campaign.TargetsJson)!;
                var devices=await db.Devices.Where(d=>targets.Contains(d.Id)&&d.Domain==domain&&d.Status!="revoked").ToListAsync(ct);
                if(devices.Count!=targets.Length)return Results.Conflict(new{error="Hedef cihazlar değişti; yeni önizleme ve kampanya oluşturun."});
                var agents=new AgentCoordinator(db);
                foreach(var device in devices)
                    if(!await agents.AutomaticEnabledAsync(device,ct))return Results.Conflict(new{error="Bir hedefin cihaz politikası toplu onarıma izin vermiyor."});
                var pilots=targets.Take(campaign.PilotLimit).ToHashSet();
                db.RemediationExecutions.AddRange(devices.Select(d=>new RemediationExecution{DeviceId=d.Id,Domain=domain,Hostname=d.Hostname,
                    CampaignId=id,CampaignPilot=pilots.Contains(d.Id),PlaybookId=book.Id,PlaybookCode=book.Code,PlaybookTitle=book.Title,
                    RiskLevel=book.RiskLevel,Status="Approved",RequestedBy=actor,ApprovedBy=actor,Automatic=false}));
                status="Pilot";
                await db.RepairCampaigns.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ApprovedBy,actor),ct);
            }
            else if(input.Action=="expand"&&status=="AwaitingExpansion")
            {
                if(book==null)return Results.Conflict();
                if(book.RiskLevel=="High"&&!input.HighRiskConfirmed)return Results.BadRequest(new{error="Yüksek risk onayı gerekli."});
                status="Expanded";
                await db.RepairCampaigns.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpandedBy,actor),ct);
            }
            else if(input.Action=="pause"&&status is "Pilot" or "Expanded")status="Paused";
            else if(input.Action=="cancel"&&status is not ("Completed" or "Cancelled" or "Expired"))
            {
                status="Cancelled";
                await db.RemediationExecutions.Where(x=>x.CampaignId==id&&x.Status=="Approved").ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"Cancelled"),ct);
            }
            else return Results.Conflict(new{error="Bu durumda işlem uygulanamaz."});
            await db.RepairCampaigns.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,status),ct);
            db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!,Actor=actor,Action="campaign."+input.Action,Target=id.ToString()});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok(new{status});
        });
    }
    private static async Task<IResult> DraftAsync(CampaignDraft input,HttpContext c,WfiDb db,bool save)
    {
        if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>120||input.DeviceIds==null||input.DeviceIds.Length is <1 or >1000||
            input.DeviceIds.Distinct().Count()!=input.DeviceIds.Length||input.PilotLimit is <1 or >5||input.MaxParallel is <1 or >2||
            input.StartsAt>=input.EndsAt||input.EndsAt>input.ExpiresAt||input.ExpiresAt<=DateTimeOffset.UtcNow||input.ExpiresAt>DateTimeOffset.UtcNow.AddDays(30))return Results.BadRequest();
        try {if(string.IsNullOrWhiteSpace(input.TimeZoneId))return Results.BadRequest();TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);}catch(TimeZoneNotFoundException){return Results.BadRequest();}catch(InvalidTimeZoneException){return Results.BadRequest();}
        var domain=c.User.FindFirstValue("domain")!;var ct=c.RequestAborted;
        var devices=await db.Devices.AsNoTracking().Where(d=>input.DeviceIds.Contains(d.Id)&&d.Domain==domain&&d.Status!="revoked").ToListAsync(ct);
        if(devices.Count!=input.DeviceIds.Length)return Results.BadRequest(new{error="Hedef kümesi geçersiz."});
        var book=await db.Playbooks.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==input.PlaybookId&&p.IsApproved&&p.Version==RemediationEngine.Version,ct);
        if(book==null||RemediationEngine.Find(book.Code)==null)return Results.BadRequest();
        var agents=new AgentCoordinator(db);var blocked=new List<Guid>();
        foreach(var d in devices)if(!await agents.AutomaticEnabledAsync(d,ct))blocked.Add(d.Id);
        var offline=devices.Where(d=>d.LastHeartbeat<DateTime.UtcNow.AddMinutes(-20)||d.Status=="pending").Select(d=>d.Id).ToArray();
        if(!save)return Results.Ok(new{targetCount=devices.Count,risk=book.RiskLevel,offline,blocked,pilotCount=Math.Min(input.PilotLimit,devices.Count),input.MaxParallel});
        var campaign=new RepairCampaign{Name=input.Name.Trim(),Domain=domain,PlaybookId=book.Id,PlaybookCode=book.Code,
            TargetsJson=JsonSerializer.Serialize(input.DeviceIds),StartsAt=input.StartsAt.UtcDateTime,EndsAt=input.EndsAt.UtcDateTime,
            ExpiresAt=input.ExpiresAt.UtcDateTime,TimeZoneId=input.TimeZoneId,PilotLimit=input.PilotLimit,MaxParallel=input.MaxParallel,CreatedBy=c.User.Identity!.Name!};
        await using var tx=await db.Database.BeginTransactionAsync(ct);db.RepairCampaigns.Add(campaign);
        db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!,Actor=campaign.CreatedBy,Action="campaign.created",Target=campaign.Id.ToString()});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok(campaign);
    }
}

