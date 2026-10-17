[CmdletBinding()]
param(
    [string]$GameDir = 'F:\Steam\steamapps\common\Stonewards',
    [string]$ProfileDir = '',
    [string]$OutputDir = '',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoDir 'dist'
}

$project = Join-Path $repoDir 'StonewardsTogglerPlus.csproj'
$releaseFiles = Join-Path $repoDir 'thunderstore'
$core = if ($ProfileDir) {
    Join-Path $ProfileDir 'BepInEx\core'
} else {
    Join-Path $GameDir 'BepInEx\core'
}
$dll = Join-Path $repoDir 'bin\Release\netstandard2.1\StonewardsTogglerPlus.dll'

if (-not (Test-Path -LiteralPath $project)) { throw "Project not found: $project" }
if (-not (Test-Path -LiteralPath (Join-Path $core 'BepInEx.dll'))) { throw "BepInEx not found: $core" }
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Stonewards_Data\Managed\Assembly-CSharp.dll'))) { throw "Game DLL not found: $GameDir" }

if (-not $SkipBuild) {
    Write-Host 'Building the restored v0.1.0 sources...' -ForegroundColor Cyan
    & dotnet build $project -c Release "-p:GameDir=$GameDir" "-p:BepInExCoreDir=$core" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. A release ZIP was not created.' }
}

if (-not (Test-Path -LiteralPath $dll)) { throw "Compiled DLL not found: $dll" }

$manifestPath = Join-Path $releaseFiles 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.version_number -ne '0.1.0') { throw 'Manifest version must be 0.1.0' }

# Reject an accidentally stale experimental class-specific build.
$assemblyBytes = [System.IO.File]::ReadAllBytes($dll)
$utf8Text = [System.Text.Encoding]::UTF8.GetString($assemblyBytes)
if ($utf8Text.Contains('ClassRingRules')) {
    throw 'This DLL contains experimental class-specific rules. Delete bin/obj and rebuild from the restored 0.1.0 sources.'
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('stonewards-tp-release-' + [Guid]::NewGuid().ToString('N'))
$plugins = Join-Path $temp 'plugins'
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
try {
    foreach ($name in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) {
        $from = Join-Path $releaseFiles $name
        if (-not (Test-Path -LiteralPath $from)) { throw "Thunderstore asset missing: $from" }
        Copy-Item -LiteralPath $from -Destination (Join-Path $temp $name)
    }
    Copy-Item -LiteralPath $dll -Destination (Join-Path $plugins 'StonewardsTogglerPlus.dll')

    $icon = [System.Drawing.Image]::FromFile((Join-Path $temp 'icon.png'))
    try {
        if ($icon.Width -ne 256 -or $icon.Height -ne 256) {
            throw 'Thunderstore icon must be exactly 256 x 256 pixels.'
        }
    } finally { $icon.Dispose() }

    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    $output = Join-Path $OutputDir ('TogglerPlus-' + $manifest.version_number + '.zip')
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
    Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $output -CompressionLevel Optimal
    $files = [System.IO.Compression.ZipFile]::OpenRead($output)
    try {
        $names = @($files.Entries | ForEach-Object { $_.FullName.Replace('\\','/') })
        foreach ($required in @('manifest.json', 'README.md', 'icon.png', 'plugins/StonewardsTogglerPlus.dll')) {
            if ($names -notcontains $required) { throw "Generated ZIP missing: $required" }
        }
    } finally { $files.Dispose() }
    Write-Host 'Thunderstore package created successfully:' -ForegroundColor Green
    Write-Host $output
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
