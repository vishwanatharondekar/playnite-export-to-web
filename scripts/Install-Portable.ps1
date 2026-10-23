param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repoRoot "PlayniteLibraryExporter\bin\$Configuration"
$destination = Join-Path $repoRoot "Playnite\Extensions\PlayniteLibraryWeb_6470f7f6-0f8e-4f44-a3f9-86c1cc82028f"

if (-not (Test-Path -LiteralPath $source)) {
    throw "Build output not found: $source"
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $destination -Recurse -Force

Write-Host "Installed Playnite Library Web to:"
Write-Host $destination
