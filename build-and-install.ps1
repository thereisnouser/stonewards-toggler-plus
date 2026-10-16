[CmdletBinding()]
param(
    [string]$GameDir = 'F:\Steam\steamapps\common\Stonewards',
    [string]$ProfileDir = ''
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$managedDir = Join-Path $GameDir 'Stonewards_Data\Managed'
$bepInExDir = if ($ProfileDir) { Join-Path $ProfileDir 'BepInEx' } else { Join-Path $GameDir 'BepInEx' }
if (-not (Test-Path (Join-Path $managedDir 'Assembly-CSharp.dll'))) { throw "Game Assembly-CSharp.dll not found: $managedDir" }
if (-not (Test-Path (Join-Path $bepInExDir 'core\BepInEx.dll'))) { throw "BepInEx.dll not found in $bepInExDir" }

$pluginProject = Join-Path $repoDir 'StonewardsTogglerDiagnostics.csproj'
$gameArg = "-p:GameDir=$GameDir"
$coreArg = "-p:BepInExCoreDir=$(Join-Path $bepInExDir 'core')"
& dotnet build $pluginProject -c Release $gameArg $coreArg --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed: nothing installed.' }
$dll = Join-Path $repoDir 'bin\Release\netstandard2.1\StonewardsTogglerDiagnostics.dll'
$dest = Join-Path $bepInExDir 'plugins\StonewardsTogglerDiagnostics'
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $dest 'StonewardsTogglerDiagnostics.dll') -Force
Write-Host "Installed diagnostics at $dest" -ForegroundColor Green
Write-Host "Run the game and retrieve BepInEx\config\StonewardsTogglerDiagnostics.upgrades.txt from the active profile."
