using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WFI.Core;
using WFI.Infrastructure;

public static class BigFixEndpoints
{
    public static void MapBigFixEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/devices/{id:guid}/bigfix",async(Guid id,HttpContext c,WfiDb db,IBigFixConnector connector)=>
        {
            var domain=c.User.FindFirstValue("domain")!;
            if(!await db.Devices.AnyAsync(d=>d.Id==id&&d.Domain==domain,c.RequestAborted))return Results.NotFound();
            var mapping=await db.BigFixMappings.AsNoTracking().SingleOrDefaultAsync(m=>m.DeviceId==id&&m.Domain==domain,c.RequestAborted);
            var verification=await db.BigFixVerifications.AsNoTracking().Where(v=>v.DeviceId==id&&v.Domain==domain)
                .OrderByDescending(v=>v.NextCheckAt).Select(v=>new{v.ExecutionId,v.Status,v.BeforeReportAt,v.LatestReportAt,v.ObservedAt}).Take(10).ToListAsync(c.RequestAborted);
            return Results.Ok(new{configured=connector.Configured,mapping,verification,mappingCurrent=mapping!=null&&mapping.ConnectorFingerprint==connector.Fingerprint});
        }).RequireAuthorization();
        api.MapPost("/devices/{id:guid}/bigfix/mapping",async(Guid id,BigFixMappingRequest input,HttpContext c,WfiDb db,IBigFixConnector connector)=>
        {
            if(input.ComputerId<=0||!input.ConfirmIdentity)return Results.BadRequest();
            var domain=c.User.FindFirstValue("domain")!;var ct=c.RequestAborted;
            var device=await db.Devices.AsNoTracking().SingleOrDefaultAsync(d=>d.Id==id&&d.Domain==domain&&d.Status!="revoked",ct);
            if(device==null)return Results.NotFound();
            if(!connector.Configured)return Results.Json(new{error="BigFix connector sunucuda yapılandırılmalı."},statusCode:503);
            BigFixComputer computer;
            try {computer=await connector.ReadAsync(input.ComputerId,ct);}
            catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException or InvalidDataException or System.Xml.XmlException or InputRejectedException)
            {return Results.Json(new{error="BigFix merkez kimliği doğrulanamadı. API/TLS/yetki durumunu kontrol edin."},statusCode:503);}
            var expected=(device.Hostname+"."+domain).ToLowerInvariant();
            if(computer.DnsName!=expected)return Results.Conflict(new{error="BigFix DNS kimliği WFI cihazıyla eşleşmiyor; otomatik eşleme yapılmadı."});
            await using var tx=await db.Database.BeginTransactionAsync(ct);await DiagnosticAdmission.LockAsync(db,domain,ct);
            if(await db.BigFixMappings.AnyAsync(m=>m.Domain==domain&&m.ConnectorFingerprint==connector.Fingerprint&&m.ComputerId==input.ComputerId&&m.DeviceId!=id,ct))return Results.Conflict();
            var mapping=await db.BigFixMappings.SingleOrDefaultAsync(m=>m.DeviceId==id,ct);
            if(mapping==null){mapping=new(){DeviceId=id,Domain=domain};db.BigFixMappings.Add(mapping);}
            mapping.ComputerId=input.ComputerId;mapping.ConnectorFingerprint=connector.Fingerprint;mapping.VerifiedDnsName=computer.DnsName;mapping.VerifiedBy=c.User.Identity!.Name!;mapping.VerifiedAt=DateTime.UtcNow;
            db.Audit.Add(new() { Domain=c.User.FindFirstValue("domain")!,Actor=mapping.VerifiedBy,Action="bigfix.mapping.verified",Target=id.ToString()});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok(new{mapping,computer});
        }).RequireAuthorization("Admin");
        api.MapPost("/devices/{id:guid}/bigfix/probe",async(Guid id,HttpContext c,WfiDb db,IBigFixConnector connector)=>
        {
            var domain=c.User.FindFirstValue("domain")!;
            var mapping=await db.BigFixMappings.AsNoTracking().SingleOrDefaultAsync(m=>m.DeviceId==id&&m.Domain==domain,c.RequestAborted);
            if(mapping==null)return Results.NotFound();
            if(!connector.Configured||mapping.ConnectorFingerprint!=connector.Fingerprint)return Results.Conflict(new{error="Connector değişti veya yapılandırılmadı; eşlemeyi yeniden doğrulayın."});
            try
            {
                var computer=await connector.ReadAsync(mapping.ComputerId,c.RequestAborted);
                if(computer.DnsName!=mapping.VerifiedDnsName)return Results.Conflict(new{error="Merkez cihaz kimliği değişti."});
                return Results.Ok(new{computer,reportAgeSeconds=Math.Max(0,(computer.ObservedAt-computer.LastReportAt).TotalSeconds)});
            }
            catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException or InvalidDataException or System.Xml.XmlException or InputRejectedException)
            {return Results.Json(new{error="Merkez API sonucu alınamadı; bu durum istemci arızası kanıtı değildir."},statusCode:503);}
        }).RequireAuthorization("Analyze").RequireRateLimiting("upload");
    }
}

