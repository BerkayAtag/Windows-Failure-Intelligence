#Requires -RunAsAdministrator
#Requires -Version 5.1
# UTF-8 with BOM: Windows PowerShell 5.1 needs the BOM to show Turkish text correctly.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
. (Join-Path $PSScriptRoot 'ServerSetupSupport.ps1')
[Windows.Forms.Application]::EnableVisualStyles()
$red=[Drawing.Color]::FromArgb(201,0,22)
$form=New-Object Windows.Forms.Form
$form.Text='WFI Sunucu Kurulumu';$form.ClientSize=New-Object Drawing.Size(720,640);$form.StartPosition='CenterScreen';$form.FormBorderStyle='FixedDialog';$form.MaximizeBox=$false
$form.Font=New-Object Drawing.Font('Segoe UI',10);$form.BackColor=[Drawing.Color]::White
$icon=Join-Path (Split-Path $PSScriptRoot -Parent) 'api\wfi.ico'
if(Test-Path -LiteralPath $icon){try{$form.Icon=New-Object Drawing.Icon($icon)}catch{# The wizard still works with the default icon.
}}
$header=New-Object Windows.Forms.Panel;$header.SetBounds(0,0,720,84);$header.BackColor=$red;$form.Controls.Add($header)
$title=New-Object Windows.Forms.Label;$title.Text='WFI · Sunucu kurulumu';$title.SetBounds(28,14,540,36);$title.Font=New-Object Drawing.Font('Segoe UI',18,[Drawing.FontStyle]::Bold);$title.ForeColor=[Drawing.Color]::White;$header.Controls.Add($title)
$step=New-Object Windows.Forms.Label;$step.SetBounds(30,52,540,22);$step.ForeColor=[Drawing.Color]::White;$step.Text='Adım 1 / 3 · Hoş geldiniz';$header.Controls.Add($step)
$themeButton=New-Object Windows.Forms.Button;$themeButton.SetBounds(574,24,122,34);$themeButton.FlatStyle='Flat';$themeButton.ForeColor=[Drawing.Color]::White;$themeButton.FlatAppearance.BorderColor=[Drawing.Color]::White;$themeButton.Font=New-Object Drawing.Font('Segoe UI',9);$header.Controls.Add($themeButton)

# Page 1: welcome.
$intro=New-Object Windows.Forms.Label;$intro.SetBounds(30,104,660,250)
$intro.Text="Windows Failure Intelligence sunucusu bu makineye kurulacak.`n`nKurulum gerekenleri sizin yerinize hazırlar:`n  •  IIS web sunucusu ve .NET 10 Hosting Bundle`n  •  Yalnız bu makinede dinleyen PostgreSQL veritabanı`n  •  Yerel yapay zekâ: Ollama ve phi4-mini modeli (isteğe bağlı)`n  •  HTTPS sertifikası, Windows güvenlik duvarı kuralı ve veritabanı şeması`n`nİlk giriş kullanıcı adı local:admin, parolası bir sonraki adımda belirlediğiniz paroladır. LDAP / Active Directory daha sonra etkinleştirilebilir.`n`nİnternet bağlantısı ve en az 15 GB boş alan gerekir (AI kurulmazsa 5 GB). Bileşenler resmi kaynaklardan indirilir ve SHA doğrulamasından geçer."
$form.Controls.Add($intro)

