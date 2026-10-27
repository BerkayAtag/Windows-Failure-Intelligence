$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Test-AgentInstaller.ps1')
& (Join-Path $PSScriptRoot 'Test-ConnectivityValidation.ps1')
. (Join-Path $PSScriptRoot 'Resolve-Dotnet.ps1')
$dotnetExe = Resolve-WfiDotnetSdk
$pnpmCmd = Get-Command pnpm -ErrorAction SilentlyContinue
$bundledPnpm = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pnpm/bin/pnpm.cjs'
Push-Location (Join-Path $projectRoot 'src/wfi-web')
try {
    if ($pnpmCmd) { & $pnpmCmd.Source install --frozen-lockfile } elseif (Test-Path $bundledPnpm) { node $bundledPnpm install --frozen-lockfile } else { throw 'Node.js 22+ ve pnpm 11 kurun.' }
    if ($LASTEXITCODE -ne 0) { throw 'Frontend bağımlılık kurulumu başarısız.' }
    if ($pnpmCmd) { & $pnpmCmd.Source run build } else { node $bundledPnpm run build }
    if ($LASTEXITCODE -ne 0) { throw 'Frontend derlenemedi.' }
} finally { Pop-Location }
Push-Location $projectRoot
try {
    & $dotnetExe test WFI.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Testler başarısız.' }
    & $dotnetExe publish src/WFI.Api -c Release -o artifacts/api
    if ($LASTEXITCODE -ne 0) { throw 'API yayımlama paketi hazırlanamadı.' }
    & $dotnetExe publish src/WFI.Worker -c Release -o artifacts/worker
    if ($LASTEXITCODE -ne 0) { throw 'Worker yayımlama paketi hazırlanamadı.' }
    & $dotnetExe publish src/WFI.Agent -c Release -r win-x64 --self-contained true -o artifacts/agent
    if ($LASTEXITCODE -ne 0) { throw 'Agent yayımlama paketi hazırlanamadı.' }
    Copy-Item -LiteralPath scripts/Install-Agent.ps1 -Destination artifacts/agent/Install-Agent.ps1 -Force
    & $dotnetExe publish src/WFI.Agent.Setup -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/agent-setup
    if ($LASTEXITCODE -ne 0) { throw 'Agent setup hazırlanamadı.' }
    Copy-Item -Path artifacts/agent-setup/* -Destination artifacts/agent -Recurse -Force
    Copy-Item -LiteralPath docs/client-installation.md -Destination artifacts/agent/CLIENT-INSTALLATION.md -Force
    if (Get-ChildItem artifacts/agent -Recurse -File | Where-Object { $_.Name -like '*.local.json' }) { throw 'Ajan paketinde yerel ayar bulundu.' }
    New-Item -ItemType Directory -Force artifacts/api/distribution,src/WFI.Api/distribution | Out-Null
    Compress-Archive -Path artifacts/agent/* -DestinationPath artifacts/api/distribution/WFI-Agent.zip -Force
    Copy-Item -LiteralPath artifacts/api/distribution/WFI-Agent.zip -Destination src/WFI.Api/distribution/WFI-Agent.zip -Force
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    if(-not(Test-Path -LiteralPath $compiler)){throw '.NET Framework C# compiler is required to build the single-file bootstrap.'}
    $manifest=Join-Path $projectRoot 'src/WFI.Agent.Setup/app.manifest'
    $payload=Join-Path $projectRoot 'artifacts/api/distribution/WFI-Agent.zip'
    $bootstrap=Join-Path $projectRoot 'scripts/AgentBootstrap.cs'
    $setupOutput=Join-Path $projectRoot 'artifacts/api/distribution/WFI-Agent-Setup.exe'
    & $compiler '/nologo' '/target:winexe' '/platform:x64' '/optimize+' '/reference:System.IO.Compression.dll' '/reference:System.IO.Compression.FileSystem.dll' '/reference:System.Windows.Forms.dll' "/win32manifest:$manifest" "/resource:$payload,WfiPayload" "/out:$setupOutput" $bootstrap
    if($LASTEXITCODE -ne 0){throw 'Single-file agent setup build failed.'}
    $setupFile=Get-Item artifacts/api/distribution/WFI-Agent-Setup.exe
    @{version='0.6.0-preview.2';size=$setupFile.Length;sha256=(Get-FileHash -LiteralPath $setupFile.FullName -Algorithm SHA256).Hash;platform='Windows x64';signed=$false} | ConvertTo-Json | Set-Content -LiteralPath artifacts/api/distribution/agent-manifest.json -Encoding utf8
    Copy-Item -LiteralPath artifacts/api/distribution/WFI-Agent-Setup.exe,artifacts/api/distribution/agent-manifest.json -Destination src/WFI.Api/distribution -Force
} finally { Pop-Location }


& (Join-Path $PSScriptRoot 'Build-ServerSetup.ps1')

