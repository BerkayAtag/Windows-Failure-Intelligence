using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;
namespace WFI.Tests;

public class OperationsTests
{
    [Fact]
    public async Task OperationsAndCampaignsRequireAdminAndHealthDoesNotCountForeignDomain()
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        db.DiagnosticJobs.AddRange(new DiagnosticJob{Domain="example.invalid",Status="pending"},new DiagnosticJob{Domain="other.example",Status="pending"});await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/operations/health")).StatusCode);
        async Task Login(string user)
        {
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            var csrf=await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());
            (await client.PostAsJsonAsync("/api/v1/auth/login",new{username=user,password="test-password"})).EnsureSuccessStatusCode();
        }
        await Login("analyst");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/v1/operations/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/v1/campaigns")).StatusCode);
        await Login("admin");
        var result=await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/operations/health");
        Assert.Equal(1,result.GetProperty("activeDiagnostics").GetInt32());
    }
    [Fact]
    public void PollingAdaptsAndHonorsBoundedRetryHint()
    {
        var p=new AgentPolling(30);
        Assert.Equal(60,p.Success(false,300));Assert.Equal(120,p.Success(false,300));
        Assert.Equal(30,p.Success(true,300));Assert.Equal(900,p.Failure(900));
        for(var i=0;i<20;i++)Assert.InRange(p.Failure(),10,3600);
        Assert.Equal(30,p.Success(true,300));Assert.Equal(60,p.Failure());
        Assert.Equal(3600,p.Failure(int.MaxValue));
    }
    [Fact]
    public async Task CapacityRejectsSecondWorkerAndFencesExpiredOwner()
    {
        using var app=new TestApp();using var client=app.CreateClient();
        using var one=app.Services.CreateScope();using var two=app.Services.CreateScope();
        var db=one.ServiceProvider.GetRequiredService<WfiDb>();var other=two.ServiceProvider.GetRequiredService<WfiDb>();
        var first=await ModelCapacity.TryAcquireAsync(db,1,default);Assert.NotNull(first);
        Assert.Null(await ModelCapacity.TryAcquireAsync(other,1,default));
        await other.ModelSlots.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LeaseUntil,DateTime.UtcNow.AddSeconds(-1)));
        var next=await ModelCapacity.TryAcquireAsync(other,1,default);Assert.NotNull(next);
        Assert.True(next.Generation>first.Generation);
        Assert.False(await ModelCapacity.IsCurrentAsync(db,first,default));
        Assert.Equal(0,await ModelCapacity.ReleaseAsync(db,first,default));
        Assert.True(await ModelCapacity.IsCurrentAsync(other,next,default));
        Assert.Equal(1,await ModelCapacity.ReleaseAsync(other,next,default));
    }
    [Fact]
    public async Task CheckInNegotiatesAndNeverReissuesLostRemediation()
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var key=AgentCoordinator.NewSecret();
        var d=new Device{Hostname="synthetic-combined",Domain="example.invalid",AgentCredentialHash=AgentCoordinator.Hash(key)};
        db.Devices.Add(d);var book=await db.Playbooks.FirstAsync(p=>p.Code=="PB-DNS-FLUSH");
        var job=new RemediationExecution{DeviceId=d.Id,Domain=d.Domain,PlaybookId=book.Id,PlaybookCode=book.Code,Status="Approved"};
        db.RemediationExecutions.Add(job);await db.SaveChangesAsync();
        var input=new AgentCheckIn(new("synthetic","synthetic"),["combined-check-in-v1"]);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/agent/me/check-in",input)).StatusCode);
        client.DefaultRequestHeaders.Add("X-WFI-Device",d.Id.ToString());client.DefaultRequestHeaders.Authorization=new("Bearer",key);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/agent/me/check-in",input with{Capabilities=[]})).StatusCode);
        using var first=await client.PostAsJsonAsync("/api/v1/agent/me/check-in",input);first.EnsureSuccessStatusCode();
        var response=await first.Content.ReadFromJsonAsync<AgentCheckInResponse>();Assert.Equal(job.Id,response!.Remediation!.Id);
        using var lostRetry=await client.PostAsJsonAsync("/api/v1/agent/me/check-in",input);lostRetry.EnsureSuccessStatusCode();
        Assert.Null((await lostRetry.Content.ReadFromJsonAsync<AgentCheckInResponse>())!.Remediation);
        await db.RemediationExecutions.Where(x=>x.Id==job.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LeaseUntil,DateTime.UtcNow.AddMinutes(-1)));
        using var expired=await client.PostAsJsonAsync("/api/v1/agent/me/check-in",input);expired.EnsureSuccessStatusCode();
        Assert.Null((await expired.Content.ReadFromJsonAsync<AgentCheckInResponse>())!.Remediation);
        await db.Entry(job).ReloadAsync();Assert.Equal("Unknown",job.Status);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/agent/me/heartbeat",input.Pulse)).StatusCode);
    }
}
