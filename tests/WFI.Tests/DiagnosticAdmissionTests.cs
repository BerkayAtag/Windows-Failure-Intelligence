using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;
namespace WFI.Tests;
public class DiagnosticAdmissionTests
{
    [Theory][InlineData("pending")][InlineData("running")][InlineData("evidence_received")][InlineData("analyzing")]
    public async Task ReanalysisRespectsDeviceExclusivityAndClearsCompletion(string status)
    {
        using var app=new TestApp();using var client=app.CreateClient();await Login(client);
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Domain="example.invalid",Hostname="reanalyze"};db.Devices.Add(device);
        var active=new DiagnosticJob{DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,Status=status};
        var old=new DiagnosticJob{DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,Status="completed",EvidenceJson="[]",CompletedAt=DateTime.UtcNow};
        db.DiagnosticJobs.AddRange(active,old);await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync($"/api/v1/ai/analyze/{old.Id}",null)).StatusCode);
        await db.Entry(old).ReloadAsync();Assert.Equal("completed",old.Status);Assert.NotNull(old.CompletedAt);
        (await client.PostAsync($"/api/v1/diagnostics/{active.Id}/cancel",null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsync($"/api/v1/ai/analyze/{old.Id}",null)).StatusCode);
        await db.Entry(old).ReloadAsync();Assert.Equal("evidence_received",old.Status);Assert.Null(old.CompletedAt);
    }
    private static async Task Login(HttpClient client,string user="analyst")
    {
        async Task Csrf(){client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");var token=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());}
        await Csrf();(await client.PostAsJsonAsync("/api/v1/auth/login",new{username=user,password="test-password"})).EnsureSuccessStatusCode();await Csrf();
    }
    [Fact]
    public async Task ConcurrentDevicesRespectDomainCapAndCancellationFreesSlot()
    {
        using var app=new TestApp(diagnosticLimit:1);using var a=app.CreateClient();using var b=app.CreateClient();using var foreign=app.CreateClient();
        await Login(a);await Login(b);await Login(foreign,"foreign");
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        db.Devices.AddRange(new Device{Domain="example.invalid",Hostname="cap-a"},new Device{Domain="example.invalid",Hostname="cap-b"},new Device{Domain="other.example",Hostname="cap-c"});await db.SaveChangesAsync();
        var results=await Task.WhenAll(a.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="cap-a",scanType="FullSystemScan"}),b.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="cap-b",scanType="FullSystemScan"}));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.TooManyRequests);
        var job=await db.DiagnosticJobs.SingleAsync();
        Assert.Equal(HttpStatusCode.Created,(await foreign.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="cap-c",scanType="FullSystemScan"})).StatusCode);
        (await a.PostAsync($"/api/v1/diagnostics/{job.Id}/cancel",null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created,(await b.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="cap-b",scanType="FullSystemScan"})).StatusCode);
    }
    [Theory][InlineData("pending")][InlineData("running")][InlineData("evidence_received")][InlineData("analyzing")]
    public async Task ReanalysisCannotBypassActiveLimit(string status)
    {
        using var app=new TestApp(diagnosticLimit:1);using var client=app.CreateClient();await Login(client);
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var active=new DiagnosticJob{Domain="example.invalid",Hostname="busy",Status=status};var finished=new DiagnosticJob{Domain="example.invalid",Hostname="finished",Status="completed",EvidenceJson="[]"};
        db.DiagnosticJobs.AddRange(active,finished);await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.TooManyRequests,(await client.PostAsync($"/api/v1/ai/analyze/{finished.Id}",null)).StatusCode);
        await db.Entry(finished).ReloadAsync();Assert.Equal("completed",finished.Status);
        (await client.PostAsync($"/api/v1/diagnostics/{active.Id}/cancel",null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsync($"/api/v1/ai/analyze/{finished.Id}",null)).StatusCode);
    }
    [Fact]
    public async Task FullQueueDefersAutomaticWorkWithoutFailingHeartbeat()
    {
        using var app=new TestApp(diagnosticLimit:1);using var client=app.CreateClient();
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var token=AgentCoordinator.NewSecret();var device=new Device{Domain="example.invalid",Hostname="auto-cap",Status="pending",AutomationMode="Enabled",EnrollmentToken=AgentCoordinator.Hash(token),EnrollmentExpiresAt=DateTime.UtcNow.AddHours(1)};
        db.Devices.Add(device);db.DiagnosticJobs.Add(new(){Domain="example.invalid",Hostname="busy"});await db.SaveChangesAsync();
        var key=AgentCoordinator.NewSecret();(await client.PostAsJsonAsync("/api/v1/agent/enroll",new AgentEnrollment(device.Id,token,key))).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("X-WFI-Device",device.Id.ToString());client.DefaultRequestHeaders.Authorization=new("Bearer",key);
        var before=DateTime.UtcNow;
        (await client.PostAsJsonAsync("/api/v1/agent/me/heartbeat",new AgentPulse("test","synthetic"))).EnsureSuccessStatusCode();
        await db.Entry(device).ReloadAsync();Assert.InRange(device.NextDiagnosticAt!.Value,before.AddSeconds(60),DateTime.UtcNow.AddSeconds(120));
        Assert.Single(await db.DiagnosticJobs.ToListAsync());
    }
}
