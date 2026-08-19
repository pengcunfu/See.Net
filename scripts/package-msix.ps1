param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfSign,
    [string]$Version = "1.0.0.0",
    [string]$Publisher = "",
    [string]$PfxPath = "",
    [string]$PfxPassword = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$publishRoot = Join-Path $root "artifacts\publish"
$layout = Join-Path $root "artifacts\msix-layout"
$msixDir = Join-Path $root "artifacts\msix"

function Convert-ToMsixVersion([string]$raw) {
    $trimmed = $raw.Trim().TrimStart("v", "V")
    $parts = @($trimmed -split "\." | Where-Object { $_ -ne "" })
    if ($parts.Count -eq 0) { throw "Invalid version: $raw" }
    while ($parts.Count -lt 4) { $parts += "0" }
    foreach ($p in $parts[0..3]) {
        if ($p -notmatch "^\d+$") { throw "Invalid version: $raw" }
    }
    return ($parts[0..3] -join ".")
}

$Version = Convert-ToMsixVersion $Version
$msix = Join-Path $msixDir "See.Net_${Version}_x64.msix"

$resolvedLayout = [System.IO.Path]::GetFullPath($layout)
$resolvedRoot = [System.IO.Path]::GetFullPath($root)
if (-not $resolvedLayout.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Layout directory is outside the workspace: $resolvedLayout"
}

Write-Host "== 1/5 Publish app =="
dotnet publish (Join-Path $root "See.Net\See.Net.csproj") -c $Configuration -r $Runtime --self-contained true -o (Join-Path $publishRoot "See.Net")
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$publishedExe = Join-Path $publishRoot "See.Net\See.exe"
if (-not (Test-Path $publishedExe)) { throw "Published exe not found: $publishedExe" }

Write-Host "== 2/5 Assemble MSIX layout =="
if (Test-Path $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
New-Item -ItemType Directory -Path $layout | Out-Null
Copy-Item (Join-Path $root "packaging\MSIX\AppxManifest.xml") $layout
Copy-Item (Join-Path $publishRoot "See.Net") (Join-Path $layout "See.Net") -Recurse
Copy-Item (Join-Path $root "packaging\assets") (Join-Path $layout "Assets") -Recurse

$manifestPath = Join-Path $layout "AppxManifest.xml"
$manifest = [System.IO.File]::ReadAllText($manifestPath)
$manifest = [regex]::Replace($manifest, 'Version="[\d.]+"', "Version=`"$Version`"")
if (-not [string]::IsNullOrWhiteSpace($Publisher)) {
    $manifest = [regex]::Replace($manifest, 'Publisher="[^"]+"', "Publisher=`"$Publisher`"")
    Write-Host "Publisher: $Publisher"
}
$manifest = [regex]::Replace($manifest, 'Executable="[^"]+"', 'Executable="See.Net\See.exe"')
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($manifestPath, $manifest, $utf8)
Write-Host "Version: $Version"

Write-Host "== 3/5 Restore SDK packaging tools =="
dotnet restore (Join-Path $root "packaging\MSIX\MSIX.csproj")
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

$toolsRoot = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools"
$sdkVer = Get-ChildItem $toolsRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty Name
$bin = Join-Path $toolsRoot "$sdkVer\bin\10.0.26100.0\x64"
if (-not (Test-Path $bin)) {
    $bin = Get-ChildItem (Join-Path $toolsRoot $sdkVer "bin") -Directory |
        Sort-Object Name -Descending |
        Select-Object -First 1 |
        ForEach-Object { Join-Path $_.FullName "x64" }
}
$makeappx = Join-Path $bin "makeappx.exe"
$signtool = Join-Path $bin "signtool.exe"
if (-not (Test-Path $makeappx)) { throw "makeappx.exe not found: $makeappx" }

Write-Host "== 4/5 Pack =="
if (-not (Test-Path $msixDir)) { New-Item -ItemType Directory -Path $msixDir | Out-Null }
& $makeappx pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

$signed = $false
$cerPath = Join-Path $msixDir "See.Net.cer"

function Export-PublicCer([System.Security.Cryptography.X509Certificates.X509Certificate2]$cert, [string]$path) {
    $bytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    [System.IO.File]::WriteAllBytes($path, $bytes)
    Write-Host "Certificate: $path"
}

if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
    Write-Host "== 5/5 Sign with PFX =="
    if (-not (Test-Path $PfxPath)) { throw "PFX not found: $PfxPath" }
    if (-not (Test-Path $signtool)) { throw "signtool.exe not found: $signtool" }
    & $signtool sign /f $PfxPath /p $PfxPassword /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool sign failed" }
    $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($PfxPath, $PfxPassword)
    Export-PublicCer $cert $cerPath
    $signed = $true
} elseif ($SelfSign) {
    Write-Host "== 5/5 Self-sign =="
    $subject = if ([string]::IsNullOrWhiteSpace($Publisher)) { "CN=See.Net Dev" } else { $Publisher }
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5)
    }
    $pfx = Join-Path $env:TEMP "seenet-dev-$([Guid]::NewGuid().ToString('N')).pfx"
    $password = [Guid]::NewGuid().ToString("N")
    Export-PfxCertificate -Cert $cert -FilePath $pfx -Password (ConvertTo-SecureString $password -AsPlainText -Force) | Out-Null
    & $signtool sign /f $pfx /p $password /fd SHA256 $msix
    $signExit = $LASTEXITCODE
    Remove-Item -LiteralPath $pfx -Force -ErrorAction SilentlyContinue
    if ($signExit -ne 0) { throw "signtool sign failed" }
    Export-PublicCer $cert $cerPath
    Write-Host "Signed. Thumbprint: $($cert.Thumbprint)"
    $signed = $true
} else {
    Write-Host "== 5/5 Unsigned =="
}

Write-Host "MSIX: $msix"
if ($signed) {
    Write-Host "Install: Add-AppxPackage -Path '$msix'"
} else {
    Write-Host "Install (unsigned): Add-AppxPackage -Path '$msix' -AllowUnsigned"
}
