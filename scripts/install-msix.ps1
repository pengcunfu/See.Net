# Install the self-signed See.Net MSIX (run as Administrator).
# Place this script next to See.Net.cer and the .msix file.

#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $dir) { $dir = Get-Location }

$cer = Join-Path $dir "See.Net.cer"
$msix = Get-ChildItem $dir -Filter "See.Net_*.msix" | Sort-Object Name -Descending | Select-Object -First 1

if (-not (Test-Path $cer)) { throw "Certificate not found: $cer" }
if (-not $msix) { throw "No See.Net_*.msix found in $dir" }

Write-Host "Importing certificate to TrustedPeople..."
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

Write-Host "Installing $($msix.Name)..."
Add-AppxPackage -Path $msix.FullName

Write-Host "Done."
