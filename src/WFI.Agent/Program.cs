using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace WFI.Agent;
public static class Program
{
    public static async Task Main(string[] args)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("WFI Agent requires Windows.");
        var builder=Host.CreateApplicationBuilder(args);
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory,"appsettings.local.json"),optional:false,reloadOnChange:false).AddEnvironmentVariables();
        var config=builder.Configuration.GetSection("Agent").Get<AgentConfig>()??throw new InvalidOperationException("Agent config missing.");
        config.Validate();
        var statePath=Path.GetFullPath(config.StatePath);
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        // A file handle remains valid across async thread switches, unlike a thread-owned mutex.
        using var instanceLock=new FileStream(statePath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        builder.Services.AddWindowsService(o=>o.ServiceName="WFI Agent");
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton<AgentStateStore>();
        builder.Services.AddSingleton<PowerShellRunner>();
        builder.Services.AddSingleton<RemediationRunner>();
        builder.Services.AddSingleton<WindowsEvidenceCollector>();
        builder.Services.AddHttpClient<AgentService>(c=>{c.BaseAddress=new Uri(config.ServerUrl.TrimEnd('/')+"/");c.Timeout=TimeSpan.FromSeconds(60);c.MaxResponseContentBufferSize=1024*1024;})
            .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
        builder.Services.AddHostedService<AgentHost>();
        await builder.Build().RunAsync();
    }
}

public sealed class AgentConfig
{
    public void Validate()
    {
        if(!Uri.TryCreate(ServerUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||uri.AbsolutePath!="/"||uri.Query.Length>0||uri.Fragment.Length>0)
            throw new InvalidOperationException("Agent ServerUrl must be an HTTPS origin without credentials, path, query or fragment.");
        if(DeviceId==Guid.Empty||PollSeconds<10||PollSeconds>600)throw new InvalidOperationException("Invalid device/poll configuration.");
        if(string.IsNullOrWhiteSpace(StatePath)||!Path.IsPathFullyQualified(StatePath))throw new InvalidOperationException("Agent state requires an absolute file path.");
    }
    public string BigFixReinstallSha256 {get;set;}="";
    public string ServerUrl {get;set;}="https://wfi.example.invalid";
    public Guid DeviceId {get;set;}
    public string EnrollmentToken {get;set;}="";
    public int PollSeconds {get;set;}=30;
    public string StatePath {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"WFI","agent-state.bin");
}
