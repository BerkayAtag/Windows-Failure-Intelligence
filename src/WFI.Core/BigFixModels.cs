namespace WFI.Core;
public sealed class BigFixMapping
{
    public Guid DeviceId {get;set;}
    public string Domain {get;set;}="";
    public long ComputerId {get;set;}
    public string ConnectorFingerprint {get;set;}="";
    public string VerifiedDnsName {get;set;}="";
    public string VerifiedBy {get;set;}="";
    public DateTime VerifiedAt {get;set;}=DateTime.UtcNow;
}
public sealed class BigFixVerification
{
    public Guid ExecutionId {get;set;}
    public Guid DeviceId {get;set;}
    public string Domain {get;set;}="";
    public long ComputerId {get;set;}
    public string ConnectorFingerprint {get;set;}="";
    public string ExpectedDnsName {get;set;}="";
    public string Status {get;set;}="PendingBaseline";
    public bool BaselineReady {get;set;}
    public DateTime? BeforeReportAt {get;set;}
    public DateTime? LatestReportAt {get;set;}
    public DateTime? ObservedAt {get;set;}
    public DateTime NextCheckAt {get;set;}=DateTime.UtcNow;
    public Guid? FetchToken {get;set;}
}
public sealed record BigFixComputer(long ComputerId,string Name,string DnsName,DateTime LastReportAt,DateTime ObservedAt);
public sealed record BigFixMappingRequest(long ComputerId,bool ConfirmIdentity);
