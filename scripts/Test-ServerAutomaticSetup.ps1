$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ServerSetupSupport.ps1')
function Reject([scriptblock]$Action){$rejected=$false;try{& $Action}catch{$rejected=$true};if(!$rejected){throw 'Unsafe setup input was accepted.'}}
$options=@{Hostname='wfi.synthetic.invalid';Domain='synthetic.invalid';InstallPath='C:\Wfi-Synthetic-App';DataPath='C:\Wfi-Synthetic-Data';PasswordHash=(New-WfiPasswordHash 'Synthetic-only-password!');CertificateThumbprint=''}
Assert-WfiSetupOptions $options
Reject {New-WfiPasswordHash 'short'}
$options.Hostname='wfi;injection';Reject {Assert-WfiSetupOptions $options};$options.Hostname='wfi.synthetic.invalid'
$options.Domain='-invalid.example';Reject {Assert-WfiSetupOptions $options};$options.Domain='synthetic.invalid'
$options.DataPath='C:\Wfi-Synthetic-App\db';Reject {Assert-WfiSetupOptions $options};$options.DataPath='C:\Wfi-Synthetic-Data'
$options.DataPath='C:relative';Reject {Assert-WfiSetupOptions $options};$options.DataPath='C:\Wfi-Synthetic-Data'
$options.DataPath='C:\Wfi[bad]';Reject {Assert-WfiSetupOptions $options};$options.DataPath='C:\Wfi-Synthetic-Data'
$options.CertificateThumbprint='invalid';Reject {Assert-WfiSetupOptions $options};$options.CertificateThumbprint=''
$options.PasswordHash='pbkdf2-sha256:1:AA==:AA==';Reject {Assert-WfiSetupOptions $options}
$port=Get-WfiAvailablePostgresPort
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
try{$listener.Server.ExclusiveAddressUse=$true;$listener.Start();if((Get-WfiAvailablePostgresPort) -eq $port){throw 'Occupied database port was reused.'}}finally{$listener.Stop()}
$root=Join-Path ([IO.Path]::GetTempPath()) ('wfi-auto-test-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
try {
    $file=Join-Path $root 'dependency.bin';[IO.File]::WriteAllText($file,'synthetic package')
    $spec=@{file='dependency.bin';algorithm='SHA256';hash=(Get-FileHash $file).Hash}
    Assert-WfiDependency $file $spec
    [IO.File]::WriteAllText($file,'tampered package');Reject {Assert-WfiDependency $file $spec}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipPath=Join-Path $root 'unsafe.zip';$zip=[IO.Compression.ZipFile]::Open($zipPath,'Create')
    try{$entry=$zip.CreateEntry('../outside.txt');$writer=New-Object IO.StreamWriter($entry.Open());$writer.Write('synthetic');$writer.Dispose()}finally{$zip.Dispose()}
    Reject {Expand-WfiDependency $zipPath (Join-Path $root 'target')}
    if(Test-Path (Join-Path $root 'outside.txt')){throw 'Archive escaped extraction root.'}
    foreach($script in @('Setup-Server.ps1','Install-ServerAutomatic.ps1','ServerSetupSupport.ps1','Uninstall-Wfi.ps1')){
        $tokens=$null;$errors=$null;[void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $script),[ref]$tokens,[ref]$errors)
        if($errors.Count){throw "Invalid script syntax: $script"}
    }
    $specs=Get-Content (Join-Path $PSScriptRoot 'server-dependencies.json') -Raw | ConvertFrom-Json
    foreach($entry in $specs.PSObject.Properties){$s=$entry.Value;if(([uri]$s.url).Scheme -ne 'https' -or $s.hash -notmatch '^[a-fA-F0-9]{64}([a-fA-F0-9]{64})?$'){throw 'Unpinned dependency.'}}
    Write-Host 'Automatic setup regressions passed: credentials, hostname/domain, paths, certificate, tampered dependency, archive traversal and script syntax. No services or registry changed.'
}finally{
    if((Split-Path $root -Parent) -eq ([IO.Path]::GetTempPath()).TrimEnd('\') -and (Split-Path $root -Leaf).StartsWith('wfi-auto-test-')){Remove-Item -LiteralPath $root -Recurse -Force}
}
