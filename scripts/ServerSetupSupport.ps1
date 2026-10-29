# Pure helpers: importing this file does not install or start anything.
. (Join-Path $PSScriptRoot 'InstallerSupport.ps1')
function Assert-WfiSetupOptions($Options) {
    foreach($name in @('InstallPath','DataPath')) {
        if([string]$Options.$name -notmatch '^[A-Za-z]:[\\/]' -or [string]$Options.$name -match '[\[\]*?"\r\n]'){throw 'Use a fully qualified local directory without wildcard characters.'}
    }
    foreach($name in @('Hostname','Domain')) {
        if([string]$Options.$name -notmatch '^(?=.{1,253}$)[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$'){throw "Invalid $name."}
    }
    Assert-WfiDistinctDirectories @([string]$Options.InstallPath,[string]$Options.DataPath)
    if([string]$Options.PasswordHash -notmatch '^pbkdf2-sha256:600000:[A-Za-z0-9+/]{43}=:[A-Za-z0-9+/]{43}=$'){throw 'Invalid administrator hash.'}
    if($Options.CertificateThumbprint -and [string]$Options.CertificateThumbprint -notmatch '^[A-Fa-f0-9]{40}$'){throw 'Invalid certificate thumbprint.'}
}
function New-WfiPasswordHash([string]$Password) {
    if($Password.Length -lt 14 -or $Password.Length -gt 1024){throw 'Use a password of 14 to 1024 characters.'}
    $salt=New-Object byte[] 32
    $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
    try{$rng.GetBytes($salt)}finally{$rng.Dispose()}
    $derive=[Security.Cryptography.Rfc2898DeriveBytes]::new($Password,$salt,600000,[Security.Cryptography.HashAlgorithmName]::SHA256)
    try{return 'pbkdf2-sha256:600000:'+ [Convert]::ToBase64String($salt)+':'+[Convert]::ToBase64String($derive.GetBytes(32))}finally{$derive.Dispose()}
}
function New-WfiRandomSecret {
    $bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create()
    try{$rng.GetBytes($bytes);return ([BitConverter]::ToString($bytes)).Replace('-','')}finally{$rng.Dispose()}
}
function Set-WfiProtectedDirectory([string]$Path,[string]$ServiceSid='') {
    [void](Assert-WfiSafePath $Path)
    [void][IO.Directory]::CreateDirectory($Path)
    $acl=New-Object Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true,$false)
    foreach($sid in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
    if($ServiceSid){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($ServiceSid,'Modify','ContainerInherit,ObjectInherit','None','Allow'))}
    Set-Acl -LiteralPath $Path -AclObject $acl
}
function Assert-WfiDependency([string]$Path,$Spec) {
    if(!(Test-Path -LiteralPath $Path -PathType Leaf)){throw 'Dependency missing.'}
    [void](Assert-WfiSafePath $Path)
    if((Get-FileHash -LiteralPath $Path -Algorithm $Spec.algorithm).Hash -ne $Spec.hash){throw "Dependency integrity check failed: $($Spec.file)"}
}
function Assert-WfiSetupOwnership([string]$Path) {
    [void](Assert-WfiSafePath $Path)
    $acl=Get-Acl -LiteralPath $Path
    if(!$acl.AreAccessRulesProtected){throw 'Setup directory must have protected permissions.'}
    foreach($rule in $acl.Access) {
        $sid=$rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if($rule.AccessControlType -eq 'Allow' -and $sid -notin @('S-1-5-18','S-1-5-32-544')){throw 'Setup directory has untrusted access permissions.'}
    }
}
function Invoke-WfiExecutable([string]$File,[string[]]$Arguments,[int]$TimeoutSeconds=600) {
    # Caller arguments contain only validated paths/options, never passwords.
    $quoted=@($Arguments | ForEach-Object {if($_ -match '["\r\n]'){throw 'Invalid native argument.'};'"'+$_.TrimEnd('\')+'"'})
    $p=Start-Process -FilePath $File -ArgumentList ($quoted -join ' ') -PassThru -WindowStyle Hidden
    if(!$p.WaitForExit($TimeoutSeconds*1000)){
        # Keep the setup lock while a prerequisite owns Windows Installer/database files.
        # Never launch a second installer over a still-running prerequisite.
        $p.WaitForExit()
    }
    if($p.ExitCode -eq 3010){throw 'REBOOT_REQUIRED: Restart Windows, then run this setup again.'}
    if($p.ExitCode -ne 0){throw "Prerequisite failed with exit code $($p.ExitCode)."}
}
function Expand-WfiDependency([string]$Archive,[string]$Destination,[string]$Prefix='') {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [void](Assert-WfiSafePath $Destination)
    $root=[IO.Path]::GetFullPath($Destination).TrimEnd('\')+'\'
    $zip=[IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        foreach($entry in $zip.Entries) {
            $relative=$entry.FullName.Replace('/','\')
            if($Prefix -and $relative -notmatch '^pgsql\\(bin|lib|share)\\'){continue}
            if(!$entry.Name){continue}
            $path=[IO.Path]::GetFullPath((Join-Path $Destination $relative))
            if(!$path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe archive entry.'}
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$path,$true)
        }
    }finally{$zip.Dispose()}
}
