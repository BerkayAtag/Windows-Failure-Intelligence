function Resolve-WfiDotnetSdk {
    $installed = @(Get-Command dotnet -CommandType Application -All -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
    $candidates = @($installed) + @(Join-Path $env:LOCALAPPDATA 'WFI/dotnet/dotnet.exe')
    foreach ($root in @($env:DOTNET_ROOT, $env:DOTNET_ROOT_X64, (Join-Path $env:ProgramFiles 'dotnet'), (Join-Path $env:USERPROFILE '.dotnet'))) {
        if ($root) { $candidates += Join-Path $root 'dotnet.exe' }
    }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        $sdks = @(& $candidate --list-sdks 2>$null)
        if ($LASTEXITCODE -eq 0 -and ($sdks | Where-Object { $_ -match '^10\.' })) { return $candidate }
    }
    throw '.NET 10 SDK bulunamadı. Runtime tek başına build veya dotnet run için yeterli değildir.'
}
