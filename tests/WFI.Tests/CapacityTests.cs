using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;

public class CapacityTests
{
    [Fact]
    public async Task FortyThousandDevicesHaveDeterministicBoundedDomainScopedPages()
    {
        using var app=new TestApp();using var client=app.CreateClient();
        using(var scope=app.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
            for(var i=0;i<40000;i++)db.Devices.Add(new(){Id=Guid.Parse($"30000000-0000-0000-0000-{i+1:D12}"),Domain="example.invalid",Hostname=$"fleet-{i:D5}",LastHeartbeat=DateTime.UnixEpoch,SystemInfoJson=new string('x',1024)});
            db.Devices.Add(new(){Domain="other.example",Hostname="foreign",LastHeartbeat=DateTime.UtcNow});await db.SaveChangesAsync();
        }
        var csrf=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());
        (await client.PostAsJsonAsync("/api/v1/auth/login",new{username="viewer",password="test-password"})).EnsureSuccessStatusCode();
        foreach(var page in new[]{1,2,400,800,1})
        {
            var response=await client.GetAsync($"/api/v1/devices?page={page}");response.EnsureSuccessStatusCode();var body=await response.Content.ReadAsStringAsync();
            using var doc=JsonDocument.Parse(body);var root=doc.RootElement;Assert.Equal(40000,root.GetProperty("total").GetInt32());
            var items=root.GetProperty("items");Assert.Equal(50,items.GetArrayLength());var index=0;
            foreach(var item in items.EnumerateArray())
            {
                Assert.Equal("example.invalid",item.GetProperty("domain").GetString());
                Assert.Equal($"fleet-{39999-(page-1)*50-index++:D5}",item.GetProperty("hostname").GetString());
            }
            Assert.True(body.Length<32768);Assert.DoesNotContain("systemInfoJson",body);Assert.DoesNotContain("agentCredentialHash",body);Assert.DoesNotContain("enrollmentToken",body);
        }
        var filtered=await client.GetFromJsonAsync<JsonElement>("/api/v1/devices?search=%20FLEET-39999%20");Assert.Equal(1,filtered.GetProperty("total").GetInt32());
        Assert.Equal(System.Net.HttpStatusCode.BadRequest,(await client.GetAsync("/api/v1/devices?search="+new string('a',64))).StatusCode);
    }
    [Fact]
    public async Task FortyThousandRowsRemainPagedAndDomainScoped()
    {
        using var app = new TestApp(); using var client = app.CreateClient();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WfiDb>();
            for (var i = 0; i < 40000; i++) db.Jobs.Add(new AnalysisJob {
                Domain = "example.invalid", Hostname = $"pc-{i:D5}", Filename = "synthetic.log", Status = "completed",
                DedupKey = $"capacity-{i}", CreatedAt = DateTime.UnixEpoch.AddSeconds(i), ResultJson = new string('x', 2048)
            });
            db.Jobs.Add(new AnalysisJob { Domain="other.example", Hostname="foreign", DedupKey="foreign" });
            await db.SaveChangesAsync();
        }
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username="viewer", password="test-password" })).EnsureSuccessStatusCode();
        var timings = new List<double>();
        for (var i = 0; i < 60; i++)
        {
            var page = i % 2 == 0 ? 1 : 1600;
            var timer = Stopwatch.StartNew();
            var response = await client.GetAsync($"/api/v1/analyses?page={page}"); response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(); timings.Add(timer.Elapsed.TotalMilliseconds);
            var result = JsonDocument.Parse(json).RootElement;
            Assert.Equal(40000, result.GetProperty("total").GetInt32());
            Assert.Equal(25, result.GetProperty("items").GetArrayLength());
            Assert.All(result.GetProperty("items").EnumerateArray(), item => Assert.Equal("example.invalid", item.GetProperty("domain").GetString()));
            Assert.DoesNotContain("resultJson", json); Assert.True(json.Length < 20000);
        }
        var target = Environment.GetEnvironmentVariable("WFI_CAPACITY_REPORT");
        if (!string.IsNullOrWhiteSpace(target))
        {
            timings.Sort();
            await File.WriteAllTextAsync(target, JsonSerializer.Serialize(new {
                generatedUtc=DateTime.UtcNow, rows=40000, sequentialRequests=60, concurrentUsers=1,
                provider="SQLite / in-process TestServer", p50Ms=timings[29], p95Ms=timings[56], maxMs=timings[^1],
                limitation="Not a PostgreSQL, network, LDAP, upload, or 40000 concurrent-client benchmark."
            }, new JsonSerializerOptions { WriteIndented=true }));
        }
    }
}
