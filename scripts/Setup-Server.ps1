#Requires -RunAsAdministrator
#Requires -Version 5.1
$ErrorActionPreference='Stop'
Write-Host 'WFI Server Setup - IIS + .NET 10 Hosting Bundle and prepared PostgreSQL required.'
$configuration=Read-Host 'Protected production JSON configuration path'
$hostname=Read-Host 'HTTPS hostname (for example wfi.example.invalid)'
$certificate=Read-Host 'LocalMachine/My TLS certificate thumbprint'
$destination=Read-Host 'New installation path (default C:\Program Files\WFI Server)'
if(-not $destination){$destination='C:\Program Files\WFI Server'}
& (Join-Path $PSScriptRoot 'Install-Server.ps1') -PackagePath (Split-Path $PSScriptRoot -Parent) -ConfigurationPath $configuration -Hostname $hostname -CertificateThumbprint $certificate -InstallPath $destination
[void](Read-Host 'Press Enter to close')

