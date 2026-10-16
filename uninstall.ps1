[CmdletBinding()]
param(
    [string]$GameDir = 'F:\Steam\steamapps\common\Stonewards',
    [string]$ProfileDir = '',
    [switch]$RemoveDiagnostics
)
$ErrorActionPreference = 'Stop'
$root = if ($ProfileDir) { $ProfileDir } else { $GameDir }
$plugin = Join-Path $root 'BepInEx\plugins\StonewardsTogglerPlus'
if (Test-Path -LiteralPath $plugin) {
    Remove-Item -LiteralPath $plugin -Force -Recurse
    Write-Host 'Removed Stonewards Toggler Plus.' -ForegroundColor Green
} else {
    Write-Host 'Toggler Plus was not installed in this profile.'
}
if ($RemoveDiagnostics) {
    $diagnostics = Join-Path $root 'BepInEx\plugins\StonewardsTogglerDiagnostics'
    if (Test-Path -LiteralPath $diagnostics) {
        Remove-Item -LiteralPath $diagnostics -Force -Recurse
        Write-Host 'Removed the optional diagnostic plugin.' -ForegroundColor Green
    }
}
Write-Host 'Game saves, ShopData and diagnostic reports have not been changed.'
