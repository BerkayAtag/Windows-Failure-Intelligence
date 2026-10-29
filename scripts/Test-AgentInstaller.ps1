# No administrator rights, service changes, credentials or machine configuration required.
$ErrorActionPreference = 'Stop'
$tokens=$null; $parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Install-Agent.ps1'),[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count){throw 'Installer syntax errors.'}
$guard=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-WfiServiceUpgrade'},$true)
if(-not $guard){throw 'Upgrade guard missing.'}
. ([scriptblock]::Create($guard.Extent.Text))
$destination=Join-Path ([IO.Path]::GetTempPath()) 'wfi-synthetic-install'
$binary=Join-Path $destination 'WFI.Agent.exe'
$cases=@(
    @{Account='LocalSystem';Binary=('"'+$binary+'"');Accept=$true},
    @{Account='NT AUTHORITY\SYSTEM';Binary=$binary;Accept=$true},
    @{Account='synthetic\service';Binary=('"'+$binary+'"');Accept=$false},
    @{Account='LocalSystem';Binary=('"'+$binary+'" --other');Accept=$false},
    @{Account='LocalSystem';Binary=(Join-Path $destination 'Other.exe');Accept=$false}
)
foreach($case in $cases){
    $accepted=$true
    try{Assert-WfiServiceUpgrade ([pscustomobject]@{StartName=$case.Account;PathName=$case.Binary}) $destination}catch{$accepted=$false}
    if($accepted -ne $case.Accept){throw 'Service upgrade guard behavior failed.'}
}
$rejected=$false
try{Assert-WfiServiceUpgrade $null $destination}catch{$rejected=$true}
if(-not $rejected){throw 'Missing service configuration accepted.'}
Write-Host 'Agent installer preflight: 6/6 synthetic cases passed; no services modified.'
