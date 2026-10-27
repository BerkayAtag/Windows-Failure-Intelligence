using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WFI.Core;
using WFI.Infrastructure;
public sealed record AiFeedbackRequest(string Outcome);
public sealed record AiQualityRequest(string Model);
public static class AiQualityEndpoints
{
    public static void MapAiQualityEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/ai/analysis/{id:guid}/feedback",async(Guid id,AiFeedbackRequest input,HttpContext c,WfiDb db)=>
        {
            if(input.Outcome is not ("Resolved" or "NotResolved" or "Temporary"))return Results.BadRequest();
            var domain=c.User.FindFirstValue("domain")!;var actor=c.User.Identity!.Name!;var ct=c.RequestAborted;
            await using var tx=await db.Database.BeginTransactionAsync(ct);await DiagnosticAdmission.LockAsync(db,domain,ct);
            if(!await db.AiAnalysisJobs.AnyAsync(a=>a.Id==id&&a.Domain==domain,ct))return Results.NotFound();
            var row=await db.AiFeedbacks.SingleOrDefaultAsync(f=>f.AnalysisId==id&&f.Actor==actor&&f.Domain==domain,ct);
            if(row==null){row=new(){AnalysisId=id,Actor=actor,Domain=domain};db.AiFeedbacks.Add(row);}
            row.Outcome=input.Outcome;row.UpdatedAt=DateTime.UtcNow;
            db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!,Actor=actor,Action="ai.feedback",Target=id+":"+input.Outcome});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok(row);
        }).RequireAuthorization("Analyst");
        var group=api.MapGroup("/ai/quality").RequireAuthorization("Admin");
        group.MapGet("",async(HttpContext c,WfiDb db)=>
        {
            var domain=c.User.FindFirstValue("domain")!;
            var rows=await db.AiQualityRuns.AsNoTracking().Where(r=>r.Domain==domain).OrderByDescending(r=>r.CreatedAt).Take(20).ToListAsync(c.RequestAborted);
            return Results.Ok(rows.Select(r=>new{r.Id,r.Model,r.Status,r.DatasetVersion,r.PromptVersion,r.NextCase,total=AiQualityDataset.Cases.Length*2,
                r.CreatedAt,r.CompletedAt,results=JsonSerializer.Deserialize<AiQualityResult[]>(r.ResultsJson)}));
        });
        group.MapPost("",async(AiQualityRequest input,HttpContext c,WfiDb db,IOllamaService models)=>
        {
            if(string.IsNullOrWhiteSpace(input.Model)||input.Model.Length>128)return Results.BadRequest();
            if(!(await models.GetModelsAsync(c.RequestAborted)).Any(m=>m.Name==input.Model))return Results.BadRequest(new{error="Model yerel Ollama listesinde bulunamadı."});
            var domain=c.User.FindFirstValue("domain")!;var ct=c.RequestAborted;
            await using var tx=await db.Database.BeginTransactionAsync(ct);await DiagnosticAdmission.LockAsync(db,domain,ct);
            if(await db.AiQualityRuns.AnyAsync(r=>r.Domain==domain&&(r.Status=="Queued"||r.Status=="Running"),ct))return Results.Conflict(new{error="Bu domain için değerlendirme zaten sırada/çalışıyor."});
            var run=new AiQualityRun{Domain=domain,Model=input.Model,RequestedBy=c.User.Identity!.Name!};db.AiQualityRuns.Add(run);
            db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!,Actor=run.RequestedBy,Action="ai.quality.requested",Target=run.Id.ToString()});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok(new{run.Id});
        });
        group.MapPost("/{id:guid}/cancel",async(Guid id,HttpContext c,WfiDb db)=>
        {
            var domain=c.User.FindFirstValue("domain")!;var ct=c.RequestAborted;
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            if(await db.AiQualityRuns.Where(r=>r.Id==id&&r.Domain==domain&&(r.Status=="Queued"||r.Status=="Running"))
                .ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"Cancelled").SetProperty(r=>r.LeaseToken,(Guid?)null),ct)==0)return Results.NotFound();
            db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!,Actor=c.User.Identity!.Name!,Action="ai.quality.cancelled",Target=id.ToString()});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.NoContent();
        });
    }
}

