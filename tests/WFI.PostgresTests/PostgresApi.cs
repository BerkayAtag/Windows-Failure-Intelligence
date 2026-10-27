using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.PostgresTests;

internal sealed class PostgresApi(string connectionString,string? sharedRoot=null,int diagnosticLimit=1000):WebApplicationFactory<Program>
{
    private readonly string root=sharedRoot??Path.Combine(Path.GetTempPath(),"wfi-pg-api-"+Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{["Diagnostics:MaxActivePerDomain"]=diagnosticLimit.ToString()}));
        builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{
            ["Demo:Enabled"]="true",["Worker:Enabled"]="false",["Storage:Path"]=Path.Combine(root,"logs"),["DataProtection:Path"]=Path.Combine(root,"keys")
        }));
        builder.ConfigureServices(services=>{
            foreach(var service in services.Where(s=>s.ImplementationType==typeof(AnalysisWorker)||s.ImplementationType==typeof(DiagnosticJobProcessor)).ToList())services.Remove(service);
            services.RemoveAll<DbContextOptions<WfiDb>>();services.RemoveAll<IDbContextOptionsConfiguration<WfiDb>>();services.RemoveAll<WfiDb>();
            services.AddDbContext<WfiDb>(o=>o.UseNpgsql(connectionString));
            services.RemoveAll<IAuthenticator>();services.AddSingleton<IAuthenticator,TestAuthenticator>();
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if(disposing&&sharedRoot==null&&Directory.Exists(root))Directory.Delete(root,true);
    }
    private sealed class TestAuthenticator:IAuthenticator
    {
        public ClaimsPrincipal? Authenticate(string username,string password)=>password!="synthetic-password"?null:new(new ClaimsIdentity(new[]{
            new Claim(ClaimTypes.Name,username),new Claim("domain",username=="foreign"?"other.example":"synthetic.example"),new Claim(ClaimTypes.Role,username=="viewer"?"Viewer":"Admin")
        },"Cookies"));
    }
    private static async Task<string> Login(HttpClient client,string user)
    {
        async Task Csrf(){client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");var token=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());}
        await Csrf();using var response=await client.PostAsJsonAsync("/api/v1/auth/login",new{username=user,password="synthetic-password"});response.EnsureSuccessStatusCode();await Csrf();
        return response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("WFI.Session=",StringComparison.Ordinal)).Split(';')[0];
    }
    public static async Task VerifyLargeInventory(string connectionString,WfiDb db)
    {
        // The enclosing acceptance test owns an isolated, randomly named database.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Devices" ("Id","Domain","Hostname","IpAddress","OsVersion","AgentVersion","Status","LastHeartbeat","EnrolledAt","AutomationMode","SystemInfoJson")
            SELECT ('30000000-0000-0000-0000-' || lpad(n::text,12,'0'))::uuid,
              'synthetic.example','fleet-' || lpad((n-1)::text,5,'0'),'','synthetic','test','online',
              TIMESTAMPTZ '2000-01-01 00:00:00+00',TIMESTAMPTZ '2000-01-01 00:00:00+00','Disabled',repeat('x',1024)
            FROM generate_series(1,40000) AS n;
            """);
        var foreign=new Device{Domain="other.example",Hostname="inventory-foreign"};db.Devices.Add(foreign);await db.SaveChangesAsync();
        using var app=new PostgresApi(connectionString);using var client=app.CreateClient();await Login(client,"viewer");
        foreach(var page in new[]{1,2,400,800,1})
        {
            using var response=await client.GetAsync($"/api/v1/devices?page={page}");response.EnsureSuccessStatusCode();
            var body=await response.Content.ReadAsStringAsync();using var doc=JsonDocument.Parse(body);var root=doc.RootElement;
            Assert.Equal(40000,root.GetProperty("total").GetInt32());var items=root.GetProperty("items");Assert.Equal(50,items.GetArrayLength());var index=0;
            foreach(var item in items.EnumerateArray())
            {
                Assert.Equal("synthetic.example",item.GetProperty("domain").GetString());
                Assert.Equal($"fleet-{39999-(page-1)*50-index++:D5}",item.GetProperty("hostname").GetString());
            }
            Assert.True(body.Length<32768);Assert.DoesNotContain("systemInfoJson",body);Assert.DoesNotContain("agentCredentialHash",body);Assert.DoesNotContain("enrollmentToken",body);
        }
        var filtered=await client.GetFromJsonAsync<JsonElement>("/api/v1/devices?search=%20FLEET-39999%20");Assert.Equal(1,filtered.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/v1/devices/{foreign.Id}")).StatusCode);
        await db.Devices.Where(d=>d.Domain=="synthetic.example"&&d.Hostname.StartsWith("fleet-")||d.Id==foreign.Id).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
    }
    public static async Task VerifySharedSessions(string connectionString)
    {
        var shared=Path.Combine(Path.GetTempPath(),"wfi-pg-shared-"+Guid.NewGuid().ToString("N"));
        try
        {
            using var first=new PostgresApi(connectionString,shared);
            first.UseKestrel(options=>options.Listen(IPAddress.Loopback,0));
            using var firstClient=first.CreateClient();
            Assert.True(firstClient.BaseAddress!.IsLoopback);
            using var networkProbe=new HttpClient{BaseAddress=firstClient.BaseAddress,Timeout=TimeSpan.FromSeconds(5)};
            Assert.Equal(HttpStatusCode.OK,(await networkProbe.GetAsync("/api/v1/health")).StatusCode);
            var cookie=await Login(firstClient,"admin");
            using var second=new PostgresApi(connectionString,shared);
            second.UseKestrel(options=>options.Listen(IPAddress.Loopback,0));
            second.ClientOptions.HandleCookies=false;
            using var secondClient=second.CreateClient();
            Assert.True(secondClient.BaseAddress!.IsLoopback);
            Assert.NotEqual(firstClient.BaseAddress.Port,secondClient.BaseAddress.Port);
            secondClient.DefaultRequestHeaders.Add("Cookie",cookie);
            Assert.Equal(HttpStatusCode.OK,(await secondClient.GetAsync("/api/v1/auth/me")).StatusCode);
            (await firstClient.PostAsync("/api/v1/auth/logout",null)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized,(await secondClient.GetAsync("/api/v1/auth/me")).StatusCode);
            cookie=await Login(firstClient,"admin");
            secondClient.DefaultRequestHeaders.Remove("Cookie");secondClient.DefaultRequestHeaders.Add("Cookie",cookie);
            Assert.Equal(HttpStatusCode.OK,(await secondClient.GetAsync("/api/v1/auth/me")).StatusCode);
            first.Dispose();
            await Assert.ThrowsAsync<HttpRequestException>(()=>networkProbe.GetAsync("/api/v1/health"));
            Assert.Equal(HttpStatusCode.OK,(await secondClient.GetAsync("/api/v1/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await secondClient.GetAsync("/api/v1/automation")).StatusCode);
        }
        finally {if(Directory.Exists(shared))Directory.Delete(shared,true);}
    }
    public static async Task VerifyPolicies(string connectionString,WfiDb db)
    {
        using var app=new PostgresApi(connectionString);
        using var admin=app.CreateClient();using var viewer=app.CreateClient();using var foreign=app.CreateClient();
        await Login(admin,"admin");await Login(viewer,"viewer");await Login(foreign,"foreign");
        var own=new Device{Hostname="policy-own",Domain="synthetic.example",AutomationMode="Disabled"};
        var other=new Device{Hostname="policy-other",Domain="other.example",AutomationMode="Enabled"};
        db.Devices.AddRange(own,other);await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden,(await viewer.PutAsJsonAsync("/api/v1/automation",new{enabled=true,applyToAll=true})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await viewer.PutAsJsonAsync($"/api/v1/devices/{own.Id}/automation",new{mode="Enabled"})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await foreign.PutAsJsonAsync($"/api/v1/devices/{own.Id}/automation",new{mode="Enabled"})).StatusCode);
        (await admin.PutAsJsonAsync("/api/v1/automation",new{enabled=true,applyToAll=true})).EnsureSuccessStatusCode();
        await db.Entry(own).ReloadAsync();await db.Entry(other).ReloadAsync();
        Assert.Equal("Inherit",own.AutomationMode);Assert.Equal("Enabled",other.AutomationMode);
        Assert.True((await db.AutomationPolicies.SingleAsync(p=>p.Domain==own.Domain)).Enabled);
        Assert.False(await db.AutomationPolicies.AnyAsync(p=>p.Domain==other.Domain));
        Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="automation.all-devices"));
        (await admin.PutAsJsonAsync($"/api/v1/devices/{own.Id}/automation",new{mode="Disabled"})).EnsureSuccessStatusCode();
        await db.Entry(own).ReloadAsync();Assert.Equal("Disabled",own.AutomationMode);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_policy_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW."Action" LIKE 'automation.%' THEN RAISE EXCEPTION 'synthetic audit failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_policy_audit BEFORE INSERT ON "Audit" FOR EACH ROW EXECUTE FUNCTION reject_policy_audit();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await admin.PutAsJsonAsync("/api/v1/automation",new{enabled=false,applyToAll=true})).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await admin.PutAsJsonAsync($"/api/v1/devices/{own.Id}/automation",new{mode="Enabled"})).StatusCode);
            await db.Entry(own).ReloadAsync();Assert.Equal("Disabled",own.AutomationMode);
            Assert.True((await db.AutomationPolicies.AsNoTracking().SingleAsync(p=>p.Domain==own.Domain)).Enabled);
            Assert.Equal(2,await db.Audit.CountAsync(a=>a.Action.StartsWith("automation.")));
        }
        finally {await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_policy_audit ON \"Audit\"; DROP FUNCTION reject_policy_audit();");}
    }
    public static async Task VerifyDuplicateDiagnostics(string connectionString,WfiDb db)
    {
        using var app=new PostgresApi(connectionString);
        using var first=app.CreateClient();using var second=app.CreateClient();await Login(first,"admin");await Login(second,"admin");
        var device=new Device{Domain="synthetic.example",Hostname="duplicate-pg"};db.Devices.Add(device);await db.SaveChangesAsync();
        var responses=await Task.WhenAll(first.PostAsJsonAsync("/api/v1/diagnostics",new{hostname=device.Hostname,scanType="FullSystemScan"}),second.PostAsJsonAsync("/api/v1/diagnostics",new{hostname=device.Hostname,scanType="NetworkScan"}));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        var job=await db.DiagnosticJobs.SingleAsync(j=>j.DeviceId==device.Id);
        Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="diagnostic.requested"));
        (await first.PostAsync($"/api/v1/diagnostics/{job.Id}/cancel",null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created,(await second.PostAsJsonAsync("/api/v1/diagnostics",new{hostname=device.Hostname,scanType="NetworkScan"})).StatusCode);
        Assert.Equal(2,await db.DiagnosticJobs.CountAsync(j=>j.DeviceId==device.Id));
    }
    public static async Task VerifyDomainAdmission(string connectionString,WfiDb db)
    {
        var limit=1+await db.DiagnosticJobs.CountAsync(j=>j.Domain=="synthetic.example"&&(j.Status=="pending"||j.Status=="running"||j.Status=="evidence_received"||j.Status=="analyzing"));
        using var nodeA=new PostgresApi(connectionString,diagnosticLimit:limit);using var nodeB=new PostgresApi(connectionString,diagnosticLimit:limit);
        using var a=nodeA.CreateClient();using var b=nodeB.CreateClient();await Login(a,"admin");await Login(b,"admin");
        db.Devices.AddRange(new Device{Domain="synthetic.example",Hostname="admission-a"},new Device{Domain="synthetic.example",Hostname="admission-b"});await db.SaveChangesAsync();
        var responses=await Task.WhenAll(a.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="admission-a",scanType="FullSystemScan"}),b.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="admission-b",scanType="FullSystemScan"}));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.TooManyRequests);
        Assert.Equal(limit,await db.DiagnosticJobs.CountAsync(j=>j.Domain=="synthetic.example"&&(j.Status=="pending"||j.Status=="running"||j.Status=="evidence_received"||j.Status=="analyzing")));
    }
    public static async Task VerifyReanalysisRace(string connectionString,WfiDb db)
    {
        using var nodeA=new PostgresApi(connectionString);using var nodeB=new PostgresApi(connectionString);
        using var a=nodeA.CreateClient();using var b=nodeB.CreateClient();await Login(a,"admin");await Login(b,"admin");
        var device=new Device{Domain="synthetic.example",Hostname="reanalysis-race"};db.Devices.Add(device);
        var old=new DiagnosticJob{DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,Status="completed",EvidenceJson="[]",CompletedAt=DateTime.UtcNow};db.DiagnosticJobs.Add(old);await db.SaveChangesAsync();
        var results=await Task.WhenAll(a.PostAsync($"/api/v1/ai/analyze/{old.Id}",null),b.PostAsJsonAsync("/api/v1/diagnostics",new{hostname=device.Hostname,scanType="FullSystemScan"}));
        Assert.Single(results,r=>r.IsSuccessStatusCode);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(1,await db.DiagnosticJobs.CountAsync(j=>j.DeviceId==device.Id&&(j.Status=="pending"||j.Status=="evidence_received")));
    }
    public static async Task VerifyCancellationRace(string connectionString,WfiDb db)
    {
        using var app=new PostgresApi(connectionString);using var client=app.CreateClient();await Login(client,"admin");
        var book=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        var device=new Device{Domain="synthetic.example",Hostname="cancel-race",Status="online"};db.Devices.Add(device);
        var job=new RemediationExecution{Domain=device.Domain,DeviceId=device.Id,PlaybookId=book.Id,PlaybookCode=book.Code,Status="Approved"};db.RemediationExecutions.Add(job);await db.SaveChangesAsync();
        using var scope=app.Services.CreateScope();var claimDb=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var claimTask=new RemediationCoordinator(claimDb,new AgentCoordinator(claimDb)).ClaimAsync(device,default);
        var cancelTask=client.PostAsync($"/api/v1/remediations/{job.Id}/cancel",null);
        await Task.WhenAll(claimTask,cancelTask);
        var claim=await claimTask;using var cancelled=await cancelTask;await db.Entry(job).ReloadAsync();
        if(claim==null){Assert.Equal(HttpStatusCode.OK,cancelled.StatusCode);Assert.Equal("Cancelled",job.Status);Assert.Null(job.ExecutedAt);}
        else{Assert.Equal(HttpStatusCode.Conflict,cancelled.StatusCode);Assert.Equal("Executing",job.Status);Assert.Equal(job.Id,claim.Id);}
        Assert.Equal(claim==null?1:0,await db.Audit.CountAsync(a=>a.Action=="remediation.cancelled"&&a.Target==job.Id.ToString()));
    }
    public static async Task VerifyDuplicateRepairs(string connectionString,WfiDb db)
    {
        using var first=new PostgresApi(connectionString);using var second=new PostgresApi(connectionString);
        using var a=first.CreateClient();using var b=second.CreateClient();await Login(a,"admin");await Login(b,"admin");
        var device=new Device{Domain="synthetic.example",Hostname="repair-duplicate"};db.Devices.Add(device);await db.SaveChangesAsync();
        var request=new{hostname=device.Hostname,domain=device.Domain,playbookCode="PB-DNS-FLUSH"};
        var results=await Task.WhenAll(a.PostAsJsonAsync("/api/v1/remediations",request),b.PostAsJsonAsync("/api/v1/remediations",request));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);
        var job=await db.RemediationExecutions.SingleAsync(r=>r.DeviceId==device.Id);
        Assert.Equal(1,await db.Audit.CountAsync(x=>x.Action=="remediation.requested"&&x.Target==job.Id.ToString()));
        (await a.PostAsync($"/api/v1/remediations/{job.Id}/cancel",null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created,(await b.PostAsJsonAsync("/api/v1/remediations",request)).StatusCode);
    }
    private sealed class SyntheticModel:IOllamaService
    {
        public Task<List<OllamaModel>> GetModelsAsync(CancellationToken ct=default)=>Task.FromResult(new List<OllamaModel>());
        public Task<string> GetActiveModelAsync(CancellationToken ct=default)=>Task.FromResult("synthetic");
        public Task SetActiveModelAsync(string model,CancellationToken ct=default)=>Task.CompletedTask;
        public Task<AiAnalysisJob> DiagnoseAsync(DiagnosticJob job,List<Finding> findings,CancellationToken ct=default)=>Task.FromResult(new AiAnalysisJob{DiagnosticJobId=job.Id,Domain=job.Domain,Hostname=job.Hostname,Status="completed",Conclusion="Synthetic",ConfidenceScore=.5});
    }
    public static async Task VerifyOldAutomaticExclusion(string connectionString,WfiDb db)
    {
        // Finish earlier synthetic queue fixtures before exercising the real worker selection.
        await db.DiagnosticJobs.Where(j=>j.Status=="evidence_received"||j.Status=="analyzing").ExecuteUpdateAsync(s=>s.SetProperty(j=>j.Status,"failed"));
        using var app=new PostgresApi(connectionString);using var client=app.CreateClient();
        var book=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        foreach(var status in new[]{"Approved","Unknown"})
        {
            var device=new Device{Domain="synthetic.example",Hostname="old-auto-"+status.ToLowerInvariant(),AutomationMode="Enabled"};db.Devices.Add(device);
            db.RemediationExecutions.Add(new(){DeviceId=device.Id,Domain=device.Domain,PlaybookId=book.Id,PlaybookCode=book.Code,Status=status,RequestedAt=DateTime.UtcNow.AddDays(-2)});
            var job=new DiagnosticJob{DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,Status="evidence_received",EvidenceJson=JsonSerializer.Serialize(new[]{new EvidenceItem{Source="synthetic",Content="ERROR_NO_LOGON_SERVERS"}},AnalysisWorker.JsonOptions)};
            db.DiagnosticJobs.Add(job);await db.SaveChangesAsync();
            var worker=new DiagnosticJobProcessor(app.Services.GetRequiredService<IServiceScopeFactory>(),new SyntheticModel(),Microsoft.Extensions.Logging.Abstractions.NullLogger<DiagnosticJobProcessor>.Instance);
            Assert.True(await worker.ProcessOneAsync(default));await db.Entry(job).ReloadAsync();Assert.Equal("completed",job.Status);
            Assert.Equal(1,await db.RemediationExecutions.CountAsync(r=>r.DeviceId==device.Id));
        }
    }
}
