using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;

namespace WFI.Infrastructure;
public sealed class WfiDb(DbContextOptions<WfiDb> options) : DbContext(options)
{
    // Existing v0.2
    public DbSet<AnalysisJob> Jobs => Set<AnalysisJob>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();

    // WFI 2.0 Endpoint Agent & Inventory
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AgentHeartbeat> Heartbeats => Set<AgentHeartbeat>();
    public DbSet<DiagnosticJob> DiagnosticJobs => Set<DiagnosticJob>();

    // WFI 2.0 Incident Management
    public DbSet<Incident> Incidents => Set<Incident>();

    // WFI 2.0 Remediation
    public DbSet<Playbook> Playbooks => Set<Playbook>();
    public DbSet<RemediationExecution> RemediationExecutions => Set<RemediationExecution>();

    // WFI 2.0 AI Engine
    public DbSet<AiConfig> AiConfigs => Set<AiConfig>();
    public DbSet<AiAnalysisJob> AiAnalysisJobs => Set<AiAnalysisJob>();
    public DbSet<AutomationPolicy> AutomationPolicies => Set<AutomationPolicy>();
    public DbSet<ModelSlot> ModelSlots => Set<ModelSlot>();
    public DbSet<RepairCampaign> RepairCampaigns => Set<RepairCampaign>();
    public DbSet<BigFixMapping> BigFixMappings => Set<BigFixMapping>();
    public DbSet<BigFixVerification> BigFixVerifications => Set<BigFixVerification>();
    public DbSet<AiFeedback> AiFeedbacks => Set<AiFeedback>();
    public DbSet<AiQualityRun> AiQualityRuns => Set<AiQualityRun>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AuditEvent>().HasIndex(x=>new{x.Domain,x.Id});
        model.Entity<ModelSlot>().Property(x=>x.Id).ValueGeneratedNever();
        model.Entity<AiFeedback>().HasIndex(x=>new{x.AnalysisId,x.Actor}).IsUnique();
        model.Entity<AiQualityRun>().HasIndex(x=>new{x.Status,x.LeaseUntil});
        model.Entity<BigFixMapping>().HasKey(x=>x.DeviceId);
        model.Entity<BigFixMapping>().HasIndex(x=>new{x.Domain,x.ConnectorFingerprint,x.ComputerId}).IsUnique();
        model.Entity<BigFixVerification>().HasKey(x=>x.ExecutionId);
        model.Entity<BigFixVerification>().HasIndex(x=>new{x.Status,x.NextCheckAt});
        model.Entity<RepairCampaign>().HasIndex(x=>new{x.Domain,x.Status});
        model.Entity<RemediationExecution>().HasIndex(x=>new{x.CampaignId,x.Status});
        model.Entity<AutomationPolicy>().HasKey(x => x.Domain);
        model.Entity<Playbook>().HasData(RemediationEngine.Catalog.Select((p,i)=>new Playbook {
            Id=Guid.Parse($"20000000-0000-0000-0000-{i+1:D12}"),Code=p.Code,Version=RemediationEngine.Version,
            Title=p.Title,Description=p.Title,RiskLevel=p.Risk,RequiredRole=p.Risk=="High"?"Admin":"Analyst",
            Mode=p.AllowAutomatic?"AutoRemediate":"Recommend",Signature="",IsApproved=true,CreatedAt=DateTime.UnixEpoch
        }).ToArray());
        model.Entity<Device>().HasIndex(x => x.AgentCredentialHash).IsUnique();
        model.Entity<DiagnosticJob>().HasIndex(x => new { x.DeviceId, x.Status, x.LeaseUntil });
        model.Entity<RemediationExecution>().HasIndex(x => new { x.DeviceId, x.Status });
        // Existing v0.2 indexes
        model.Entity<AnalysisJob>().HasIndex(x => x.DedupKey).IsUnique();
        model.Entity<AnalysisJob>().HasIndex(x => new { x.Domain, x.CreatedAt });
        model.Entity<AnalysisJob>().HasIndex(x => new { x.Status, x.LeaseUntil });
        model.Entity<AuditEvent>().HasIndex(x => x.OccurredAt);
        model.Entity<AuditEvent>().HasIndex(x => new { x.Action, x.Target });

        // WFI 2.0 - Device & Agent
        model.Entity<Device>().HasIndex(x => new { x.Domain, x.Hostname }).IsUnique();
        model.Entity<Device>().HasIndex(x => new { x.Domain, x.LastHeartbeat, x.Id });
        model.Entity<Device>().HasIndex(x => x.CertificateThumbprint).IsUnique();
        model.Entity<Device>().HasIndex(x => x.EnrollmentToken).IsUnique();
        model.Entity<AgentHeartbeat>().HasIndex(x => new { x.DeviceId, x.Timestamp });
        model.Entity<AgentHeartbeat>().HasIndex(x => x.Hostname);
        model.Entity<DiagnosticJob>().HasIndex(x => new { x.Domain, x.Status, x.CreatedAt });
        model.Entity<DiagnosticJob>().HasIndex(x => x.DeviceId);

        // WFI 2.0 - Incidents
        model.Entity<Incident>().HasIndex(x => new { x.Domain, x.Status, x.CreatedAt });
        model.Entity<Incident>().HasIndex(x => x.CreatedAt);

        // WFI 2.0 - Remediation
        model.Entity<Playbook>().HasIndex(x => x.Code).IsUnique();
        model.Entity<RemediationExecution>().HasIndex(x => new { x.Hostname, x.RequestedAt });
        model.Entity<RemediationExecution>().HasIndex(x => x.Status);
        model.Entity<RemediationExecution>().HasIndex(x => new { x.Domain, x.Status, x.CompletedAt });

        // WFI 2.0 - AI
        model.Entity<AiAnalysisJob>().HasIndex(x => new { x.Domain, x.CreatedAt });
        model.Entity<AiAnalysisJob>().HasIndex(x => x.DiagnosticJobId);
    }
}
public static class InfrastructureRegistration
{
    public static IServiceCollection AddWfiDatabase(this IServiceCollection services, IConfiguration config, bool development)
    {
        services.AddDbContext<WfiDb>(o =>
        {
            var cs = config.GetConnectionString("Wfi");
            if (!string.IsNullOrWhiteSpace(cs)) o.UseNpgsql(cs);
            else if (development) { Directory.CreateDirectory(".data"); o.UseSqlite("Data Source=.data/wfi.db;Default Timeout=30"); }
            else throw new InvalidOperationException("Production için ConnectionStrings__Wfi zorunludur.");
        });
        return services;
    }
}
