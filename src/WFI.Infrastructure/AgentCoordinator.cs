using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WFI.Core;

namespace WFI.Infrastructure;

public sealed class AgentCoordinator(WfiDb db)
{
    public static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public async Task<AgentEnrollmentResponse?> EnrollAsync(AgentEnrollment request, CancellationToken ct)
    {
        if (request.Token?.Length != 64 || request.Credential?.Length != 64 || !request.Credential.All(Uri.IsHexDigit)) return null;
        var hash = Hash(request.Token); var credentialHash = Hash(request.Credential);
        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.Devices.Where(d => d.Id == request.DeviceId && d.Status == "pending" &&
                d.EnrollmentToken == hash && d.EnrollmentExpiresAt > now && d.AgentCredentialHash == null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.AgentCredentialHash, credentialHash)
                .SetProperty(d => d.EnrollmentToken, (string?)null).SetProperty(d => d.EnrollmentExpiresAt, (DateTime?)null)
                .SetProperty(d => d.Status, "online").SetProperty(d => d.LastHeartbeat, now), ct);
        if (updated != 1) return null;
        var device = await db.Devices.AsNoTracking().SingleAsync(d => d.Id == request.DeviceId, ct);
        db.Audit.Add(new() { Domain=device.Domain, Actor="agent", Action="agent.enrolled", Target=device.Id.ToString() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(device.Id, device.Hostname, device.Domain);
    }

    public Task<Device?> AuthenticateAsync(Guid id, string credential, CancellationToken ct)
    {
        if (credential.Length != 64 || !credential.All(Uri.IsHexDigit)) return Task.FromResult<Device?>(null);
        var hash = Hash(credential);
        return db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.AgentCredentialHash == hash && d.Status != "revoked", ct);
    }

    public async Task<DiagnosticAssignment?> ClaimDiagnosticAsync(Guid deviceId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var job = await db.DiagnosticJobs.AsNoTracking().Where(j => j.DeviceId == deviceId &&
            (j.Status == "pending" || (j.Status == "running" && j.LeaseUntil < now))).OrderBy(j => j.CreatedAt).FirstOrDefaultAsync(ct);
        if (job == null) return null;
        var token = Guid.NewGuid();
        var changed = await db.DiagnosticJobs.Where(j => j.Id == job.Id && (j.Status == "pending" || (j.Status == "running" && j.LeaseUntil < now)))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "running").SetProperty(j => j.LeaseToken, token)
                .SetProperty(j => j.LeaseUntil, now.AddMinutes(10)), ct);
        return changed == 1 ? new(job.Id, token, job.ScanType) : null;
    }

    public async Task<bool> AutomaticEnabledAsync(Device device, CancellationToken ct)
    {
        if (device.AutomationMode == "Disabled") return false;
        if (device.AutomationMode == "Enabled") return true;
        return await db.AutomationPolicies.AnyAsync(p => p.Domain == device.Domain && p.Enabled, ct);
    }
}

