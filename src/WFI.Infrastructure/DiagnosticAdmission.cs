using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace WFI.Infrastructure;

public static class DiagnosticAdmission
{
    public static int Limit(IConfiguration config)
    {
        var limit=config.GetValue("Diagnostics:MaxActivePerDomain",1000);
        if(limit is <1 or >10000)throw new InvalidOperationException("Diagnostics:MaxActivePerDomain must be between 1 and 10000.");
        return limit;
    }
    public static async Task LockAsync(WfiDb db,string domain,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("Diagnostic admission requires a transaction.");
        // SQLite's write transaction serializes admission; PostgreSQL needs a shared domain lock across nodes.
        if(db.Database.IsNpgsql())
        {
            var key=BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes("WFI:diagnostics:"+domain)));
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})",ct);
        }
    }
    public static async Task<bool> HasCapacityAsync(WfiDb db,string domain,IConfiguration config,CancellationToken ct)
    {
        var limit=Limit(config);
        var count=await db.DiagnosticJobs.AsNoTracking().Where(j=>j.Domain==domain&&
            (j.Status=="pending"||j.Status=="running"||j.Status=="evidence_received"||j.Status=="analyzing"))
            .Take(limit).CountAsync(ct);
        return count<limit;
    }
}
