using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;
public class AgentProtocolTests
{
    [Theory][InlineData("expired")][InlineData("cancelled")][InlineData("foreign")]
    public async Task LateOrForeignEvidenceCannotChangeJobOrCreateAudit(string scenario)
    {
        using var app=new TestApp();using var client=app.CreateClient();var owner=await Seed(app);var sender=scenario=="foreign"?await Seed(app):owner;
        var key=AgentCoordinator.NewSecret();
        (await client.PostAsJsonAsync("/api/v1/agent/enroll",new AgentEnrollment(sender.Id,sender.Token,key))).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("X-WFI-Device",sender.Id.ToString());client.DefaultRequestHeaders.Authorization=new("Bearer",key);
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var oldLease=Guid.NewGuid();var status=scenario=="cancelled"?"failed":"running";
        var job=new DiagnosticJob{DeviceId=owner.Id,Domain="example.invalid",Hostname="late-evidence",Status=status,LeaseToken=oldLease,LeaseUntil=DateTime.UtcNow.AddMinutes(scenario=="expired"?-1:5),Error=scenario=="cancelled"?"İstemci tarafından iptal edildi.":null};
        db.DiagnosticJobs.Add(job);await db.SaveChangesAsync();
        var endpoint=$"/api/v1/agent/me/diagnostics/{job.Id}/evidence";
        var submission=new EvidenceSubmission(oldLease,[new(){Source="synthetic",Content="late evidence"}]);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(endpoint,submission)).StatusCode);
        await db.Entry(job).ReloadAsync();Assert.Equal(status,job.Status);Assert.Null(job.EvidenceJson);
        Assert.False(await db.Audit.AnyAsync(a=>a.Action=="diagnostic.evidence"&&a.Target==job.Id.ToString()));
        if(scenario=="expired")
        {
            using var response=await client.PostAsync("/api/v1/agent/me/diagnostics/claim",null);response.EnsureSuccessStatusCode();
            var renewed=await response.Content.ReadFromJsonAsync<DiagnosticAssignment>();Assert.NotNull(renewed);Assert.NotEqual(oldLease,renewed.LeaseToken);
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(endpoint,submission)).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync(endpoint,submission with{LeaseToken=renewed.LeaseToken})).StatusCode);
            Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="diagnostic.evidence"&&a.Target==job.Id.ToString()));
        }
    }
    [Fact]
    public async Task AuthenticatedDevicesBehindSameAddressHaveIndependentLimits()
    {
        using var app=new TestApp();using var client=app.CreateClient();
        for(var device=0;device<2;device++)
        {
            var d=await Seed(app);var key=AgentCoordinator.NewSecret();
            (await client.PostAsJsonAsync("/api/v1/agent/enroll",new AgentEnrollment(d.Id,d.Token,key))).EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Remove("X-WFI-Device");client.DefaultRequestHeaders.Add("X-WFI-Device",d.Id.ToString());
            client.DefaultRequestHeaders.Authorization=new("Bearer",key);
            for(var i=0;i<60;i++)Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsync("/api/v1/agent/me/diagnostics/claim",null)).StatusCode);
        }
    }
    [Fact]
    public async Task AutomationDefaultsOffAndHonorsDeviceOverrideAndDomain()
    {
        using var app=new TestApp(); using var client=app.CreateClient();
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var coordinator=new AgentCoordinator(db);
        var device=new Device{Domain="example.invalid"};
        Assert.False(await coordinator.AutomaticEnabledAsync(device,default));
        db.AutomationPolicies.Add(new(){Domain="other.example",Enabled=true});await db.SaveChangesAsync();
        Assert.False(await coordinator.AutomaticEnabledAsync(device,default));
        db.AutomationPolicies.Add(new(){Domain="example.invalid",Enabled=true});await db.SaveChangesAsync();
        Assert.True(await coordinator.AutomaticEnabledAsync(device,default));
        device.AutomationMode="Disabled";Assert.False(await coordinator.AutomaticEnabledAsync(device,default));
        device.AutomationMode="Enabled";Assert.True(await coordinator.AutomaticEnabledAsync(device,default));
    }
    private static async Task<(Guid Id, string Token)> Seed(TestApp app)
    {
        using var scope=app.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var token=AgentCoordinator.NewSecret();
        var device=new Device { Hostname="synthetic-"+Guid.NewGuid().ToString("N")[..8], Domain="example.invalid", Status="pending", EnrollmentToken=AgentCoordinator.Hash(token), EnrollmentExpiresAt=DateTime.UtcNow.AddHours(1) };
        db.Devices.Add(device); await db.SaveChangesAsync(); return (device.Id,token);
    }
    [Fact]
    public async Task EnrollmentIsOneTimeAndRevocationRejectsCredential()
    {
        using var app=new TestApp(); using var client=app.CreateClient(); var d=await Seed(app);
        var credential=AgentCoordinator.NewSecret(); var request=new AgentEnrollment(d.Id,d.Token,credential);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/agent/enroll",request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/agent/enroll",request)).StatusCode);
        client.DefaultRequestHeaders.Add("X-WFI-Device",d.Id.ToString()); client.DefaultRequestHeaders.Authorization=new("Bearer",credential);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/agent/me/heartbeat",new AgentPulse("test","synthetic"))).StatusCode);
        using var scope=app.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        await db.Devices.Where(x=>x.Id==d.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"revoked"));
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/agent/me/heartbeat",new AgentPulse("test","synthetic"))).StatusCode);
    }
    [Fact]
    public async Task CredentialCannotImpersonateAnotherDevice()
    {
        using var app=new TestApp(); using var client=app.CreateClient(); var a=await Seed(app); var b=await Seed(app);
        var key=AgentCoordinator.NewSecret();
        (await client.PostAsJsonAsync("/api/v1/agent/enroll",new AgentEnrollment(a.Id,a.Token,key))).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("X-WFI-Device",b.Id.ToString()); client.DefaultRequestHeaders.Authorization=new("Bearer",key);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/agent/me/heartbeat",new AgentPulse("test","synthetic"))).StatusCode);
    }
    [Fact]
    public async Task EvidenceRequiresOwnedLeaseAndCannotBeReplayed()
    {
        using var app=new TestApp(); using var client=app.CreateClient(); var d=await Seed(app); var key=AgentCoordinator.NewSecret();
        (await client.PostAsJsonAsync("/api/v1/agent/enroll",new AgentEnrollment(d.Id,d.Token,key))).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("X-WFI-Device",d.Id.ToString()); client.DefaultRequestHeaders.Authorization=new("Bearer",key);
        using var scope=app.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var job=new DiagnosticJob{DeviceId=d.Id,Domain="example.invalid",Hostname="synthetic"}; db.DiagnosticJobs.Add(job); await db.SaveChangesAsync();
        var claim=await (await client.PostAsync("/api/v1/agent/me/diagnostics/claim",null)).Content.ReadFromJsonAsync<DiagnosticAssignment>();
        Assert.NotNull(claim); Assert.Equal(job.Id,claim.JobId);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsync("/api/v1/agent/me/diagnostics/claim",null)).StatusCode);
        var url=$"/api/v1/agent/me/diagnostics/{job.Id}/evidence";
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(url,new EvidenceSubmission(Guid.NewGuid(),[]))).StatusCode);
        var evidence=new EvidenceSubmission(claim.LeaseToken,[new(){Content="0xC1900101 Authorization: Bearer TEST_SECRET"}]);
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync(url,evidence)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(url,evidence)).StatusCode);
        await db.Entry(job).ReloadAsync(); Assert.DoesNotContain("TEST_SECRET",job.EvidenceJson!);
    }
}
