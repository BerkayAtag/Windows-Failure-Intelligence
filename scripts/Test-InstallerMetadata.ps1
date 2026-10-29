$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'InstallerSupport.ps1')
$root=Join-Path ([IO.Path]::GetTempPath()) ('wfi-installer-test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    foreach($product in @('Agent','Server')) {
        $properties=Get-WfiUninstallProperties $product $root
        if($properties.Publisher -ne ('Berkay Ata'+[char]0x011F) -or $properties.DisplayName -ne "WFI $product"){throw 'Publisher/product mismatch'}
        if($properties.UninstallString -ne ('"'+(Join-Path $root 'WFI-Uninstall.exe')+'"')){throw 'Uninstaller must be quoted.'}
        if($properties.DisplayIcon -ne (Join-Path $root 'wfi.ico')){throw 'Icon must be the unquoted installed icon path.'}
        if($properties.DisplayVersion -ne '0.7.0' -or $properties.URLInfoAbout -notlike 'https://*'){throw 'Version/support metadata mismatch.'}
    }
    & {
        $writes=@{}
        function New-Item { param($Path,[switch]$Force) if($Path -notlike 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WFI.*'){throw 'Unexpected registration key'} }
        function New-ItemProperty { param($LiteralPath,$Name,$Value,$PropertyType,[switch]$Force) $writes[$Name]=@{Value=$Value;Type=$PropertyType} }
        Register-WfiInstallation 'Agent' $root
        if($writes.Publisher.Value -ne ('Berkay Ata'+[char]0x011F) -or $writes.NoModify.Type -ne 'DWord' -or $writes.DisplayIcon.Type -ne 'String' -or $writes.EstimatedSize.Type -ne 'DWord' -or $writes.InstallDate.Value -notmatch '^\d{8}$'){throw 'Registration values or types incorrect.'}
        $saved=Get-Content -LiteralPath (Join-Path $root 'wfi-installation.json') -Raw | ConvertFrom-Json
        if($saved.Product -ne 'Agent' -or $saved.InstallPath -ne $root){throw 'Installation manifest mismatch.'}
    }
    if(!(Test-WfiRetainedAgentInstallation $root)){throw 'Retained agent cannot be restored.'}
    [IO.File]::WriteAllText((Join-Path $root 'unrelated.txt'),'synthetic')
    if(Test-WfiRetainedAgentInstallation $root){throw 'Unrelated files accepted as retained installation.'}
    Remove-Item -LiteralPath (Join-Path $root 'unrelated.txt')
    foreach($paths in @(@((Join-Path $root 'same'),(Join-Path $root 'same')),@((Join-Path $root 'parent'),(Join-Path $root 'parent/child')))) {
        $rejected=$false;try{Assert-WfiDistinctDirectories $paths}catch{$rejected=$true};if(!$rejected){throw 'Overlapping storage/key paths accepted.'}
    }
    Assert-WfiDistinctDirectories @((Join-Path $root 'app'),(Join-Path $root 'evidence'),(Join-Path $root 'keys'))
    foreach($bad in @('relative','C:\','\\synthetic\share')) {
        $rejected=$false;try{[void](Assert-WfiSafePath $bad)}catch{$rejected=$true};if(!$rejected){throw 'Unsafe root accepted'}
    }
    $target=Join-Path $root 'target';New-Item -ItemType Directory $target | Out-Null
    $link=Join-Path $root 'junction';New-Item -ItemType Junction -Path $link -Target $target | Out-Null
    $rejected=$false;try{[void](Assert-WfiSafePath (Join-Path $link 'not-created-yet'))}catch{$rejected=$true};if(!$rejected){throw 'Reparse ancestor accepted.'}
    # Remove only the known junction itself before recursive cleanup of the verified synthetic root.
    [IO.Directory]::Delete($link)
    foreach($script in @('InstallerSupport.ps1','Install-Agent.ps1','Install-Server.ps1','Uninstall-Wfi.ps1')) {
        $tokens=$null;$errors=$null;[void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $script),[ref]$tokens,[ref]$errors)
        if($errors.Count){throw "Syntax errors: $script"}
    }
    Write-Host 'Installer metadata/path tests passed: products, publisher, icon path/quoted uninstaller, unsafe roots and junction ancestor; no services/registry modified.'
} finally {
    if((Split-Path $root -Parent) -eq ([IO.Path]::GetTempPath()).TrimEnd('\') -and (Split-Path $root -Leaf).StartsWith('wfi-installer-test-')){Remove-Item -LiteralPath $root -Recurse -Force}
}
