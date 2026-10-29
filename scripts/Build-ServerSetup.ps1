$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$staging=Join-Path $projectRoot ('artifacts/server-setup-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
foreach($folder in @('scripts','deploy','docs')){New-Item -ItemType Directory -Path (Join-Path $staging $folder) | Out-Null}
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts/api') -Destination $staging -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts/ollama-host') -Destination $staging -Recurse
foreach($name in @('Setup-Server.ps1','Install-Server.ps1','InstallerSupport.ps1','Uninstall-Wfi.ps1','ServerSetupSupport.ps1','Install-ServerAutomatic.ps1','server-dependencies.json')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $staging 'scripts')}
foreach($name in @('schema.sql','appsettings.example.json')){Copy-Item -LiteralPath (Join-Path $projectRoot "deploy/$name") -Destination (Join-Path $staging 'deploy')}
foreach($name in @('server-installation.md','client-installation.md','firewall-requirements.md')){Copy-Item -LiteralPath (Join-Path $projectRoot "docs/$name") -Destination (Join-Path $staging 'docs')}
if(Get-ChildItem -LiteralPath $staging -Recurse -File | Where-Object {$_.Name -like '*.local.json' -or $_.Extension -in @('.pfx','.key','.db')}){throw 'Private file found in server payload.'}
$payload=Join-Path $projectRoot 'artifacts/WFI-Server-Payload.zip'
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $payload -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output=Join-Path $projectRoot 'artifacts/WFI-Server-Setup.exe'
& $compiler '/nologo' '/target:winexe' '/platform:x64' '/optimize+' '/reference:System.IO.Compression.dll' '/reference:System.IO.Compression.FileSystem.dll' '/reference:System.Windows.Forms.dll' "/win32manifest:$projectRoot/src/WFI.Agent.Setup/app.manifest" "/resource:$payload,WfiPayload" "/out:$output" "/win32icon:$projectRoot/artifacts/branding/wfi.ico" (Join-Path $PSScriptRoot 'ServerBootstrap.cs')
if($LASTEXITCODE -ne 0){throw 'Server bootstrap compilation failed.'}
Get-FileHash -LiteralPath $output -Algorithm SHA256 | Select-Object Algorithm,Hash,Path
# Only remove this invocation's verified staging child, using one shell.
$resolved=(Resolve-Path -LiteralPath $staging).Path
$allowed=(Join-Path $projectRoot 'artifacts')+[IO.Path]::DirectorySeparatorChar
if(!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $resolved -Leaf).StartsWith('server-setup-')){throw 'Unsafe staging path'}
Remove-Item -LiteralPath $resolved -Recurse -Force
