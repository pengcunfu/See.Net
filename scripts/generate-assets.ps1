param(
    [string]$OutputDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "packaging\assets")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

function New-Logo {
    param([int]$Width, [int]$Height, [string]$TargetPath)

    $bmp = [System.Drawing.Bitmap]::new($Width, $Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $radius = [Math]::Max(6, $Width / 8)
    $rect = [System.Drawing.Rectangle]::new(0, 0, $Width - 1, $Height - 1)
    $d = $radius * 2
    $corner = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $corner.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $corner.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $corner.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $corner.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $corner.CloseFigure()

    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, 0, $Width, $Height),
        [System.Drawing.Color]::FromArgb(255, 38, 166, 154),
        [System.Drawing.Color]::FromArgb(255, 30, 60, 114),
        45)
    $g.FillPath($brush, $corner)

    $fontSize = $Width * 0.55
    $font = [System.Drawing.Font]::new("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf = [System.Drawing.StringFormat]::new()
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $textRect = [System.Drawing.RectangleF]::new(0, -$Height * 0.02, $Width, $Height)
    $g.DrawString("S", $font, $white, $textRect, $sf)

    $eyeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(230, 255, 214, 0))
    $eyeSize = [Math]::Max(4, $Width * 0.16)
    $g.FillEllipse($eyeBrush, $Width - $Width * 0.28, $Height * 0.16, $eyeSize, $eyeSize)

    $g.Dispose()
    $bmp.Save($TargetPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "generated $TargetPath"
}

New-Logo -Width 44 -Height 44 -TargetPath (Join-Path $OutputDir "Square44x44Logo.png")
New-Logo -Width 150 -Height 150 -TargetPath (Join-Path $OutputDir "Square150x150Logo.png")
New-Logo -Width 300 -Height 300 -TargetPath (Join-Path $OutputDir "StoreLogo.png")
New-Logo -Width 30 -Height 30 -TargetPath (Join-Path $OutputDir "Square30x30Logo.png")

# exe icon (32x32)
$iconBmp = [System.Drawing.Bitmap]::new(32, 32)
$iconG = [System.Drawing.Graphics]::FromImage($iconBmp)
$iconG.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$iconG.Clear([System.Drawing.Color]::Transparent)
$iconBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.Rectangle]::new(0, 0, 32, 32),
    [System.Drawing.Color]::FromArgb(255, 38, 166, 154),
    [System.Drawing.Color]::FromArgb(255, 30, 60, 114),
    45)
$iconG.FillEllipse($iconBrush, 0, 0, 31, 31)
$iconFont = [System.Drawing.Font]::new("Segoe UI", 18, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$iconSf = [System.Drawing.StringFormat]::new()
$iconSf.Alignment = [System.Drawing.StringAlignment]::Center
$iconSf.LineAlignment = [System.Drawing.StringAlignment]::Center
$iconWhite = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$iconG.DrawString("S", $iconFont, $iconWhite, [System.Drawing.RectangleF]::new(0, 0, 32, 32), $iconSf)
$iconHandle = $iconBmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconHandle)
$icoStream = [System.IO.File]::Create((Join-Path $OutputDir "app.ico"))
$icon.Save($icoStream)
$icoStream.Dispose()
$iconG.Dispose()
$iconBmp.Dispose()
Write-Host "generated app.ico"

# wide banner: square logo centered on transparent 310x150 canvas
$bmp = [System.Drawing.Bitmap]::new(310, 150)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Transparent)
$src = [System.Drawing.Image]::FromFile((Join-Path $OutputDir "Square150x150Logo.png"))
$g.DrawImage($src, 80, 0, 150, 150)
$g.Dispose()
$bmp.Save((Join-Path $OutputDir "Wide310x150Logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$src.Dispose()
Write-Host "generated Wide310x150Logo.png"

Write-Host "assets ready: $OutputDir"
