[CmdletBinding()]
param(
    [string]$GameDir = 'F:\Steam\steamapps\common\Stonewards',
    [string]$ProfileDir = ''
)
$root = if ($ProfileDir) { $ProfileDir } else { $GameDir }
$plugin = Join-Path $root 'BepInEx\plugins\StonewardsTogglerDiagnostics'
if (Test-Path -LiteralPath $plugin) { Remove-Item -LiteralPath $plugin -Force -Recurse }
Write-Host 'Diagnostic plugin removed. Reports are kept under BepInEx\config.'
