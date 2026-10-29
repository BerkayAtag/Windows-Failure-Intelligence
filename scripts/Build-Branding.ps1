$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$assets=Join-Path $root 'artifacts/branding'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
Add-Type -AssemblyName System.Drawing
# Original WFI pulse mark, built at Windows icon sizes; no external brand asset.
$frames=@()
foreach($size in @(16,32,48,64,256)) {
    $bitmap=[Drawing.Bitmap]::new($size,$size)
    $g=[Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([Drawing.Color]::FromArgb(227,6,19))
    $pen=[Drawing.Pen]::new([Drawing.Color]::White,[single]($size*0.065))
    $pen.LineJoin=[Drawing.Drawing2D.LineJoin]::Round
    $points=@(@(.12,.51),@(.31,.51),@(.4,.25),@(.56,.76),@(.67,.44),@(.77,.51),@(.88,.51)) | ForEach-Object { [Drawing.PointF]::new([single]($_[0]*$size),[single]($_[1]*$size)) }
    $g.DrawLines($pen,[Drawing.PointF[]]$points)
    $ms=[IO.MemoryStream]::new();$bitmap.Save($ms,[Drawing.Imaging.ImageFormat]::Png)
    $frames+=,@{Size=$size;Bytes=$ms.ToArray()}
    $ms.Dispose();$pen.Dispose();$g.Dispose();$bitmap.Dispose()
}
$stream=[IO.File]::Create((Join-Path $assets 'wfi.ico'));$writer=[IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
    $offset=6+16*$frames.Count
    foreach($frame in $frames){$edge=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$edge);$writer.Write([byte]$edge);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
    foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
} finally {$writer.Dispose();$stream.Dispose()}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler '/nologo' '/target:winexe' '/platform:x64' '/reference:System.Windows.Forms.dll' "/win32icon:$assets/wfi.ico" "/win32manifest:$root/src/WFI.Agent.Setup/app.manifest" "/out:$assets/WFI-Uninstall.exe" (Join-Path $PSScriptRoot 'UninstallBootstrap.cs')
if($LASTEXITCODE -ne 0){throw 'Uninstaller build failed.'}
