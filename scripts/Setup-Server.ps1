#Requires -RunAsAdministrator
#Requires -Version 5.1
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
. (Join-Path $PSScriptRoot 'ServerSetupSupport.ps1')
[Windows.Forms.Application]::EnableVisualStyles()
$form=New-Object Windows.Forms.Form
$form.Text='WFI Sunucu Kurulumu';$form.Size=New-Object Drawing.Size(700,650);$form.StartPosition='CenterScreen';$form.FormBorderStyle='FixedDialog';$form.MaximizeBox=$false
$form.Font=New-Object Drawing.Font('Segoe UI',10);$form.BackColor=[Drawing.Color]::White
$title=New-Object Windows.Forms.Label;$title.Text='WFI | Sunucu kurulumu';$title.SetBounds(30,20,620,40);$title.Font=New-Object Drawing.Font('Segoe UI',20,[Drawing.FontStyle]::Bold);$title.ForeColor=[Drawing.Color]::FromArgb(201,0,22);$form.Controls.Add($title)
$intro=New-Object Windows.Forms.Label;$intro.SetBounds(30,75,620,115);$intro.Text="Ileri > Kur > Bitir. IIS, .NET, PostgreSQL ve yerel AI otomatik hazirlanir.`nIlk giris: local:admin. LDAP daha sonra ayarlanabilir.`nInternet baglantisi ve en az 15 GB bos alan gerekir. Bilesenler ve model resmi kaynaklardan indirilir. Bu paket cevrimdisi tam paket degildir.";$form.Controls.Add($intro)
$panel=New-Object Windows.Forms.Panel;$panel.SetBounds(30,195,620,300);$form.Controls.Add($panel)
function Field([string]$label,[int]$y,[string]$value,[bool]$secret=$false){$l=New-Object Windows.Forms.Label;$l.Text=$label;$l.SetBounds(0,$y,205,25);$panel.Controls.Add($l);$t=New-Object Windows.Forms.TextBox;$t.SetBounds(210,$y,405,25);$t.Text=$value;$t.UseSystemPasswordChar=$secret;$panel.Controls.Add($t);return $t}
$app=Field 'Uygulama dizini' 0 'C:\Program Files\WFI Server'
$data=Field 'Veritabani / veri dizini' 40 'C:\ProgramData\WFI Server'
$defaultHostname=$env:COMPUTERNAME
try{$defaultHostname=[Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName}catch{# Offline DNS must not prevent the wizard opening.
}
$hostname=Field 'Sunucu DNS adi' 80 $defaultHostname
$defaultDomain='example.invalid'
try{$detectedDomain=[Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().DomainName;if($detectedDomain){$defaultDomain=$detectedDomain}}catch{}
$domain=Field 'Istemci domain' 120 $defaultDomain
$password=Field 'Yonetici parolasi (14+)' 160 '' $true
$confirm=Field 'Parola tekrar' 200 '' $true
$cert=Field 'Sertifika izi (istege bagli)' 240 ''
$panel.Visible=$false
$status=New-Object Windows.Forms.Label;$status.SetBounds(30,500,620,65);$status.Text='Varsayilan ayarlar tek sunuculu kurulum icindir. Mevcut sunucuyu yukseltmek icin bakim kilavuzunu kullanin.';$form.Controls.Add($status)
$next=New-Object Windows.Forms.Button;$next.Text='Ileri';$next.SetBounds(530,570,120,32);$next.BackColor=[Drawing.Color]::FromArgb(201,0,22);$next.ForeColor=[Drawing.Color]::White;$form.Controls.Add($next)
$script:phase=0;$script:process=$null;$script:result=2
$timer=New-Object Windows.Forms.Timer;$timer.Interval=1000
$stateFile=Join-Path (Split-Path $PSScriptRoot -Parent) 'setup-status.txt'
$timer.Add_Tick({
    if(Test-Path $stateFile){try{$status.Text=[IO.File]::ReadAllText($stateFile)}catch{}}
    if($script:process -and $script:process.HasExited){$timer.Stop();$next.Enabled=$true;$next.Text='Bitir';$script:phase=3;$script:result=$script:process.ExitCode;if($script:result -ne 0){$status.ForeColor=[Drawing.Color]::DarkRed};$script:process.Dispose();$script:process=$null}
})
$next.Add_Click({
    try {
        if($script:phase -eq 0){$panel.Visible=$true;$intro.Text="Kurulum ayarlarini kontrol edin. Bos sertifika alani: sunucuda guvenilen yerel HTTPS sertifikasi olusturulur; istemcilere guven GPO ile dagitilmalidir.`nVeritabani ve AI yalniz 127.0.0.1 uzerinden dinler.";$next.Text='Kur';$script:phase=1;return}
        if($script:phase -eq 3){$form.Close();return}
        if($password.Text -cne $confirm.Text){throw 'Parolalar ayni olmali.'}
        $options=@{InstallPath=$app.Text.Trim();DataPath=$data.Text.Trim();Hostname=$hostname.Text.Trim();Domain=$domain.Text.Trim();CertificateThumbprint=$cert.Text.Trim();PasswordHash=(New-WfiPasswordHash $password.Text)}
        Assert-WfiSetupOptions $options
        $optionsPath=Join-Path (Split-Path $PSScriptRoot -Parent) 'setup-options.json'
        $options | ConvertTo-Json | Set-Content -LiteralPath $optionsPath -Encoding UTF8
        $password.Clear();$confirm.Clear();$panel.Enabled=$false;$next.Enabled=$false;$script:phase=2
        $start=New-Object Diagnostics.ProcessStartInfo
        $start.FileName=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true
        $start.Arguments='-NoProfile -File "'+(Join-Path $PSScriptRoot 'Install-ServerAutomatic.ps1')+'" -OptionsPath "'+$optionsPath+'"'
        $script:process=[Diagnostics.Process]::Start($start);$timer.Start();$status.Text='Kurulum baslatildi. Lutfen bekleyin.'
    }catch{[void][Windows.Forms.MessageBox]::Show($_.Exception.Message,'WFI Kurulum');if($script:phase -eq 2){$script:phase=1;$panel.Enabled=$true;$next.Enabled=$true}}
})
$form.Add_FormClosing({param($sender,$event)if($script:process -and !$script:process.HasExited){$event.Cancel=$true;[void][Windows.Forms.MessageBox]::Show('Kurulum calisiyor. Veri butunlugu icin bitmesini bekleyin.','WFI')}})
try{[void]$form.ShowDialog()}finally{$timer.Dispose();$form.Dispose()}
exit $script:result
