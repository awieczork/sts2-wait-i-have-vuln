<#
.SYNOPSIS
Copies Slay the Spire 2 progress from a normal save profile into the matching modded profile.

.DESCRIPTION
Backs up the whole Steam account save folder first, then copies progress.save, prefs.save,
their .backup files, and the run history from profile<N>\saves to modded\profile<N>\saves.
Turn off Steam Cloud for the game before running it, or the game replaces the copied files on its next start.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\copy-vanilla-saves.ps1

.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\copy-vanilla-saves.ps1 -ProfileNumber 2
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 3)]
    [int]$ProfileNumber = 1,
    [string]$SavesRoot = (Join-Path $env:APPDATA 'SlayTheSpire2\steam'),
    [string]$SteamId,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
$saveFiles = @('progress.save', 'progress.save.backup', 'prefs.save', 'prefs.save.backup')

if (Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue) {
    throw 'Close Slay the Spire 2 before copying saves.'
}

if (-not (Test-Path -LiteralPath $SavesRoot -PathType Container)) {
    throw "Save folder not found: $SavesRoot"
}

if (-not $SteamId) {
    $accounts = @(Get-ChildItem -LiteralPath $SavesRoot -Directory | Where-Object { $_.Name -match '^\d+$' })
    if ($accounts.Count -ne 1) {
        throw "Found $($accounts.Count) Steam ID folders in $SavesRoot. Run again with -SteamId <id>."
    }
    $SteamId = $accounts[0].Name
}

$accountDir = Join-Path $SavesRoot $SteamId
$source = Join-Path $accountDir "profile$ProfileNumber\saves"
$target = Join-Path $accountDir "modded\profile$ProfileNumber\saves"

if (-not (Test-Path -LiteralPath (Join-Path $source 'progress.save') -PathType Leaf)) {
    throw "No progress.save found in $source."
}

Write-Host "From: $source"
Write-Host "To:   $target"
Write-Host ''
Write-Host 'Steam Cloud for Slay the Spire 2 must be OFF (Steam > right-click the game > Properties > General).'
Write-Host 'Otherwise the game may replace the copied files with the Steam Cloud copies on its next start.'
Write-Host 'Progress and settings in the modded profile will be overwritten; extra run-history files there are kept.'
if (-not $Yes) {
    $answer = Read-Host 'Type yes to continue'
    if ($answer -ne 'yes') {
        Write-Host 'Cancelled. Nothing was changed.'
        return
    }
}

$backupDir = Join-Path (Split-Path -Parent $SavesRoot) ('save-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
Copy-Item -LiteralPath $accountDir -Destination $backupDir -Recurse
Write-Host "Backup:  $backupDir"

New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($name in $saveFiles) {
    $file = Join-Path $source $name
    if (Test-Path -LiteralPath $file -PathType Leaf) {
        Copy-Item -LiteralPath $file -Destination $target -Force
        Write-Host "Copied:  $name"
    }
}

$history = Join-Path $source 'history'
if (Test-Path -LiteralPath $history -PathType Container) {
    $targetHistory = Join-Path $target 'history'
    New-Item -ItemType Directory -Path $targetHistory -Force | Out-Null
    Copy-Item -Path (Join-Path $history '*') -Destination $targetHistory -Recurse -Force
    Write-Host 'Copied:  history'
}

Write-Host ''
Write-Host 'Done. Start the game with mods on and check that your unlocks are there.'
