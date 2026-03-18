param(
    [string]$GameDir,
    [string]$PayloadDir = $(Join-Path (Split-Path -Parent $PSScriptRoot) "dist\installer\payload"),
    [string]$ModId
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Sts2InstallHelpers.ps1")

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ModId)) {
    $manifest = Get-ProjectManifest -ProjectRoot $projectRoot
    $ModId = Resolve-Sts2ModId -Manifest $manifest
}

if ([string]::IsNullOrWhiteSpace($ModId)) {
    throw "Could not resolve ModId."
}

$resolvedGameDir = Resolve-Sts2GameDir -RequestedPath $GameDir
$sourceModDir = Join-Path $PayloadDir $ModId
if (-not (Test-Path -LiteralPath $sourceModDir)) {
    throw "Installer payload not found: $sourceModDir"
}

$modsRoot = Resolve-Sts2ModsRoot -GameDir $resolvedGameDir
$targetModDir = Join-Path $modsRoot $ModId

New-Item -ItemType Directory -Force -Path $targetModDir | Out-Null
Copy-DirectoryContents -SourceDir $sourceModDir -DestinationDir $targetModDir
$legacyManifestPath = Join-Path $targetModDir "mod_manifest.json"
if (Test-Path -LiteralPath $legacyManifestPath) {
    Remove-Item -LiteralPath $legacyManifestPath -Force
}

Write-Host "Detected game dir: $resolvedGameDir"
Write-Host "Installed $ModId to $targetModDir"
