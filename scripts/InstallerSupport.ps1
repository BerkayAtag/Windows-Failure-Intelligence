# Shared install/remove guards; dot-sourcing has no side effects.
function Assert-WfiSafePath([string]$Path) {
    if (-not [IO.Path]::IsPathRooted($Path) -or $Path.StartsWith('\\')) { throw 'A local absolute path is required.' }
    $full=[IO.Path]::GetFullPath($Path)
    if ($full -eq [IO.Path]::GetPathRoot($full)) { throw 'A drive root is not an installation directory.' }
    for($parent=$full;$parent;$parent=Split-Path $parent -Parent) {
        if((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse paths or ancestors are not supported.' }
    }
    if(Test-Path -LiteralPath $full) {
        if(Get-ChildItem -LiteralPath $full -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Reparse descendants are not supported.' }
    }
    return $full
}
function Get-WfiUninstallProperties([string]$Product,[string]$InstallPath) {
    if($Product -notin @('Agent','Server')){throw 'Unknown WFI product.'}
    $full=Assert-WfiSafePath $InstallPath
    return @{
        DisplayName="WFI $Product"; Publisher=("Berkay Ata"+[char]0x011F); DisplayVersion='0.7.1'
        # Programs and Features reads an unquoted icon path; quoting plus an index can hide the icon.
        DisplayIcon=(Join-Path $full 'wfi.ico'); InstallLocation=$full
        URLInfoAbout='https://berkayatag.github.io/Windows-Failure-Intelligence/'
        HelpLink='https://github.com/BerkayAtag/Windows-Failure-Intelligence'
        Comments='Windows Failure Intelligence (WFI)'
        UninstallString=('"'+(Join-Path $full 'WFI-Uninstall.exe')+'"')
        NoModify=1; NoRepair=1
    }
}
function Register-WfiInstallation([string]$Product,[string]$InstallPath,[string]$SiteName='') {
    $properties=Get-WfiUninstallProperties $Product $InstallPath
    $manifest=@{Product=$Product;InstallPath=$properties.InstallLocation;SiteName=$SiteName}
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $InstallPath 'wfi-installation.json') -Encoding UTF8
    $key="HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WFI.$Product"
    $properties.InstallDate=(Get-Date).ToString('yyyyMMdd')
    # Programs and Features shows EstimatedSize in KB; it is informational only.
    $bytes=(Get-ChildItem -LiteralPath $InstallPath -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    $properties.EstimatedSize=[int][Math]::Min([int]::MaxValue,[Math]::Ceiling([double]$bytes/1KB))
    New-Item -Path $key -Force | Out-Null
    foreach($name in $properties.Keys) {
        $kind=if($properties[$name] -is [int]){'DWord'}else{'String'}
        New-ItemProperty -LiteralPath $key -Name $name -Value $properties[$name] -PropertyType $kind -Force | Out-Null
    }
}
function Assert-WfiDistinctDirectories([string[]]$Paths) {
    $normalized=@($Paths | ForEach-Object { (Assert-WfiSafePath $_).TrimEnd('\') })
    for($i=0;$i -lt $normalized.Count;$i++) {
        for($j=$i+1;$j -lt $normalized.Count;$j++) {
            if($normalized[$i] -eq $normalized[$j] -or $normalized[$i].StartsWith($normalized[$j]+'\',[StringComparison]::OrdinalIgnoreCase) -or $normalized[$j].StartsWith($normalized[$i]+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Uygulama, kanıt ve anahtar dizinleri aynı veya iç içe olamaz.'}
        }
    }
}
function Test-WfiRetainedAgentInstallation([string]$Path) {
    $marker=Join-Path $Path 'wfi-installation.json'
    if(!(Test-Path -LiteralPath $marker)){return $false}
    try {
        $identity=Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
        if($identity.Product -ne 'Agent' -or $identity.InstallPath -ne [IO.Path]::GetFullPath($Path)){return $false}
        $allowed=@('WFI-Uninstall.exe','Uninstall-Wfi.ps1','InstallerSupport.ps1','wfi.ico','wfi-installation.json','appsettings.local.json')
        return -not [bool](Get-ChildItem -LiteralPath $Path -Force | Where-Object { $_.PSIsContainer -or $_.Name -notin $allowed })
    }catch{return $false}
}
