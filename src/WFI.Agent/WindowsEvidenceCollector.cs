using System.Diagnostics.Eventing.Reader;
using System.Net.NetworkInformation;
using System.Text;
using WFI.Core;

namespace WFI.Agent;
public class WindowsEvidenceCollector
{
    public virtual Task<List<EvidenceItem>> CollectAsync(string scan,CancellationToken ct)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(!Enum.TryParse<ScanType>(scan,out var type)||!Enum.IsDefined(type))throw new InvalidDataException("Unknown scan.");
        var items=new List<EvidenceItem>();
        if(type is ScanType.FullSystemScan or ScanType.BigFixScan)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var service=new System.ServiceProcess.ServiceController("BESClient");
                var status=service.Status;
                Add("BESClient","BigFix",status==System.ServiceProcess.ServiceControllerStatus.Running ? "BESClient Running" : "WFI_BESCLIENT_STOPPED Status="+status);
            }
            catch(InvalidOperationException ex) when(ex.InnerException is System.ComponentModel.Win32Exception { NativeErrorCode:1060 })
            { Add("BESClient","BigFix","WFI_BESCLIENT_MISSING"); }
            catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            { Add("BESClient","BigFix","Service state unavailable; permissions must be checked. No automatic repair evidence."); }
            foreach(var programFiles in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}.Distinct())
            {
                var folder=Path.Combine(programFiles,"BigFix Enterprise","BES Client","__BESData","__Global","Logs");
                var path=Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture)+".log");
                try
                {
                    if(!File.Exists(path))continue;
                    using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                    stream.Position=Math.Max(0,stream.Length-12000);
                    var bytes=new byte[12000];var count=0;
                    while(count<bytes.Length){ct.ThrowIfCancellationRequested();var read=stream.Read(bytes,count,bytes.Length-count);if(read==0)break;count+=read;}
                    Add("BESClient daily log","BigFix",Encoding.UTF8.GetString(bytes,0,count));
                }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Add("BESClient daily log","BigFix","Log unavailable; inspect local permissions.");}
            }
        }
        if(type is ScanType.FullSystemScan or ScanType.EventLogScan or ScanType.UpdateScan)
        {
            var query=new EventLogQuery("System",PathType.LogName,"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= 86400000]]]"){ReverseDirection=true};
            try
            {
                using var reader=new EventLogReader(query);
                for(var i=0;i<30;i++)
                {
                    ct.ThrowIfCancellationRequested();using var record=reader.ReadEvent();if(record==null)break;
                    string description;try{description=record.FormatDescription()??"";}catch(EventLogException){description="Message unavailable";}
                    var marker=record.Id==5719&&string.Equals(record.ProviderName,"NETLOGON",StringComparison.OrdinalIgnoreCase)?"ERROR_NO_LOGON_SERVERS\n":"";
                    Add("System/"+record.Id,"EventLog",$"Provider={record.ProviderName} EventID={record.Id}\n{marker}{description}");
                }
            }
            catch(EventLogException){Add("System","EventLog","Event log unavailable; permission/channel check required.");}
        }
        if(type is ScanType.FullSystemScan or ScanType.SccmScan)
        {
            foreach(var name in new[]{"CcmExec.log","LocationServices.log","ClientIDManagerStartup.log"})
            {
                ct.ThrowIfCancellationRequested();var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"CCM","Logs",name);
                if(!File.Exists(path))continue;
                try
                {
                    using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                    var offset=Math.Max(0,stream.Length-16000);stream.Position=offset;
                    var bytes=new byte[16000];var count=0;
                    while(count<bytes.Length){ct.ThrowIfCancellationRequested();var read=stream.Read(bytes,count,bytes.Length-count);if(read==0)break;count+=read;}
                    var tail=Encoding.UTF8.GetString(bytes,0,count);
                    if(offset>0){var newline=tail.IndexOf('\n');tail=newline>=0?tail[(newline+1)..]:"";}
                    Add(name,"SCCM",tail);
                }
                catch(IOException){Add(name,"SCCM","Log temporarily unavailable.");}
            }
        }
        if(type is ScanType.FullSystemScan or ScanType.NetworkScan)
        {
            var dns=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up)
                .SelectMany(n=>n.GetIPProperties().DnsAddresses).Select(a=>a.ToString()).Distinct().Take(10);
            Add("Network","Domain / DNS","DNS servers: "+string.Join(", ",dns));
        }
        var root=Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
        Add("OS","System",$"OS: {Environment.OSVersion}; System drive available bytes: {new DriveInfo(root).AvailableFreeSpace}");
        return Task.FromResult(items);
        void Add(string source,string category,string content)
        {
            var safe=LogAnalyzer.Redact(content);if(safe.Length>12000)safe=safe[..12000];
            items.Add(new(){Source=source,Category=category,Content=safe});
        }
    }
}
