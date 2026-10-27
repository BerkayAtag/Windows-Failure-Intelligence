using Microsoft.EntityFrameworkCore;
using WFI.Core;
namespace WFI.Infrastructure;

public sealed class RemediationCoordinator(WfiDb db, AgentCoordinator agents)
{
    public async Task<RemediationAssignment?> ClaimAsync(Device device, CancellationToken ct)
    {
        var now=DateTime.UtcNow;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        // Serialize dispatches per device across API nodes and against revocation.
        var active=await db.Devices.Where(d=>d.Id==device.Id&&d.Status!="revoked")
            .ExecuteUpdateAsync(s=>s.SetProperty(d=>d.Status,d=>d.Status),ct);
        if(active==0)return null;
        device=await db.Devices.AsNoTracking().SingleAsync(d=>d.Id==device.Id,ct);
        // Expired executions are not automatically dispatched twice.
        await db.RemediationExecutions.Where(x=>x.DeviceId==device.Id&&x.Status=="Executing"&&x.LeaseUntil<now)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"Unknown").SetProperty(x=>x.Error,"Ajan sonucu alınamadı; yeniden çalıştırmadan önce cihazı kontrol edin."),ct);
        if(await db.RemediationExecutions.AnyAsync(x=>x.DeviceId==device.Id&&x.Status=="Executing",ct))return null;
        var automatic=await agents.AutomaticEnabledAsync(device,ct);
        var supported=RemediationEngine.Catalog.Select(p=>p.Code).ToArray();
        var automaticCodes=RemediationEngine.Catalog.Where(p=>p.AllowAutomatic).Select(p=>p.Code).ToArray();
        // Filter before limiting: disabled automatic or obsolete catalog jobs must not starve eligible work.
        var candidates=await db.RemediationExecutions.AsNoTracking().Where(x=>x.DeviceId==device.Id&&x.Status=="Approved"&&
                        (x.CampaignId==null||(automatic&&db.RepairCampaigns.Any(c=>c.Id==x.CampaignId&&c.Domain==device.Domain&&c.StartsAt<=now&&c.EndsAt>now&&c.ExpiresAt>now&&
                (c.Status=="Expanded"||(c.Status=="Pilot"&&x.CampaignPilot))&&
                db.RemediationExecutions.Count(e=>e.CampaignId==c.Id&&e.Status=="Executing")<c.MaxParallel)))&&
            supported.Contains(x.PlaybookCode)&&(!x.Automatic||(automatic&&automaticCodes.Contains(x.PlaybookCode)))&&
            db.Playbooks.Any(p=>p.Id==x.PlaybookId&&p.Code==x.PlaybookCode&&p.Version==RemediationEngine.Version&&p.IsApproved))
            .OrderBy(x=>x.RequestedAt).ThenBy(x=>x.Id).Take(10).ToListAsync(ct);
        foreach(var job in candidates)
        {
            var definition=RemediationEngine.Find(job.PlaybookCode);
            if(definition==null)continue;
            if(!await db.Playbooks.AnyAsync(p=>p.Id==job.PlaybookId&&p.Code==job.PlaybookCode&&p.Version==RemediationEngine.Version&&p.IsApproved,ct))continue;
            if(job.Automatic&&(!definition.AllowAutomatic||!await agents.AutomaticEnabledAsync(device,ct)))continue;
            if(!await CampaignCoordinator.CanDispatchAsync(db,job,device,ct))continue;
            if(!await BigFixVerificationWorker.BaselineReadyAsync(db,job,device,ct))continue;
            var token=Guid.NewGuid();var expiry=now.AddSeconds(definition.TimeoutSeconds+300);
            var changed=await db.RemediationExecutions.Where(x=>x.Id==job.Id&&x.Status=="Approved")
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"Executing").SetProperty(x=>x.ExecutedAt,now)
                    .SetProperty(x=>x.LeaseToken,token).SetProperty(x=>x.LeaseUntil,expiry),ct);
            if(changed==1){await tx.CommitAsync(ct);return new(job.Id,token,job.PlaybookCode,RemediationEngine.Version,expiry);}
        }
        await tx.CommitAsync(ct);return null;
    }
    public async Task<bool> CompleteAsync(Device device, Guid id, RemediationReceipt receipt, CancellationToken ct)
    {
        if(receipt.Precheck==null||receipt.Output==null||receipt.Verification==null||receipt.Precheck.Length>4096||receipt.Output.Length>16384||receipt.Verification.Length>4096)return false;
        var job=await db.RemediationExecutions.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id&&x.DeviceId==device.Id&&x.LeaseToken==receipt.LeaseToken,ct);
        if(job==null)return false;
        if(job.Status is "Success" or "Failed")return true;
        if(job.Status is not ("Executing" or "Unknown"))return false;
        var pre=LogAnalyzer.Redact(receipt.Precheck);var output=LogAnalyzer.Redact(receipt.Output);var verification=LogAnalyzer.Redact(receipt.Verification);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var changed=await db.RemediationExecutions.Where(x=>x.Id==id&&x.DeviceId==device.Id&&x.LeaseToken==receipt.LeaseToken&&(x.Status=="Executing"||x.Status=="Unknown"))
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,receipt.Success?"Success":"Failed").SetProperty(x=>x.PrecheckResult,pre)
                .SetProperty(x=>x.ExecutionOutput,output).SetProperty(x=>x.ValidationResult,verification).SetProperty(x=>x.CompletedAt,DateTime.UtcNow),ct);
        if(changed==0)return false;
        db.Audit.Add(new() { Domain=device.Domain,Actor="agent:"+device.Id,Action="remediation.result",Target=id.ToString()});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
}

