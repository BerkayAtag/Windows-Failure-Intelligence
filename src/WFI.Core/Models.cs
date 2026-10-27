namespace WFI.Core;

public sealed class AnalysisJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Domain { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string Filename { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string DedupKey { get; set; } = "";
    public long Size { get; set; }
    public string Actor { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public int Attempts { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public string? ResultJson { get; set; }
}

public sealed class AuditEvent
{
    public string Domain { get; set; } = "";
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
}

public sealed record Evidence(string Source, int Line, string Text);
public sealed record Finding(string RuleId, string Version, string Category, string Severity,
    string Title, string Explanation, string[] NextChecks, Evidence[] Evidence);
public sealed record AnalysisResult(string EngineVersion, string Conclusion, Finding[] Findings, int Files, int Lines);

// WFI 2.0 Endpoint Agent & Inventory Models
public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Domain { get; set; } = "example.invalid";
    public string Hostname { get; set; } = "";
    public string IpAddress { get; set; } = "127.0.0.1";
    public string OsVersion { get; set; } = "Windows 11 Enterprise (23H2)";
    public string AgentVersion { get; set; } = "2.0.0";
    public string Status { get; set; } = "online"; // online, offline, degraded
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public string? CertificateThumbprint { get; set; }
    public string? EnrollmentToken { get; set; }
    public DateTime? EnrollmentExpiresAt { get; set; }
    public string? AgentCredentialHash { get; set; }
    public string AutomationMode { get; set; } = "Inherit";
    public DateTime? NextDiagnosticAt { get; set; }
    public DateTime? NextAutomaticAt { get; set; }
    public string? SystemInfoJson { get; set; }
}

public sealed class AgentHeartbeat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string Hostname { get; set; } = "";
    public string Domain { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskFreeSpaceGb { get; set; }
    public string HealthStatus { get; set; } = "Healthy";
    public string MetricsJson { get; set; } = "{}";
}

public sealed class DiagnosticJob
{
    public string ProblemDescription { get; set; } = "";
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DeviceId { get; set; }
    public string Domain { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string ScanType { get; set; } = "FullSystemScan"; // FullSystemScan, EventLogScan, UpdateScan, SccmScan
    public string Status { get; set; } = "pending"; // pending, running, completed, failed
    public string RequestedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? EvidenceJson { get; set; }
    public string? Error { get; set; }
}

// WFI 2.0 Incident Management
public sealed class Incident
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Severity { get; set; } = "Medium"; // Critical, High, Medium, Low
    public string Status { get; set; } = "Open"; // Open, Investigating, Resolved, Closed
    public string Domain { get; set; } = "example.invalid";
    public string AffectedHostname { get; set; } = "";
    public string PrimaryCategory { get; set; } = "General Windows Failure";
    public string Summary { get; set; } = "";
    public Guid? DiagnosticJobId { get; set; }
    public Guid? AiAnalysisId { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public DiagnosticJob? DiagnosticJob { get; set; }
    public AiAnalysisJob? AiAnalysisJob { get; set; }
}

// WFI 2.0 Remediation Playbooks
public sealed class Playbook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Windows System";
    public string RiskLevel { get; set; } = "Low"; // Low, Medium, High, Critical
    public string RequiredRole { get; set; } = "Analyst"; // Analyst, Admin
    public string Mode { get; set; } = "Recommend"; // Observe, Recommend, AutoRemediate
    public string PrechecksScript { get; set; } = "";
    public string ScriptContent { get; set; } = "";
    public string RollbackScript { get; set; } = "";
    public string Signature { get; set; } = "SIGNED-BY-WFI-ROOT";
    public bool IsApproved { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class RemediationExecution
{
    public Guid? CampaignId {get;set;}
    public bool CampaignPilot {get;set;}
    public bool LearningExcluded { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? DiagnosticJobId { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public bool Automatic { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaybookId { get; set; }
    public string PlaybookCode { get; set; } = "";
    public string PlaybookTitle { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string RiskLevel { get; set; } = "Low";
    public string Status { get; set; } = "PendingApproval"; // PendingApproval, Approved, Executing, Success, Failed, RolledBack
    public string PrecheckResult { get; set; } = "Pending";
    public string ExecutionOutput { get; set; } = "";
    public string ValidationResult { get; set; } = "Pending";
    public string RequestedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }

    // Navigation property
    public Playbook? Playbook { get; set; }
}

// WFI 2.0 Ollama AI Engine
public sealed class AiConfig
{
    public int Id { get; set; } = 1;
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ActiveModel { get; set; } = "phi4-mini";
    public double Temperature { get; set; } = 0.2;
    public string SystemPrompt { get; set; } = "Sen enterprise seviye bir Windows Failure Intelligence AI uzmanısın. Yalnızca verilen somut kanıtlara dayalı kök neden hipotezleri üret. Doğrulanmamış varsayımları kesin gerçek olarak sunma.";
    public bool IsAvailable { get; set; } = true;
}

public sealed class AiAnalysisJob
{
    public string VersionsJson {get;set;}="{}";
    public string LearningContextJson { get; set; } = "[]";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DiagnosticJobId { get; set; }
    public string Domain { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string ModelUsed { get; set; } = "phi4-mini";
    public string Status { get; set; } = "completed"; // pending, processing, completed, failed
    public string PromptContext { get; set; } = "";
    public string Conclusion { get; set; } = "";
    public string HypothesesJson { get; set; } = "[]"; // Array of AiHypothesis
    public double ConfidenceScore { get; set; } // 0.0 - 1.0; never fabricate model confidence
    public string MissingEvidenceJson { get; set; } = "[]";
    public string RecommendedPlaybooksJson { get; set; } = "[]";
    public string RawResponse { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
}

public sealed record AiHypothesis(
    string RuleOrId,
    string Title,
    string ConfidenceLevel, // High, Medium, Low
    string Explanation,
    string[] EvidenceCitations,
    string[] VerificationSteps
);

public sealed class InputRejectedException(string message) : Exception(message);
