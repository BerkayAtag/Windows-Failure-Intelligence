namespace WFI.Core;

public sealed class RepairCampaign
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Domain {get;set;}="";
    public string Name {get;set;}="";
    public Guid PlaybookId {get;set;}
    public string PlaybookCode {get;set;}="";
    public string TargetsJson {get;set;}="[]";
    public int PilotLimit {get;set;}=5;
    public int MaxParallel {get;set;}=2;
    public string Status {get;set;}="Draft";
    public DateTime StartsAt {get;set;}
    public DateTime EndsAt {get;set;}
    public DateTime ExpiresAt {get;set;}
    public string TimeZoneId {get;set;}="UTC";
    public string CreatedBy {get;set;}="";
    public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
    public DateTime LastCheckedAt {get;set;}=DateTime.UnixEpoch;
    public string? ApprovedBy {get;set;}
    public string? ExpandedBy {get;set;}
}
public sealed record CampaignDraft(string Name,Guid PlaybookId,Guid[] DeviceIds,DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,DateTimeOffset ExpiresAt,string TimeZoneId,int PilotLimit=5,int MaxParallel=2);
public sealed record CampaignCommand(string Action,bool HighRiskConfirmed=false);
