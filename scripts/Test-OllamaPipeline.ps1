param([string]$BaseUrl='http://127.0.0.1:5080',[switch]$BigFix)
$ErrorActionPreference='Stop'
$uri=[uri]$BaseUrl
if (-not $uri.IsLoopback) { throw 'This synthetic smoke test is restricted to loopback development servers.' }
$csrf=(Invoke-RestMethod "$BaseUrl/api/v1/auth/csrf" -SessionVariable session -UseBasicParsing).token
$headers=@{'X-CSRF-TOKEN'=$csrf}
Invoke-RestMethod "$BaseUrl/api/v1/auth/demo" -Method Post -WebSession $session -Headers $headers | Out-Null
$headers['X-CSRF-TOKEN']=(Invoke-RestMethod "$BaseUrl/api/v1/auth/csrf" -WebSession $session).token
function Send-User([string]$Path,$Body) {
    Invoke-RestMethod "$BaseUrl/api/v1/$Path" -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 8)
}
$registration=Send-User 'devices/register' @{hostname=('synthetic-'+[guid]::NewGuid().ToString('N').Substring(0,12))}
# Windows PowerShell 5.1 compatible: no Convert.ToHexString / static GetBytes.
$bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();try{$rng.GetBytes($bytes)}finally{$rng.Dispose()}
$credential=[BitConverter]::ToString($bytes).Replace('-','')
$agentHeaders=@{'X-WFI-Device'=$registration.id;Authorization="Bearer $credential"}
try {
    Invoke-RestMethod "$BaseUrl/api/v1/agent/enroll" -Method Post -ContentType 'application/json' -Body (@{deviceId=$registration.id;token=$registration.enrollmentToken;credential=$credential}|ConvertTo-Json) | Out-Null
    # Explicitly disable automatic actions: this test never requests a repair.
    Invoke-RestMethod "$BaseUrl/api/v1/devices/$($registration.id)/automation" -Method Put -WebSession $session -Headers $headers -ContentType 'application/json' -Body '{"mode":"Disabled"}' | Out-Null
    $scan=if($BigFix){'BigFixScan'}else{'NetworkScan'}
    $job=Send-User 'diagnostics' @{hostname=$registration.hostname;scanType=$scan;problemDescription='Synthetic operator report: selected component is not working; diagnose from evidence only.'}
    $claim=Invoke-RestMethod "$BaseUrl/api/v1/agent/me/diagnostics/claim" -Method Post -Headers $agentHeaders
    $evidence=@{leaseToken=$claim.leaseToken;evidence=@(@{source='synthetic-fixture';category='Domain / DNS';content='Synthetic test only. ERROR_NO_LOGON_SERVERS observed once. DNS and DC health have not been verified.'})}
    if($BigFix){$evidence.evidence=@(@{source='BESClient';category='BigFix';content='WFI_BESCLIENT_STOPPED Status=Stopped'})}
    Invoke-RestMethod "$BaseUrl/api/v1/agent/me/diagnostics/$($claim.jobId)/evidence" -Method Post -Headers $agentHeaders -ContentType 'application/json' -Body ($evidence|ConvertTo-Json -Depth 5) | Out-Null
    $deadline=[DateTime]::UtcNow.AddMinutes(5)
    do {
        Start-Sleep -Seconds 3
        $result=Invoke-RestMethod "$BaseUrl/api/v1/diagnostics/$($job.id)" -WebSession $session
    } while ($result.status -notin @('completed','failed') -and [DateTime]::UtcNow -lt $deadline)
    if ($result.status -ne 'completed' -or $result.analysis.status -ne 'completed') { throw "AI pipeline did not complete: $($result.status); $($result.error)" }
    $expected=if($BigFix){'PB-BIGFIX-REPAIR'}else{'PB-DNS-FLUSH'}
    if ($result.analysis.recommendedPlaybooksJson -notmatch $expected) { throw 'Deterministic recommendation missing.' }
    [pscustomobject]@{Status=$result.status;Model=$result.analysis.modelUsed;Confidence=$result.analysis.confidenceScore;JobId=$job.id;ActualRepairExecuted=$false}
} finally {
    Send-User "devices/$($registration.id)/revoke" @{} | Out-Null
    $credential=$null;$agentHeaders=$null;$registration=$null
}
