param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfSign,
    [string]$Version = "1.0.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$publishRoot = Join-Path $root "artifacts\publish"
$layout = Join-Path $root "artifacts\msix-layout"
$msixDir = Join-Path $root "artifacts\msix"
$msix = Join-Path $msixDir "See.Net_${Version}_x64.msix"

# 安全校验：递归删除仅限工作区内的 artifacts 目录
$resolvedLayout = [System.IO.Path]::GetFullPath($layout)
$resolvedRoot = [System.IO.Path]::GetFullPath($root)
if (-not $resolvedLayout.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "布局目录超出工作区范围，已中止：$resolvedLayout"
}

Write-Host "== 1/5 发布应用 =="
dotnet publish (Join-Path $root "See.Net\See.Net.csproj") -c $Configuration -r $Runtime --self-contained true -o (Join-Path $publishRoot "See.Net")
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }

Write-Host "== 2/5 组装 MSIX 布局 =="
if (Test-Path $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
New-Item -ItemType Directory -Path $layout | Out-Null
Copy-Item (Join-Path $root "packaging\MSIX\AppxManifest.xml") $layout
Copy-Item (Join-Path $publishRoot "See.Net") (Join-Path $layout "See.Net") -Recurse
Copy-Item (Join-Path $root "packaging\assets") (Join-Path $layout "Assets") -Recurse

Write-Host "== 3/5 还原 SDK 打包工具 =="
dotnet restore (Join-Path $root "packaging\MSIX\MSIX.csproj")
if ($LASTEXITCODE -ne 0) { throw "dotnet restore 失败" }

$toolsRoot = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools"
$ver = Get-ChildItem $toolsRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty Name
$bin = Join-Path $toolsRoot "$ver\bin\10.0.26100.0\x64"
$makeappx = Join-Path $bin "makeappx.exe"
$signtool = Join-Path $bin "signtool.exe"
if (-not (Test-Path $makeappx)) { throw "未找到 makeappx.exe：$makeappx" }

Write-Host "== 4/5 打包 =="
if (-not (Test-Path $msixDir)) { New-Item -ItemType Directory -Path $msixDir | Out-Null }
& $makeappx pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx 打包失败" }

if ($SelfSign) {
    Write-Host "== 5/5 自签名 =="
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq "CN=See.Net Dev" -and $_.HasPrivateKey } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=See.Net Dev" -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(3)
    }
    $pfx = Join-Path $env:TEMP "seenet-dev-$([Guid]::NewGuid().ToString('N')).pfx"
    $password = [Guid]::NewGuid().ToString("N")
    Export-PfxCertificate -Cert $cert -FilePath $pfx -Password (ConvertTo-SecureString $password -AsPlainText -Force) | Out-Null
    & $signtool sign /f $pfx /p $password /fd SHA256 $msix
    $signExit = $LASTEXITCODE
    Remove-Item -LiteralPath $pfx -Force -ErrorAction SilentlyContinue
    if ($signExit -ne 0) { throw "signtool 签名失败" }
    Write-Host "已签名，证书指纹：$($cert.Thumbprint)"
} else {
    Write-Host "== 5/5 未签名（如需安装请使用 Add-AppxPackage -AllowUnsigned） =="
}

Write-Host "MSIX 包已生成：$msix"
Write-Host "安装示例：Add-AppxPackage -Path '$msix' -AllowUnsigned"
