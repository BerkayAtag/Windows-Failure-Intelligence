using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;
namespace WFI.Tests;
public class DiagnosticPipelineTests
{
    [Theory][InlineData("PendingApproval",1)][InlineData("Approved",1)][InlineData("Executing",1)][InlineData("Unknown",1)][InlineData("Success",2)][InlineData("Cancelled",2)]
    public async Task AutomaticWorkDoesNotDuplicateOldActiveOrUncertainRepair(string status,int expected)
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Hostname="old-repair",Domain="example.invalid",AutomationMode="Enabled"};db.Devices.Add(device);
        var book=await db.Playbooks.SingleAsync(p=>p.Code=="PB-DNS-FLUSH");
        db.RemediationExecutions.Add(new(){DeviceId=device.Id,Domain=device.Domain,PlaybookId=book.Id,PlaybookCode=book.Code,Status=status,RequestedAt=DateTime.UtcNow.AddDays(-2)});
        db.DiagnosticJobs.Add(new(){DeviceId=device.Id,Domain=device.Domain,Hostname=device.Hostname,Status="evidence_received",EvidenceJson=JsonSerializer.Serialize(new[]{new EvidenceItem{Source="synthetic",Content="ERROR_NO_LOGON_SERVERS"}},AnalysisWorker.JsonOptions)});
        await db.SaveChangesAsync();
        var worker=new DiagnosticJobProcessor(app.Services.GetRequiredService<IServiceScopeFactory>(),new Model(true),NullLogger<DiagnosticJobProcessor>.Instance);
        Assert.True(await worker.ProcessOneAsync(default));Assert.Equal(expected,await db.RemediationExecutions.CountAsync());
        Assert.Equal("completed",(await db.DiagnosticJobs.AsNoTracking().SingleAsync()).Status);
    }
    private sealed class Model(bool success):IOllamaService
    {
        public List<Finding> Received=[];
        public Task<List<OllamaModel>> GetModelsAsync(CancellationToken ct=default)=>Task.FromResult(new List<OllamaModel>{new("synthetic")});
        public Task<string> GetActiveModelAsync(CancellationToken ct=default)=>Task.FromResult("synthetic");
        public Task SetActiveModelAsync(string name,CancellationToken ct=default)=>Task.CompletedTask;
        public Task<AiAnalysisJob> DiagnoseAsync(DiagnosticJob job,List<Finding> findings,CancellationToken ct=default)
        {
            Received=findings;
            return Task.FromResult(new AiAnalysisJob{DiagnosticJobId=job.Id,Domain=job.Domain,Hostname=job.Hostname,Status=success?"completed":"failed",Conclusion="Synthetic",ConfidenceScore=0.5,RecommendedPlaybooksJson="[\"arbitrary-command\"]",CompletedAt=DateTime.UtcNow});
        }
    }
    [Theory][InlineData(true,true,1)][InlineData(false,true,0)][InlineData(true,false,0)]
    public async Task QueueUsesEvidencePolicyAndModelSuccessBeforeScheduling(bool success,bool enabled,int count)
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Hostname="synthetic",Domain="example.invalid",AutomationMode=enabled?"Enabled":"Disabled"};db.Devices.Add(device);
        var job=new DiagnosticJob{DeviceId=device.Id,Hostname=device.Hostname,Domain=device.Domain,Status="evidence_received",EvidenceJson=JsonSerializer.Serialize(new[]{new EvidenceItem{Source="fixture",Content="ERROR_NO_LOGON_SERVERS"}},AnalysisWorker.JsonOptions)};
        db.DiagnosticJobs.Add(job);await db.SaveChangesAsync();
        var model=new Model(success);var worker=new DiagnosticJobProcessor(app.Services.GetRequiredService<IServiceScopeFactory>(),model,NullLogger<DiagnosticJobProcessor>.Instance);
        Assert.True(await worker.ProcessOneAsync(default));Assert.False(await worker.ProcessOneAsync(default));
        Assert.Contains(model.Received,f=>f.RuleId=="AD-001");Assert.Equal(count,await db.RemediationExecutions.CountAsync());
        var ai=await db.AiAnalysisJobs.SingleAsync();Assert.DoesNotContain("arbitrary-command",ai.RecommendedPlaybooksJson);
        if(count==1)
        {
            var action=await db.RemediationExecutions.SingleAsync();Assert.Equal("PB-DNS-FLUSH",action.PlaybookCode);Assert.True(action.Automatic);
            // A new diagnosis does not bypass the 24-hour per-device automatic action cooldown.
            db.DiagnosticJobs.Add(new(){DeviceId=device.Id,Hostname=device.Hostname,Domain=device.Domain,Status="evidence_received",EvidenceJson=job.EvidenceJson});await db.SaveChangesAsync();
            await worker.ProcessOneAsync(default);Assert.Equal(1,await db.RemediationExecutions.CountAsync());
        }
    }
}
