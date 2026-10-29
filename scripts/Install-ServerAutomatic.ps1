#Requires -RunAsAdministrator
#Requires -Version 5.1
param([Parameter(Mandatory)][string]$OptionsPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ServerSetupSupport.ps1')
$options=Get-Content -LiteralPath $OptionsPath -Raw | ConvertFrom-Json
Assert-WfiSetupOptions $options
$statusPath=Join-Path (Split-Path $OptionsPath -Parent) 'setup-status.txt'
function Status([string]$Message){[IO.File]::WriteAllText($statusPath,$Message,[Text.Encoding]::UTF8)}
$mutex=New-Object Threading.Mutex($false,'Global\WFI.Server.Setup')
$locked=$false
try {
    $locked=$mutex.WaitOne(0)
    if(!$locked){throw 'Another WFI setup is running.'}
    $data=[IO.Path]::GetFullPath($options.DataPath)
    $app=[IO.Path]::GetFullPath($options.InstallPath)
    $journal=Join-Path $data 'setup-state.json'
    $package=Split-Path $PSScriptRoot -Parent
    if(Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WFI.Server'){throw 'WFI is already installed. Use the documented maintenance procedure.'}
    if(Test-Path -LiteralPath $data) {
        Assert-WfiSetupOwnership $data
        if(!(Test-Path -LiteralPath $journal)){throw 'Data directory already exists without WFI setup identity. Choose a new directory.'}
        $state=Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json
        if($state.Kind -ne 'WFI-Automatic-Server-v1' -or $state.InstallPath -ne $app -or $state.Hostname -ne $options.Hostname -or $state.Domain -ne $options.Domain){throw 'Resume settings do not match the original installation.'}
    } else {
        if(Test-Path -LiteralPath $app){throw 'Application directory already exists.'}
        $drive=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($data))
        if($drive.AvailableFreeSpace -lt 15GB){throw 'At least 15 GB free space is required on the selected data drive.'}
        foreach($name in @('WFI.PostgreSQL','WFI.Ollama')){if(Get-Service $name -ErrorAction SilentlyContinue){throw "Existing service: $name"}}
        foreach($port in @(55432,11435)){
            $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
            try{$listener.Server.ExclusiveAddressUse=$true;$listener.Start()}catch{throw "Local port $port is occupied or reserved by Windows."}finally{$listener.Stop()}
        }
        Set-WfiProtectedDirectory $data
        $state=[pscustomobject]@{Kind='WFI-Automatic-Server-v1';InstallPath=$app;Hostname=$options.Hostname;Domain=$options.Domain;OwnerPassword=(New-WfiRandomSecret);AppPassword=(New-WfiRandomSecret);DatabaseReady=$false;SchemaReady=$false}
        $state | ConvertTo-Json | Set-Content -LiteralPath $journal -Encoding UTF8
    }
    Status '1/7 - Prerequisite packages: downloading and verifying. This can take several minutes.'
    $cache=Join-Path $data 'downloads';Set-WfiProtectedDirectory $cache
    $specs=Get-Content (Join-Path $PSScriptRoot 'server-dependencies.json') -Raw | ConvertFrom-Json
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    foreach($property in $specs.PSObject.Properties) {
        $spec=$property.Value;$file=Join-Path $cache $spec.file
        if(!(Test-Path -LiteralPath $file)){
            $side=if($env:WFI_SETUP_SOURCE){Join-Path $env:WFI_SETUP_SOURCE ('WFI-Dependencies\'+$spec.file)}else{''}
            if($side -and (Test-Path -LiteralPath $side)){Copy-Item -LiteralPath $side -Destination $file}
            else{Invoke-WebRequest -UseBasicParsing -Uri $spec.url -OutFile ($file+'.partial');Move-Item -LiteralPath ($file+'.partial') -Destination $file -Force}
        }
        Assert-WfiDependency $file $spec
    }
    Status '2/7 - Enabling IIS and installing Microsoft runtime prerequisites.'
    if(Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
        $result=Install-WindowsFeature Web-Server,Web-Mgmt-Console
        if(!$result.Success){throw 'IIS installation failed.'}
        if($result.RestartNeeded -eq 'Yes'){throw 'REBOOT_REQUIRED: Restart Windows and rerun setup.'}
    } else {
        $result=Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole,IIS-WebServer,IIS-ManagementConsole,IIS-StaticContent,IIS-DefaultDocument -All -NoRestart
        if($result.RestartNeeded){throw 'REBOOT_REQUIRED: Restart Windows and rerun setup.'}
    }
    foreach($name in @('vc','hosting')) {
        $file=Join-Path $cache $specs.$name.file
        $signature=Get-AuthenticodeSignature -FilePath $file
        if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation,'){throw 'Microsoft prerequisite signature verification failed.'}
        Invoke-WfiExecutable $file @('/install','/quiet','/norestart') 1200
    }
    Import-Module WebAdministration
    if(!(Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2')){throw 'Hosting module unavailable. Restart Windows and rerun setup.'}
    Status '3/7 - Preparing dedicated PostgreSQL service and database.'
    $runtime=Join-Path $data 'runtime'
    if(!(Test-Path $runtime)){Set-WfiProtectedDirectory $runtime}
    if(!(Get-Service 'WFI.PostgreSQL' -ErrorAction SilentlyContinue)){Expand-WfiDependency (Join-Path $cache $specs.postgres.file) $runtime 'pgsql'}
    $pg=Join-Path $runtime 'pgsql\bin';$cluster=Join-Path $data 'postgres-data'
    $pwfile=Join-Path $data 'init-password.tmp'
    if(!(Test-Path (Join-Path $cluster 'PG_VERSION'))) {
        if(Test-Path $cluster){throw 'Incomplete database initialization: retain for investigation; do not overwrite automatically.'}
        [IO.File]::WriteAllText($pwfile,$state.OwnerPassword,[Text.Encoding]::ASCII)
        try{Invoke-WfiExecutable (Join-Path $pg 'initdb.exe') @('-D',$cluster,'-U','wfi_owner','--auth-host=scram-sha-256','--auth-local=scram-sha-256',('--pwfile='+$pwfile),'--encoding=UTF8','--locale=C')}
        finally{if(Test-Path $pwfile){Remove-Item -LiteralPath $pwfile -Force}}
        Add-Content (Join-Path $cluster 'postgresql.conf') "`nlisten_addresses = '127.0.0.1'`nport = 55432`npassword_encryption = 'scram-sha-256'"
    }
    function EnsureService([string]$Name,[string]$Binary) {
        $existing=Get-CimInstance Win32_Service -Filter ("Name='"+$Name+"'")
        if($existing){if($existing.PathName -ne $Binary){throw 'Service ownership mismatch.'}}
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
        $start.FileName=Join-Path $pg 'psql.exe';$start.Arguments='-X -w -h 127.0.0.1 -p 55432 -U wfi_owner -d '+$Database+' --set=ON_ERROR_STOP=1'
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        $start.EnvironmentVariables['PGPASSWORD']=$state.OwnerPassword
        $start.EnvironmentVariables['PGCLIENTENCODING']='UTF8'
        $start.EnvironmentVariables['PGOPTIONS']='-c statement_timeout=120000 -c lock_timeout=10000'
        $p=[Diagnostics.Process]::Start($start)
        try{$out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync();$p.StandardInput.Write($Text);$p.StandardInput.Close();if(!$p.WaitForExit(130000)){$p.Kill();$p.WaitForExit();throw 'Database command timed out.'};if($p.ExitCode -ne 0){throw 'Database provisioning failed; credentials and SQL were suppressed.'}}
        finally{$p.Dispose()}
    }
    if(!$state.DatabaseReady){
        # Idempotent resume; this cluster is exclusively owned by this installer.
        Sql 'postgres' ("SELECT 'CREATE ROLE wfi_app LOGIN' WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname='wfi_app')\gexec`nALTER ROLE wfi_app WITH NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '"+$state.AppPassword+"';`nSELECT 'CREATE DATABASE wfi OWNER wfi_owner' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname='wfi')\gexec`n")
        $state.DatabaseReady=$true;$state | ConvertTo-Json | Set-Content $journal -Encoding UTF8
    }
    if(!$state.SchemaReady){Sql 'wfi' (Get-Content (Join-Path $package 'deploy\schema.sql') -Raw -Encoding UTF8);$state.SchemaReady=$true;$state | ConvertTo-Json | Set-Content $journal -Encoding UTF8}
    Sql 'wfi' "REVOKE ALL ON DATABASE wfi FROM PUBLIC; GRANT CONNECT ON DATABASE wfi TO wfi_app; REVOKE CREATE ON SCHEMA public FROM PUBLIC; GRANT USAGE ON SCHEMA public TO wfi_app; GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA public TO wfi_app; GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA public TO wfi_app;"
    Status '4/7 - Preparing local AI service and downloading phi4-mini. Model download may take a long time.'
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
    for($i=0;$i -lt 60;$i++){try{$null=Invoke-RestMethod 'http://127.0.0.1:11435/api/tags' -TimeoutSec 2;$ready=$true;break}catch{Start-Sleep 1}}
    if(!$ready){throw 'Local AI service did not become ready.'}
    $pull=Invoke-RestMethod 'http://127.0.0.1:11435/api/pull' -Method Post -ContentType 'application/json' -Body '{"model":"phi4-mini:latest","stream":false}' -TimeoutSec 14400
    if($pull.status -ne 'success'){throw 'Model download did not complete. Rerun setup to resume.'}
    Status '5/7 - Preparing HTTPS and protected application configuration.'
    $thumb=$options.CertificateThumbprint
    if(!$thumb) {
        $cert=New-SelfSignedCertificate -DnsName $options.Hostname -CertStoreLocation Cert:\LocalMachine\My -FriendlyName 'WFI local setup HTTPS' -NotAfter (Get-Date).AddYears(1) -KeyExportPolicy NonExportable
        $thumb=$cert.Thumbprint
        $store=[Security.Cryptography.X509Certificates.X509Store]::new('Root','LocalMachine');$store.Open('ReadWrite')
        try{$store.Add([Security.Cryptography.X509Certificates.X509Certificate2]::new($cert.Export('Cert')))}finally{$store.Close()}
    }
    $config=@{AllowedHosts=$options.Hostname;ConnectionStrings=@{Wfi=('Host=127.0.0.1;Port=55432;Database=wfi;Username=wfi_app;Password='+$state.AppPassword+';SSL Mode=Disable')};Ldap=@{Enabled=$false;Domain=$options.Domain;Server='';BaseDn=(($options.Domain.Split('.') | ForEach-Object {'DC='+$_}) -join ',');GroupRoles=@{}};LocalAdmin=@{Enabled=$true;PasswordHash=$options.PasswordHash};Demo=@{Enabled=$false};Worker=@{Enabled=$true};Storage=@{Path=(Join-Path $data 'evidence')};DataProtection=@{Path=(Join-Path $data 'keys')};Ollama=@{BaseUrl='http://127.0.0.1:11435';DefaultModel='phi4-mini:latest';MaxConcurrent=1};HighAvailability=@{Enabled=$false}}
    $configPath=Join-Path $data 'installation-config.json';$config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8
    Status '6/7 - Installing WFI web application.'
    & (Join-Path $PSScriptRoot 'Install-Server.ps1') -PackagePath $package -ConfigurationPath $configPath -Hostname $options.Hostname -CertificateThumbprint $thumb -InstallPath $app
    $installation=Get-Content (Join-Path $app 'wfi-installation.json') -Raw | ConvertFrom-Json
    $installation | Add-Member -NotePropertyName AutomaticDataPath -NotePropertyValue $data
    $installation | Add-Member -NotePropertyName FirewallCreated -NotePropertyValue $false
    $installation | ConvertTo-Json | Set-Content (Join-Path $app 'wfi-installation.json') -Encoding UTF8
    if(!(Get-NetFirewallRule -Name 'WFI-HTTPS' -ErrorAction SilentlyContinue)){
        New-NetFirewallRule -Name 'WFI-HTTPS' -DisplayName 'WFI HTTPS (Domain)' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 443 -Profile Domain | Out-Null
        $installation.FirewallCreated=$true;$installation | ConvertTo-Json | Set-Content (Join-Path $app 'wfi-installation.json') -Encoding UTF8
    }
    Status '7/7 - Checking HTTPS and database readiness.'
    $healthy=$false
    for($i=0;$i -lt 30;$i++){try{$null=Invoke-WebRequest -UseBasicParsing ('https://'+$options.Hostname+'/api/v1/ready') -TimeoutSec 5;$healthy=$true;break}catch{Start-Sleep 2}}
    if(!$healthy){throw 'Installation files are present, but HTTPS readiness failed. Check hostname/DNS, certificate and IIS. Setup is not verified complete.'}
    Status ('SUCCESS: https://'+$options.Hostname+' - Sign in as local:admin using your installation password.')
    exit 0
}catch{Status ('FAILED: '+$_.Exception.Message);exit 1}
finally{
    if($locked -and $data -and (Test-Path $journal)){
        try{Copy-Item -LiteralPath $statusPath -Destination (Join-Path $data 'last-setup-status.txt') -Force}catch{}
    }
    if($locked){$mutex.ReleaseMutex()};$mutex.Dispose()
}
