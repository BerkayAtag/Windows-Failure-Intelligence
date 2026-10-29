param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$target=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $target){throw 'Public export must use a new directory.'}
New-Item -ItemType Directory -Path $target | Out-Null
Push-Location $root
try {
    $useRg=[bool](Get-Command rg -CommandType Application -ErrorAction SilentlyContinue)
    if($useRg){
        $files=& rg --files --hidden --no-require-git -g '!docs/design/**' -g '!AGENTS.md'
        if($LASTEXITCODE -ne 0){throw 'Source inventory failed.'}
    } else {
        # Fallback without ripgrep: walk only exportable roots and never descend into build/dependency folders.
        $skip='^(bin|obj|node_modules|distribution|wwwroot|test-results|playwright-report|TestResults|dist|\.data)$'
        function Get-ExportFiles([string]$Path){
            foreach($item in Get-ChildItem -LiteralPath $Path -Force){
                if($item.PSIsContainer){if($item.Name -notmatch $skip){Get-ExportFiles $item.FullName}}
                else{$item.FullName.Substring($root.Length+1)}
            }
        }
        $files=@(foreach($entry in Get-ChildItem -LiteralPath $root -Force){
            if($entry.PSIsContainer){if($entry.Name -in @('src','tests','scripts','deploy','docs','site','.github')){Get-ExportFiles $entry.FullName}}
            else{$entry.Name}
        }) | Where-Object { $_.Replace('\','/') -notlike 'docs/design/*' -and $_ -ne 'AGENTS.md' }
    }
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
        # Windows PowerShell 5.1 reads BOM-less scripts with the ANSI code page; keep UTF-8 BOM for Turkish setup text.
        if($extension -eq '.ps1'){[IO.File]::WriteAllText($out,$text,[Text.UTF8Encoding]::new($true))}else{[IO.File]::WriteAllText($out,$text)}
    }
    # Text-only leak gates. Never copy runtime data, keys, private configuration or old screenshots.
    $pattern='BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|aknet\.akb'
    if($useRg){
        $matches=& rg -l --hidden -i $pattern $target
        if($LASTEXITCODE -eq 0){throw 'Public export leak gate failed; inspect flagged paths locally.'}
        if($LASTEXITCODE -gt 1){throw 'Public export scan failed.'}
    } elseif(@(Get-ChildItem -LiteralPath $target -Recurse -File -Force | Where-Object Extension -ne '.png' | Select-String -Pattern $pattern -List).Count -gt 0) {
        throw 'Public export leak gate failed; inspect flagged paths locally.'
    }
    Write-Host "Sanitized public source prepared: $target"
} finally {Pop-Location}
