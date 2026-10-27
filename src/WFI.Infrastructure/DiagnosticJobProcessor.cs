using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WFI.Core;
namespace WFI.Infrastructure;

public sealed class DiagnosticJobProcessor(IServiceScopeFactory scopes,IOllamaService ollama,ILogger<DiagnosticJobProcessor> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try{if(await ProcessOneAsync(ct))continue;}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("Diagnostic processing failed ({Type}).",ex.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(3),ct);
        }
    }
    public static List<Finding> AnalyzeEvidence(string? json,CancellationToken ct)
    {
        var evidence=JsonSerializer.Deserialize<List<EvidenceItem>>(json??"[]",AnalysisWorker.JsonOptions)??[];
        var findings=new List<Finding>();
        foreach(var item in evidence.Take(50))
        {
            ct.ThrowIfCancellationRequested();if(string.IsNullOrWhiteSpace(item.Content))continue;
            using var stream=new MemoryStream(Encoding.UTF8.GetBytes(item.Content));
            findings.AddRange(new LogAnalyzer().Analyze(stream,"evidence.log",ct).Findings
                .Where(f=>f.RuleId!="BF-001"||(item.Source=="BESClient"&&item.Category=="BigFix"&&
                    (item.Content=="WFI_BESCLIENT_MISSING"||System.Text.RegularExpressions.Regex.IsMatch(item.Content,"\\AWFI_BESCLIENT_STOPPED Status=(Stopped|StartPending|StopPending|ContinuePending|PausePending|Paused)\\z"))))
                .Select(f=>f with{Evidence=f.Evidence.Select(e=>e with{Source=item.Source}).ToArray()}));
        }
        return findings.GroupBy(x=>x.RuleId).Select(g=>g.First() with{Evidence=g.SelectMany(x=>x.Evidence).Take(30).ToArray()}).ToList();
    }
    public async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();var now=DateTime.UtcNow;
        var aged=now.AddMinutes(-5);
        var job=await db.DiagnosticJobs.AsNoTracking().Where(j=>j.Status=="evidence_received"||(j.Status=="analyzing"&&j.LeaseUntil<now)).OrderBy(j=>j.RequestedBy=="automation"&&j.CreatedAt>aged?1:0).ThenBy(j=>j.CreatedAt).FirstOrDefaultAsync(ct);
        if(job==null)return false;
        var token=Guid.NewGuid();
        var claim=await db.DiagnosticJobs.Where(j=>j.Id==job.Id&&(j.Status=="evidence_received"||(j.Status=="analyzing"&&j.LeaseUntil<now)))
            .ExecuteUpdateAsync(s=>s.SetProperty(j=>j.Status,"analyzing").SetProperty(j=>j.LeaseToken,token).SetProperty(j=>j.LeaseUntil,now.AddMinutes(10)),ct);
        if(claim==0)return true;
        WfiTelemetry.QueueAge.Record(Math.Max(0,(now-job.CreatedAt).TotalSeconds));
        AiAnalysisJob analysis;List<Finding> findings=[];
        try{findings=AnalyzeEvidence(job.EvidenceJson,ct);analysis=await ollama.DiagnoseAsync(job,findings,ct);}
        catch(ModelCapacityUnavailableException)
        {
            await db.DiagnosticJobs.Where(j=>j.Id==job.Id&&j.Status=="analyzing"&&j.LeaseToken==token)
                .ExecuteUpdateAsync(s=>s.SetProperty(j=>j.Status,"evidence_received").SetProperty(j=>j.LeaseToken,(Guid?)null).SetProperty(j=>j.LeaseUntil,(DateTime?)null),ct);
            return false;
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
        catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException or JsonException or InputRejectedException)
        {analysis=new(){DiagnosticJobId=job.Id,Domain=job.Domain,Hostname=job.Hostname,Status="failed",ConfidenceScore=0,Error="Model/kanıt işlenemedi. Sunucu durumunu kontrol edin.",Conclusion="AI analizi tamamlanmadı.",CompletedAt=DateTime.UtcNow};}
        var recommended=RemediationEngine.Catalog.Where(p=>p.RuleIds.Any(id=>findings.Any(f=>f.RuleId==id))).Select(p=>p.Code).ToArray();
        // Recommendations are constrained by server catalog and evidence, regardless of model output.
        analysis.RecommendedPlaybooksJson=JsonSerializer.Serialize(recommended);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var changed=await db.DiagnosticJobs.Where(j=>j.Id==job.Id&&j.Status=="analyzing"&&j.LeaseToken==token)
            .ExecuteUpdateAsync(s=>s.SetProperty(j=>j.Status,analysis.Status=="completed"?"completed":"failed").SetProperty(j=>j.Error,analysis.Error).SetProperty(j=>j.CompletedAt,DateTime.UtcNow),ct);
        if(changed==0)return true;
        db.AiAnalysisJobs.Add(analysis);
        db.Audit.Add(new() { Domain=job.Domain,Actor="worker",Action="diagnostic.analyzed",Target=job.Id.ToString()});
        var device=await db.Devices.AsNoTracking().FirstOrDefaultAsync(d=>d.Id==job.DeviceId&&d.Status!="revoked",ct);
        if(device!=null&&analysis.Status=="completed")
        {
            // Share the device lock with manual creation and dispatch before inspecting existing work/policy.
            var locked=await db.Devices.Where(d=>d.Id==device.Id&&d.Status!="revoked")
                .ExecuteUpdateAsync(s=>s.SetProperty(d=>d.Status,d=>d.Status),ct);
            device=locked==0?null:await db.Devices.AsNoTracking().SingleAsync(d=>d.Id==device.Id,ct);
        }
        var agents=new AgentCoordinator(db);
        if(device!=null&&analysis.Status=="completed"&&await agents.AutomaticEnabledAsync(device,ct))
        {
            foreach(var definition in RemediationEngine.Catalog.Where(p=>p.AllowAutomatic&&recommended.Contains(p.Code)))
            {
                var cutoff=now.AddHours(-24);
                if(await db.RemediationExecutions.AnyAsync(x=>x.DeviceId==device.Id&&x.PlaybookCode==definition.Code&&
                    (x.RequestedAt>cutoff||x.Status=="PendingApproval"||x.Status=="Approved"||x.Status=="Executing"||x.Status=="Unknown"),ct))continue;
                var playbook=await db.Playbooks.SingleOrDefaultAsync(p=>p.Code==definition.Code&&p.Version==RemediationEngine.Version&&p.IsApproved,ct);
                if(playbook==null)continue;
                var admitted=await db.Devices.Where(d=>d.Id==device.Id&&d.Status!="revoked"&&(d.NextAutomaticAt==null||d.NextAutomaticAt<=now))
                    .ExecuteUpdateAsync(s=>s.SetProperty(d=>d.NextAutomaticAt,now.AddHours(24)),ct);
                if(admitted==1)db.RemediationExecutions.Add(new(){DeviceId=device.Id,DiagnosticJobId=job.Id,Domain=device.Domain,Hostname=device.Hostname,
                    PlaybookId=playbook.Id,PlaybookCode=definition.Code,PlaybookTitle=definition.Title,RiskLevel=definition.Risk,Automatic=true,Status="Approved",RequestedBy="automation",ApprovedBy="device-policy"});
            }
        }
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
}

