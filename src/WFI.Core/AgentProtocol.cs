namespace WFI.Core;

public sealed class AutomationPolicy
{
    public string Domain { get; set; } = "";
    public bool Enabled { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "";
}

public sealed record RegisterDeviceRequest(string Hostname);
public sealed record AgentEnrollment(Guid DeviceId, string Token, string Credential);
public sealed record AgentEnrollmentResponse(Guid DeviceId, string Hostname, string Domain);
public sealed record AgentPulse(string AgentVersion, string OsVersion);
public sealed record AgentCheckIn(AgentPulse Pulse, string[] Capabilities);
public sealed record AgentCheckInResponse(int ProtocolVersion, string[] Capabilities, int IdlePollSeconds,
    DiagnosticAssignment? Diagnostic, RemediationAssignment? Remediation);
public sealed record DiagnosticAssignment(Guid JobId, Guid LeaseToken, string ScanType);
public sealed record EvidenceSubmission(Guid LeaseToken, List<EvidenceItem> Evidence);
public sealed record RemediationAssignment(Guid Id, Guid LeaseToken, string Code, string Version, DateTime ExpiresAt);
public sealed record RemediationReceipt(Guid LeaseToken, bool Success, string Precheck, string Output, string Verification);
public sealed record AutomationUpdate(bool Enabled, bool ApplyToAll = false);
public sealed record DeviceAutomationUpdate(string Mode);
