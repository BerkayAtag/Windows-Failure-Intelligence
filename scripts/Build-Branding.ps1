$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$assets=Join-Path $root 'artifacts/branding'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
Add-Type -AssemblyName System.Drawing
# Original WFI pulse mark (same geometry as site/assets/wfi.svg): red rounded tile, white pulse, transparent corners.
$frames=@()
foreach($size in @(16,24,32,48,64,128,256)) {
    $bitmap=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([Drawing.Color]::Transparent)
    $radius=[single][Math]::Max(2,$size*12/64);$edge=[single]($size-0.5)
    $tile=[Drawing.Drawing2D.GraphicsPath]::new()
    $tile.AddArc(0,0,$radius*2,$radius*2,180,90);$tile.AddArc($edge-$radius*2,0,$radius*2,$radius*2,270,90)
    $tile.AddArc($edge-$radius*2,$edge-$radius*2,$radius*2,$radius*2,0,90);$tile.AddArc(0,$edge-$radius*2,$radius*2,$radius*2,90,90);$tile.CloseFigure()
    $brush=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.PointF]::new(0,0),[Drawing.PointF]::new(0,$size),[Drawing.Color]::FromArgb(240,22,36),[Drawing.Color]::FromArgb(196,0,18))
    $g.FillPath($brush,$tile)
    $pen=[Drawing.Pen]::new([Drawing.Color]::White,[single][Math]::Max(1.6,$size*4/64))
    $pen.LineJoin=[Drawing.Drawing2D.LineJoin]::Round;$pen.StartCap=[Drawing.Drawing2D.LineCap]::Round;$pen.EndCap=[Drawing.Drawing2D.LineCap]::Round
    $points=@(@(8,33),@(20,33),@(26,16),@(36,49),@(43,28),@(50,33),@(56,33)) | ForEach-Object { [Drawing.PointF]::new([single]($_[0]*$size/64),[single]($_[1]*$size/64)) }
    $g.DrawLines($pen,[Drawing.PointF[]]$points)
    $ms=[IO.MemoryStream]::new();$bitmap.Save($ms,[Drawing.Imaging.ImageFormat]::Png)
    $frames+=,@{Size=$size;Bytes=$ms.ToArray()}
    if($size -eq 256){[IO.File]::WriteAllBytes((Join-Path $assets 'wfi-256.png'),$ms.ToArray())}
    $ms.Dispose();$pen.Dispose();$brush.Dispose();$tile.Dispose();$g.Dispose();$bitmap.Dispose()
}
$stream=[IO.File]::Create((Join-Path $assets 'wfi.ico'));$writer=[IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
    $offset=6+16*$frames.Count
    foreach($frame in $frames){$edge=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$edge);$writer.Write([byte]$edge);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
    foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
} finally {$writer.Dispose();$stream.Dispose()}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler '/nologo' '/codepage:65001' '/target:winexe' '/platform:x64' '/reference:System.Windows.Forms.dll' "/win32icon:$assets/wfi.ico" "/win32manifest:$root/src/WFI.Agent.Setup/app.manifest" "/out:$assets/WFI-Uninstall.exe" (Join-Path $PSScriptRoot 'UninstallBootstrap.cs')
if($LASTEXITCODE -ne 0){throw 'Uninstaller build failed.'}
