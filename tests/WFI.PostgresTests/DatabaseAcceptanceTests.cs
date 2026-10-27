using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.PostgresTests;

public class DatabaseAcceptanceTests
{
    private sealed class InstalledModels : IHttpClientFactory
    {
        private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int requests;
        private sealed class Handler(InstalledModels owner):HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
            {
                if(Interlocked.Increment(ref owner.requests)>=2)owner.ready.TrySetResult();
                await owner.ready.Task.WaitAsync(ct);
                return new(System.Net.HttpStatusCode.OK){Content=new StringContent("{\"models\":[{\"name\":\"synthetic-a:latest\"},{\"name\":\"synthetic-b:latest\"}]}")};
            }
        }
        public HttpClient CreateClient(string name)=>new(new Handler(this));
    }
    private static async Task VerifyModelSelection(string connectionString,WfiDb db)
    {
        using var firstServices=new ServiceCollection().AddDbContext<WfiDb>(o=>o.UseNpgsql(connectionString)).BuildServiceProvider();
        using var secondServices=new ServiceCollection().AddDbContext<WfiDb>(o=>o.UseNpgsql(connectionString)).BuildServiceProvider();
        var clients=new InstalledModels();var configuration=new ConfigurationBuilder().Build();
        using var first=new OllamaService(clients,configuration,firstServices.GetRequiredService<IServiceScopeFactory>());
        using var second=new OllamaService(clients,configuration,secondServices.GetRequiredService<IServiceScopeFactory>());
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await Task.WhenAll(first.SetActiveModelAsync("synthetic-a",timeout.Token),second.SetActiveModelAsync("synthetic-b",timeout.Token));
        var row=await db.AiConfigs.SingleAsync();Assert.Equal(1,row.Id);
        Assert.Contains(row.ActiveModel,new[]{"synthetic-a:latest","synthetic-b:latest"});
        row.Temperature=0.6;row.SystemPrompt="Synthetic preserved setting";await db.SaveChangesAsync();
        await first.SetActiveModelAsync("synthetic-a",timeout.Token);
        await Assert.ThrowsAsync<InputRejectedException>(()=>second.SetActiveModelAsync("not-installed",timeout.Token));
        Assert.Equal("synthetic-a:latest",await second.GetActiveModelAsync(timeout.Token));
        await db.Entry(row).ReloadAsync();Assert.Equal(0.6,row.Temperature);Assert.Equal("Synthetic preserved setting",row.SystemPrompt);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task MigrationsAndConcurrentDispatchUseRealPostgres(bool useDeploymentSql)
    {
        var source=Environment.GetEnvironmentVariable("WFI_TEST_POSTGRES")??throw new InvalidOperationException("Set WFI_TEST_POSTGRES to an isolated loopback PostgreSQL cluster; never production.");
        var adminSettings=new NpgsqlConnectionStringBuilder(source);
        if(adminSettings.Host is not ("127.0.0.1" or "localhost" or "::1"))throw new InvalidOperationException("Only loopback test clusters are allowed.");
        adminSettings.Database="postgres";adminSettings.Pooling=false;
        var database="wfi_test_"+Guid.NewGuid().ToString("N");
        await using var admin=new NpgsqlConnection(adminSettings.ConnectionString);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE DATABASE \"{database}\"",admin))await create.ExecuteNonQueryAsync();
        var settings=new NpgsqlConnectionStringBuilder(adminSettings.ConnectionString){Database=database};
        var options=new DbContextOptionsBuilder<WfiDb>().UseNpgsql(settings.ConnectionString).Options;
        try
        {
            await using var db=new WfiDb(options);
            if(useDeploymentSql)
            {
                var directory=new DirectoryInfo(AppContext.BaseDirectory);
                while(directory!=null&&!File.Exists(Path.Combine(directory.FullName,"deploy","schema.sql")))directory=directory.Parent;
                Assert.NotNull(directory);
                var sql=await File.ReadAllTextAsync(Path.Combine(directory.FullName,"deploy","schema.sql"));
                await db.Database.ExecuteSqlRawAsync(sql);
                await db.Database.ExecuteSqlRawAsync(sql);
            }
            await db.Database.MigrateAsync();await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(RemediationEngine.Catalog.Length,await db.Playbooks.CountAsync());
            await PostgresApi.VerifyLargeInventory(settings.ConnectionString,db);
            await VerifyModelSelection(settings.ConnectionString,db);
            await using(var other=new WfiDb(options))
            {
                var leases=await Task.WhenAll(ModelCapacity.TryAcquireAsync(db,1,default),ModelCapacity.TryAcquireAsync(other,1,default));
                Assert.Single(leases,x=>x!=null);var owner=leases.Single(x=>x!=null)!;
                await db.ModelSlots.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LeaseUntil,DateTime.UtcNow.AddMinutes(-1)));
                var replacement=await ModelCapacity.TryAcquireAsync(other,1,default);Assert.NotNull(replacement);
                Assert.False(await ModelCapacity.IsCurrentAsync(db,owner,default));
                Assert.Equal(0,await ModelCapacity.ReleaseAsync(db,owner,default));
                Assert.True(await ModelCapacity.IsCurrentAsync(other,replacement,default));
                await ModelCapacity.ReleaseAsync(other,replacement,default);
            }
            await PostgresApi.VerifyPolicies(settings.ConnectionString,db);
            await PostgresApi.VerifyDuplicateDiagnostics(settings.ConnectionString,db);
            await PostgresApi.VerifyDomainAdmission(settings.ConnectionString,db);
            await PostgresApi.VerifyReanalysisRace(settings.ConnectionString,db);
            await PostgresApi.VerifySharedSessions(settings.ConnectionString);
            await ProcessApi.Verify(settings.ConnectionString);
            var queuedDevice=new Device{Hostname="queue-filter",Domain="synthetic.example",Status="online",AutomationMode="Disabled"};db.Devices.Add(queuedDevice);
            var dns=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
            for(var i=0;i<12;i++)db.RemediationExecutions.Add(new(){DeviceId=queuedDevice.Id,PlaybookId=dns.Id,PlaybookCode=dns.Code,Status="Approved",Automatic=true,RequestedAt=DateTime.UtcNow.AddHours(-1)});
            var manual=new RemediationExecution{DeviceId=queuedDevice.Id,PlaybookId=dns.Id,PlaybookCode=dns.Code,Status="Approved"};db.RemediationExecutions.Add(manual);await db.SaveChangesAsync();
            var admitted=await new RemediationCoordinator(db,new AgentCoordinator(db)).ClaimAsync(queuedDevice,default);
            Assert.NotNull(admitted);Assert.Equal(manual.Id,admitted.Id);
            Assert.Equal(12,await db.RemediationExecutions.CountAsync(x=>x.DeviceId==queuedDevice.Id&&x.Status=="Approved"));
            await db.RemediationExecutions.Where(x=>x.DeviceId==queuedDevice.Id).ExecuteDeleteAsync();
            db.ChangeTracker.Clear();
            var device=new Device{Hostname="synthetic-postgres",Domain="synthetic.example",Status="online"};db.Devices.Add(device);
            var playbook=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
            for(var i=0;i<2;i++)db.RemediationExecutions.Add(new(){DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,PlaybookId=playbook.Id,PlaybookCode=playbook.Code,Status="Approved"});
            await db.SaveChangesAsync();
            async Task<RemediationAssignment?> Claim()
            {
                await using var context=new WfiDb(options);
                return await new RemediationCoordinator(context,new AgentCoordinator(context)).ClaimAsync(device,default);
            }
            var claims=await Task.WhenAll(Task.Run(Claim),Task.Run(Claim));
            var claimed=Assert.Single(claims,x=>x!=null)!;
            Assert.Equal(1,await db.RemediationExecutions.CountAsync(x=>x.Status=="Executing"));
            Assert.Equal(1,await db.RemediationExecutions.CountAsync(x=>x.Status=="Approved"));
            var coordinator=new RemediationCoordinator(db,new AgentCoordinator(db));
            var receipt=new RemediationReceipt(claimed.LeaseToken,true,"Synthetic","No OS command executed","Synthetic verification");
            Assert.True(await coordinator.CompleteAsync(device,claimed.Id,receipt,default));
            Assert.True(await coordinator.CompleteAsync(device,claimed.Id,receipt,default));
            Assert.Equal(1,await db.Audit.CountAsync(a=>a.Action=="remediation.result"));
            var next=await Claim();Assert.NotNull(next);Assert.NotEqual(claimed.Id,next.Id);
            await PostgresApi.VerifyCancellationRace(settings.ConnectionString,db);
            await PostgresApi.VerifyDuplicateRepairs(settings.ConnectionString,db);
            await PostgresApi.VerifyOldAutomaticExclusion(settings.ConnectionString,db);
            await VerifyCampaignParallelLimit(options,db);
        }
        finally
        {
            await using var drop=new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private static async Task VerifyCampaignParallelLimit(DbContextOptions<WfiDb> options,WfiDb db)
    {
        var now=DateTime.UtcNow;var book=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        var devices=Enumerable.Range(0,3).Select(i=>new Device{Domain="campaign.synthetic.invalid",Hostname="parallel-"+i,AutomationMode="Enabled"}).ToArray();
        db.Devices.AddRange(devices);
        var campaign=new RepairCampaign{Domain=devices[0].Domain,Status="Pilot",MaxParallel=1,PilotLimit=3,StartsAt=now.AddMinutes(-1),EndsAt=now.AddHours(1),ExpiresAt=now.AddHours(2)};
        db.RepairCampaigns.Add(campaign);
        db.RemediationExecutions.AddRange(devices.Select(d=>new RemediationExecution{DeviceId=d.Id,Domain=d.Domain,CampaignId=campaign.Id,CampaignPilot=true,PlaybookId=book.Id,PlaybookCode=book.Code,Status="Approved"}));
        await db.SaveChangesAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var claims=await Task.WhenAll(devices.Select(async device=>{
            await using var context=new WfiDb(options);
            return await new RemediationCoordinator(context,new AgentCoordinator(context)).ClaimAsync(device,timeout.Token);
        }));
        Assert.Single(claims,x=>x!=null);
        Assert.Equal(1,await db.RemediationExecutions.CountAsync(x=>x.CampaignId==campaign.Id&&x.Status=="Executing"));
        Assert.Equal(2,await db.RemediationExecutions.CountAsync(x=>x.CampaignId==campaign.Id&&x.Status=="Approved"));
    }
}
