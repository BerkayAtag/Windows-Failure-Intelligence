using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WFI.Infrastructure;
using Xunit;
namespace WFI.Tests;
public class DevelopmentSchemaTests
{
    [Fact]
    public async Task ExistingDemoAddsJsonVersionColumnWithoutLosingAnalysisAndUpgradeIsRepeatable()
    {
        using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        using var db=new WfiDb(new DbContextOptionsBuilder<WfiDb>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var id=Guid.NewGuid();db.AiAnalysisJobs.Add(new(){Id=id,Conclusion="Synthetic retained analysis"});await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE AiAnalysisJobs DROP COLUMN VersionsJson");db.ChangeTracker.Clear();
        await DevelopmentSchema.EnsureAsync(db);await DevelopmentSchema.EnsureAsync(db);
        var analysis=await db.AiAnalysisJobs.SingleAsync(x=>x.Id==id);
        Assert.Equal("{}",analysis.VersionsJson);Assert.Equal("Synthetic retained analysis",analysis.Conclusion);
    }
}

