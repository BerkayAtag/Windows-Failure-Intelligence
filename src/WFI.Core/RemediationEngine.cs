namespace WFI.Core;

public sealed record RemediationDefinition(string Code, string Title, string Risk, bool AllowAutomatic, string[] RuleIds, string Precheck, string Action, string Verify, int TimeoutSeconds);

public static class RemediationEngine
{
    public const string Version = "4.0.0";
    // Shipped with server and agent. Model-generated or database scripts are never executed.
    public static readonly RemediationDefinition[] Catalog =
    [
        new("PB-DNS-FLUSH", "DNS istemci önbelleğini temizle", "Low", true, ["AD-001"],
            "Get-Command Clear-DnsClientCache -ErrorAction Stop | Out-Null", "Clear-DnsClientCache -ErrorAction Stop",
            "Get-DnsClientCache -ErrorAction Stop | Out-Null; Write-Output 'Cache cleared; resolver accessible. Domain connectivity requires further diagnosis.'", 60),
        new("PB-WU-START", "Windows Update servislerini başlat", "Low", true, [],
            "Get-Service wuauserv,bits -ErrorAction Stop | Out-Null", "Start-Service wuauserv,bits -ErrorAction Stop",
            "foreach($s in Get-Service wuauserv,bits){if($s.Status -ne 'Running'){throw 'Service not running'}}; Write-Output 'Update services running.'", 120),
        new("PB-SCCM-RESTART", "SCCM istemci servisini yeniden başlat", "Medium", false, ["SCCM-001"],
            "Get-Service CcmExec -ErrorAction Stop | Out-Null", "Restart-Service CcmExec -ErrorAction Stop",
            "if((Get-Service CcmExec).Status -ne 'Running'){throw 'CcmExec not running'}; Write-Output 'CcmExec running.'", 180),
        new("PB-DISM-REPAIR", "Windows bileşen deposunu onar", "High", false, ["WU-001"],
            "if(-not (Test-Path \"$env:SystemRoot/System32/dism.exe\")){throw 'DISM unavailable'}",
            "& \"$env:SystemRoot/System32/dism.exe\" /Online /Cleanup-Image /RestoreHealth /NoRestart; if($LASTEXITCODE -ne 0){throw \"DISM exit $LASTEXITCODE\"}",
            "& \"$env:SystemRoot/System32/dism.exe\" /Online /Cleanup-Image /CheckHealth /NoRestart; if($LASTEXITCODE -ne 0){throw \"DISM verification exit $LASTEXITCODE\"}", 1800),
        new("PB-BIGFIX-REPAIR", "BigFix BESClient servisini onar", "High", true, ["BF-001"],
            "Get-Command Get-Service,Start-Service -ErrorAction Stop | Out-Null", BigFixRepair,
            "if((Get-Service BESClient -ErrorAction Stop).Status -ne 'Running'){throw 'BESClient verification failed'}; Write-Output 'BESClient running; management connectivity requires separate verification.'", 900)
    ];
    // Only the agent's locally configured, administrator-approved hash can replace this marker.
    public const string BigFixRepair = """
        $service = Get-Service BESClient -ErrorAction SilentlyContinue
        if ($service -and $service.Status -eq 'Running') { Write-Output 'BESClient already running'; return }
        try {
            Start-Service BESClient -ErrorAction Stop
            (Get-Service BESClient).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))
            Write-Output 'BESClient started'; return
        } catch { Write-Output 'Service start failed; checking approved reinstall file.' }
        $expected = '__WFI_BIGFIX_SHA256__'
        if ($expected -notmatch '^[a-fA-F0-9]{64}$') { throw 'Approved BigFix reinstall SHA256 is not configured on agent' }
        $path = 'C:\Windows\Bigfix\Cache\reinstall.cmd'
        $item = Get-Item -LiteralPath $path -Force -ErrorAction Stop
        for ($current=$item; $null -ne $current; $current= $(if($current -is [IO.FileInfo]){$current.Directory}else{$current.Parent})) {
            if ($current.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse path rejected' }
            $acl=Get-Acl -LiteralPath $current.FullName
            $trusted=@('S-1-5-18','S-1-5-32-544','S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464')
            if($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin $trusted){throw 'BigFix path owned by untrusted principal'}
            foreach($rule in $acl.Access) {
                if($rule.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly){continue}
                $sid=$rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
                $mask=if($current.FullName -eq [IO.Path]::GetPathRoot($path)){0xC0040}else{0xD0156}
                if($rule.AccessControlType -eq 'Allow' -and ([int]$rule.FileSystemRights -band $mask) -ne 0 -and $sid -notin $trusted) { throw 'BigFix path writable by untrusted principal' }
            }
        }
        $lock=[IO.File]::Open($path,'Open','Read','Read')
        try {
            $sha=[Security.Cryptography.SHA256]::Create()
            try { $actual=[BitConverter]::ToString($sha.ComputeHash($lock)).Replace('-','') } finally { $sha.Dispose() }
            if($actual -ne $expected){throw 'BigFix reinstall hash mismatch'}
            Push-Location -LiteralPath 'C:\Windows\Bigfix\Cache'
            try { & "$env:SystemRoot\System32\cmd.exe" /d /c $path; if($LASTEXITCODE -ne 0){throw "BigFix reinstall exit $LASTEXITCODE"} } finally { Pop-Location }
            (Get-Service BESClient -ErrorAction Stop).WaitForStatus('Running',[TimeSpan]::FromSeconds(60))
        } finally { $lock.Dispose() }
        """;
    public static RemediationDefinition? Find(string code) => Catalog.FirstOrDefault(x => x.Code == code);
}


