#Requires -RunAsAdministrator
#Requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][uri]$ServerUrl,
    [Parameter(Mandatory)][guid]$DeviceId,
    [string]$InstallPath = 'C:\Program Files\WFI Agent',
    [ValidateRange(10,600)][int]$PollSeconds = 30,
    [ValidatePattern('^$|^[a-fA-F0-9]{64}$')][string]$BigFixReinstallSha256 = '',
    [Security.SecureString]$EnrollmentToken
)
$ErrorActionPreference = 'Stop'
if ($ServerUrl.Scheme -ne 'https' -or $ServerUrl.UserInfo -or $ServerUrl.AbsolutePath -ne '/' -or $ServerUrl.Query -or $ServerUrl.Fragment) { throw 'HTTPS WFI server origin without path/query/fragment is required.' }
if ($DeviceId -eq [guid]::Empty) { throw 'Register a device in WFI first.' }
$source = (Resolve-Path -LiteralPath $PackagePath).Path
if (-not (Test-Path -LiteralPath (Join-Path $source 'WFI.Agent.exe'))) { throw 'Published win-x64 agent package missing.' }
$destination = [IO.Path]::GetFullPath($InstallPath)
if ($destination -eq [IO.Path]::GetPathRoot($destination) -or $destination -eq $env:windir) { throw 'Invalid installation directory.' }
if ($destination.StartsWith($env:windir.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or $destination -eq $source -or $destination.StartsWith($source.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Use a dedicated agent installation directory outside Windows and the source package.' }
if ((Test-Path -LiteralPath $destination) -and (Get-ChildItem -LiteralPath $destination -Force | Select-Object -First 1) -and -not (Test-Path -LiteralPath (Join-Path $destination 'WFI.Agent.exe'))) { throw 'Destination is not empty and is not an existing WFI agent installation.' }
$stateDirectory = Join-Path $env:ProgramData 'WFI'
$configPath = Join-Path $destination 'appsettings.local.json'
if (Test-Path -LiteralPath $configPath) {
    $previous = (Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json).Agent
    if (-not $PSBoundParameters.ContainsKey('BigFixReinstallSha256')) { $BigFixReinstallSha256 = [string]$previous.BigFixReinstallSha256 }
    if ([guid]$previous.DeviceId -ne $DeviceId -or ([string]$previous.ServerUrl).TrimEnd('/') -ne $ServerUrl.AbsoluteUri.TrimEnd('/')) { throw 'Existing agent identity/server differs. Use an explicit re-enrollment maintenance procedure; upgrade will not replace identity.' }
    if (-not $PSBoundParameters.ContainsKey('PollSeconds')) { $PollSeconds = [int]$previous.PollSeconds }
    if ($PollSeconds -lt 10 -or $PollSeconds -gt 600) { throw 'Existing PollSeconds is invalid; supply a value between 10 and 600.' }
    if ([IO.Path]::GetFullPath([string]$previous.StatePath) -ne (Join-Path $stateDirectory 'agent-state.bin')) { throw 'Existing custom state path requires a dedicated upgrade procedure.' }
}
foreach ($candidate in @($source,$destination,$stateDirectory)) {
    if(Test-Path -LiteralPath $candidate) {
        $items=@(Get-Item -LiteralPath $candidate -Force)+@(Get-ChildItem -LiteralPath $candidate -Recurse -Force)
        if($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Reparse points are not supported in agent installation/state directories.' }
    }
}
function Protect-WfiDirectory([string]$Path) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    foreach ($item in @((Get-Item -LiteralPath $Path))+@(Get-ChildItem -LiteralPath $Path -Recurse -Force)) {
        $acl = Get-Acl -LiteralPath $item.FullName
        $acl.SetAccessRuleProtection($true, $false)
        foreach ($existing in @($acl.Access)) { [void]$acl.RemoveAccessRuleSpecific($existing) }
        foreach ($sid in @('S-1-5-18','S-1-5-32-544')) {
            $identity = [Security.Principal.SecurityIdentifier]::new($sid)
            $inheritance = if ($item.PSIsContainer) { 'ContainerInherit,ObjectInherit' } else { 'None' }
            $rule = [Security.AccessControl.FileSystemAccessRule]::new($identity,'FullControl',$inheritance,'None','Allow')
            $acl.SetAccessRule($rule)
        }
        Set-Acl -LiteralPath $item.FullName -AclObject $acl
    }
}
function Assert-WfiServiceUpgrade($Instance, [string]$Destination) {
    if ($null -eq $Instance) { throw 'Existing service configuration could not be read.' }
    if ($Instance.StartName -notin @('LocalSystem','NT AUTHORITY\SYSTEM')) { throw 'Existing service uses a custom account. Preserve DPAPI identity through a dedicated upgrade procedure.' }
    $expected = Join-Path $Destination 'WFI.Agent.exe'
    $actual = ([string]$Instance.PathName).Trim()
    if ($actual -ne ('"' + $expected + '"') -and $actual -ne $expected) { throw 'Existing service binary differs from the requested installation path. Use a dedicated relocation/upgrade procedure.' }
}
$service = Get-Service -Name 'WFI Agent' -ErrorAction SilentlyContinue
if ($service) {
    $instance = Get-CimInstance Win32_Service -Filter "Name='WFI Agent'"
    Assert-WfiServiceUpgrade $instance $destination
}
$secureToken = if($PSBoundParameters.ContainsKey('EnrollmentToken')){$EnrollmentToken}else{Read-Host 'One-time enrollment token (hidden; reuse state on upgrades)' -AsSecureString}
$token = ConvertFrom-SecureString $secureToken -AsPlainText
if (-not (Test-Path -LiteralPath (Join-Path $stateDirectory 'agent-state.bin')) -and $token -notmatch '^[0-9a-fA-F]{64}$') { throw 'A new installation requires a valid enrollment token.' }
if ($service) { Stop-Service -Name 'WFI Agent' -ErrorAction Stop }
Protect-WfiDirectory $destination
Protect-WfiDirectory $stateDirectory
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
$settings = @{ Agent = @{ BigFixReinstallSha256=$BigFixReinstallSha256; ServerUrl=$ServerUrl.AbsoluteUri.TrimEnd('/'); DeviceId=$DeviceId.ToString(); EnrollmentToken=$token; PollSeconds=$PollSeconds; StatePath=(Join-Path $stateDirectory 'agent-state.bin') } }
$settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'appsettings.local.json') -Encoding utf8
$token = $null; $settings = $null
$binary = '"' + (Join-Path $destination 'WFI.Agent.exe') + '"'
if (-not $service) { New-Service -Name 'WFI Agent' -BinaryPathName $binary -DisplayName 'WFI Agent' -StartupType Automatic | Out-Null }
else {
    $change = Invoke-CimMethod -InputObject $instance -MethodName Change -Arguments @{ PathName=$binary; StartMode='Automatic' }
    if ($change.ReturnValue -ne 0) { throw "Service update failed: $($change.ReturnValue)" }
}
Start-Service -Name 'WFI Agent'
Get-Service -Name 'WFI Agent'
