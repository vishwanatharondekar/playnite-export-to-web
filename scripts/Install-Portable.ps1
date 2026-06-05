param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repoRoot "PlayniteLibraryExporter\bin\$Configuration"
$destination = Join-Path $repoRoot "Playnite\Extensions\PlayniteLibraryExporter_7d089a0e-b862-44df-bca8-df3dc13165ee"

if (-not (Test-Path -LiteralPath $source)) {
    throw "Build output not found: $source"
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $destination -Recurse -Force

Write-Host "Installed Playnite Library Exporter to:"
Write-Host $destination
