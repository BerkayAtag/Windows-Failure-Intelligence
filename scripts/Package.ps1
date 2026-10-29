$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    foreach ($directory in @('artifacts/api', 'artifacts/worker', 'artifacts/agent', 'artifacts/ollama-host')) {
        if (-not (Test-Path -LiteralPath $directory)) { throw 'Önce scripts/Build.ps1 çalıştırın.' }
        $privateFiles = Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.Name -like '*.local.json' -or $_.FullName -match '[\\/]\.data[\\/]' }
        if ($privateFiles) { throw 'Paket içinde yerel ayar veya runtime verisi bulundu; paket oluşturulmadı.' }
    }
    Compress-Archive -Path artifacts/api,artifacts/worker,artifacts/agent,artifacts/ollama-host,deploy,docs,scripts,Setup-Server.cmd,README.md,SECURITY.md,history-release.md -DestinationPath artifacts/WFI-0.6.0-preview.5-pilot.zip -Force
    Get-FileHash -LiteralPath artifacts/WFI-0.6.0-preview.5-pilot.zip -Algorithm SHA256 | Select-Object Algorithm, Hash, Path
} finally { Pop-Location }


