using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;

public class ConfigurationConcurrencyTests
{
    private sealed class ModelClients : IHttpClientFactory
    {
        private sealed class Handler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"models\":[{\"name\":\"first:latest\"},{\"name\":\"second:latest\"}]}")});
        }
        public HttpClient CreateClient(string name)=>new(new Handler());
    }
    private static async Task Login(HttpClient client)
    {
        var csrf=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());
        (await client.PostAsJsonAsync("/api/v1/auth/login",new{username="admin",password="test-password"})).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        csrf=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());
    }
    [Fact]
    public async Task IndependentModelServicesCanCreateAndUpdateOneConfiguration()
    {
        using var app=new TestApp();using var client=app.CreateClient();
        var scopes=app.Services.GetRequiredService<IServiceScopeFactory>();
        using var first=new OllamaService(new ModelClients(),new ConfigurationBuilder().Build(),scopes);
        using var second=new OllamaService(new ModelClients(),new ConfigurationBuilder().Build(),scopes);
        await Task.WhenAll(Task.Run(()=>first.SetActiveModelAsync("first")),Task.Run(()=>second.SetActiveModelAsync("second")));
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var row=await db.AiConfigs.SingleAsync();Assert.Equal(1,row.Id);Assert.Contains(row.ActiveModel,new[]{"first:latest","second:latest"});
        row.Temperature=0.7;row.SystemPrompt="Synthetic preserved setting";await db.SaveChangesAsync();
        await first.SetActiveModelAsync("first");db.ChangeTracker.Clear();row=await db.AiConfigs.SingleAsync();
        Assert.Equal("first:latest",row.ActiveModel);Assert.Equal(0.7,row.Temperature);Assert.Equal("Synthetic preserved setting",row.SystemPrompt);
        await Assert.ThrowsAsync<InputRejectedException>(()=>second.SetActiveModelAsync("missing"));
        Assert.Equal("first:latest",await second.GetActiveModelAsync());
    }
    [Fact]
    public async Task ConcurrentFirstPolicyWritesSucceedAndEachIsAudited()
    {
        using var app=new TestApp();using var first=app.CreateClient();using var second=app.CreateClient();await Login(first);await Login(second);
        var responses=await Task.WhenAll(first.PutAsJsonAsync("/api/v1/automation",new{enabled=true,applyToAll=false}),second.PutAsJsonAsync("/api/v1/automation",new{enabled=false,applyToAll=false}));
        foreach(var response in responses){response.EnsureSuccessStatusCode();response.Dispose();}
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        Assert.Single(await db.AutomationPolicies.ToListAsync());Assert.Equal(2,await db.Audit.CountAsync(a=>a.Action=="automation.default"));
        (await first.PutAsJsonAsync("/api/v1/automation",new{enabled=false,applyToAll=false})).EnsureSuccessStatusCode();
        Assert.False((await db.AutomationPolicies.AsNoTracking().SingleAsync()).Enabled);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task AuditFailureRollsBackDeviceAndGlobalPolicy(bool global)
    {
        using var app=new TestApp();using var client=app.CreateClient();await Login(client);
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Hostname="synthetic",Domain="example.invalid",AutomationMode="Disabled"};db.Devices.Add(device);await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_policy_audit BEFORE INSERT ON Audit WHEN NEW.Action LIKE 'automation.%' BEGIN SELECT RAISE(ABORT, 'synthetic audit failure'); END;");
        var response=global?await client.PutAsJsonAsync("/api/v1/automation",new{enabled=true,applyToAll=true}):await client.PutAsJsonAsync($"/api/v1/devices/{device.Id}/automation",new{mode="Enabled"});
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        db.ChangeTracker.Clear();Assert.Equal("Disabled",(await db.Devices.SingleAsync()).AutomationMode);Assert.Empty(await db.AutomationPolicies.ToListAsync());
    }
}
