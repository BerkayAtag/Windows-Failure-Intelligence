using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Agent;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;
public class RemediationTests
{
    [Theory][InlineData("disabled")][InlineData("obsolete")][InlineData("unknown")]
    public async Task IneligibleQueueHeadDoesNotStarveManualWork(string reason)
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Hostname="queue-head",Domain="example.invalid",Status="online",AutomationMode="Disabled"};db.Devices.Add(device);
        var playbook=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        var blocked=playbook;
        if(reason=="obsolete"){blocked=await db.Playbooks.FirstAsync(p=>p.Code!="PB-DNS-FLUSH");blocked.Version="obsolete-test";}
        for(var i=0;i<12;i++)db.RemediationExecutions.Add(new(){DeviceId=device.Id,PlaybookId=blocked.Id,PlaybookCode=reason=="unknown"?"UNKNOWN":blocked.Code,Status="Approved",Automatic=reason=="disabled",RequestedAt=DateTime.UtcNow.AddHours(-1)});
        var manual=new RemediationExecution{DeviceId=device.Id,PlaybookId=playbook.Id,PlaybookCode=playbook.Code,Status="Approved"};db.RemediationExecutions.Add(manual);await db.SaveChangesAsync();
        var result=await new RemediationCoordinator(db,new AgentCoordinator(db)).ClaimAsync(device,default);
        Assert.NotNull(result);Assert.Equal(manual.Id,result.Id);
        Assert.Equal(12,await db.RemediationExecutions.CountAsync(x=>x.DeviceId==device.Id&&x.Status=="Approved"));
        Assert.Equal(1,await db.RemediationExecutions.CountAsync(x=>x.DeviceId==device.Id&&x.Status=="Executing"));
    }
    private sealed class LostResultResponse(HttpMessageHandler inner):DelegatingHandler(inner)
    {
        private bool dropped;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var response=await base.SendAsync(request,ct);
            if(!dropped&&request.RequestUri!.AbsolutePath.EndsWith("/result",StringComparison.Ordinal))
            {
                dropped=true;response.Dispose();throw new HttpRequestException("Synthetic response loss after server commit.");
            }
            return response;
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RestartNeverRepeatsCommittedOrUncertainRemediation(bool uncertain)
    {
        if(!OperatingSystem.IsWindows())return;
        using var app=new TestApp();using var boot=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var credential=AgentCoordinator.NewSecret();var device=new Device{Hostname="restart-test",Domain="example.invalid",Status="online",AgentCredentialHash=AgentCoordinator.Hash(credential)};
        db.Devices.Add(device);var playbook=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        var job=new RemediationExecution{DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,PlaybookId=playbook.Id,PlaybookCode=playbook.Code,Status="Approved"};db.RemediationExecutions.Add(job);await db.SaveChangesAsync();
        var config=new AgentConfig{DeviceId=device.Id,StatePath=Path.Combine(app.Root,"restart.bin")};var store=new AgentStateStore(config);
        var state=new AgentState{DeviceId=device.Id,ServerUrl=config.ServerUrl,Credential=credential,Enrolled=true};
        if(uncertain)state.ActiveJob=await new RemediationCoordinator(db,new AgentCoordinator(db)).ClaimAsync(device,default);
        await store.SaveAsync(state,default);var commands=new Commands();
        if(!uncertain)
        {
            using var lossy=new HttpClient(new LostResultResponse(app.Server.CreateHandler())){BaseAddress=boot.BaseAddress};
            var initial=new AgentService(lossy,config,store,new Evidence(),new RemediationRunner(commands));
            await Assert.ThrowsAsync<HttpRequestException>(()=>initial.CycleAsync(default));
            Assert.NotNull((await store.LoadAsync(default)).PendingReceipt);
        }
        // A new service/store instance models process restart, using only persisted DPAPI state.
        using var recovered=app.CreateClient();var restarted=new AgentService(recovered,config,new AgentStateStore(config),new Evidence(),new RemediationRunner(commands));
        await restarted.CycleAsync(default);await db.Entry(job).ReloadAsync();
        Assert.Equal(uncertain?"Failed":"Success",job.Status);Assert.Equal(uncertain?0:3,commands.Count);
        Assert.Null((await store.LoadAsync(default)).ActiveJob);
        Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="remediation.result"));
    }
    [Fact]
    public async Task DispatchSerializesDeviceAndNeverReplaysExpiredExecution()
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Hostname="dispatch-test",Domain="example.invalid",Status="online",AutomationMode="Enabled"};db.Devices.Add(device);
        var playbook=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        var first=new RemediationExecution{DeviceId=device.Id,PlaybookId=playbook.Id,PlaybookCode=playbook.Code,Status="Approved",RequestedAt=DateTime.UtcNow.AddMinutes(-1)};
        var second=new RemediationExecution{DeviceId=device.Id,PlaybookId=playbook.Id,PlaybookCode=playbook.Code,Status="Approved",Automatic=true};
        db.RemediationExecutions.AddRange(first,second);await db.SaveChangesAsync();
        var coordinator=new RemediationCoordinator(db,new AgentCoordinator(db));
        var assignment=await coordinator.ClaimAsync(device,default);Assert.NotNull(assignment);Assert.Equal(first.Id,assignment.Id);
        Assert.Null(await coordinator.ClaimAsync(device,default));
        await db.RemediationExecutions.Where(x=>x.Id==first.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LeaseUntil,DateTime.UtcNow.AddMinutes(-1)));
        await db.Devices.Where(d=>d.Id==device.Id).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.AutomationMode,"Disabled"));
        Assert.Null(await coordinator.ClaimAsync(device,default));
        await db.Entry(first).ReloadAsync();Assert.Equal("Unknown",first.Status);
        Assert.False(await coordinator.CompleteAsync(new Device{Id=Guid.NewGuid()},first.Id,new(assignment.LeaseToken,true,"test","test","test"),default));
        Assert.True(await coordinator.CompleteAsync(device,first.Id,new(assignment.LeaseToken,false,"test","test","uncertain"),default));
        Assert.True(await coordinator.CompleteAsync(device,first.Id,new(assignment.LeaseToken,false,"test","test","uncertain"),default));
        Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="remediation.result"));
    }
    private sealed class Commands(int failAt=0):PowerShellRunner
    {
        public int Count;
        public override Task<CommandResult> RunAsync(string script,CancellationToken ct){Count++;return Task.FromResult(new CommandResult(Count==failAt?1:0,"synthetic result"));}
    }
    private sealed class Evidence:WindowsEvidenceCollector
    {
        public override Task<List<EvidenceItem>> CollectAsync(string scan,CancellationToken ct)=>Task.FromResult(new List<EvidenceItem>{new(){Source="synthetic",Content="0xC1900101"}});
    }
    [Theory][InlineData(1,1)][InlineData(2,2)][InlineData(3,3)]
    public async Task FailedPrecheckActionOrVerificationCannotReportSuccess(int failAt,int expectedCalls)
    {
        var commands=new Commands(failAt);
        var result=await new RemediationRunner(commands).ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"PB-DNS-FLUSH",RemediationEngine.Version,DateTime.UtcNow.AddMinutes(10)),default);
        Assert.False(result.Success);Assert.Equal(expectedCalls,commands.Count);
    }
    [Fact]
    public async Task UnknownCommandNeverReachesShell()
    {
        var commands=new Commands();var runner=new RemediationRunner(commands);
        Assert.False((await runner.ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"arbitrary script",RemediationEngine.Version,DateTime.UtcNow.AddMinutes(1)),default)).Success);
        Assert.Equal(0,commands.Count);
    }
    [Fact]
    public async Task WindowsRunnerActuallyExecutesAndReportsExitCode()
    {
        if(!OperatingSystem.IsWindows())return;
        // Hosted Windows runners can cold-start PowerShell slowly. Each independent
        // invocation gets a bounded budget; production remediation deadlines are unchanged.
        var runner=new PowerShellRunner();using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var result=await runner.RunAsync("Write-Output 'WFI_SYNTHETIC_EXECUTION'",ct.Token);
        Assert.Equal(0,result.ExitCode);Assert.Contains("WFI_SYNTHETIC_EXECUTION",result.Output);
        using var failureCt=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        Assert.NotEqual(0,(await runner.RunAsync("throw 'synthetic failure'",failureCt.Token)).ExitCode);
    }
    [Fact]
    public async Task AgentCycleEnrollsCollectsExecutesAndPersistsReceiptWithoutRepeating()
    {
        if(!OperatingSystem.IsWindows())return;
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var token=AgentCoordinator.NewSecret();
        var device=new Device{Domain="example.invalid",Hostname="agent-test",Status="pending",EnrollmentToken=AgentCoordinator.Hash(token),EnrollmentExpiresAt=DateTime.UtcNow.AddHours(1)};
        db.Devices.Add(device);await db.SaveChangesAsync();
        var diagnostic=new DiagnosticJob{DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname};
        var playbook=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        var job=new RemediationExecution{DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,PlaybookId=playbook.Id,PlaybookCode=playbook.Code,Status="Approved"};
        db.DiagnosticJobs.Add(diagnostic);db.RemediationExecutions.Add(job);await db.SaveChangesAsync();
        var config=new AgentConfig{DeviceId=device.Id,EnrollmentToken=token,StatePath=Path.Combine(app.Root,"state.bin")};
        var store=new AgentStateStore(config);var commands=new Commands();var agent=new AgentService(client,config,store,new Evidence(),new RemediationRunner(commands));
        await agent.CycleAsync(default);await db.Entry(job).ReloadAsync();await db.Entry(diagnostic).ReloadAsync();
        Assert.Equal("Success",job.Status);Assert.Equal("evidence_received",diagnostic.Status);Assert.Equal(3,commands.Count);
        var state=await store.LoadAsync(default);Assert.True(state.Enrolled);Assert.Null(state.ActiveJob);
        await agent.CycleAsync(default);Assert.Equal(3,commands.Count);
        Assert.DoesNotContain(state.Credential,await File.ReadAllTextAsync(config.StatePath));
    }
}
