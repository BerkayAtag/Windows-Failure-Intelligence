namespace WFI.Core;

// Agent ↔ Server communication DTOs
public sealed class AgentTelemetryDto
{
    public Guid DeviceId { get; set; }
    public string Hostname { get; set; } = "";
    public string Domain { get; set; } = "";
    public string IpAddress { get; set; } = "0.0.0.0";
    public string OsVersion { get; set; } = "";
    public string AgentVersion { get; set; } = "2.0.0";
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskFreeSpaceGb { get; set; }
    public string HealthStatus { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> SystemInfo { get; set; } = new();
}

public sealed class DiagnosticEvidenceDto
{
    public Guid DeviceId { get; set; }
    public string Hostname { get; set; } = "";
    public string Domain { set; get; } = "";
    public string ScanType { get; set; } = "FullSystemScan";
    public List<EvidenceItem> Evidence { get; set; } = [];
    public List<Finding> Findings { get; set; } = [];
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    public bool IsOffline { get; set; }
}

public sealed class EvidenceItem
{
    public string Source { get; set; } = "";
    public string Category { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}

[Flags]
public enum AgentCapabilities
{
    None = 0,
    EventLog = 1,
    OsHealth = 2,
    WindowsUpdate = 4,
    Sccm = 8,
    Network = 16,
    Domain = 32,
    GroupPolicy = 64,
    Remediation = 128,
    All = EventLog | OsHealth | WindowsUpdate | Sccm | Network | Domain | GroupPolicy | Remediation
}

public enum DiagnosticJobStatus
{
    Pending,
    Dispatched,
    Running,
    Completed,
    Failed
}

public enum ScanType
{
    FullSystemScan,
    EventLogScan,
    UpdateScan,
    SccmScan,
    NetworkScan,
    BigFixScan
}
