using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;
public class JobTransitionTests
{
    [Fact]
    public async Task ConcurrentManualRepairsHaveOneWinnerAndCancellationAllowsReplacement()
    {
        using var app=new TestApp();using var a=app.CreateClient();using var b=app.CreateClient();await Login(a,"admin");await Login(b,"admin");
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Domain="example.invalid",Hostname="duplicate-repair"};db.Devices.Add(device);await db.SaveChangesAsync();
        var request=new{hostname=device.Hostname,playbookCode="PB-DNS-FLUSH",domain=device.Domain};
        var results=await Task.WhenAll(a.PostAsJsonAsync("/api/v1/remediations",request),b.PostAsJsonAsync("/api/v1/remediations",request));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);
        var job=await db.RemediationExecutions.SingleAsync();Assert.Equal(1,await db.Audit.CountAsync(x=>x.Action=="remediation.requested"));
        (await a.PostAsync($"/api/v1/remediations/{job.Id}/cancel",null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created,(await b.PostAsJsonAsync("/api/v1/remediations",request)).StatusCode);
    }
    [Theory][InlineData("PendingApproval")][InlineData("Approved")][InlineData("Executing")]
    public async Task ActiveSameRepairRejectsAnotherRequest(string status)
    {
        using var app=new TestApp();using var client=app.CreateClient();await Login(client,"admin");using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Domain="example.invalid",Hostname="active-repair"};db.Devices.Add(device);var book=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        db.RemediationExecutions.Add(new(){DeviceId=device.Id,Domain=device.Domain,PlaybookId=book.Id,PlaybookCode=book.Code,Status=status});await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/v1/remediations",new{hostname=device.Hostname,playbookCode=book.Code,domain=device.Domain})).StatusCode);
        Assert.Single(await db.RemediationExecutions.ToListAsync());
        Assert.Empty(await db.Audit.Where(x=>x.Action=="remediation.requested").ToListAsync());
    }
    [Theory][InlineData("Approved")][InlineData("PendingApproval")]
    public async Task CancelQueuedRemediationPreventsDispatchAndApproval(string status)
    {
        using var app=new TestApp();using var client=app.CreateClient();await Login(client,"admin");
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Domain="example.invalid",Hostname="cancel-remediation"};db.Devices.Add(device);
        var book=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        var job=new RemediationExecution{DeviceId=device.Id,Domain=device.Domain,PlaybookId=book.Id,PlaybookCode=book.Code,Status=status};db.RemediationExecutions.Add(job);await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync($"/api/v1/remediations/{job.Id}/cancel",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync($"/api/v1/remediations/{job.Id}/cancel",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync($"/api/v1/remediations/{job.Id}/approve",null)).StatusCode);
        Assert.Null(await new RemediationCoordinator(db,new AgentCoordinator(db)).ClaimAsync(device,default));
        await db.Entry(job).ReloadAsync();Assert.Equal("Cancelled",job.Status);Assert.NotNull(job.CompletedAt);Assert.Null(job.ExecutedAt);
        Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="remediation.cancelled"));
    }
    [Theory][InlineData("Executing")][InlineData("Unknown")][InlineData("Success")][InlineData("Failed")]
    public async Task CancellationCannotRewriteStartedOrTerminalRemediation(string status)
    {
        using var app=new TestApp();using var client=app.CreateClient();await Login(client,"analyst");
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var book=await db.Playbooks.FirstAsync();
        var job=new RemediationExecution{Domain="example.invalid",PlaybookId=book.Id,Status=status};db.RemediationExecutions.Add(job);await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync($"/api/v1/remediations/{job.Id}/cancel",null)).StatusCode);
        await db.Entry(job).ReloadAsync();Assert.Equal(status,job.Status);Assert.Empty(await db.Audit.Where(a=>a.Action=="remediation.cancelled").ToListAsync());
    }
    [Theory][InlineData("viewer",HttpStatusCode.Forbidden)][InlineData("foreign",HttpStatusCode.NotFound)]
    public async Task CancellationEnforcesRoleAndDomain(string user,HttpStatusCode expected)
    {
        using var app=new TestApp();using var client=app.CreateClient();await Login(client,user);
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var book=await db.Playbooks.FirstAsync();
        var job=new RemediationExecution{Domain="example.invalid",PlaybookId=book.Id,Status="Approved"};db.RemediationExecutions.Add(job);await db.SaveChangesAsync();
        Assert.Equal(expected,(await client.PostAsync($"/api/v1/remediations/{job.Id}/cancel",null)).StatusCode);
        await db.Entry(job).ReloadAsync();Assert.Equal("Approved",job.Status);
    }
    [Fact]
    public async Task ConcurrentManualDiagnosticsHaveOneWinnerAndCancellationAllowsNewRequest()
    {
        using var app=new TestApp();using var first=app.CreateClient();using var second=app.CreateClient();await Login(first,"analyst");await Login(second,"analyst");
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        db.Devices.Add(new(){Domain="example.invalid",Hostname="duplicate-scan"});await db.SaveChangesAsync();
        var responses=await Task.WhenAll(first.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="duplicate-scan",scanType="FullSystemScan"}),second.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="duplicate-scan",scanType="NetworkScan"}));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        var job=await db.DiagnosticJobs.SingleAsync();Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="diagnostic.requested"));
        (await first.PostAsync($"/api/v1/diagnostics/{job.Id}/cancel",null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created,(await second.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="duplicate-scan",scanType="NetworkScan"})).StatusCode);
        Assert.Equal(2,await db.DiagnosticJobs.CountAsync());
    }
    [Theory][InlineData("pending")][InlineData("running")][InlineData("evidence_received")][InlineData("analyzing")]
    public async Task ActiveDiagnosticRejectsAdditionalManualJob(string status)
    {
        using var app=new TestApp();using var client=app.CreateClient();await Login(client,"analyst");
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Domain="example.invalid",Hostname="active-scan"};db.Devices.Add(device);
        db.DiagnosticJobs.Add(new(){DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,Status=status});await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/v1/diagnostics",new{hostname=device.Hostname,scanType="FullSystemScan"})).StatusCode);
        Assert.Single(await db.DiagnosticJobs.ToListAsync());Assert.Empty(await db.Audit.Where(a=>a.Action=="diagnostic.requested").ToListAsync());
    }
    private static async Task Login(HttpClient client,string user)
    {
        async Task Csrf(){client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");var token=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());}
        await Csrf();(await client.PostAsJsonAsync("/api/v1/auth/login",new{username=user,password="test-password"})).EnsureSuccessStatusCode();await Csrf();
    }
    private sealed class HeldModel:IOllamaService
    {
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<List<OllamaModel>> GetModelsAsync(CancellationToken ct=default)=>Task.FromResult(new List<OllamaModel>());
        public Task<string> GetActiveModelAsync(CancellationToken ct=default)=>Task.FromResult("synthetic");
        public Task SetActiveModelAsync(string name,CancellationToken ct=default)=>Task.CompletedTask;
        public async Task<AiAnalysisJob> DiagnoseAsync(DiagnosticJob job,List<Finding> findings,CancellationToken ct=default)
        {
            Started.TrySetResult();await Release.Task.WaitAsync(ct);
            return new(){DiagnosticJobId=job.Id,Domain=job.Domain,Hostname=job.Hostname,Status="completed",Conclusion="Synthetic"};
        }
    }
    [Fact]
    public async Task CancelDuringInferenceDiscardsLateResultAndAutomaticRepair()
    {
        using var app=new TestApp();using var client=app.CreateClient();await Login(client,"analyst");
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Domain="example.invalid",Hostname="cancel-test",AutomationMode="Enabled"};db.Devices.Add(device);
        var job=new DiagnosticJob{DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,Status="evidence_received",EvidenceJson=JsonSerializer.Serialize(new[]{new EvidenceItem{Source="synthetic",Content="ERROR_NO_LOGON_SERVERS"}},AnalysisWorker.JsonOptions)};
        db.DiagnosticJobs.Add(job);await db.SaveChangesAsync();
        var model=new HeldModel();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var worker=new DiagnosticJobProcessor(app.Services.GetRequiredService<IServiceScopeFactory>(),model,NullLogger<DiagnosticJobProcessor>.Instance);
        var run=worker.ProcessOneAsync(deadline.Token);
        try
        {
            await model.Started.Task.WaitAsync(deadline.Token);
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsync($"/api/v1/diagnostics/{job.Id}/cancel",null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync($"/api/v1/diagnostics/{job.Id}/cancel",null)).StatusCode);
        }
        finally{model.Release.TrySetResult();await run;}
        await db.Entry(job).ReloadAsync();Assert.Equal("failed",job.Status);Assert.NotNull(job.CompletedAt);
        Assert.Empty(await db.AiAnalysisJobs.ToListAsync());Assert.Empty(await db.RemediationExecutions.ToListAsync());
        Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="diagnostic.cancelled"));
    }
    [Fact]
    public async Task ConcurrentApprovalHasOneWinnerAndCannotResetExecutingJob()
    {
        using var app=new TestApp();using var a=app.CreateClient();using var b=app.CreateClient();await Login(a,"admin");await Login(b,"admin");
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var playbook=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DISM-REPAIR");
        var job=new RemediationExecution{Domain="example.invalid",Hostname="synthetic",PlaybookId=playbook.Id,PlaybookCode=playbook.Code,Status="PendingApproval"};
        db.RemediationExecutions.Add(job);await db.SaveChangesAsync();
        var responses=await Task.WhenAll(a.PostAsync($"/api/v1/remediations/{job.Id}/approve",null),b.PostAsync($"/api/v1/remediations/{job.Id}/approve",null));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        foreach(var response in responses)response.Dispose();
        await db.RemediationExecutions.Where(r=>r.Id==job.Id).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"Executing"));
        Assert.Equal(HttpStatusCode.Conflict,(await a.PostAsync($"/api/v1/remediations/{job.Id}/approve",null)).StatusCode);
        await db.Entry(job).ReloadAsync();Assert.Equal("Executing",job.Status);Assert.Equal("admin",job.ApprovedBy);
        Assert.Equal(1,await db.Audit.CountAsync(e=>e.Action=="remediation.approved"));
    }
}
