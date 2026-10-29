param([int]$Port = 5080)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Resolve-Dotnet.ps1')
$dotnetExe = Resolve-WfiDotnetSdk
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'src/WFI.Api/wwwroot/index.html'))) { throw 'Önce scripts/Build.ps1 çalıştırın.' }
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDemo = $env:Demo__Enabled
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Demo__Enabled = 'true'
    Set-Location $projectRoot
    & $dotnetExe run --project src/WFI.Api --urls "http://127.0.0.1:$Port"
    if ($LASTEXITCODE -ne 0) { throw 'Uygulama başlatılamadı.' }
} finally { $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment; $env:Demo__Enabled = $previousDemo }
