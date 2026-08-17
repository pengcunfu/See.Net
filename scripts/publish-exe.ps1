param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $root "artifacts\single-exe"
$csproj = Join-Path $root "See.Net\See.Net.csproj"

# Safety check: only allow cleaning artifacts under the workspace root
$resolvedOut = [System.IO.Path]::GetFullPath($outDir)
$resolvedRoot = [System.IO.Path]::GetFullPath($root)
if (-not $resolvedOut.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output directory is outside the workspace; aborting: $resolvedOut"
}

Write-Host "== Publishing framework-dependent single-file exe ($Configuration / $Runtime) =="
Write-Host "Output: $outDir"
Write-Host "Note: target machines need .NET 10 Desktop Runtime (windows)."

if (Test-Path $outDir) {
    Remove-Item -LiteralPath $outDir -Recurse -Force
}

dotnet publish $csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $outDir `
    --nologo

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Drop NuGet XML docs to shrink the distribute folder
Get-ChildItem $outDir -Filter "*.xml" -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

$exe = Join-Path $outDir "See.exe"
if (-not (Test-Path $exe)) { throw "Publish output not found: $exe" }

$totalMb = [math]::Round(((Get-ChildItem $outDir -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 2)
$exeMb = [math]::Round((Get-Item $exe).Length / 1MB, 2)

Write-Host ""
Write-Host "Publish succeeded."
Write-Host "  See.exe     : $exeMb MB"
Write-Host "  Total size  : $totalMb MB"
Write-Host "  Path        : $outDir"
Write-Host ""
Write-Host "Distribute See.exe together with the webassets\ folder."
Write-Host "Install .NET 10 Desktop Runtime on target machines:"
Write-Host "  https://dotnet.microsoft.com/download/dotnet/10.0"
