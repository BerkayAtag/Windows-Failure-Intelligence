using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
namespace WFI.Infrastructure;

public sealed record OllamaModel(string Name);
public interface IOllamaService
{
    Task<List<OllamaModel>> GetModelsAsync(CancellationToken ct=default);
    Task<string> GetActiveModelAsync(CancellationToken ct=default);
    Task SetActiveModelAsync(string name,CancellationToken ct=default);
    Task<AiAnalysisJob> DiagnoseAsync(DiagnosticJob job,List<Finding> findings,CancellationToken ct=default);
}
public sealed class OllamaService(IHttpClientFactory clients,IConfiguration configuration,IServiceScopeFactory scopes):IOllamaService,IDisposable
{
    private readonly SemaphoreSlim concurrency=new(Math.Clamp(configuration.GetValue("Ollama:MaxConcurrent",1),1,64),64);
    public void Dispose()=>concurrency.Dispose();
    private string BaseUrl
    {
        get
        {
            var value=configuration["Ollama:BaseUrl"]??"http://127.0.0.1:11434";
            if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||!string.IsNullOrEmpty(uri.UserInfo)||
                (uri.Scheme!="https"&&!(uri.Scheme=="http"&&uri.IsLoopback)))throw new InvalidOperationException("Ollama requires HTTPS or loopback HTTP.");
            return value;
        }
    }
    public async Task<List<OllamaModel>> GetModelsAsync(CancellationToken ct=default)
    {
        using var client=clients.CreateClient("ollama");using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try{return (await new OllamaEngine(client).GetAvailableModelsAsync(BaseUrl,timeout.Token)).Select(x=>new OllamaModel(x)).ToList();}
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){return [];}
    }
    public async Task<string> GetActiveModelAsync(CancellationToken ct=default)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        return (await db.AiConfigs.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==1,ct))?.ActiveModel??configuration["Ollama:DefaultModel"]??"phi4-mini:latest";
    }
    public async Task SetActiveModelAsync(string name,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Length>128)throw new InputRejectedException("Geçersiz model adı.");
        var models=await GetModelsAsync(ct);var selected=models.FirstOrDefault(x=>x.Name==name||x.Name==name+":latest")?.Name;
        if(selected==null)throw new InputRejectedException("Model Ollama üzerinde kurulu değil veya servis erişilemiyor.");
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var defaults=new AiConfig();
        // A database upsert also serializes first-time saves from different API nodes.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AiConfigs" ("Id", "BaseUrl", "ActiveModel", "Temperature", "SystemPrompt", "IsAvailable")
            VALUES (1, {defaults.BaseUrl}, {selected}, {defaults.Temperature}, {defaults.SystemPrompt}, {defaults.IsAvailable})
            ON CONFLICT ("Id") DO UPDATE SET "ActiveModel" = excluded."ActiveModel"
            """,ct);
    }
    public async Task<AiAnalysisJob> DiagnoseAsync(DiagnosticJob job,List<Finding> findings,CancellationToken ct=default)
    {
        await concurrency.WaitAsync(ct);
        try
        {
            using var client=clients.CreateClient("ollama");using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Ollama:TimeoutSeconds",240),10,240)));
            using var scope=scopes.CreateScope();
            var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
            var lease=await ModelCapacity.TryAcquireAsync(db,configuration.GetValue("Ollama:MaxConcurrent",1),ct);
            if(lease==null){WfiTelemetry.ModelBusy.Add(1);throw new ModelCapacityUnavailableException();}
            var started=System.Diagnostics.Stopwatch.StartNew();
            using var activity=WfiTelemetry.Traces.StartActivity("ai.inference");
            try
            {
            var codes=RemediationEngine.Catalog.Where(p=>p.RuleIds.Any(id=>findings.Any(f=>f.RuleId==id))).Select(p=>p.Code).ToArray();
            var history=codes.Length==0?[]:await ExperienceMemory.ReadAsync(scope.ServiceProvider.GetRequiredService<WfiDb>(),job.Domain,job.ScanType,codes,timeout.Token);
            var result=await new OllamaEngine(client).DiagnoseAsync(new(){BaseUrl=BaseUrl,ActiveModel=await GetActiveModelAsync(ct)},job,findings,"worker",timeout.Token,history);
            if(!await ModelCapacity.IsCurrentAsync(db,lease,ct))throw new ModelCapacityUnavailableException();
            if(result.Status!="completed")WfiTelemetry.ModelFailures.Add(1);
            return result;
            }
            catch {WfiTelemetry.ModelFailures.Add(1);throw;}
            finally
            {
                WfiTelemetry.ModelSeconds.Record(started.Elapsed.TotalSeconds);
                using var release=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await ModelCapacity.ReleaseAsync(db,lease,release.Token);
            }
        }
        finally{concurrency.Release();}
    }
}
