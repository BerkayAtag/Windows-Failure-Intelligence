using Microsoft.EntityFrameworkCore;
using WFI.Core;
namespace WFI.Infrastructure;

public static class DevelopmentSchema
{
    // SQLite demo upgrade only. Production PostgreSQL uses reviewed migrations.
    public static async Task EnsureAsync(WfiDb db,CancellationToken ct=default)
    {
        if(!db.Database.IsSqlite()){await db.Database.EnsureCreatedAsync(ct);return;}
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection=db.Database.GetDbConnection();
            await using var auditColumns=connection.CreateCommand();auditColumns.CommandText="PRAGMA table_info('Audit')";
            var auditNames=new HashSet<string>();
            await using(var reader=await auditColumns.ExecuteReaderAsync(ct)){while(await reader.ReadAsync(ct))auditNames.Add(reader.GetString(1));}
            if(auditNames.Count>0&&!auditNames.Contains("Domain"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Audit\" ADD COLUMN \"Domain\" TEXT NOT NULL DEFAULT ''",ct);
            await using var columns=connection.CreateCommand();columns.CommandText="PRAGMA table_info('Devices')";
            var names=new HashSet<string>();
            await using(var reader=await columns.ExecuteReaderAsync(ct)){while(await reader.ReadAsync(ct))names.Add(reader.GetString(1));}
            if(names.Count>0)
            {
                foreach(var column in new[]{"NextDiagnosticAt","NextAutomaticAt"})
                    if(!names.Contains(column))
                    {
                        await using var change=connection.CreateCommand();change.CommandText=$"ALTER TABLE \"Devices\" ADD COLUMN \"{column}\" TEXT NULL";await change.ExecuteNonQueryAsync(ct);
                    }
            }
            await using var diagnosticColumns=connection.CreateCommand();diagnosticColumns.CommandText="PRAGMA table_info('DiagnosticJobs')";
            var diagnosticNames=new HashSet<string>();
            await using(var reader=await diagnosticColumns.ExecuteReaderAsync(ct)){while(await reader.ReadAsync(ct))diagnosticNames.Add(reader.GetString(1));}
            if(diagnosticNames.Count>0&&!diagnosticNames.Contains("ProblemDescription"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"DiagnosticJobs\" ADD COLUMN \"ProblemDescription\" TEXT NOT NULL DEFAULT ''",ct);
            var sql=db.Database.GenerateCreateScript().Replace("CREATE TABLE ","CREATE TABLE IF NOT EXISTS ").Replace("CREATE UNIQUE INDEX ","CREATE UNIQUE INDEX IF NOT EXISTS ").Replace("CREATE INDEX ","CREATE INDEX IF NOT EXISTS ");
            await using var memoryColumns=connection.CreateCommand();memoryColumns.CommandText="PRAGMA table_info('RemediationExecutions')";
            var memoryNames=new HashSet<string>();
            await using(var reader=await memoryColumns.ExecuteReaderAsync(ct)){while(await reader.ReadAsync(ct))memoryNames.Add(reader.GetString(1));}
            if(memoryNames.Count>0&&!memoryNames.Contains("LearningExcluded"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"RemediationExecutions\" ADD COLUMN \"LearningExcluded\" INTEGER NOT NULL DEFAULT 0",ct);
            if(memoryNames.Count>0&&!memoryNames.Contains("CampaignId"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"RemediationExecutions\" ADD COLUMN \"CampaignId\" TEXT NULL",ct);
            if(memoryNames.Count>0&&!memoryNames.Contains("CampaignPilot"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"RemediationExecutions\" ADD COLUMN \"CampaignPilot\" INTEGER NOT NULL DEFAULT 0",ct);
            await using var aiColumns=connection.CreateCommand();aiColumns.CommandText="PRAGMA table_info('AiAnalysisJobs')";
            var aiNames=new HashSet<string>();
            await using(var reader=await aiColumns.ExecuteReaderAsync(ct)){while(await reader.ReadAsync(ct))aiNames.Add(reader.GetString(1));}
            if(aiNames.Count>0&&!aiNames.Contains("LearningContextJson"))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AiAnalysisJobs\" ADD COLUMN \"LearningContextJson\" TEXT NOT NULL DEFAULT '[]'",ct);
            if(aiNames.Count>0&&!aiNames.Contains("VersionsJson"))
            {
                await using var change=connection.CreateCommand();
                change.CommandText="ALTER TABLE \"AiAnalysisJobs\" ADD COLUMN \"VersionsJson\" TEXT NOT NULL DEFAULT '{}'";
                await change.ExecuteNonQueryAsync(ct);
            }
            // EF seed INSERTs cannot be replayed. SQLite IGNORE applies only to immutable catalog seeds here.
            sql=sql.Replace("INSERT INTO \"Playbooks\"","INSERT OR IGNORE INTO \"Playbooks\"");
            await using var create=connection.CreateCommand();create.CommandText=sql;
            await create.ExecuteNonQueryAsync(ct);
            foreach(var definition in RemediationEngine.Catalog)
                await db.Playbooks.Where(p=>p.Code==definition.Code).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Version,RemediationEngine.Version),ct);
        }
        finally{await db.Database.CloseConnectionAsync();}
    }
}
