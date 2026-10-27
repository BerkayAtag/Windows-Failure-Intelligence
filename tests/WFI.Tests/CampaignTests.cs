using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;
namespace WFI.Tests;

public class CampaignTests
{
    [Theory]
    [InlineData("future")][InlineData("expired")][InlineData("policy")][InlineData("failed")][InlineData("uncertain")]
    public async Task CampaignCannotDispatchOutsideWindowPolicyOrAfterFailure(string scenario)
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var now=DateTime.UtcNow;
        var device=new Device{Domain="example.invalid",Hostname="campaign-guard",AutomationMode=scenario=="policy"?"Disabled":"Enabled"};db.Devices.Add(device);
        var book=await db.Playbooks.FirstAsync(p=>p.Code=="PB-DNS-FLUSH");
        var campaign=new RepairCampaign{Domain=device.Domain,Status="Pilot",StartsAt=now.AddMinutes(scenario=="future"?10:-1),EndsAt=now.AddHours(1),ExpiresAt=now.AddHours(scenario=="expired"?-1:2)};db.RepairCampaigns.Add(campaign);
        var job=new RemediationExecution{DeviceId=device.Id,Domain=device.Domain,CampaignId=campaign.Id,CampaignPilot=true,PlaybookId=book.Id,PlaybookCode=book.Code,Status="Approved"};db.RemediationExecutions.Add(job);
        if(scenario is "failed" or "uncertain")db.RemediationExecutions.Add(new(){CampaignId=campaign.Id,DeviceId=Guid.NewGuid(),Domain=device.Domain,PlaybookId=book.Id,PlaybookCode=book.Code,Status=scenario=="failed"?"Failed":"Unknown"});
        await db.SaveChangesAsync();Assert.Null(await new RemediationCoordinator(db,new AgentCoordinator(db)).ClaimAsync(device,default));
        await db.Entry(job).ReloadAsync();Assert.NotEqual("Executing",job.Status);
    }
    [Fact]
    public async Task PilotSuccessRequiresExpansionAndConcurrentLimitIsEnforced()
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var now=DateTime.UtcNow;
        var devices=Enumerable.Range(0,3).Select(i=>new Device{Domain="example.invalid",Hostname="campaign-"+i,AutomationMode="Enabled"}).ToArray();db.Devices.AddRange(devices);
        var book=await db.Playbooks.FirstAsync(p=>p.Code=="PB-DNS-FLUSH");
        var campaign=new RepairCampaign{Domain="example.invalid",Status="Pilot",StartsAt=now.AddMinutes(-1),EndsAt=now.AddHours(1),ExpiresAt=now.AddHours(2),PilotLimit=1,MaxParallel=1};db.RepairCampaigns.Add(campaign);
        var jobs=devices.Select((d,i)=>new RemediationExecution{DeviceId=d.Id,Domain=d.Domain,CampaignId=campaign.Id,CampaignPilot=i==0,PlaybookId=book.Id,PlaybookCode=book.Code,Status="Approved"}).ToArray();db.RemediationExecutions.AddRange(jobs);await db.SaveChangesAsync();
        var coordinator=new RemediationCoordinator(db,new AgentCoordinator(db));
        var pilot=await coordinator.ClaimAsync(devices[0],default);Assert.NotNull(pilot);
        Assert.Null(await coordinator.ClaimAsync(devices[1],default));
        Assert.True(await coordinator.CompleteAsync(devices[0],pilot.Id,new(pilot.LeaseToken,true,"synthetic","synthetic","synthetic"),default));
        await using(var tx=await db.Database.BeginTransactionAsync()){var refreshed=await CampaignCoordinator.RefreshLockedAsync(db,campaign.Id,default);Assert.Equal("AwaitingExpansion",refreshed!.Status);await tx.CommitAsync();}
        Assert.Null(await coordinator.ClaimAsync(devices[1],default));
        await db.RepairCampaigns.Where(c=>c.Id==campaign.Id).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.Status,"Expanded"));
        Assert.NotNull(await coordinator.ClaimAsync(devices[1],default));
        Assert.Null(await coordinator.ClaimAsync(devices[2],default));
    }
}
