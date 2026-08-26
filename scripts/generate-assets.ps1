param(
    [string]$OutputDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "packaging\assets")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

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

Write-Host "assets ready: $OutputDir"
