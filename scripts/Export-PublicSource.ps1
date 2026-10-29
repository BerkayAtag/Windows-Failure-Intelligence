param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$target=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $target){throw 'Public export must use a new directory.'}
New-Item -ItemType Directory -Path $target | Out-Null
Push-Location $root
try {
    $files=& rg --files --hidden --no-require-git -g '!docs/design/**' -g '!AGENTS.md'
    if($LASTEXITCODE -ne 0){throw 'Source inventory failed.'}
    $extensions=@('.cs','.csproj','.props','.slnx','.json','.yaml','.yml','.md','.ps1','.cmd','.sql','.html','.css','.ts','.tsx','.js','.manifest','.conf','.xml','.log','.txt','.png','.svg')
    foreach($relative in $files){
        $normalized=$relative.Replace('\','/')
        if($normalized -notmatch '^(src/|tests/|scripts/|deploy/|docs/|site/|\.github/|README\.md$|history-release\.md$|SECURITY\.md$|Directory.Build.props$|WFI.slnx$|dotnet-tools.json$|Setup-Server.cmd$|\.gitignore$|\.editorconfig$)'){continue}
        if($normalized -match '(^|/)(bin|obj|node_modules|distribution|wwwroot|test-results|playwright-report|TestResults|dist)(/|$)' -or $normalized -match '\.local\.json$|\.tsbuildinfo$'){continue}
        $extension=[IO.Path]::GetExtension($relative)
        if($extension -notin $extensions -and [IO.Path]::GetFileName($relative) -notin @('.gitignore','.editorconfig','.nojekyll')){continue}
        if($extension -eq '.png' -and !$normalized.StartsWith('site/assets/')){continue}
        $out=Join-Path $target $relative;New-Item -ItemType Directory -Force (Split-Path $out -Parent) | Out-Null
        if($extension -eq '.png'){Copy-Item -LiteralPath $relative -Destination $out;continue}
        $text=[IO.File]::ReadAllText((Join-Path $root $relative))
        $text=$text.Replace('example.invalid','example.invalid').Replace('EXAMPLE.INVALID','EXAMPLE.INVALID').Replace('DC=example,DC=invalid','DC=example,DC=invalid').Replace('EXAMPLE','EXAMPLE')
        $text=$text.Replace('C:\WFI-Source','C:\WFI-Source').Replace('C:/WFI-Source','C:/WFI-Source')
        $text=[regex]::Replace($text,'C:\\Users\\berka\\AppData\\Local\\Temp','C:\Temp')
        [IO.File]::WriteAllText($out,$text)
    }
    # Text-only leak gates. Never copy runtime data, keys, private configuration or old screenshots.
    $matches=& rg -l --hidden -i 'BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|aknet\.akb' $target
    if($LASTEXITCODE -eq 0){throw 'Public export leak gate failed; inspect flagged paths locally.'}
    if($LASTEXITCODE -gt 1){throw 'Public export scan failed.'}
    Write-Host "Sanitized public source prepared: $target"
} finally {Pop-Location}
