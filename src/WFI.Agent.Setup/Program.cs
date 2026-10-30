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
        // Built-in Windows PowerShell 5.1: no separate PowerShell 7 prerequisite on clients.
        var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        if(!File.Exists(shell))throw new InvalidOperationException("Windows PowerShell bulunamadı.");
        var start=new ProcessStartInfo(shell){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-Command",
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

internal sealed record RetainedAgent(Uri Server,Guid DeviceId);

internal sealed class SetupForm:Form
{
    static readonly Color Red=Color.FromArgb(201,0,22);
    readonly TextBox server=new(){Text=DefaultServer(),Dock=DockStyle.Fill};
    // Suggest the WFI host in this machine's own DNS domain; the operator can change it before installing.
    static string DefaultServer()
    {
        try{var domain=System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;return "https://wfi."+(string.IsNullOrWhiteSpace(domain)?"example.invalid":domain.Trim().ToLowerInvariant());}
        catch(System.Net.NetworkInformation.NetworkInformationException){return "https://wfi.example.invalid";}
    }
    readonly TextBox username=new(){Dock=DockStyle.Fill};
    readonly TextBox password=new(){UseSystemPasswordChar=true,Dock=DockStyle.Fill};
    readonly Button install=new(){Text="Kur ve konsola kaydet",AutoSize=true,Padding=new Padding(10,4,10,4)};
    readonly Button theme=new(){Size=new Size(150,34),FlatStyle=FlatStyle.Flat,ForeColor=Color.White};
    readonly Label status=new(){AutoSize=true,MaximumSize=new Size(530,0),Text="Bu makine WFI Admin hesabınızın domain'ine otomatik kaydedilir. Parolanız saklanmaz."};
    readonly Panel header=new(){Dock=DockStyle.Top,Height=78};
    readonly Label footer=new(){Text="© 2026 Berkay Atağ · Windows Failure Intelligence (WFI)",Dock=DockStyle.Bottom,Height=30,Padding=new Padding(24,0,0,8)};
    readonly RetainedAgent? retained=ReadRetainedAgent();
    bool running,dark;

    // Uninstall keeps identity/configuration; a retained agent is reinstalled or upgraded with that identity, without a new registration.
    internal static RetainedAgent? ReadRetainedAgent(string? programFiles=null,string? programData=null)
    {
        try
        {
            var config=Path.Combine(programFiles??Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WFI Agent","appsettings.local.json");
            var state=Path.Combine(programData??Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"WFI","agent-state.bin");
            if(!File.Exists(config)||!File.Exists(state))return null;
            using var document=JsonDocument.Parse(File.ReadAllText(config));
            var agent=document.RootElement.GetProperty("Agent");
            return Uri.TryCreate(agent.GetProperty("ServerUrl").GetString(),UriKind.Absolute,out var url)&&url.Scheme=="https"&&Guid.TryParse(agent.GetProperty("DeviceId").GetString(),out var id)&&id!=Guid.Empty?new(url,id):null;
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException){return null;}
    }
    static bool SystemUsesDarkTheme()
    {
        try{return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) is int light&&light==0;}
        catch(Exception ex)when(ex is System.Security.SecurityException or IOException){return false;}
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);

    public SetupForm()
    {
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Text="WFI Ajan Kurulumu";ClientSize=new Size(590,470);MinimumSize=new Size(610,510);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);
        install.BackColor=Red;install.ForeColor=Color.White;install.FlatStyle=FlatStyle.Flat;install.FlatAppearance.BorderColor=Red;
        header.Controls.Add(new Label{Text="WFI · İstemci ajanı",Font=new Font("Segoe UI",17,FontStyle.Bold),ForeColor=Color.White,AutoSize=true,Location=new Point(22,10)});
        header.Controls.Add(new Label{Text="Makine: "+Environment.MachineName,ForeColor=Color.White,AutoSize=true,Location=new Point(25,48)});
        // Docking keeps the theme button at the header's right edge at any DPI or font scale.
        theme.FlatAppearance.BorderColor=Color.White;theme.Dock=DockStyle.Fill;
        var themeSlot=new Panel{Dock=DockStyle.Right,Width=190,Padding=new Padding(12,20,20,20)};themeSlot.Controls.Add(theme);header.Controls.Add(themeSlot);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24,16,24,8),ColumnCount=1,AutoScroll=true};
        void Field(string title,Control control){layout.Controls.Add(new Label{Text=title,AutoSize=true,Margin=new Padding(3,8,3,2)});layout.Controls.Add(control);}
        Field("WFI sunucusu (HTTPS)",server);
        if(retained==null){Field("WFI Admin kullanıcı adı",username);Field("Parola",password);}
        else
        {
            server.Text=retained.Server.AbsoluteUri.TrimEnd('/');server.ReadOnly=true;install.Text="Mevcut kimlikle kur / yükselt";
            status.Text="Bu makinede korunan WFI ajan kimliği bulundu. Yeni kayıt ve parola gerekmez; ajan aynı cihaz kimliğiyle yeniden kurulur.";
        }
        install.Margin=new Padding(3,18,3,10);layout.Controls.Add(install);layout.Controls.Add(status);
        Controls.Add(layout);Controls.Add(header);Controls.Add(footer);
        install.Click+=async(_,_)=>await Install();
        theme.Click+=(_,_)=>ApplyTheme(!dark);
        HandleCreated+=(_,_)=>ApplyTheme(dark);
        FormClosing+=(_,e)=>{if(running)e.Cancel=true;};
        ApplyTheme(SystemUsesDarkTheme());
    }
    internal void ApplyTheme(bool useDark)
    {
        dark=useDark;
        Color bg=dark?Color.FromArgb(23,25,31):Color.White,fg=dark?Color.FromArgb(232,235,241):Color.FromArgb(30,32,38),field=dark?Color.FromArgb(35,39,48):Color.White;
        BackColor=bg;ForeColor=fg;header.BackColor=dark?Color.FromArgb(163,0,18):Red;theme.BackColor=header.BackColor;
        theme.Text=dark?"☀  Aydınlık tema":"☾  Karanlık tema";
        void Walk(Control parent){foreach(Control c in parent.Controls){if(c==header)continue;
            if(c is TextBox t){t.BackColor=field;t.ForeColor=fg;t.BorderStyle=BorderStyle.FixedSingle;}
            else if(c is Label or TableLayoutPanel){c.BackColor=bg;c.ForeColor=fg;}
            Walk(c);}}
        Walk(this);
        footer.ForeColor=dark?Color.FromArgb(160,168,181):Color.DimGray;
        // Windows 10 1809+ / 11 dark title bar; older builds ignore the attribute.
        if(IsHandleCreated){var value=dark?1:0;if(DwmSetWindowAttribute(Handle,20,ref value,sizeof(int))!=0)DwmSetWindowAttribute(Handle,19,ref value,sizeof(int));}
    }
    async Task Install()
    {
        if(running)return;
        if(!Uri.TryCreate(server.Text.Trim(),UriKind.Absolute,out var url)||url.Scheme!="https"||url.UserInfo.Length>0||url.AbsolutePath!="/"||url.Query.Length>0||url.Fragment.Length>0)
        {status.Text="Yalnız HTTPS sunucu kök adresi girin.";return;}
        if(retained==null&&File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WFI Agent","appsettings.local.json")))
        {status.Text="Mevcut ajan yapılandırması bulundu ancak kimlik durumu okunamadı. İstemci rehberindeki yeniden kayıt adımlarını izleyin.";return;}
        running=true;install.Enabled=false;server.Enabled=username.Enabled=password.Enabled=false;
        try
        {
            if(retained!=null)
            {
                status.Text="Korunan kimlikle Windows servisi kuruluyor…";
                await SetupInstall.RunAsync(retained.Server,retained.DeviceId,"");
                status.Text="Ajan aynı cihaz kimliğiyle kuruldu. Konsolda cihaz birkaç dakika içinde online görünür.";
                return;
            }
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
