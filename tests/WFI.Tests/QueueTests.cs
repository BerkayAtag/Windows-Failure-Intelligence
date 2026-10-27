using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;
public class QueueTests
{
    [Fact] public async Task ExpiredLeaseRecoversButLiveLeaseIsNotStolen()
    {
        using var app = new TestApp(); using var client = app.CreateClient();
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<WfiDb>();
        var path = Path.Combine(app.Root, "input.log"); await File.WriteAllTextAsync(path, "0x800F081F");
        var expired = new AnalysisJob { Domain="example.invalid", Hostname="pc1", StorageKey=path, Filename="CBS.log", DedupKey="expired", Status="running", LeaseToken=Guid.NewGuid(), LeaseUntil=DateTime.UtcNow.AddMinutes(-1), Attempts=1 };
        var active = new AnalysisJob { Domain="example.invalid", Hostname="pc2", StorageKey=path, Filename="CBS.log", DedupKey="active", Status="running", LeaseToken=Guid.NewGuid(), LeaseUntil=DateTime.UtcNow.AddMinutes(5), Attempts=1 };
        db.Jobs.AddRange(expired, active); await db.SaveChangesAsync();
        var worker = ActivatorUtilities.CreateInstance<AnalysisWorker>(app.Services);
        Assert.True(await worker.ProcessOne(default)); Assert.False(await worker.ProcessOne(default));
        await db.Entry(expired).ReloadAsync(); await db.Entry(active).ReloadAsync();
        Assert.Equal("completed", expired.Status); Assert.Equal(2, expired.Attempts); Assert.Equal("running", active.Status);
    }
    [Fact] public async Task MalformedFileFailsWithoutStoppingNextJob()
    {
        using var app = new TestApp(); using var client = app.CreateClient();
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<WfiDb>();
        var badPath = Path.Combine(app.Root, "bad.xml"); await File.WriteAllTextAsync(badPath, "<broken>");
        var goodPath = Path.Combine(app.Root, "good.log"); await File.WriteAllTextAsync(goodPath, "0xC1900101");
        var bad = new AnalysisJob { StorageKey=badPath, Filename="bad.xml", DedupKey="bad", CreatedAt=DateTime.UtcNow.AddSeconds(-1) };
        var good = new AnalysisJob { StorageKey=goodPath, Filename="good.log", DedupKey="good" };
        db.Jobs.AddRange(bad, good); await db.SaveChangesAsync();
        var worker = ActivatorUtilities.CreateInstance<AnalysisWorker>(app.Services);
        Assert.True(await worker.ProcessOne(default)); Assert.True(await worker.ProcessOne(default));
        await db.Entry(bad).ReloadAsync(); await db.Entry(good).ReloadAsync();
        Assert.Equal("failed", bad.Status); Assert.Equal("completed", good.Status);
        Assert.Equal(2, await db.Audit.CountAsync(a => a.Actor == "worker"));
    }
}
