using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WFI.Core;

namespace WFI.Infrastructure;
public sealed class AnalysisWorker(IServiceScopeFactory scopes, ILogger<AnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ProcessOne(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Worker cycle failed ({Type})", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
    public async Task<bool> ProcessOne(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfiDb>();
        var now = DateTime.UtcNow;
        var candidate = await db.Jobs.AsNoTracking().Where(j => j.Status == "pending" || (j.Status == "running" && j.LeaseUntil < now)).OrderBy(j => j.CreatedAt).FirstOrDefaultAsync(ct);
        if (candidate == null) return false;
        var token = Guid.NewGuid();
        var claimed = await db.Jobs.Where(j => j.Id == candidate.Id && (j.Status == "pending" || (j.Status == "running" && j.LeaseUntil < now)))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "running").SetProperty(j => j.LeaseToken, token).SetProperty(j => j.LeaseUntil, now.AddMinutes(5)).SetProperty(j => j.Attempts, j => j.Attempts + 1), ct);
        if (claimed == 0) return true;
        string? error = null, result = null;
        try
        {
            if (candidate.Attempts >= 3) throw new InputRejectedException("İş üç denemede tamamlanamadı; operatör incelemesi gerekiyor.");
            await using var stream = File.OpenRead(candidate.StorageKey);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            result = JsonSerializer.Serialize(new LogAnalyzer().Analyze(stream, candidate.Filename, timeout.Token), JsonOptions);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (InputRejectedException ex) { error = ex.Message; }
        catch (Exception ex) { error = "Dosya çözümlenemedi. Biçimi, XML yapısını ve kodlamayı kontrol edin."; logger.LogWarning("Analysis {Id} rejected ({Type})", candidate.Id, ex.GetType().Name); }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var completed = await db.Jobs.Where(j => j.Id == candidate.Id && j.LeaseToken == token)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, error == null ? "completed" : "failed").SetProperty(j => j.ResultJson, result).SetProperty(j => j.Error, error).SetProperty(j => j.CompletedAt, DateTime.UtcNow).SetProperty(j => j.LeaseUntil, (DateTime?)null), ct);
        if (completed == 1)
        {
            db.Audit.Add(new() { Domain=candidate.Domain, Actor = "worker", Action = error == null ? "analysis.completed" : "analysis.failed", Target = candidate.Id.ToString() });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return true;
    }
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

