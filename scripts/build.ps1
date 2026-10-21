<#
.SYNOPSIS
  Builds the Extractor (a developer tool), installs it into BepInEx\plugins and switches the BepInEx console window + log file ON.
  -Remove takes it out again and switches the console + log OFF.

.EXAMPLE
  .\scripts\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Dice-Kingdoms"   # first time: remembers the path
  .\scripts\build.ps1
  .\scripts\build.ps1 -Remove
#>
param(
    [string]$GameDir,
    [switch]$Remove,
    [switch]$Run
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "common.ps1")
$GameDir = Get-GameDir $GameDir $root
$plugins = Join-Path $GameDir "BepInEx\plugins"

if ($Remove) {
    $f = Join-Path $plugins "DiceModders.Extractor.dll"
    if (Test-Path $f) { Remove-Item $f -Force; Write-Host "  removed DiceModders.Extractor.dll" -ForegroundColor Yellow }
    Set-BepInExLogging $GameDir $false
    return
}

Initialize-Links $root $GameDir
if (Get-Process -Name "Dice-Kingdoms" -ErrorAction SilentlyContinue) {
    Write-Warning "The game is running: Windows may refuse to overwrite loaded DLLs. Close the game, then run this again."
}
Push-Location $root
try { Build-Mod $root "Debug" } finally { Pop-Location }
Install-Mod $root "Debug" $plugins
Set-BepInExLogging $GameDir $true
Write-Host "Done. In the game hold Right Ctrl and press U / I / F11 (see README). Remove it again with  .\scripts\build.ps1 -Remove" -ForegroundColor Green
if ($Run) { Start-Process -FilePath (Join-Path $GameDir "Dice-Kingdoms.exe") -WorkingDirectory $GameDir }
