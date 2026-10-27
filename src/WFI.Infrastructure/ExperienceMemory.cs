using Microsoft.EntityFrameworkCore;
using WFI.Core;
namespace WFI.Infrastructure;

public static class ExperienceMemory
{
    public static async Task<List<LearningExample>> ReadAsync(WfiDb db,string domain,string? scanType,string[]? codes,CancellationToken ct)
    {
        var since=DateTime.UtcNow.AddDays(-180);
        var query=from r in db.RemediationExecutions.AsNoTracking()
                  join j in db.DiagnosticJobs.AsNoTracking() on r.DiagnosticJobId equals j.Id
                  join a in db.AiAnalysisJobs.AsNoTracking() on j.Id equals a.DiagnosticJobId
                  join p in db.Playbooks.AsNoTracking() on r.PlaybookId equals p.Id
                  where r.Domain==domain&&j.Domain==domain&&a.Domain==domain&&r.DeviceId==j.DeviceId&&
                    r.Status=="Success"&&!r.LearningExcluded&&r.CompletedAt>=since&&j.Status=="completed"&&a.Status=="completed"&&
                    !db.AiFeedbacks.Any(f=>f.AnalysisId==a.Id&&f.Domain==domain&&f.Outcome!="Resolved")&&
                    p.IsApproved&&p.Version==RemediationEngine.Version&&p.Code==r.PlaybookCode&&
                    (scanType==null||j.ScanType==scanType)
                  orderby r.CompletedAt descending,a.CreatedAt descending
                  select new{r.Id,DiagnosticId=j.Id,r.PlaybookCode,a.Conclusion,r.ValidationResult,r.CompletedAt};
        if(codes!=null)query=query.Where(x=>codes.Contains(x.PlaybookCode)).OrderByDescending(x=>x.CompletedAt);
        var rows=await query.Take(50).ToListAsync(ct);
        static string Safe(string value,int limit){var safe=LogAnalyzer.Redact(value);return safe[..Math.Min(safe.Length,limit)];}
        return rows.DistinctBy(x=>x.Id).Take(codes==null?25:5).Select(x=>new LearningExample(x.Id,x.DiagnosticId,x.PlaybookCode,Safe(x.Conclusion,300),Safe(x.ValidationResult,200),x.CompletedAt!.Value)).ToList();
    }
}
