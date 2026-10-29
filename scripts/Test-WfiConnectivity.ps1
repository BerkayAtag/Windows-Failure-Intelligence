#Requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$ServerUrl,
    [ValidateRange(1,60)][int]$TimeoutSeconds=10
)
$ErrorActionPreference='Stop'
function Get-WfiProbeBaseUri([string]$Value) {
    $parsed=$null
    if(-not [uri]::TryCreate($Value,[UriKind]::Absolute,[ref]$parsed) -or $parsed.Scheme -ne 'https' -or $parsed.UserInfo -or $parsed.Query -or $parsed.Fragment){throw 'Provide an HTTPS server URL without credentials, query or fragment.'}
    return [uri]($parsed.AbsoluteUri.TrimEnd('/')+'/')
}
function Test-WfiProbeResponse([int]$Status,[string]$Body,[string]$Expected) {
    if($Status -ne 200){return $false}
    try{$parsed=ConvertFrom-Json -InputObject $Body -ErrorAction Stop;return ($parsed.status -is [string] -and $parsed.status -ceq $Expected)}catch{return $false}
}
$base=Get-WfiProbeBaseUri $ServerUrl
try {
    $lookup=[Net.Dns]::GetHostAddressesAsync($base.DnsSafeHost)
    if(-not $lookup.Wait($TimeoutSeconds*1000) -or $lookup.Result.Count -eq 0){throw 'DNS unavailable.'}
    [pscustomobject]@{Check='DNS';Passed=$true;Detail='Name resolved.'}
} catch {
    [pscustomobject]@{Check='DNS';Passed=$false;Detail='Check DNS configuration and name resolution.'}
    throw 'WFI connectivity preflight failed at DNS; no settings changed.'
}
$handler=[Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect=$false
$handler.UseDefaultCredentials=$false
$handler.UseCookies=$false
$client=[Net.Http.HttpClient]::new($handler)
$client.Timeout=[TimeSpan]::FromSeconds($TimeoutSeconds)
$client.MaxResponseContentBufferSize=65536
$passed=$true
try {
    foreach($probe in @(@{Path='api/v1/health';Expected='alive'},@{Path='api/v1/ready';Expected='ready'})) {
        $response=$null
        try {
            $response=$client.GetAsync([uri]::new($base,$probe.Path)).GetAwaiter().GetResult()
            $body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $ok=Test-WfiProbeResponse ([int]$response.StatusCode) $body $probe.Expected
            if(-not $ok){$passed=$false}
            [pscustomobject]@{Check=$probe.Path;Passed=$ok;Detail=if($ok){'HTTPS and expected API response verified.'}else{'Unexpected HTTP status or API response; check proxy/backend/readiness.'};HttpStatus=[int]$response.StatusCode}
        } catch {
            $passed=$false
            [pscustomobject]@{Check=$probe.Path;Passed=$false;Detail='HTTPS failed: check certificate trust/name, proxy, firewall, service and timeout.';HttpStatus=$null}
        } finally {if($null -ne $response){$response.Dispose()}}
    }
} finally {$client.Dispose()}
if(-not $passed){throw 'WFI connectivity preflight failed; no settings changed.'}
Write-Host 'Preflight passed. AD login, agent identity, model, storage and fleet/HA acceptance still require separate tests.'