# Page 2: settings.
$panel=New-Object Windows.Forms.Panel;$panel.SetBounds(30,98,660,430);$panel.Visible=$false;$form.Controls.Add($panel)
function Field([string]$label,[int]$y,[string]$value,[bool]$secret=$false){$l=New-Object Windows.Forms.Label;$l.Text=$label;$l.SetBounds(0,($y+3),220,24);$panel.Controls.Add($l);$t=New-Object Windows.Forms.TextBox;$t.SetBounds(225,$y,430,26);$t.Text=$value;$t.UseSystemPasswordChar=$secret;$panel.Controls.Add($t);return $t}
$app=Field 'Uygulama dizini' 0 'C:\Program Files\WFI Server'
$data=Field 'Veritabanı / veri dizini' 34 'C:\ProgramData\WFI Server'
$defaultHostname=$env:COMPUTERNAME
try{$defaultHostname=[Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName}catch{# Offline DNS must not prevent the wizard opening.
}
$hostname=Field 'Sunucu DNS adı' 68 $defaultHostname
$defaultDomain='example.invalid'
try{$detectedDomain=[Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().DomainName;if($detectedDomain){$defaultDomain=$detectedDomain}}catch{}
$domain=Field 'İstemci domain' 102 $defaultDomain
$password=Field 'Yönetici parolası (en az 14)' 136 '' $true
$confirm=Field 'Parola tekrar' 170 '' $true
$cert=Field 'Sertifika parmak izi (isteğe bağlı)' 204 ''
$aiBox=New-Object Windows.Forms.GroupBox;$aiBox.Text='Yerel yapay zekâ (Ollama)';$aiBox.SetBounds(0,246,655,130);$panel.Controls.Add($aiBox)
$existingOllama=Test-WfiExistingOllama
$aiInstall=New-Object Windows.Forms.RadioButton;$aiInstall.SetBounds(14,26,630,24);$aiInstall.Text='Ollama ve phi4-mini modelini WFI için kur (önerilen)';$aiBox.Controls.Add($aiInstall)
$aiExisting=New-Object Windows.Forms.RadioButton;$aiExisting.SetBounds(14,56,630,24);$aiBox.Controls.Add($aiExisting)
$aiNone=New-Object Windows.Forms.RadioButton;$aiNone.SetBounds(14,86,630,24);$aiNone.Text='Şimdi kurma (AI teşhisi, Ollama sonradan hazırlanınca çalışır)';$aiBox.Controls.Add($aiNone)
if($existingOllama){$aiExisting.Text='Bu makinede çalışan Ollama bulundu: onu kullan (127.0.0.1:11434)';$aiExisting.Checked=$true}
else{$aiExisting.Text='Bu makinede çalışan Ollama bulunamadı';$aiExisting.Enabled=$false;$aiInstall.Checked=$true}
$hint=New-Object Windows.Forms.Label;$hint.SetBounds(0,382,655,48);$hint.ForeColor=[Drawing.Color]::DimGray
$hint.Text='Sertifika boş bırakılırsa bu sunucu için HTTPS sertifikası oluşturulur; istemcilere güveni GPO ile dağıtın. Veritabanı ve AI yalnız 127.0.0.1 üzerinden dinler.';$panel.Controls.Add($hint)

# Page 3: progress.
$progress=New-Object Windows.Forms.ProgressBar;$progress.SetBounds(30,140,660,24);$progress.Minimum=0;$progress.Maximum=7;$progress.Visible=$false;$form.Controls.Add($progress)
$status=New-Object Windows.Forms.Label;$status.SetBounds(30,176,660,180);$status.Visible=$false;$form.Controls.Add($status)
$open=New-Object Windows.Forms.LinkLabel;$open.SetBounds(30,370,660,26);$open.Visible=$false;$open.LinkColor=$red;$form.Controls.Add($open)
$open.Add_LinkClicked({try{Start-Process $open.Tag}catch{}})

$line=New-Object Windows.Forms.Label;$line.SetBounds(0,560,720,1);$line.BackColor=[Drawing.Color]::Gainsboro;$form.Controls.Add($line)
$footer=New-Object Windows.Forms.Label;$footer.SetBounds(30,590,400,22);$footer.ForeColor=[Drawing.Color]::DimGray;$footer.Font=New-Object Drawing.Font('Segoe UI',8.5);$footer.Text='© 2026 Berkay Atağ · Windows Failure Intelligence (WFI)';$form.Controls.Add($footer)
$next=New-Object Windows.Forms.Button;$next.Text='İleri >';$next.SetBounds(560,580,130,36);$next.BackColor=$red;$next.ForeColor=[Drawing.Color]::White;$next.FlatStyle='Flat';$form.Controls.Add($next);$form.AcceptButton=$next
$cancel=New-Object Windows.Forms.Button;$cancel.Text='İptal';$cancel.SetBounds(440,580,110,36);$form.Controls.Add($cancel)
$cancel.Add_Click({$form.Close()})

# Theme: follows the Windows app theme on start; the header button switches light/dark for this window.
Add-Type -Namespace WfiSetup -Name Dwm -MemberDefinition '[DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);'
function Test-WfiSystemDarkTheme {
    try{return (Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name AppsUseLightTheme -ErrorAction Stop).AppsUseLightTheme -eq 0}catch{return $false}
}
$script:dark=$false;$script:failed=$false
function Set-WfiTheme([bool]$Dark) {
    $script:dark=$Dark
    $rgb={param($r,$g,$b)[Drawing.Color]::FromArgb($r,$g,$b)}
    $bg=if($Dark){& $rgb 23 25 31}else{[Drawing.Color]::White}
    $fg=if($Dark){& $rgb 232 235 241}else{& $rgb 30 32 38}
    $muted=if($Dark){& $rgb 160 168 181}else{[Drawing.Color]::DimGray}
    $field=if($Dark){& $rgb 35 39 48}else{[Drawing.Color]::White}
    $edge=if($Dark){& $rgb 60 66 78}else{[Drawing.Color]::Gainsboro}
    $form.BackColor=$bg;$header.BackColor=if($Dark){& $rgb 163 0 18}else{$red};$themeButton.BackColor=$header.BackColor
    $themeButton.Text=if($Dark){'☀  Aydınlık tema'}else{'☾  Karanlık tema'}
    $walk={param($parent)foreach($c in $parent.Controls){
        if($c -eq $header){continue}
        if($c -is [Windows.Forms.TextBox]){$c.BackColor=$field;$c.ForeColor=$fg;$c.BorderStyle='FixedSingle'}
        elseif($c -is [Windows.Forms.LinkLabel]){$c.LinkColor=if($Dark){& $rgb 255 107 118}else{$red};$c.ActiveLinkColor=$c.LinkColor}
        elseif($c -is [Windows.Forms.Label] -or $c -is [Windows.Forms.RadioButton] -or $c -is [Windows.Forms.GroupBox]){$c.ForeColor=$fg;$c.BackColor=$bg}
        elseif($c -is [Windows.Forms.Panel]){$c.BackColor=$bg}
        if($c.Controls.Count){& $walk $c}}}
    & $walk $form
    $hint.ForeColor=$muted;$footer.ForeColor=$muted;$line.BackColor=$edge
    if($script:failed){$status.ForeColor=if($Dark){& $rgb 255 140 140}else{[Drawing.Color]::DarkRed}}
    $cancel.FlatStyle='Flat';$cancel.BackColor=if($Dark){& $rgb 40 44 53}else{& $rgb 245 245 247};$cancel.ForeColor=$fg;$cancel.FlatAppearance.BorderColor=$edge
    $next.FlatAppearance.BorderColor=$red
    # Windows 10 1809+ / 11 dark title bar; older builds ignore the attribute.
    if($form.IsHandleCreated){$value=[int]$Dark;foreach($attribute in @(20,19)){if([WfiSetup.Dwm]::DwmSetWindowAttribute($form.Handle,$attribute,[ref]$value,4) -eq 0){break}}}
}
$themeButton.Add_Click({Set-WfiTheme (-not $script:dark)})
$form.Add_HandleCreated({Set-WfiTheme $script:dark})
Set-WfiTheme (Test-WfiSystemDarkTheme)

$script:phase=0;$script:process=$null;$script:result=2
$timer=New-Object Windows.Forms.Timer;$timer.Interval=1000
$stateFile=Join-Path (Split-Path $PSScriptRoot -Parent) 'setup-status.txt'
function Show-Status([string]$Text) {
    $status.Text=$Text
    if($Text -match '^(\d)/7'){$progress.Value=[Math]::Min(7,[Math]::Max(0,[int]$Matches[1]-1))}
}
$timer.Add_Tick({
    if(Test-Path $stateFile){try{Show-Status ([IO.File]::ReadAllText($stateFile))}catch{}}
    if($script:process -and $script:process.HasExited){
        $timer.Stop();$script:result=$script:process.ExitCode;$script:process.Dispose();$script:process=$null
        $next.Enabled=$true;$next.Text='Bitir';$script:phase=3;$cancel.Visible=$false
        if($script:result -eq 0){
            $progress.Value=7;$step.Text='Adım 3 / 3 · Kurulum tamamlandı'
            $url='https://'+$hostname.Text.Trim()+'/';$open.Tag=$url;$open.Text='Konsolu aç: '+$url;$open.Visible=$true
        } else {
            $step.Text='Adım 3 / 3 · Kurulum tamamlanamadı';$script:failed=$true;Set-WfiTheme $script:dark
            $status.Text+="`n`nAyarlar korunur. Sorunu giderdikten sonra kurulumu aynı ayarlarla yeniden çalıştırın; tamamlanan adımlar tekrarlanmaz."
        }
    }
})
$next.Add_Click({
    try {
        if($script:phase -eq 0){$intro.Visible=$false;$panel.Visible=$true;$next.Text='Kur';$step.Text='Adım 2 / 3 · Ayarlar';$script:phase=1;$app.Focus();return}
        if($script:phase -eq 3){$form.Close();return}
        if($password.Text -cne $confirm.Text){throw 'Parolalar aynı olmalı.'}
        $aiMode=if($aiExisting.Checked){'Existing'}elseif($aiNone.Checked){'None'}else{'Install'}
        $options=@{InstallPath=$app.Text.Trim();DataPath=$data.Text.Trim();Hostname=$hostname.Text.Trim();Domain=$domain.Text.Trim();CertificateThumbprint=$cert.Text.Trim().Replace(' ','');AiMode=$aiMode;PasswordHash=(New-WfiPasswordHash $password.Text)}
        Assert-WfiSetupOptions $options
        $optionsPath=Join-Path (Split-Path $PSScriptRoot -Parent) 'setup-options.json'
        $options | ConvertTo-Json | Set-Content -LiteralPath $optionsPath -Encoding UTF8
        $password.Clear();$confirm.Clear();$panel.Visible=$false;$next.Enabled=$false;$cancel.Enabled=$false;$script:phase=2
        $progress.Value=0;$progress.Visible=$true;$status.Visible=$true;$step.Text='Adım 3 / 3 · Kuruluyor'
        $start=New-Object Diagnostics.ProcessStartInfo
        $start.FileName=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true
        $start.Arguments='-NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $PSScriptRoot 'Install-ServerAutomatic.ps1')+'" -OptionsPath "'+$optionsPath+'"'
        if(Test-Path $stateFile){Remove-Item -LiteralPath $stateFile -Force}
        $script:process=[Diagnostics.Process]::Start($start);$timer.Start();$status.Text='Kurulum başlatıldı. Bu işlem bileşen ve model indirmesine göre uzun sürebilir; pencereyi kapatmayın.'
    }catch{[void][Windows.Forms.MessageBox]::Show($_.Exception.Message,'WFI Kurulum');if($script:phase -eq 2){$script:phase=1;$panel.Visible=$true;$progress.Visible=$false;$status.Visible=$false;$next.Enabled=$true;$cancel.Enabled=$true}}
})
$form.Add_FormClosing({param($sender,$event)if($script:process -and !$script:process.HasExited){$event.Cancel=$true;[void][Windows.Forms.MessageBox]::Show('Kurulum çalışıyor. Veri bütünlüğü için bitmesini bekleyin.','WFI')}})
try{[void]$form.ShowDialog()}finally{$timer.Dispose();$form.Dispose()}
exit $script:result
