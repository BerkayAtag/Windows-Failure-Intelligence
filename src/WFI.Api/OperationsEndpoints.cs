using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WFI.Infrastructure;

public static class OperationsEndpoints
{
    public static void MapOperationsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/operations/health",async(HttpContext c,WfiDb db,IConfiguration config)=>
        {
            var domain=c.User.FindFirstValue("domain")!;var now=DateTime.UtcNow;
            var pending=db.DiagnosticJobs.AsNoTracking().Where(j=>j.Domain==domain&&
                (j.Status=="pending"||j.Status=="running"||j.Status=="evidence_received"||j.Status=="analyzing"));
            var oldest=await pending.OrderBy(j=>j.CreatedAt).Select(j=>(DateTime?)j.CreatedAt).FirstOrDefaultAsync(c.RequestAborted);
            var since=now.AddHours(-24);
            return Results.Ok(new{sampledAt=now,domain,activeDiagnostics=await pending.CountAsync(c.RequestAborted),
                oldestPendingSeconds=oldest.HasValue?Math.Max(0,(now-oldest.Value).TotalSeconds):0,
                completedLast24Hours=await db.DiagnosticJobs.CountAsync(j=>j.Domain==domain&&j.Status=="completed"&&j.CompletedAt>=since,c.RequestAborted),
                failedLast24Hours=await db.DiagnosticJobs.CountAsync(j=>j.Domain==domain&&j.Status=="failed"&&j.CompletedAt>=since,c.RequestAborted),
                modelCapacity=config.GetValue("Ollama:MaxConcurrent",1),
                telemetryConfigured=!string.IsNullOrWhiteSpace(config["Telemetry:OtlpEndpoint"])});
        }).RequireAuthorization("Admin");
    }
}

