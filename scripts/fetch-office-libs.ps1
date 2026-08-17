# 拉取 Office 网页预览所需的第三方 JS/CSS 库到 See.Net/webassets/lib。
# 固定版本 + SHA-256 校验，幂等：文件已存在且 hash 一致则跳过。
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$libDir = Join-Path $root "See.Net\webassets\lib"
New-Item -ItemType Directory -Force -Path $libDir | Out-Null

$files = @(
    @{ Name = "mammoth.browser.min.js"; Url = "https://cdnjs.cloudflare.com/ajax/libs/mammoth/1.12.1/mammoth.browser.min.js"; Hash = "0180991546A6DAB1E03E387E8273CD7E8A74957DFB6B3EAE7C66A7D8DE7F5926" },
    @{ Name = "xlsx.full.min.js";        Url = "https://cdn.sheetjs.com/xlsx-0.20.3/package/dist/xlsx.full.min.js";          Hash = "CC015130AA8521E7F088F88898EBA949CCDCBFB38DF0BD129B44B7273C3A6F41" },
    @{ Name = "pptxjs.min.js";           Url = "https://cdn.jsdelivr.net/gh/meshesha/PPTXjs@v1.21.1/js/pptxjs.min.js";      Hash = "845555EC4179F557F0B78822BAEFFBAA6AA14C303EAABA1DEF7F608367EACA46" },
    @{ Name = "filereader.js";           Url = "https://cdn.jsdelivr.net/gh/meshesha/PPTXjs@v1.21.1/js/filereader.js";      Hash = "96DF9B7A2C5801E64FBF4917EA8D10167E22B90DE17767352E286E8202737079" },
    @{ Name = "jszip.min.js";            Url = "https://cdn.jsdelivr.net/gh/meshesha/PPTXjs@v1.21.1/js/jszip.min.js";       Hash = "215FB2537B13D82DAABD46E1EE59FFE4DCE90ABD0ACB0AC5432E77071F422E9C" },
    @{ Name = "jquery-1.11.3.min.js";    Url = "https://cdn.jsdelivr.net/gh/meshesha/PPTXjs@v1.21.1/js/jquery-1.11.3.min.js"; Hash = "ECB916133A9376911F10BC5C659952EB0031E457F5DF367CDE560EDBFBA38FB8" },
    @{ Name = "pptxjs.css";              Url = "https://cdn.jsdelivr.net/gh/meshesha/PPTXjs@v1.21.1/css/pptxjs.css";        Hash = "C488FB0EC604387BB2A6B8CC8F1E2427DC6E24B2E38CF239B38B10A480195ED3" }
)

foreach ($f in $files) {
    $dest = Join-Path $libDir $f.Name
    if (Test-Path $dest) {
        $existing = (Get-FileHash $dest -Algorithm SHA256).Hash
        if ($existing -eq $f.Hash) {
            Write-Host "OK      $($f.Name)（已存在，hash 一致）"
            continue
        }
        Write-Warning "$($f.Name) hash 不一致，重新下载"
        Remove-Item $dest
    }

    Write-Host "下载    $($f.Name) ..."
    Invoke-WebRequest -Uri $f.Url -OutFile $dest -UseBasicParsing
    $actual = (Get-FileHash $dest -Algorithm SHA256).Hash
    if ($actual -ne $f.Hash) {
        Remove-Item $dest
        throw "$($f.Name) SHA-256 校验失败：期望 $($f.Hash)，实际 $actual"
    }
    Write-Host "OK      $($f.Name)"
}

Write-Host "全部 Office 渲染库就绪：$libDir"
