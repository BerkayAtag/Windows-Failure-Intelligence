using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;
public sealed class TestApp(string? sharedRoot = null, int diagnosticLimit = 1000) : WebApplicationFactory<Program>
{
    public string Root { get; } = sharedRoot ?? Path.Combine(Path.GetTempPath(), "wfi-test-" + Guid.NewGuid());
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Root);
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?> { ["Diagnostics:MaxActivePerDomain"] = diagnosticLimit.ToString() }));
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?> { ["Demo:Enabled"] = "true", ["Worker:Enabled"] = "false", ["Storage:Path"] = Path.Combine(Root, "logs"), ["DataProtection:Path"] = Path.Combine(Root,"keys"), ["Ollama:BaseUrl"] = "http://localhost:11434", ["Ollama:DefaultModel"] = "phi4-mini" }));
        builder.ConfigureServices(services =>
        {
            foreach (var worker in services.Where(s => s.ImplementationType == typeof(AnalysisWorker) || s.ImplementationType == typeof(DiagnosticJobProcessor)).ToList()) services.Remove(worker);
            services.RemoveAll<DbContextOptions<WfiDb>>(); services.RemoveAll<WfiDb>();
            services.AddDbContext<WfiDb>(o => o.UseSqlite("Data Source=" + Path.Combine(Root, "test.db")));
            services.RemoveAll<IAuthenticator>(); services.AddSingleton<IAuthenticator, FakeAuthenticator>();
            services.RemoveAll<IOllamaService>(); services.AddSingleton<IOllamaService, OllamaService>();
        });
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing && sharedRoot == null && Directory.Exists(Root)) { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(Root, true); } }
    private sealed class FakeAuthenticator : IAuthenticator
    {
        public ClaimsPrincipal? Authenticate(string username, string password) => password != "test-password" ? null : new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username), new Claim("domain", username == "foreign" ? "other.example" : "example.invalid"), new Claim(ClaimTypes.Role, username == "viewer" ? "Viewer" : username == "admin" ? "Admin" : "Analyst") }, "Cookies"));
    }
}
public class ApiTests
{
    private static async Task Token(HttpClient client) { client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString()); }
    private static async Task Login(HttpClient client, string user = "analyst") { await Token(client); var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = user, password = "test-password" }); Assert.Equal(HttpStatusCode.OK, login.StatusCode); await Token(client); }
    private static MultipartFormDataContent Upload(string hostname = "PC-001") { var form = new MultipartFormDataContent(); form.Add(new StringContent(hostname), "hostname"); form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("FIRST_BOOT\n0xC1900101")), "file", "setuperr.log"); return form; }
    [Fact] public async Task AnonymousCannotReadAnalyses() { using var app = new TestApp(); using var client = app.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/analyses")).StatusCode); }
    [Fact] public async Task LoginRequiresCsrf() { using var app = new TestApp(); using var client = app.CreateClient(); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username="analyst", password="test-password" })).StatusCode); }
    [Fact] public async Task ViewerCannotUploadOrReadAudit() { using var app = new TestApp(); using var client = app.CreateClient(); await Login(client, "viewer"); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/analyses/upload", Upload())).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/audit")).StatusCode); }
    [Fact] public async Task UploadDeduplicatesAndWorkerProducesEvidence()
    {
        using var app = new TestApp(); using var client = app.CreateClient(); await Login(client);
        var first = await client.PostAsync("/api/v1/analyses/upload", Upload()); Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>(); var id = body.GetProperty("id").GetGuid();
        var again = await client.PostAsync("/api/v1/analyses/upload", Upload("pc-001")); Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(id, (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        var worker = ActivatorUtilities.CreateInstance<AnalysisWorker>(app.Services); Assert.True(await worker.ProcessOne(default)); Assert.False(await worker.ProcessOne(default));
        var job = await client.GetFromJsonAsync<JsonElement>("/api/v1/analyses/" + id); Assert.Equal("completed", job.GetProperty("status").GetString()); Assert.Equal("UPG-001", job.GetProperty("result").GetProperty("findings")[0].GetProperty("ruleId").GetString());
        using var foreign = app.CreateClient(); await Login(foreign, "foreign"); Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync("/api/v1/analyses/" + id)).StatusCode);
        var other = await foreign.PostAsync("/api/v1/analyses/upload", Upload()); Assert.Equal(HttpStatusCode.Accepted, other.StatusCode); Assert.NotEqual(id, (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }
    [Fact] public async Task LogoutRevokesClientCookie() { using var app = new TestApp(); using var client = app.CreateClient(); await Login(client); Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode); }
    [Fact] public async Task InvalidHostnameIsRejected() { using var app = new TestApp(); using var client = app.CreateClient(); await Login(client); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/analyses/upload", Upload("../bad"))).StatusCode); }
    [Fact] public async Task UnknownApiReturns404() { using var app = new TestApp(); using var client = app.CreateClient(); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/does-not-exist")).StatusCode); }
    [Fact] public async Task ReadinessQueriesDatabase() { using var app = new TestApp(); using var client = app.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/ready")).StatusCode); }
    [Fact] public async Task RepeatedLoginAttemptsAreLimited()
    {
        using var app = new TestApp(); using var client = app.CreateClient(); await Token(client);
        for (var i=0; i<5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username="analyst", password="wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username="analyst", password="wrong" })).StatusCode);
    }
    [Fact] public async Task ApiResponsesPreventCachingAndFraming()
    {
        using var app = new TestApp(); using var client = app.CreateClient(); var response = await client.GetAsync("/api/v1/health");
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }
    [Fact] public async Task LogoutRejectsReplayedCookie()
    {
        using var app = new TestApp(); using var client = app.CreateClient(); await Token(client);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username="analyst", password="test-password" });
        var cookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("WFI.Session=")).Split(';')[0];
        await Token(client); Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        using var replay = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/v1/auth/me")).StatusCode);
    }
    [Fact] public async Task SharedKeysAllowCrossNodeSessionAndLogoutRevokesOnBothNodes()
    {
        using var first = new TestApp(); using var client = first.CreateClient(); await Token(client);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username="analyst", password="test-password" });
        var cookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("WFI.Session=")).Split(';')[0];
        using var second = new TestApp(first.Root); using var other = second.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies=false });
        other.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/v1/auth/me")).StatusCode);
        await Token(client); Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/v1/auth/me")).StatusCode);
    }
    [Fact] public async Task StatusPollingIsDomainScopedAndDoesNotWriteAudit()
    {
        using var app = new TestApp(); using var client = app.CreateClient(); await Login(client);
        var upload = await client.PostAsync("/api/v1/analyses/upload", Upload());
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<WfiDb>();
        var before = await db.Audit.CountAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/analyses/{id}/status")).StatusCode);
        Assert.Equal(before, await db.Audit.CountAsync());
        using var foreign = app.CreateClient(); await Login(foreign, "foreign");
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/v1/analyses/{id}/status")).StatusCode);
    }
}
