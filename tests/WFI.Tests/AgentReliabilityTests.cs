using System.Net;
using System.Net.Http.Json;
using WFI.Agent;
using WFI.Core;
using Xunit;
namespace WFI.Tests;
public sealed class AgentReliabilityTests
{
    [Theory]
    [InlineData("https://wfi.example.invalid",true)]
    [InlineData("https://wfi.example.invalid:8443/",true)]
    [InlineData("http://wfi.example.invalid",false)]
    [InlineData("https://user:secret@wfi.example.invalid",false)]
    [InlineData("https://wfi.example.invalid/api",false)]
    [InlineData("https://wfi.example.invalid/?token=x",false)]
    [InlineData("https://wfi.example.invalid/#x",false)]
    public void RuntimeAcceptsOnlyTheSameHttpsOriginContractAsSetup(string url,bool accepted)
    {
        var config=new AgentConfig{DeviceId=Guid.NewGuid(),ServerUrl=url};
        if(accepted)config.Validate();else Assert.Throws<InvalidOperationException>(config.Validate);
    }
    private sealed class Handler(HttpStatusCode status):HttpMessageHandler
    {
        public readonly List<string> Paths=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            var path=req.RequestUri!.AbsolutePath;Paths.Add(path);
            if(path.EndsWith("check-in"))
            {
                var response=new HttpResponseMessage(status);
                if(status==HttpStatusCode.TooManyRequests)response.Headers.RetryAfter=new(TimeSpan.FromSeconds(120));
                return Task.FromResult(response);
            }
            return Task.FromResult(new HttpResponseMessage(path.EndsWith("heartbeat")?HttpStatusCode.OK:HttpStatusCode.NoContent));
        }
    }
    private sealed class NoCommands:PowerShellRunner
    {public override Task<CommandResult> RunAsync(string script,CancellationToken ct)=>throw new InvalidOperationException("No command permitted in communication tests");}
    [Theory][InlineData(404)][InlineData(401)][InlineData(429)][InlineData(503)]
    public async Task OnlyUnsupportedEndpointFallsBackAndRateLimitIsRespected(int code)
    {
        if(!OperatingSystem.IsWindows())return;
        var directory=Path.Combine(Path.GetTempPath(),"wfi-agent-protocol-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var config=new AgentConfig{DeviceId=Guid.NewGuid(),StatePath=Path.Combine(directory,"state.bin")};var store=new AgentStateStore(config);
            await store.SaveAsync(new(){DeviceId=config.DeviceId,ServerUrl=config.ServerUrl,Enrolled=true},default);
            var handler=new Handler((HttpStatusCode)code);using var client=new HttpClient(handler){BaseAddress=new Uri(config.ServerUrl)};
            var agent=new AgentService(client,config,store,new WindowsEvidenceCollector(),new RemediationRunner(new NoCommands()));
            if(code==404){await agent.CycleAsync(default);Assert.Equal(4,handler.Paths.Count);await agent.CycleAsync(default);Assert.Equal(1,handler.Paths.Count(p=>p.EndsWith("check-in")));}
            else {await Assert.ThrowsAsync<HttpRequestException>(()=>agent.CycleAsync(default));Assert.Single(handler.Paths);}
            if(code==429)Assert.Equal(120,agent.RetryAfterSeconds);
            Assert.True((await store.LoadAsync(default)).Enrolled);
        }
        finally {Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task MismatchedReceiptIsRejectedWithoutDiscardingPersistedState()
    {
        if(!OperatingSystem.IsWindows())return;
        var directory=Path.Combine(Path.GetTempPath(),"wfi-agent-journal-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var config=new AgentConfig{DeviceId=Guid.NewGuid(),StatePath=Path.Combine(directory,"state.bin")};var store=new AgentStateStore(config);
            await store.SaveAsync(new(){DeviceId=config.DeviceId,ServerUrl=config.ServerUrl,ActiveJob=new(Guid.NewGuid(),Guid.NewGuid(),"PB-DNS-FLUSH",RemediationEngine.Version,DateTime.UtcNow.AddMinutes(1)),PendingReceipt=new(Guid.NewGuid(),true,"synthetic","synthetic","synthetic")},default);
            var before=await File.ReadAllBytesAsync(config.StatePath);
            await Assert.ThrowsAsync<InvalidDataException>(()=>store.LoadAsync(default));Assert.Equal(before,await File.ReadAllBytesAsync(config.StatePath));
        }
        finally {Directory.Delete(directory,true);}
    }
}
