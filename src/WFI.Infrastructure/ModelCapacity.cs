using Microsoft.EntityFrameworkCore;
using WFI.Core;
namespace WFI.Infrastructure;

public sealed class ModelCapacityUnavailableException:Exception;
public sealed record ModelLease(int Slot,Guid Token,long Generation,DateTime Until);
public static class ModelCapacity
{
    // Inference is cancelled within 240s; the 300s lease includes transport/cleanup margin.
    public static async Task<ModelLease?> TryAcquireAsync(WfiDb db,int limit,CancellationToken ct)
    {
        if(limit is <1 or >64)throw new ArgumentOutOfRangeException(nameof(limit));
        for(var id=1;id<=limit;id++)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ModelSlots" ("Id","Generation") VALUES ({id},0) ON CONFLICT ("Id") DO NOTHING
                """,ct);
            var now=await NowAsync(db,ct);var until=now.AddSeconds(300);var token=Guid.NewGuid();
            var changed=await db.ModelSlots.Where(s=>s.Id==id&&(s.Token==null||s.LeaseUntil<=now))
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Token,token).SetProperty(x=>x.LeaseUntil,until)
                    .SetProperty(x=>x.Generation,x=>x.Generation+1),ct);
            if(changed==1)
            {
                var slot=await db.ModelSlots.AsNoTracking().SingleAsync(s=>s.Id==id&&s.Token==token,ct);
                return new(id,token,slot.Generation,until);
            }
        }
        return null;
    }
    private static Task<DateTime> NowAsync(WfiDb db,CancellationToken ct)=>db.Database.SqlQueryRaw<DateTime>("SELECT CURRENT_TIMESTAMP AS \"Value\"").SingleAsync(ct);
    public static async Task<bool> IsCurrentAsync(WfiDb db,ModelLease lease,CancellationToken ct)
    {
        var now=await NowAsync(db,ct);
        return await db.ModelSlots.AnyAsync(s=>s.Id==lease.Slot&&s.Token==lease.Token&&s.Generation==lease.Generation&&s.LeaseUntil>now,ct);
    }
    public static Task<int> ReleaseAsync(WfiDb db,ModelLease lease,CancellationToken ct)=>
        db.ModelSlots.Where(s=>s.Id==lease.Slot&&s.Token==lease.Token&&s.Generation==lease.Generation)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Token,(Guid?)null).SetProperty(x=>x.LeaseUntil,(DateTime?)null),ct);
}
