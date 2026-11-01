# Publishes the desktop app and signs the published files (SEC-16).
# Usage: powershell -File scripts/publish_release.ps1 [-Out publish] [-Key config/release-signing.key] [-Version 1.0.0]
param(
    [string]$Out = "publish",
    [string]$Key = "config/release-signing.key",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Key)) { throw "Signing key not found: $Key (the offline key is not in git)" }
if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }

dotnet publish src/Reports.Desktop -c Release -o $Out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

python scripts/sign_release.py --dir $Out --key $Key --version $Version
if ($LASTEXITCODE -ne 0) { throw "signing failed" }

Write-Output "Signed release is in $Out"
