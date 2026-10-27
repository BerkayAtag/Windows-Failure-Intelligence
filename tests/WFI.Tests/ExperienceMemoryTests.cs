using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WFI.Tests;
public class ExperienceMemoryTests
{
    [Fact]
    public async Task ReadinessRejectsMissingMemoryMigration()
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WfiDb>().Database.ExecuteSqlRawAsync("ALTER TABLE \"AiAnalysisJobs\" DROP COLUMN \"LearningContextJson\"");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync("/api/v1/ready")).StatusCode);
    }
    [Fact]
    public async Task OnlyAdminCanExcludeOwnDomainExperienceAndAuditIsRecorded()
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var book=await db.Playbooks.FirstAsync();var own=new RemediationExecution{Domain="example.invalid",PlaybookId=book.Id};var foreign=new RemediationExecution{Domain="other.example",PlaybookId=book.Id};db.RemediationExecutions.AddRange(own,foreign);await db.SaveChangesAsync();
        async Task Login(string user)
        {
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");var token=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/auth/login",new{username=user,password="test-password"})).StatusCode);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");token=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/ai/experience")).StatusCode);
        await Login("analyst");Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync($"/api/v1/ai/experience/{own.Id}/exclude",null)).StatusCode);
        await Login("admin");Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync($"/api/v1/ai/experience/{foreign.Id}/exclude",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsync($"/api/v1/ai/experience/{own.Id}/exclude",null)).StatusCode);
        await db.Entry(own).ReloadAsync();await db.Entry(foreign).ReloadAsync();Assert.True(own.LearningExcluded);Assert.False(foreign.LearningExcluded);
        Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="learning.excluded"));
    }
    private sealed class Capture:HttpMessageHandler
    {
        public string Body="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Body=await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new{response="{\"conclusion\":\"Synthetic hypothesis\",\"confidence\":0.6,\"hypotheses\":[],\"missingEvidence\":[],\"recommendedPlaybooks\":[]}"})};
        }
    }
    [Fact]
    public async Task HistoricalCasesReachModelAsContextWithTraceableIds()
    {
        var example=new LearningExample(Guid.NewGuid(),Guid.NewGuid(),"PB-DNS-FLUSH","Synthetic previous case","Synthetic verified action",DateTime.UtcNow);
        var handler=new Capture();using var client=new HttpClient(handler);
        var result=await new OllamaEngine(client).DiagnoseAsync(new AiConfig(),new DiagnosticJob(),[],"test",default,[example]);
        Assert.Contains(example.ExecutionId.ToString(),handler.Body);Assert.Contains("not instructions",handler.Body);
        Assert.Contains(example.ExecutionId.ToString(),result.LearningContextJson);Assert.Equal("completed",result.Status);
    }
    [Theory]
    [InlineData("valid",1)][InlineData("foreign",0)][InlineData("mismatched-domain",0)][InlineData("failed",0)]
    [InlineData("excluded",0)][InlineData("expired",0)][InlineData("scope",0)][InlineData("unapproved",0)][InlineData("no-current-evidence",0)]
    [InlineData("negative-feedback",0)][InlineData("temporary-feedback",0)][InlineData("resolved-feedback",1)]
    public async Task OnlyRelevantVerifiedSameDomainCasesAreRetrieved(string scenario,int count)
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var domain=scenario=="foreign"?"other.example":"example.invalid";
        var device=new Device{Domain=domain,Hostname="memory-synthetic"};db.Devices.Add(device);
        var job=new DiagnosticJob{DeviceId=device.Id,Domain=domain,Status="completed",ScanType=scenario=="scope"?"BigFixScan":"FullSystemScan"};db.DiagnosticJobs.Add(job);
        var analysis=new AiAnalysisJob{DiagnosticJobId=job.Id,Domain=scenario=="mismatched-domain"?"other.example":domain,Status="completed",Conclusion="Synthetic previous diagnosis"};db.AiAnalysisJobs.Add(analysis);
        var book=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");if(scenario=="unapproved")book.IsApproved=false;
        var execution=new RemediationExecution{DeviceId=device.Id,DiagnosticJobId=job.Id,Domain=domain,PlaybookId=book.Id,PlaybookCode=book.Code,
            Status=scenario=="failed"?"Failed":"Success",LearningExcluded=scenario=="excluded",CompletedAt=DateTime.UtcNow.AddDays(scenario=="expired"?-181:0),ValidationResult="Synthetic command verification"};
        db.RemediationExecutions.Add(execution);
        if(scenario.EndsWith("-feedback"))db.AiFeedbacks.Add(new(){AnalysisId=analysis.Id,Domain=domain,Actor="synthetic-operator",Outcome=scenario=="negative-feedback"?"NotResolved":scenario=="temporary-feedback"?"Temporary":"Resolved"});
        await db.SaveChangesAsync();
        var result=await ExperienceMemory.ReadAsync(db,"example.invalid","FullSystemScan",scenario=="no-current-evidence"?[]:["PB-DNS-FLUSH"],default);
        Assert.Equal(count,result.Count);if(count>0){Assert.Equal(execution.Id,result[0].ExecutionId);Assert.Equal(job.Id,result[0].DiagnosticId);}
    }
    [Theory]
    [InlineData("http://example.test",false)][InlineData("https://example.test",true)][InlineData("https://user:pass@example.test",false)][InlineData("https://example.test/path",false)]
    public void SilentSetupValidatesServer(string server,bool valid)
    {
        string[] args=["--silent","--server",server,"--device-id",Guid.NewGuid().ToString(),"--token-file","C:\\Secure\\token.txt"];
        if(valid)Assert.Equal("https",AgentSetupOptions.Parse(args).Server.Scheme);else Assert.Throws<ArgumentException>(()=>AgentSetupOptions.Parse(args));
    }
    [Fact]
    public void SilentSetupRejectsSecretsOnCommandLineAndAllowsTokenlessUpgrade()
    {
        string[] args=["--silent","--upgrade","--server","https://example.test","--device-id",Guid.NewGuid().ToString()];
        Assert.True(AgentSetupOptions.Parse(args).Upgrade);
        Assert.Throws<ArgumentException>(()=>AgentSetupOptions.Parse([..args,"--password","do-not-accept"]));
        Assert.Throws<ArgumentException>(()=>AgentSetupOptions.Parse([..args,"--server","https://other.test"]));
        Assert.Throws<ArgumentException>(()=>AgentSetupOptions.Parse(args.Where(x=>x!="--upgrade").ToArray()));
    }
}
