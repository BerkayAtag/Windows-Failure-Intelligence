$ErrorActionPreference='Stop'
$tokens=$null;$parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Test-WfiConnectivity.ps1'),[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count){throw 'Connectivity script syntax errors.'}
foreach($name in @('Get-WfiProbeBaseUri','Test-WfiProbeResponse')){
    $function=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name},$true)
    if(-not $function){throw 'Probe function missing.'}
    . ([scriptblock]::Create($function.Extent.Text))
}
foreach($url in @('http://synthetic.example','https://user:synthetic@synthetic.example','https://synthetic.example/?token=synthetic','https://synthetic.example/#fragment','not a url')){
    $rejected=$false;try{Get-WfiProbeBaseUri $url | Out-Null}catch{$rejected=$true}
    if(-not $rejected){throw 'Unsafe or invalid probe URL accepted.'}
}
if((Get-WfiProbeBaseUri 'https://synthetic.example/wfi').AbsoluteUri -ne 'https://synthetic.example/wfi/'){throw 'Path prefix lost.'}
$cases=@(
    @{Code=200;Body='{"status":"alive"}';Expected='alive';Pass=$true},
    @{Code=200;Body='{"status":"ready"}';Expected='ready';Pass=$true},
    @{Code=200;Body='<html>proxy page</html>';Expected='ready';Pass=$false},
    @{Code=200;Body='{"status":"alive"}';Expected='ready';Pass=$false},
    @{Code=503;Body='{"status":"ready"}';Expected='ready';Pass=$false},
    @{Code=302;Body='{"status":"ready"}';Expected='ready';Pass=$false}
)
foreach($case in $cases){if((Test-WfiProbeResponse $case.Code $case.Body $case.Expected) -ne $case.Pass){throw 'Probe response validation failed.'}}
Write-Host 'Connectivity validation: 12/12 synthetic cases passed; no network requests sent.'
