#Requires -RunAsAdministrator
#Requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$ConfigurationPath,
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$Hostname,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$CertificateThumbprint,
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$SiteName='WFI',
    [string]$InstallPath='C:\Program Files\WFI Server'
)
$ErrorActionPreference='Stop'
Import-Module WebAdministration -ErrorAction Stop
$source=(Resolve-Path -LiteralPath $PackagePath).Path
$configFile=(Resolve-Path -LiteralPath $ConfigurationPath).Path
$settings=Get-Content -LiteralPath $configFile -Raw | ConvertFrom-Json
if($settings.Demo.Enabled -ne $false -or -not $settings.ConnectionStrings.Wfi -or -not $settings.Ldap.Server) { throw 'Production DB/LDAP configuration and Demo.Enabled=false are required.' }
if(($settings | ConvertTo-Json -Depth 20) -match 'SET_LOCALLY|REPLACE|POSTGRES-FQDN') { throw 'Replace configuration placeholders locally before installation.' }
if($Hostname -notin ([string]$settings.AllowedHosts).Split(';')) { throw 'Hostname must be explicitly present in AllowedHosts.' }
$destination=[IO.Path]::GetFullPath($InstallPath)
if(Test-Path -LiteralPath $destination) { throw 'Fresh installation requires a new empty destination path. Use the documented maintenance procedure for upgrades.' }
if(Test-Path "IIS:\Sites\$SiteName") { throw 'IIS site already exists.' }
if(Test-Path "IIS:\AppPools\$SiteName") { throw 'IIS application pool already exists.' }
if(-not (Test-Path -LiteralPath (Join-Path $source 'api\WFI.Api.dll'))) { throw 'Published API package missing.' }
if(-not (Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2')) { throw 'Install .NET 10 Hosting Bundle after IIS, then restart IIS before setup.' }
$certificate=Get-Item "Cert:\LocalMachine\My\$CertificateThumbprint"
if(-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date)) { throw 'An unexpired LocalMachine certificate with private key is required.' }
foreach($path in @($destination,[string]$settings.Storage.Path,[string]$settings.DataProtection.Path)) {
    if(-not [IO.Path]::IsPathRooted($path)) { throw 'Use absolute installation, storage and key paths.' }
    for($parent=$path;$parent;$parent=Split-Path $parent -Parent) {
        if((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse installation/data paths are not supported.' }
    }
}
foreach($path in @([string]$settings.Storage.Path,[string]$settings.DataProtection.Path)) {
    if(Test-Path -LiteralPath $path) { throw 'This fresh-install setup requires new dedicated data/key directories; existing or HA shared storage needs the documented manual installation.' }
    $full=[IO.Path]::GetFullPath($path)
    if($full -eq $destination -or $full.StartsWith($destination.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Data and keys must be outside the application directory.' }
}
New-Item -ItemType Directory -Path $destination | Out-Null
$acl=Get-Acl -LiteralPath $destination
$acl.SetAccessRuleProtection($true,$false)
foreach($rule in @($acl.Access)){[void]$acl.RemoveAccessRuleSpecific($rule)}
foreach($sid in @('S-1-5-18','S-1-5-32-544')) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
}
Set-Acl -LiteralPath $destination -AclObject $acl
Copy-Item -Path (Join-Path $source 'api\*') -Destination $destination -Recurse -Force
Copy-Item -LiteralPath $configFile -Destination (Join-Path $destination 'appsettings.local.json')
New-WebAppPool -Name $SiteName | Out-Null
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name startMode -Value AlwaysRunning
$identity="IIS AppPool\$SiteName"
$acl=Get-Acl -LiteralPath $destination
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity,'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow'))
Set-Acl -LiteralPath $destination -AclObject $acl
foreach($path in @([string]$settings.Storage.Path,[string]$settings.DataProtection.Path)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    $dataAcl=Get-Acl -LiteralPath $path
    $dataAcl.SetAccessRuleProtection($true,$false)
    foreach($rule in @($dataAcl.Access)){[void]$dataAcl.RemoveAccessRuleSpecific($rule)}
    foreach($sid in @('S-1-5-18','S-1-5-32-544')) { $dataAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow')) }
    $dataAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity,'Modify','ContainerInherit,ObjectInherit','None','Allow'))
    Set-Acl -LiteralPath $path -AclObject $dataAcl
}
New-Website -Name $SiteName -PhysicalPath $destination -ApplicationPool $SiteName -Port 443 -HostHeader $Hostname -Ssl -SslFlags 1 | Out-Null
$binding=Get-WebBinding -Name $SiteName -Protocol https
$binding.AddSslCertificate($CertificateThumbprint,'My')
Write-Host "Installed: https://$Hostname . Apply deploy/schema.sql using your DBA account before accepting traffic. Check /api/v1/ready. No production migration was run."
