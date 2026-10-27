using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;
public class AgentSetupTests
{
    [Fact]
    public async Task SetupRegistersMachineAndEnrollmentBringsItOnline()
    {
        using var app=new TestApp();using var client=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});
        var ticket=await AgentSetupRegistration.RegisterAsync(client,"admin","test-password"," SETUP-PC ",default);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/auth/me")).StatusCode);
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=await db.Devices.SingleAsync(d=>d.Id==ticket.Id);
        Assert.Equal("setup-pc",device.Hostname);Assert.Equal("example.invalid",device.Domain);Assert.Equal("pending",device.Status);
        Assert.NotEqual(ticket.EnrollmentToken,device.EnrollmentToken);
        var credential=AgentCoordinator.NewSecret();
        var enrolled=await client.PostAsJsonAsync("/api/v1/agent/enroll",new AgentEnrollment(ticket.Id,ticket.EnrollmentToken,credential));
        Assert.Equal(HttpStatusCode.OK,enrolled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/agent/enroll",new AgentEnrollment(ticket.Id,ticket.EnrollmentToken,AgentCoordinator.NewSecret()))).StatusCode);
        await db.Entry(device).ReloadAsync();Assert.Equal("online",device.Status);Assert.Null(device.EnrollmentToken);
    }
    [Theory][InlineData("viewer","test-password")][InlineData("analyst","test-password")][InlineData("admin","incorrect")]
    public async Task SetupRejectsUnprivilegedOrInvalidAccounts(string username,string password)
    {
        using var app=new TestApp();using var client=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AgentSetupRegistration.RegisterAsync(client,username,password,"setup-denied",default));
        using var scope=app.Services.CreateScope();Assert.False(await scope.ServiceProvider.GetRequiredService<WfiDb>().Devices.AnyAsync());
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }
    [Fact]
    public async Task SetupRejectsHttpBeforeSendingCredentials()
    {
        using var client=new HttpClient{BaseAddress=new Uri("http://localhost")};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AgentSetupRegistration.RegisterAsync(client,"synthetic","synthetic","setup",default));
    }
    [Fact]
    public async Task DuplicateSetupCannotReplaceExistingIdentity()
    {
        using var app=new TestApp();using var first=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});
        using var second=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});
        var ticket=await AgentSetupRegistration.RegisterAsync(first,"admin","test-password","setup-duplicate",default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AgentSetupRegistration.RegisterAsync(second,"admin","test-password","SETUP-DUPLICATE",default));
        using var scope=app.Services.CreateScope();var device=await scope.ServiceProvider.GetRequiredService<WfiDb>().Devices.SingleAsync();
        Assert.Equal(ticket.Id,device.Id);Assert.Equal(AgentCoordinator.Hash(ticket.EnrollmentToken),device.EnrollmentToken);
    }
    [Fact]
    public async Task ConcurrentSetupCreatesOnlyOneIdentity()
    {
        using var app=new TestApp();using var first=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});
        using var second=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});
        async Task Authenticate(HttpClient client)
        {
            var csrf=await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/auth/csrf");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/auth/login",new{username="admin",password="test-password"})).StatusCode);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");csrf=await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/auth/csrf");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());
        }
        // Isolate registration contention from unrelated login/audit writes on the SQLite test store.
        await Authenticate(first);await Authenticate(second);
        var outcomes=await Task.WhenAll(first.PostAsJsonAsync("/api/v1/devices/register",new{hostname="setup-race"}),second.PostAsJsonAsync("/api/v1/devices/register",new{hostname="setup-race"}));
        Assert.Single(outcomes,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(outcomes,x=>x.StatusCode==HttpStatusCode.Conflict);
        foreach(var response in outcomes)response.Dispose();
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        Assert.Equal(1,await db.Devices.CountAsync());Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="device.registered"));
    }
}
