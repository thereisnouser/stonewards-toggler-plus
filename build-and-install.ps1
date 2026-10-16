[CmdletBinding()]
param(
    [string]$GameDir = 'F:\Steam\steamapps\common\Stonewards',
    [string]$ProfileDir = ''
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$managedDir = Join-Path $GameDir 'Stonewards_Data\Managed'
$bepInExDir = if ($ProfileDir) { Join-Path $ProfileDir 'BepInEx' } else { Join-Path $GameDir 'BepInEx' }
$coreDir = Join-Path $bepInExDir 'core'

foreach ($file in @(
    (Join-Path $managedDir 'Assembly-CSharp.dll'),
    (Join-Path $managedDir 'UnityEngine.UIElementsModule.dll'),
    (Join-Path $coreDir 'BepInEx.dll'),
    (Join-Path $coreDir '0Harmony.dll')
)) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Required dependency not found: $file" }
}

$project = Join-Path $repoDir 'StonewardsTogglerPlus.csproj'
Write-Host 'Building Stonewards Toggler Plus...'
& dotnet build $project -c Release "-p:GameDir=$GameDir" "-p:BepInExCoreDir=$coreDir" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed; no plugin was installed.' }

$dll = Join-Path $repoDir 'bin\Release\netstandard2.1\StonewardsTogglerPlus.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw "Compiled DLL not found: $dll" }
$dest = Join-Path $bepInExDir 'plugins\StonewardsTogglerPlus'
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $dest 'StonewardsTogglerPlus.dll') -Force
Write-Host "Installed Stonewards Toggler Plus at $dest" -ForegroundColor Green
Write-Host 'Launch the game with this r2modman profile. Check BepInEx/LogOutput.log for Toggler Plus logs.'
