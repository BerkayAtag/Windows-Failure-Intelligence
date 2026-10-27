using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WFI.Core;
namespace WFI.Infrastructure;

public static class CampaignCoordinator
{
    // Caller owns transaction. Row lock orders all dispatch/state decisions across API nodes.
    public static async Task<RepairCampaign?> RefreshLockedAsync(WfiDb db,Guid id,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("Campaign transaction required");
        if(await db.RepairCampaigns.Where(c=>c.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.LastCheckedAt,DateTime.UtcNow),ct)==0)return null;
        var campaign=await db.RepairCampaigns.AsNoTracking().SingleAsync(c=>c.Id==id,ct);
        var status=campaign.Status;var now=DateTime.UtcNow;
        if(status is "Cancelled" or "Completed" or "Expired")return campaign;
        if(campaign.ExpiresAt<=now||campaign.EndsAt<=now)status="Expired";
        else if(status is "Pilot" or "Expanded")
        {
            var jobs=db.RemediationExecutions.Where(x=>x.CampaignId==id);
            // A missing receipt is uncertain even before that particular device next reconnects.
            if(await jobs.AnyAsync(x=>x.Status=="Failed"||x.Status=="Unknown"||(x.Status=="Executing"&&x.LeaseUntil<=now),ct))status="Paused";
            else if(status=="Pilot"&&await jobs.AnyAsync(x=>x.CampaignPilot,ct)&&!await jobs.AnyAsync(x=>x.CampaignPilot&&x.Status!="Success",ct))
                status=await jobs.AnyAsync(x=>!x.CampaignPilot,ct)?"AwaitingExpansion":"Completed";
            else if(status=="Expanded"&&!await jobs.AnyAsync(x=>x.Status!="Success",ct))status="Completed";
        }
        if(status!=campaign.Status)
        {
            await db.RepairCampaigns.Where(c=>c.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.Status,status),ct);
            db.Audit.Add(new() { Domain=campaign.Domain,Actor="campaign-supervisor",Action="campaign."+status.ToLowerInvariant(),Target=id.ToString()});
            if(status=="Expired")await db.RemediationExecutions.Where(x=>x.CampaignId==id&&x.Status=="Approved")
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"Cancelled"),ct);
            await db.SaveChangesAsync(ct);campaign.Status=status;
        }
        return campaign;
    }
    public static async Task<bool> CanDispatchAsync(WfiDb db,RemediationExecution job,Device device,CancellationToken ct)
    {
        if(job.CampaignId==null)return true;
        var campaign=await RefreshLockedAsync(db,job.CampaignId.Value,ct);var now=DateTime.UtcNow;
        if(campaign==null||campaign.Domain!=device.Domain||now<campaign.StartsAt||now>=campaign.EndsAt||now>=campaign.ExpiresAt||
            !(campaign.Status=="Expanded"||(campaign.Status=="Pilot"&&job.CampaignPilot)))return false;
        if(!await new AgentCoordinator(db).AutomaticEnabledAsync(device,ct))return false;
        return await db.RemediationExecutions.CountAsync(x=>x.CampaignId==campaign.Id&&x.Status=="Executing",ct)<campaign.MaxParallel;
    }
}

public sealed class CampaignSupervisor(IServiceScopeFactory scopes,ILogger<CampaignSupervisor> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
                var ids=await db.RepairCampaigns.Where(c=>c.Status!="Completed"&&c.Status!="Cancelled"&&c.Status!="Expired")
                    .OrderBy(c=>c.LastCheckedAt).ThenBy(c=>c.CreatedAt).Select(c=>c.Id).Take(100).ToListAsync(ct);
                foreach(var id in ids)
                {
                    await using var tx=await db.Database.BeginTransactionAsync(ct);
                    await CampaignCoordinator.RefreshLockedAsync(db,id,ct);await tx.CommitAsync(ct);
                }
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("Campaign refresh failed ({Type})",ex.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(5),ct);
        }
    }
}

