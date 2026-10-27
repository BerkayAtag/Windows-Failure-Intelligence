using System.Security.Cryptography;
using System.Text.Json;
using WFI.Core;

namespace WFI.Agent;
public sealed class AgentState
{
    public Guid DeviceId {get;set;}
    public string ServerUrl {get;set;}="";
    public string Credential {get;set;}=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public bool Enrolled {get;set;}
    public RemediationAssignment? ActiveJob {get;set;}
    public RemediationReceipt? PendingReceipt {get;set;}
}
public sealed class AgentStateStore(AgentConfig config)
{
    public async Task<AgentState> LoadAsync(CancellationToken ct)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(!File.Exists(config.StatePath))return new(){DeviceId=config.DeviceId,ServerUrl=config.ServerUrl};
        if(new FileInfo(config.StatePath).Length>131072)throw new InvalidDataException("Agent state exceeds size limit.");
        var bytes=ProtectedData.Unprotect(await File.ReadAllBytesAsync(config.StatePath,ct),null,DataProtectionScope.CurrentUser);
        var state=JsonSerializer.Deserialize<AgentState>(bytes)??throw new InvalidDataException("Invalid agent state.");
        if(state.DeviceId!=config.DeviceId||state.ServerUrl!=config.ServerUrl)throw new InvalidDataException("Agent state belongs to a different device/server.");
        if(state.Credential==null||!System.Text.RegularExpressions.Regex.IsMatch(state.Credential,"\\A[0-9a-fA-F]{64}\\z")||
            (state.PendingReceipt!=null&&(state.ActiveJob==null||state.PendingReceipt.LeaseToken!=state.ActiveJob.LeaseToken)))
            throw new InvalidDataException("Agent journal identity or receipt is invalid; preserve it for recovery.");
        return state;
    }
    public async Task SaveAsync(AgentState state,CancellationToken ct)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        var path=Path.GetFullPath(config.StatePath);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes=ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(state),null,DataProtectionScope.CurrentUser);
        var temp=path+".new";
        await using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
        {
            await file.WriteAsync(bytes,ct);file.Flush(flushToDisk:true);
        }
        File.Move(temp,path,true);
    }
}
