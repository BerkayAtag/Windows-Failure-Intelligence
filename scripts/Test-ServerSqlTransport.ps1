#Requires -Version 5.1
param([Parameter(Mandatory)][string]$PostgresBin,[ValidateRange(1024,65535)][int]$Port=35441)
$ErrorActionPreference='Stop'
$pgPort=$Port
$project=Split-Path $PSScriptRoot -Parent
$pg=(Resolve-Path -LiteralPath $PostgresBin).Path
$root=Join-Path $env:TEMP ('wfi-transport-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$cluster=Join-Path $root 'data';$pw=Join-Path $root 'pw.txt'
$bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($bytes);$rng.Dispose()
$state=[pscustomobject]@{OwnerPassword=([BitConverter]::ToString($bytes)).Replace('-','');AppPassword='Synthetic-transport-password-only'}
[IO.File]::WriteAllText($pw,$state.OwnerPassword,[Text.Encoding]::ASCII)
$started=$false
try {
 & (Join-Path $pg 'initdb.exe') -D $cluster -U wfi_owner --auth=scram-sha-256 --pwfile=$pw --encoding=UTF8 --locale=C
 if($LASTEXITCODE -ne 0){throw 'initdb failed'}
 Remove-Item -LiteralPath $pw
 & (Join-Path $pg 'pg_ctl.exe') -D $cluster -l (Join-Path $root 'postgres.log') -o "-h 127.0.0.1 -p $Port" -w start
 if($LASTEXITCODE -ne 0){throw 'start failed'};$started=$true
 $t=$null;$e=$null;$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $project 'scripts/Install-ServerAutomatic.ps1'),[ref]$t,[ref]$e)
 $fn=$ast.Find({param($a)$a -is [Management.Automation.Language.FunctionDefinitionAst] -and $a.Name -eq 'Sql'},$true)
 . ([scriptblock]::Create($fn.Extent.Text))
 $create="SELECT 'CREATE ROLE wfi_app LOGIN' WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname='wfi_app')\gexec`nALTER ROLE wfi_app WITH NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '"+$state.AppPassword+"';`nSELECT 'CREATE DATABASE wfi OWNER wfi_owner' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname='wfi')\gexec`n"
 Write-Host 'Testing idempotent provisioning'
 Sql 'postgres' $create
 Write-Host 'Testing idempotent provisioning'
 Sql 'postgres' $create
 Write-Host 'Testing schema provisioning'
 Sql 'wfi' (Get-Content (Join-Path $project 'deploy/schema.sql') -Raw -Encoding UTF8)
 $word='Te'+[char]0x015F+'his'
 Write-Host 'Testing UTF8 insert'
 Sql 'wfi' ("CREATE TABLE transport_test(value text);INSERT INTO transport_test VALUES ('"+$word+"');")
 $si=New-Object Diagnostics.ProcessStartInfo;$si.FileName=Join-Path $pg 'psql.exe';$si.Arguments='-X -w -h 127.0.0.1 -p '+$Port+' -U wfi_owner -d wfi -At';$si.UseShellExecute=$false;$si.CreateNoWindow=$true;$si.RedirectStandardInput=$true;$si.RedirectStandardOutput=$true;$si.RedirectStandardError=$true;$si.EnvironmentVariables['PGPASSWORD']=$state.OwnerPassword
 $p=[Diagnostics.Process]::Start($si);$p.StandardInput.WriteLine("SELECT encode(convert_to(value,'UTF8'),'hex') FROM transport_test;");$p.StandardInput.Close();$output=$p.StandardOutput.ReadToEnd();$errorText=$p.StandardError.ReadToEnd();$p.WaitForExit()
 if($p.ExitCode -ne 0 -or $output.Trim() -ne '5465c59f686973'){throw 'UTF8 transport failed'};$p.Dispose()
 Write-Host 'Windows PowerShell 5.1 real PostgreSQL transport passed: idempotent role/database creation, schema provisioning, UTF8 Turkish text roundtrip.'
}finally{if(Test-Path $pw){Remove-Item -LiteralPath $pw};if($started){& (Join-Path $pg 'pg_ctl.exe') -D $cluster -m fast -w stop}}
