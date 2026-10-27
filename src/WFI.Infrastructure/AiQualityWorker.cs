using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WFI.Core;
namespace WFI.Infrastructure;
public sealed class AiQualityWorker(IServiceScopeFactory scopes,IHttpClientFactory clients,IConfiguration config,ILogger<AiQualityWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try {if(await ProcessOneAsync(ct))continue;}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("AI quality run failed ({Type})",ex.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(5),ct);
        }
    }
    public async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var now=DateTime.UtcNow;
        if(await db.DiagnosticJobs.AnyAsync(j=>j.Status=="evidence_received"||j.Status=="analyzing",ct))return false;
        var run=await db.AiQualityRuns.AsNoTracking().Where(r=>r.Status=="Queued"||(r.Status=="Running"&&r.LeaseUntil<=now)).OrderBy(r=>r.CreatedAt).FirstOrDefaultAsync(ct);
        if(run==null)return false;
        if(run.DatasetVersion!=AiQualityDataset.Version||run.PromptVersion!=OllamaEngine.PromptVersion||run.NextCase<0||run.NextCase>=AiQualityDataset.Cases.Length*2)
        {
            await db.AiQualityRuns.Where(r=>r.Id==run.Id&&(r.Status=="Queued"||(r.Status=="Running"&&r.LeaseUntil<=now)))
                .ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"BlockedVersionChanged"),ct);return true;
        }
        var raw=config["Ollama:BaseUrl"]??"http://127.0.0.1:11434";
        if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri)||!uri.IsLoopback||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||uri.Scheme is not ("http" or "https"))
        {
            await db.AiQualityRuns.Where(r=>r.Id==run.Id&&(r.Status=="Queued"||(r.Status=="Running"&&r.LeaseUntil<=now)))
                .ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"BlockedLocalModelRequired").SetProperty(r=>r.LeaseToken,(Guid?)null).SetProperty(r=>r.LeaseUntil,(DateTime?)null),ct);return true;
        }
        var slot=await ModelCapacity.TryAcquireAsync(db,config.GetValue("Ollama:MaxConcurrent",1),ct);if(slot==null)return false;
        try
        {
            var token=Guid.NewGuid();
            if(await db.AiQualityRuns.Where(r=>r.Id==run.Id&&r.NextCase==run.NextCase&&(r.Status=="Queued"||(r.Status=="Running"&&r.LeaseUntil<=now)))
                .ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"Running").SetProperty(r=>r.LeaseToken,token).SetProperty(r=>r.LeaseUntil,now.AddMinutes(5)),ct)==0)return true;
            var sample=AiQualityDataset.Cases[run.NextCase/2];var history=run.NextCase%2==1;
            var diagnostic=new DiagnosticJob{Domain="synthetic.invalid",Hostname="quality-synthetic",ProblemDescription=sample.Description,EvidenceJson=sample.EvidenceJson};
            var codes=RemediationEngine.Catalog.Where(p=>p.RuleIds.Any(id=>sample.Findings.Any(f=>f.RuleId==id))).Select(p=>p.Code).ToArray();
            var examples=history?codes.Select(code=>new LearningExample(Guid.Parse("11111111-1111-1111-1111-111111111111"),Guid.Parse("22222222-2222-2222-2222-222222222222"),code,"Synthetic prior action; current evidence must be checked.","Synthetic action check",DateTime.UnixEpoch)).ToArray():[];
            using var client=clients.CreateClient("ollama");using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(120));
            var timer=Stopwatch.StartNew();AiAnalysisJob result;
            try {result=await new OllamaEngine(client).DiagnoseAsync(new(){BaseUrl=uri.AbsoluteUri,ActiveModel=run.Model},diagnostic,sample.Findings,"quality",timeout.Token,examples);}
            catch(OperationCanceledException)when(!ct.IsCancellationRequested){result=new(){Status="failed",RawResponse=""};}
            if(!await ModelCapacity.IsCurrentAsync(db,slot,ct))return false;
            var results=JsonSerializer.Deserialize<List<AiQualityResult>>(run.ResultsJson)??[];
            results.Add(AiQualityDataset.Score(sample,result,history,timer.ElapsedMilliseconds));
            var next=run.NextCase+1;var completed=next==AiQualityDataset.Cases.Length*2;
            await db.AiQualityRuns.Where(r=>r.Id==run.Id&&r.Status=="Running"&&r.LeaseToken==token)
                .ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ResultsJson,JsonSerializer.Serialize(results)).SetProperty(r=>r.NextCase,next)
                    .SetProperty(r=>r.Status,completed?"Completed":"Queued").SetProperty(r=>r.CompletedAt,completed?(DateTime?)DateTime.UtcNow:null)
                    .SetProperty(r=>r.LeaseToken,(Guid?)null).SetProperty(r=>r.LeaseUntil,(DateTime?)null),ct);
            return true;
        }
        finally {using var release=new CancellationTokenSource(TimeSpan.FromSeconds(5));await ModelCapacity.ReleaseAsync(db,slot,release.Token);}
    }
}
