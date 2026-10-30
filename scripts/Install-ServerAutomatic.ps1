#Requires -RunAsAdministrator
#Requires -Version 5.1
param([Parameter(Mandatory)][string]$OptionsPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ServerSetupSupport.ps1')
$options=Get-Content -LiteralPath $OptionsPath -Raw | ConvertFrom-Json
Assert-WfiSetupOptions $options
$aiMode=Get-WfiAiMode $options
$statusPath=Join-Path (Split-Path $OptionsPath -Parent) 'setup-status.txt'
function Status([string]$Message){[IO.File]::WriteAllText($statusPath,$Message,[Text.Encoding]::UTF8)}
$mutex=New-Object Threading.Mutex($false,'Global\WFI.Server.Setup')
$locked=$false
$pgPort=55432
try {
    $locked=$mutex.WaitOne(0)
    if(!$locked){throw 'Başka bir WFI kurulumu çalışıyor.'}
    $data=[IO.Path]::GetFullPath($options.DataPath)
    $app=[IO.Path]::GetFullPath($options.InstallPath)
    $journal=Join-Path $data 'setup-state.json'
    $package=Split-Path $PSScriptRoot -Parent
    if(Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WFI.Server'){throw 'WFI Server zaten kurulu. Yükseltme için sunucu kurulum rehberindeki bakım adımlarını izleyin.'}
    if(Test-Path -LiteralPath $data) {
        Assert-WfiSetupOwnership $data
        if(!(Test-Path -LiteralPath $journal)){throw 'Veri dizini zaten var ve WFI kurulumuna ait değil. Yeni bir dizin seçin.'}
        $state=Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json
        if($state.Kind -ne 'WFI-Automatic-Server-v1' -or $state.InstallPath -ne $app -or $state.Hostname -ne $options.Hostname -or $state.Domain -ne $options.Domain -or (Get-WfiAiMode $state) -ne $aiMode){throw 'Devam ettirilen kurulumun ayarları ilk kurulumla aynı olmalı (dizin, DNS adı, domain ve AI seçimi).'}
        if($state.PostgresPort){$pgPort=[int]$state.PostgresPort;if($pgPort -lt 1024 -or $pgPort -gt 65535){throw 'Kayıtlı veritabanı portu geçersiz.'}}
    } else {
        if(Test-Path -LiteralPath $app){throw 'Uygulama dizini zaten var. Yeni ve boş bir dizin seçin.'}
        $drive=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($data))
        $required=if($aiMode -eq 'Install'){15GB}else{5GB}
        if($drive.AvailableFreeSpace -lt $required){throw ('Seçilen veri sürücüsünde en az '+($required/1GB)+' GB boş alan gerekir.')}
        foreach($name in @('WFI.PostgreSQL','WFI.Ollama')){if(Get-Service $name -ErrorAction SilentlyContinue){throw "Bu adla bir servis zaten var: $name"}}
        $pgPort=Get-WfiAvailablePostgresPort
        foreach($port in @(if($aiMode -eq 'Install'){11435})){
            $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
            try{$listener.Server.ExclusiveAddressUse=$true;$listener.Start()}catch{throw "Yerel port $port dolu veya Windows tarafından ayrılmış."}finally{$listener.Stop()}
        }
        Set-WfiProtectedDirectory $data
        $state=[pscustomobject]@{Kind='WFI-Automatic-Server-v1';InstallPath=$app;Hostname=$options.Hostname;Domain=$options.Domain;PostgresPort=$pgPort;AiMode=$aiMode;OwnerPassword=(New-WfiRandomSecret);AppPassword=(New-WfiRandomSecret);DatabaseReady=$false;SchemaReady=$false}
        $state | ConvertTo-Json | Set-Content -LiteralPath $journal -Encoding UTF8
    }
    Status '1/7 · Ön koşul paketleri indiriliyor ve doğrulanıyor. Bu işlem birkaç dakika sürebilir.'
    $cache=Join-Path $data 'downloads';Set-WfiProtectedDirectory $cache
    $specs=Get-Content (Join-Path $PSScriptRoot 'server-dependencies.json') -Raw | ConvertFrom-Json
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    foreach($property in $specs.PSObject.Properties) {
        if($property.Name -eq 'ollama' -and $aiMode -ne 'Install'){continue}
        $spec=$property.Value;$file=Join-Path $cache $spec.file
        if(!(Test-Path -LiteralPath $file)){
            $side=if($env:WFI_SETUP_SOURCE){Join-Path $env:WFI_SETUP_SOURCE ('WFI-Dependencies\'+$spec.file)}else{''}
            if($side -and (Test-Path -LiteralPath $side)){Copy-Item -LiteralPath $side -Destination $file}
            else{Invoke-WebRequest -UseBasicParsing -Uri $spec.url -OutFile ($file+'.partial');Move-Item -LiteralPath ($file+'.partial') -Destination $file -Force}
        }
        Assert-WfiDependency $file $spec
    }
    Status '2/7 · IIS etkinleştiriliyor; Microsoft çalışma zamanı ön koşulları kuruluyor.'
    if(Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
        $result=Install-WindowsFeature Web-Server,Web-Mgmt-Console
        if(!$result.Success){throw 'IIS kurulumu başarısız oldu.'}
        if($result.RestartNeeded -eq 'Yes'){throw 'YENİDEN BAŞLATMA GEREKLİ: Windows''u yeniden başlatıp kurulumu aynı ayarlarla tekrar çalıştırın.'}
    } else {
        $result=Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole,IIS-WebServer,IIS-ManagementConsole,IIS-StaticContent,IIS-DefaultDocument -All -NoRestart
        if($result.RestartNeeded){throw 'YENİDEN BAŞLATMA GEREKLİ: Windows''u yeniden başlatıp kurulumu aynı ayarlarla tekrar çalıştırın.'}
    }
    foreach($name in @('vc','hosting')) {
        $file=Join-Path $cache $specs.$name.file
        $signature=Get-AuthenticodeSignature -FilePath $file
        if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation,'){throw 'Microsoft ön koşul paketinin imza doğrulaması başarısız.'}
        Invoke-WfiExecutable $file @('/install','/quiet','/norestart') 1200
    }
    Import-Module WebAdministration
    if(!(Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2')){throw 'ASP.NET Core IIS modülü yüklenemedi. Windows''u yeniden başlatıp kurulumu tekrar çalıştırın.'}
    Status '3/7 · Özel PostgreSQL servisi ve veritabanı hazırlanıyor.'
    $runtime=Join-Path $data 'runtime'
    if(!(Test-Path $runtime)){Set-WfiProtectedDirectory $runtime}
    if(!(Get-Service 'WFI.PostgreSQL' -ErrorAction SilentlyContinue)){Expand-WfiDependency (Join-Path $cache $specs.postgres.file) $runtime 'pgsql'}
    $pg=Join-Path $runtime 'pgsql\bin';$cluster=Join-Path $data 'postgres-data'
    $pwfile=Join-Path $data 'init-password.tmp'
    if(!(Test-Path (Join-Path $cluster 'PG_VERSION'))) {
        if(Test-Path $cluster){throw 'Yarım kalmış veritabanı hazırlığı bulundu; inceleme için korunuyor ve otomatik üzerine yazılmıyor.'}
        [IO.File]::WriteAllText($pwfile,$state.OwnerPassword,[Text.Encoding]::ASCII)
        try{Invoke-WfiExecutable (Join-Path $pg 'initdb.exe') @('-D',$cluster,'-U','wfi_owner','--auth-host=scram-sha-256','--auth-local=scram-sha-256',('--pwfile='+$pwfile),'--encoding=UTF8','--locale=C')}
        finally{if(Test-Path $pwfile){Remove-Item -LiteralPath $pwfile -Force}}
        Add-Content (Join-Path $cluster 'postgresql.conf') "`nlisten_addresses = '127.0.0.1'`nport = $pgPort`npassword_encryption = 'scram-sha-256'"
    }
    function EnsureService([string]$Name,[string]$Binary) {
        $existing=Get-CimInstance Win32_Service -Filter ("Name='"+$Name+"'")
        if($existing){if($existing.PathName -ne $Binary){throw 'Aynı adlı servis başka bir kuruluma ait.'}}
        else {
            $credential=[Management.Automation.PSCredential]::new(('NT SERVICE\'+$Name),[Security.SecureString]::new())
            New-Service -Name $Name -BinaryPathName $Binary -StartupType Automatic -Credential $credential | Out-Null
        }
        Invoke-WfiExecutable (Join-Path $env:WINDIR 'System32\sc.exe') @('failure',$Name,'reset=','86400','actions=','restart/10000/restart/30000/restart/60000')
    }
    $pgBinary='"'+(Join-Path $pg 'pg_ctl.exe')+'" runservice -N "WFI.PostgreSQL" -D "'+$cluster+'" -w'
    EnsureService 'WFI.PostgreSQL' $pgBinary
    Set-WfiProtectedDirectory $cluster 'NT SERVICE\WFI.PostgreSQL'
    # Runtime binaries are readable/executable, never writable, by their service identities.
    function GrantRead([string]$Path,[string]$Identity){$acl=Get-Acl $Path;$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($Identity,'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow'));Set-Acl $Path $acl}
    GrantRead $runtime 'NT SERVICE\WFI.PostgreSQL'
    Start-Service 'WFI.PostgreSQL'
    function Sql([string]$Database,[string]$Text) {
        $start=New-Object Diagnostics.ProcessStartInfo
        $start.FileName=Join-Path $pg 'psql.exe';$start.Arguments='-X -w -h 127.0.0.1 -p '+$pgPort+' -U wfi_owner -d '+$Database+' --set=ON_ERROR_STOP=1'
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        $start.EnvironmentVariables['PGPASSWORD']=$state.OwnerPassword
        $start.EnvironmentVariables['PGCLIENTENCODING']='UTF8'
        $start.EnvironmentVariables['PGOPTIONS']='-c statement_timeout=120000 -c lock_timeout=10000'
        $p=[Diagnostics.Process]::Start($start)
        try{
            $out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync()
            # Framework PowerShell uses the console code page for StandardInput's writer.
            # Write explicit UTF8 bytes so PostgreSQL receives the declared encoding.
            $bytes=[Text.Encoding]::UTF8.GetBytes($Text)
            $p.StandardInput.BaseStream.Write($bytes,0,$bytes.Length);$p.StandardInput.BaseStream.Close()
            if(!$p.WaitForExit(130000)){$p.Kill();$p.WaitForExit();throw 'Veritabanı komutu zaman aşımına uğradı.'}
            if($p.ExitCode -ne 0){throw 'Veritabanı hazırlığı başarısız; parola ve SQL çıktısı gizlendi.'}
        }
        finally{$p.Dispose()}
    }
    if(!$state.DatabaseReady){
        # Idempotent resume; this cluster is exclusively owned by this installer.
        Sql 'postgres' ("SELECT 'CREATE ROLE wfi_app LOGIN' WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname='wfi_app')\gexec`nALTER ROLE wfi_app WITH NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '"+$state.AppPassword+"';`nSELECT 'CREATE DATABASE wfi OWNER wfi_owner' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname='wfi')\gexec`n")
        $state.DatabaseReady=$true;$state | ConvertTo-Json | Set-Content $journal -Encoding UTF8
    }
    if(!$state.SchemaReady){Sql 'wfi' (Get-Content (Join-Path $package 'deploy\schema.sql') -Raw -Encoding UTF8);$state.SchemaReady=$true;$state | ConvertTo-Json | Set-Content $journal -Encoding UTF8}
    Sql 'wfi' "REVOKE ALL ON DATABASE wfi FROM PUBLIC; GRANT CONNECT ON DATABASE wfi TO wfi_app; REVOKE CREATE ON SCHEMA public FROM PUBLIC; GRANT USAGE ON SCHEMA public TO wfi_app; GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA public TO wfi_app; GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA public TO wfi_app;"
    $aiBase='http://127.0.0.1:11434';$aiWarning=''
    if($aiMode -eq 'Install') {
        Status '4/7 · Yerel AI servisi hazırlanıyor ve phi4-mini modeli indiriliyor. Model indirmesi uzun sürebilir.'
        $aiBase='http://127.0.0.1:11435'
        $ollama=Join-Path $runtime 'ollama'
        if(!(Get-Service 'WFI.Ollama' -ErrorAction SilentlyContinue)) {
            Expand-WfiDependency (Join-Path $cache $specs.ollama.file) $ollama
            Copy-Item -Path (Join-Path $package 'ollama-host\*') -Destination $ollama -Recurse -Force
        }
        $models=Join-Path $data 'models'
        [IO.File]::WriteAllText((Join-Path $ollama 'models-path.txt'),$models,[Text.UTF8Encoding]::new($false))
        EnsureService 'WFI.Ollama' ('"'+(Join-Path $ollama 'WFI.Ollama.Host.exe')+'"')
        GrantRead $runtime 'NT SERVICE\WFI.Ollama';Set-WfiProtectedDirectory $models 'NT SERVICE\WFI.Ollama'
        Set-WfiProtectedDirectory (Join-Path $data 'ollama-home') 'NT SERVICE\WFI.Ollama'
        Start-Service 'WFI.Ollama'
        $ready=$false
        for($i=0;$i -lt 60;$i++){if(Test-WfiExistingOllama $aiBase){$ready=$true;break};Start-Sleep 1}
        if(!$ready){throw 'Yerel AI servisi hazır duruma gelmedi.'}
        $pull=Invoke-RestMethod ($aiBase+'/api/pull') -Method Post -ContentType 'application/json' -Body '{"model":"phi4-mini:latest","stream":false}' -TimeoutSec 14400
        if($pull.status -ne 'success'){throw 'Model indirmesi tamamlanmadı. Kaldığı yerden devam etmek için kurulumu yeniden çalıştırın.'}
    } elseif($aiMode -eq 'Existing') {
        Status '4/7 · Bu makinedeki mevcut Ollama kullanılıyor; phi4-mini modeli denetleniyor.'
        if(!(Test-WfiExistingOllama $aiBase)){throw 'Mevcut Ollama 127.0.0.1:11434 adresinde yanıt vermiyor. Ollama''yı başlatın veya kurulumda "Ollama ve phi4-mini modelini WFI için kur" seçeneğini kullanın.'}
        try{
            $pull=Invoke-RestMethod ($aiBase+'/api/pull') -Method Post -ContentType 'application/json' -Body '{"model":"phi4-mini:latest","stream":false}' -TimeoutSec 14400
            if($pull.status -ne 'success'){throw 'pull'}
        }catch{$aiWarning=' Uyarı: phi4-mini indirilemedi; konsolda Cihazlar ve AI ekranından kurulu bir model seçin.'}
    } else {
        Status '4/7 · Yerel AI kurulumu atlandı. Ollama sonradan 127.0.0.1:11434 adresinde hazırlanabilir.'
        $aiWarning=' AI teşhisi için Ollama sonradan kurulmalıdır.'
    }
    Status '5/7 · HTTPS sertifikası ve korumalı uygulama yapılandırması hazırlanıyor.'
    $thumb=$options.CertificateThumbprint
    # Resume reuses the certificate this setup already created instead of adding another one.
    if(!$thumb -and $state.CertificateThumbprint -and (Test-Path -LiteralPath (Join-Path 'Cert:\LocalMachine\My' ([string]$state.CertificateThumbprint)))){$thumb=[string]$state.CertificateThumbprint}
    if(!$thumb) {
        $cert=New-SelfSignedCertificate -DnsName $options.Hostname -CertStoreLocation Cert:\LocalMachine\My -FriendlyName 'WFI local setup HTTPS' -NotAfter (Get-Date).AddYears(1) -KeyExportPolicy NonExportable
        $thumb=$cert.Thumbprint
        $state | Add-Member -NotePropertyName CertificateThumbprint -NotePropertyValue $thumb -Force;$state | ConvertTo-Json | Set-Content $journal -Encoding UTF8
        $store=[Security.Cryptography.X509Certificates.X509Store]::new('Root','LocalMachine');$store.Open('ReadWrite')
        try{$store.Add([Security.Cryptography.X509Certificates.X509Certificate2]::new($cert.Export('Cert')))}finally{$store.Close()}
    }
    $config=@{AllowedHosts=$options.Hostname;ConnectionStrings=@{Wfi=('Host=127.0.0.1;Port='+$pgPort+';Database=wfi;Username=wfi_app;Password='+$state.AppPassword+';SSL Mode=Disable')};Ldap=@{Enabled=$false;Domain=$options.Domain;Server='';BaseDn=(($options.Domain.Split('.') | ForEach-Object {'DC='+$_}) -join ',');GroupRoles=@{}};LocalAdmin=@{Enabled=$true;PasswordHash=$options.PasswordHash};Demo=@{Enabled=$false};Worker=@{Enabled=$true};Storage=@{Path=(Join-Path $data 'evidence')};DataProtection=@{Path=(Join-Path $data 'keys')};Ollama=@{BaseUrl=$aiBase;DefaultModel='phi4-mini:latest';MaxConcurrent=1;TimeoutSeconds=240};HighAvailability=@{Enabled=$false}}
    $configPath=Join-Path $data 'installation-config.json';$config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8
    Status '6/7 · WFI web uygulaması IIS üzerine kuruluyor.'
    & (Join-Path $PSScriptRoot 'Install-Server.ps1') -PackagePath $package -ConfigurationPath $configPath -Hostname $options.Hostname -CertificateThumbprint $thumb -InstallPath $app
    $installation=Get-Content (Join-Path $app 'wfi-installation.json') -Raw | ConvertFrom-Json
    $installation | Add-Member -NotePropertyName AutomaticDataPath -NotePropertyValue $data
    $installation | Add-Member -NotePropertyName FirewallCreated -NotePropertyValue $false
    $installation | ConvertTo-Json | Set-Content (Join-Path $app 'wfi-installation.json') -Encoding UTF8
    if(!(Get-NetFirewallRule -Name 'WFI-HTTPS' -ErrorAction SilentlyContinue)){
        New-NetFirewallRule -Name 'WFI-HTTPS' -DisplayName 'WFI HTTPS (Domain)' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 443 -Profile Domain | Out-Null
        $installation.FirewallCreated=$true;$installation | ConvertTo-Json | Set-Content (Join-Path $app 'wfi-installation.json') -Encoding UTF8
    }
    Status '7/7 · HTTPS ve veritabanı hazırlığı denetleniyor.'
    $healthy=$false
    for($i=0;$i -lt 30;$i++){try{$null=Invoke-WebRequest -UseBasicParsing ('https://'+$options.Hostname+'/api/v1/ready') -TimeoutSec 5;$healthy=$true;break}catch{Start-Sleep 2}}
    if(!$healthy){throw 'Dosyalar kuruldu ancak HTTPS hazırlık denetimi başarısız. DNS adını, sertifikayı ve IIS sitesini kontrol edin; kurulum doğrulanmış sayılmaz.'}
    Status ('BAŞARILI: https://'+$options.Hostname+' adresinde local:admin kullanıcı adı ve kurulum parolanızla giriş yapın.'+$aiWarning)
    exit 0
}catch{Status ('BAŞARISIZ: '+$_.Exception.Message);exit 1}
finally{
    if($locked -and $data -and (Test-Path $journal)){
        try{Copy-Item -LiteralPath $statusPath -Destination (Join-Path $data 'last-setup-status.txt') -Force}catch{}
    }
    if($locked){$mutex.ReleaseMutex()};$mutex.Dispose()
}
