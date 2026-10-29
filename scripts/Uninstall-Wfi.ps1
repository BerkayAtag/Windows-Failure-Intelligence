#Requires -RunAsAdministrator
#Requires -Version 5.1
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'InstallerSupport.ps1')
$root=Assert-WfiSafePath $PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $root 'wfi-installation.json') -Raw | ConvertFrom-Json
if($manifest.Product -notin @('Agent','Server') -or $manifest.InstallPath -ne $root){throw 'Installation identity mismatch.'}
$key="HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WFI.$($manifest.Product)"
$registered=Get-ItemProperty -LiteralPath $key
if($registered.InstallLocation -ne $root){throw 'Registered installation path mismatch.'}
if($manifest.Product -eq 'Agent') {
    $service=Get-CimInstance Win32_Service -Filter "Name='WFI Agent'"
    if($service) {
        $expected='"'+(Join-Path $root 'WFI.Agent.exe')+'"'
        if($service.PathName -ne $expected){throw 'Agent service path mismatch.'}
        if($service.State -ne 'Stopped'){Stop-Service -Name 'WFI Agent' -ErrorAction Stop}
        $result=Invoke-CimMethod -InputObject $service -MethodName Delete
        if($result.ReturnValue -ne 0){throw 'Service removal failed.'}
    }
} else {
    if($manifest.AutomaticDataPath) {
        $automatic=Assert-WfiSafePath $manifest.AutomaticDataPath
        $state=Get-Content -LiteralPath (Join-Path $automatic 'setup-state.json') -Raw | ConvertFrom-Json
        if($state.Kind -ne 'WFI-Automatic-Server-v1' -or $state.InstallPath -ne $root){throw 'Automatic setup ownership mismatch.'}
        $expectedServices=@{
            'WFI.PostgreSQL'=('"'+(Join-Path $automatic 'runtime\pgsql\bin\pg_ctl.exe')+'" runservice -N "WFI.PostgreSQL" -D "'+(Join-Path $automatic 'postgres-data')+'" -w')
            'WFI.Ollama'=('"'+(Join-Path $automatic 'runtime\ollama\WFI.Ollama.Host.exe')+'"')
        }
        foreach($name in $expectedServices.Keys){
            $service=Get-CimInstance Win32_Service -Filter ("Name='"+$name+"'")
            if($service -and $service.PathName -ne $expectedServices[$name]){throw 'Dependency service ownership mismatch.'}
        }
    }
    Import-Module WebAdministration -ErrorAction Stop
    if($manifest.SiteName -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid site identity.'}
    $site=Get-Website -Name $manifest.SiteName
    if($site) {
        if($site.physicalPath -ne $root -or $site.applicationPool -ne $manifest.SiteName){throw 'IIS ownership mismatch.'}
        $other=@(Get-Website | Where-Object { $_.Name -ne $manifest.SiteName -and $_.applicationPool -eq $manifest.SiteName })
        $otherApps=@(Get-WebApplication | Where-Object { $_.applicationPool -eq $manifest.SiteName -and $_.ItemXPath -notlike ("*[@name='"+$manifest.SiteName+"']/*") })
        if($other.Count -or $otherApps.Count){throw 'Shared IIS pool requires manual removal.'}
        Stop-Website -Name $manifest.SiteName
        Remove-Website -Name $manifest.SiteName
    }
    if(Test-Path "IIS:\AppPools\$($manifest.SiteName)"){Remove-WebAppPool -Name $manifest.SiteName}
    if($manifest.AutomaticDataPath) {
        foreach($name in $expectedServices.Keys){
            $service=Get-CimInstance Win32_Service -Filter ("Name='"+$name+"'")
            if($service){Stop-Service $name -ErrorAction Stop;$result=Invoke-CimMethod -InputObject $service -MethodName Delete;if($result.ReturnValue -ne 0){throw 'Dependency service removal failed.'}}
        }
        if($manifest.FirewallCreated){Remove-NetFirewallRule -Name 'WFI-HTTPS' -ErrorAction SilentlyContinue}
    }
}
# Keep configuration, device identity, evidence, database and keys for explicit operator retention.
# Running uninstaller and its small recovery helpers stay in the protected application directory.
$keep=@('WFI-Uninstall.exe','Uninstall-Wfi.ps1','InstallerSupport.ps1','wfi.ico','wfi-installation.json','appsettings.local.json')
foreach($item in Get-ChildItem -LiteralPath $root -Force){
    if($item.Name -in $keep){continue}
    $resolved=[IO.Path]::GetFullPath($item.FullName)
    if(!$resolved.StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe removal target.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction Stop
}
Remove-Item -LiteralPath $key -ErrorAction Stop
Write-Host 'WFI removed. Configuration, identity, database/evidence and protected uninstall helpers retained.'
