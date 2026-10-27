using System.Diagnostics;
using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using WFI.Core;

namespace WFI.Agent.Setup;
internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if(args.Length>0)
        {
            try
            {
                var options=AgentSetupOptions.Parse(args);
                var token="";
                if(options.TokenFile!=null)
                {
                    var info=new FileInfo(Path.GetFullPath(options.TokenFile));
                    for(FileSystemInfo? current=info;current!=null;current=current is FileInfo f?f.Directory:((DirectoryInfo)current).Parent)
                        if(current.Attributes.HasFlag(FileAttributes.ReparsePoint))return 3;
                    var acl=info.GetAccessControl();
                    var trusted=new[]{"S-1-5-18","S-1-5-32-544",WindowsIdentity.GetCurrent().User!.Value};
                    var owner=acl.GetOwner(typeof(SecurityIdentifier))?.Value;
                    if(!acl.AreAccessRulesProtected||owner==null||!trusted.Contains(owner))return 3;
                    foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
                        if(rule.AccessControlType==AccessControlType.Allow&&!trusted.Contains(rule.IdentityReference.Value))return 3;
                    if(info.Length>256)return 3;
                    token=File.ReadAllText(info.FullName).Trim();
                    if(token.Length!=64||!token.All(Uri.IsHexDigit))return 3;
                }
                var existing=File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WFI Agent","appsettings.local.json"));
                if(existing!=options.Upgrade)return 4;
                SetupInstall.RunAsync(options.Server,options.DeviceId,token,options.BigFixHash).GetAwaiter().GetResult();return 0;
            }
            catch(ArgumentException){return 2;}
            catch{ return 10; }
        }
        ApplicationConfiguration.Initialize();Application.Run(new SetupForm());return 0;
    }
}

internal static class SetupInstall
{
    internal static async Task RunAsync(Uri server,Guid deviceId,string token,string? bigFixHash=null)
    {
        var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PowerShell","7","pwsh.exe");
        if(!File.Exists(shell))throw new InvalidOperationException("PowerShell 7 x64 gerekli.");
        var start=new ProcessStartInfo(shell){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-NonInteractive","-Command",
            "$ErrorActionPreference='Stop'; try { $p=[Console]::In.ReadLine() | ConvertFrom-Json; $options=@{PackagePath=$p.packagePath;ServerUrl=$p.serverUrl;DeviceId=$p.deviceId;EnrollmentToken=$(if($p.token){ConvertTo-SecureString $p.token -AsPlainText -Force}else{[Security.SecureString]::new()})}; if($p.bigFixHash){$options.BigFixReinstallSha256=$p.bigFixHash}; & (Join-Path $p.packagePath 'Install-Agent.ps1') @options; exit 0 } catch { exit 1 }"})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)??throw new IOException();
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{packagePath=AppContext.BaseDirectory,serverUrl=server.AbsoluteUri,deviceId,token,bigFixHash}));process.StandardInput.Close();
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);await process.WaitForExitAsync();throw;}
        await Task.WhenAll(output,error);
        if(process.ExitCode!=0)throw new InvalidOperationException("Servis kurulumu tamamlanamadı. Bekleyen kayıt varsa token yenileyip kurulum rehberindeki kurtarma adımlarını izleyin.");
    }
}

internal sealed class SetupForm:Form
{
    readonly TextBox server=new(){Text="https://wfi.example.invalid",Dock=DockStyle.Fill};
    readonly TextBox username=new(){Dock=DockStyle.Fill};
    readonly TextBox password=new(){UseSystemPasswordChar=true,Dock=DockStyle.Fill};
    readonly Button install=new(){Text="Kur ve konsola kaydet",AutoSize=true};
    readonly Label status=new(){AutoSize=true,MaximumSize=new Size(510,0),Text="Bu makine WFI Admin hesabınızın domain'ine otomatik kaydedilir. Parolanız saklanmaz."};
    bool running;
    public SetupForm()
    {
        Text="WFI Ajan Kurulumu";ClientSize=new Size(570,410);MinimumSize=new Size(590,450);StartPosition=FormStartPosition.CenterScreen;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,AutoScroll=true};
        void Field(string title,Control control){layout.Controls.Add(new Label{Text=title,AutoSize=true});layout.Controls.Add(control);}
        layout.Controls.Add(new Label{Text="WFI • İstemci ajanı",Font=new Font(Font.FontFamily,18,FontStyle.Bold),AutoSize=true});
        layout.Controls.Add(new Label{Text="Makine: "+Environment.MachineName,AutoSize=true});
        Field("WFI sunucusu (HTTPS)",server);Field("WFI Admin kullanıcı adı",username);Field("Parola",password);
        layout.Controls.Add(install);layout.Controls.Add(status);Controls.Add(layout);
        install.Click+=async(_,_)=>await Install();
        FormClosing+=(_,e)=>{if(running)e.Cancel=true;};
    }
    async Task Install()
    {
        if(running)return;
        if(!Uri.TryCreate(server.Text.Trim(),UriKind.Absolute,out var url)||url.Scheme!="https"||url.UserInfo.Length>0||url.AbsolutePath!="/"||url.Query.Length>0||url.Fragment.Length>0)
        {status.Text="Yalnız HTTPS sunucu kök adresi girin.";return;}
        var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PowerShell","7","pwsh.exe");
        if(!File.Exists(shell)){status.Text="Önce PowerShell 7 x64 kurun, ardından kurulumu tekrar açın.";return;}
        if(File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WFI Agent","appsettings.local.json")))
        {status.Text="Mevcut ajan bulundu. Kimliği koruyarak yükseltmek için paket içindeki Install-Agent.ps1 ve istemci rehberini kullanın.";return;}
        running=true;install.Enabled=false;server.Enabled=username.Enabled=password.Enabled=false;
        try
        {
            status.Text="Kimlik doğrulanıyor ve makine kaydediliyor…";
            using var handler=new HttpClientHandler{AllowAutoRedirect=false,UseCookies=true};
            using var client=new HttpClient(handler){BaseAddress=url,Timeout=TimeSpan.FromSeconds(30)};
            var ticket=await AgentSetupRegistration.RegisterAsync(client,username.Text.Trim(),password.Text,Environment.MachineName,CancellationToken.None);
            password.Clear();status.Text="Kayıt hazır. Windows servisi kuruluyor…";
            await SetupInstall.RunAsync(url,ticket.Id,ticket.EnrollmentToken);
            status.Text="Ajan kuruldu. Cihazlar ve AI ekranı otomatik yenilenir; ilk bağlantı yaklaşık 30–60 saniye sürebilir. Bağlantı gelmezse servis ve HTTPS erişimini kontrol edin.";
        }
        catch(InvalidOperationException ex){status.Text=ex.Message;}
        catch(Exception ex)when(ex is HttpRequestException or IOException or OperationCanceledException or JsonException or System.ComponentModel.Win32Exception)
        {status.Text="Kurulum tamamlanamadı. HTTPS/CA, sunucu erişimi ve istemci rehberini kontrol edin. Bekleyen kayıt varsa yeniden oluşturmadan konsoldan token yenileyin.";}
        finally{password.Clear();running=false;install.Enabled=true;server.Enabled=username.Enabled=password.Enabled=true;}
    }
}
