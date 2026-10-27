using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WFI.Core;
namespace WFI.Infrastructure;

public sealed class BigFixVerificationWorker(IServiceScopeFactory scopes,IBigFixConnector connector,ILogger<BigFixVerificationWorker> logger):BackgroundService
{
    public static async Task<bool> BaselineReadyAsync(WfiDb db,RemediationExecution job,Device device,CancellationToken ct)
    {
        if(job.PlaybookCode!="PB-BIGFIX-REPAIR")return true;
        var mapping=await db.BigFixMappings.AsNoTracking().SingleOrDefaultAsync(m=>m.DeviceId==device.Id&&m.Domain==device.Domain,ct);
        if(mapping==null)return true; // Unmapped devices retain local repair, explicitly without central verification.
        var row=await db.BigFixVerifications.AsNoTracking().SingleOrDefaultAsync(v=>v.ExecutionId==job.Id,ct);
        if(row!=null)return row.BaselineReady;
        db.BigFixVerifications.Add(new(){ExecutionId=job.Id,DeviceId=device.Id,Domain=device.Domain,ComputerId=mapping.ComputerId,
            ConnectorFingerprint=mapping.ConnectorFingerprint,ExpectedDnsName=mapping.VerifiedDnsName});
        await db.SaveChangesAsync(ct);return false;
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try {for(var i=0;i<5;i++)if(!await ProcessOneAsync(ct))break;}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("BigFix verification failed ({Type})",ex.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(15),ct);
        }
    }
    public async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var now=DateTime.UtcNow;
        var row=await db.BigFixVerifications.AsNoTracking().Where(v=>v.NextCheckAt<=now&&
            (v.Status=="PendingBaseline"||v.Status=="BaselineCaptured"||v.Status=="AwaitingReport"||v.Status=="ConnectorUnavailable"))
            .OrderBy(v=>v.NextCheckAt).FirstOrDefaultAsync(ct);
        if(row==null)return false;
        var token=Guid.NewGuid();
        if(await db.BigFixVerifications.Where(v=>v.ExecutionId==row.ExecutionId&&v.NextCheckAt<=now)
            .ExecuteUpdateAsync(s=>s.SetProperty(v=>v.FetchToken,token).SetProperty(v=>v.NextCheckAt,now.AddMinutes(1)),ct)==0)return true;
        var job=await db.RemediationExecutions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==row.ExecutionId&&x.Domain==row.Domain&&x.DeviceId==row.DeviceId,ct);
        var status=row.Status;var ready=row.BaselineReady;var before=row.BeforeReportAt;var latest=row.LatestReportAt;var observed=row.ObservedAt;
        if(job==null||job.Status is "Cancelled" or "Failed" or "Unknown") {status="LocalNotVerified";ready=true;}
        else if(!ready&&job.Status!="Approved") {status="BaselineUnavailable";ready=true;} // Never mislabel a post-dispatch observation as a pre-repair baseline.
        else if(ready&&job.Status!="Success")return true; // Wait for local completion without contacting BigFix.
        else if(ready&&job.CompletedAt.HasValue&&now>=job.CompletedAt.Value.AddMinutes(15))status="TimedOut";
        else
        {
            try
            {
                if(!connector.Configured||connector.Fingerprint!=row.ConnectorFingerprint)throw new InputRejectedException("Connector changed/unavailable");
                var computer=await connector.ReadAsync(row.ComputerId,ct);
                if(computer.DnsName!=row.ExpectedDnsName)throw new InputRejectedException("Identity changed");
                observed=computer.ObservedAt;latest=computer.LastReportAt;
                if(!ready){before=latest;ready=true;status="BaselineCaptured";}
                else status=before.HasValue&&latest>before&&latest>=job.ExecutedAt?"Verified":"AwaitingReport";
            }
            catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException or InputRejectedException or System.Xml.XmlException or InvalidDataException)
            {
                if(ct.IsCancellationRequested)throw;
                if(!ready){ready=true;status="BaselineUnavailable";}else status="ConnectorUnavailable";
            }
        }
        await db.BigFixVerifications.Where(v=>v.ExecutionId==row.ExecutionId&&v.FetchToken==token)
            .ExecuteUpdateAsync(s=>s.SetProperty(v=>v.Status,status).SetProperty(v=>v.BaselineReady,ready).SetProperty(v=>v.BeforeReportAt,before)
                .SetProperty(v=>v.LatestReportAt,latest).SetProperty(v=>v.ObservedAt,observed),ct);
        return true;
    }
}
