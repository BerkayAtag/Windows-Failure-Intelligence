param([Parameter(Mandatory)][string]$PostgresBin, [int]$Port=55439)
$ErrorActionPreference='Stop'
if($Port -lt 1024 -or $Port -gt 65535){throw 'Port 1024..65535 aralığında olmalı.'}
$projectRoot=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Resolve-Dotnet.ps1')
$dotnetExe=Resolve-WfiDotnetSdk
$bin=(Resolve-Path -LiteralPath $PostgresBin).Path
foreach($exe in @('initdb.exe','pg_ctl.exe')){if(-not(Test-Path -LiteralPath (Join-Path $bin $exe))){throw "Eksik PostgreSQL aracı: $exe"}}
$runRoot=Join-Path ([IO.Path]::GetTempPath()) ('wfi-pg-test-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($runRoot)|Out-Null
$data=Join-Path $runRoot 'data'
$passwordFile=Join-Path $runRoot 'bootstrap-password.txt'
$secret=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
[IO.File]::WriteAllText($passwordFile,$secret)
$previous= [Environment]::GetEnvironmentVariable('WFI_TEST_POSTGRES')
$previousRuntime= [Environment]::GetEnvironmentVariable('WFI_TEST_DOTNET')
$started=$false
try {
    & (Join-Path $bin 'initdb.exe') -D $data -U wfi_test_admin --auth=scram-sha-256 --pwfile=$passwordFile --encoding=UTF8 --locale=C
    if($LASTEXITCODE -ne 0){throw 'Test cluster oluşturulamadı.'}
    Remove-Item -LiteralPath $passwordFile
    & (Join-Path $bin 'pg_ctl.exe') -D $data -l (Join-Path $runRoot 'postgres.log') -o "-h 127.0.0.1 -p $Port" -w start
    if($LASTEXITCODE -ne 0){throw 'Test PostgreSQL başlatılamadı; portu ve test logunu kontrol edin.'}
    $started=$true
    $env:WFI_TEST_DOTNET=$dotnetExe
    $env:WFI_TEST_POSTGRES="Host=127.0.0.1;Port=$Port;Username=wfi_test_admin;Password=$secret;Database=postgres;Pooling=false"
    & $dotnetExe test (Join-Path $projectRoot 'tests/WFI.PostgresTests/WFI.PostgresTests.csproj') -c Release
    if($LASTEXITCODE -ne 0){throw 'PostgreSQL kabul testleri başarısız.'}
} finally {
    $env:WFI_TEST_POSTGRES=$previous
    $env:WFI_TEST_DOTNET=$previousRuntime
    if(Test-Path -LiteralPath $passwordFile){Remove-Item -LiteralPath $passwordFile}
    if($started){& (Join-Path $bin 'pg_ctl.exe') -D $data -m fast -w stop}
    Write-Host "Sentetik test cluster/log dizini: $runRoot"
}

# Verify the actual installer SQL transport under Windows PowerShell 5.1, not only Npgsql.
& (Join-Path $env:WINDIR "System32/WindowsPowerShell/v1.0/powershell.exe") -NoProfile -File (Join-Path $PSScriptRoot "Test-ServerSqlTransport.ps1") -PostgresBin $bin -Port $Port
if($LASTEXITCODE -ne 0){throw "Installer SQL transport regression failed."}
