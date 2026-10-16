[CmdletBinding()]
param(
    [string]$GameDir = 'F:\Steam\steamapps\common\Stonewards',
    [string]$ProfileDir = '',
    [string]$OutputDir = (Join-Path $PSScriptRoot 'out')
)

$ErrorActionPreference = 'Stop'
$gameAssembly = Join-Path $GameDir 'Stonewards_Data\Managed\Assembly-CSharp.dll'
if (-not (Test-Path -LiteralPath $gameAssembly)) {
    throw "Assembly-CSharp.dll not found: $gameAssembly"
}

$bepInExRoot = if ($ProfileDir) { Join-Path $ProfileDir 'BepInEx' } else { Join-Path $GameDir 'BepInEx' }
$cecilCandidates = @(
    (Join-Path $bepInExRoot 'core\Mono.Cecil.dll'),
    (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
)
$cecil = @($cecilCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1)
if ($cecil.Count -gt 0) { $cecil = $cecil[0] } else { $cecil = $null }
if (-not $cecil -and (Test-Path -LiteralPath $bepInExRoot)) {
    $cecil = Get-ChildItem -LiteralPath $bepInExRoot -Filter 'Mono.Cecil.dll' -Recurse -File -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty FullName -First 1
}
if (-not $cecil) {
    throw 'Mono.Cecil.dll was not found in BepInEx. Install/enable BepInEx 5 in this game profile first.'
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
Add-Type -Path $cecil
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameAssembly)

function Expand-Types($topTypes) {
    foreach ($t in $topTypes) {
        $t
        if ($t.HasNestedTypes) { Expand-Types $t.NestedTypes }
    }
}
function Method-Signature($method) {
    $args = @($method.Parameters | ForEach-Object { "$($_.ParameterType.FullName) $($_.Name)" }) -join ', '
    return "$($method.ReturnType.FullName) $($method.DeclaringType.FullName)::$($method.Name)($args)"
}
function String-Safe($value) {
    if ($null -eq $value) { return '' }
    return [string]$value
}

$allTypes = @(Expand-Types $asm.MainModule.Types)
$focusPattern = '(?i)toggl|rougelike|roguelike|unlockable|upgrade|ring|tome|reward|chest|loot'
$methodPattern = '(?i)toggl|disable|enable|unlock|available|offer|upgrade|select|random|pool|roll|pick|reward|show|build|init'
$focusTypes = @($allTypes | Where-Object { $_.FullName -match $focusPattern })
$index = New-Object 'System.Collections.Generic.List[string]'
$detail = New-Object 'System.Collections.Generic.List[string]'
$xrefs = New-Object 'System.Collections.Generic.List[string]'

$index.Add('# Stonewards Toggler Inspector: discovered types and members')
$index.Add("Assembly: $gameAssembly")
$index.Add("Assembly version: $($asm.Name.Version)")
$index.Add("Types in Assembly-CSharp: $($allTypes.Count)")
$index.Add("Matching types: $($focusTypes.Count)")
$index.Add('')

foreach ($t in $focusTypes) {
    $index.Add("TYPE $($t.FullName) : $($t.BaseType.FullName)")
    foreach ($f in $t.Fields) {
        $index.Add("  FIELD $($f.FieldType.FullName) $($f.Name) $(if ($f.IsStatic) {'static'} else {''})")
    }
    foreach ($p in $t.Properties) {
        $index.Add("  PROP  $($p.PropertyType.FullName) $($p.Name)")
    }
    foreach ($m in $t.Methods) {
        $index.Add("  METHOD $(Method-Signature $m)")
    }
    $index.Add('')
}

foreach ($t in $focusTypes) {
    $interestingType = $t.FullName -match '(?i)toggl|unlockablemanager|roguelikeupgrademanager|upgrademenu|upgradeselect|upgradewindow'
    foreach ($m in $t.Methods) {
        if (-not $m.HasBody) { continue }
        if (-not $interestingType -and $m.Name -notmatch $methodPattern) { continue }
        $detail.Add(('=' * 72))
        $detail.Add((Method-Signature $m))
        foreach ($ins in $m.Body.Instructions) { $detail.Add("  $ins") }
        $detail.Add('')
    }
}

foreach ($t in $allTypes) {
    foreach ($m in $t.Methods) {
        if (-not $m.HasBody) { continue }
        foreach ($ins in $m.Body.Instructions) {
            if ($null -eq $ins.Operand) { continue }
            $operandText = String-Safe $ins.Operand
            if ($operandText -match '(?i)toggl|unlockablemanager|roguelikeupgrademanager|rougelikeupgrademanager|upgradecategory|isdisabled|disabledupgrade|selectedupgrade') {
                $xrefs.Add("$(Method-Signature $m) | $ins")
            }
        }
    }
}

$index | Set-Content -Path (Join-Path $OutputDir '01-types-and-methods.txt') -Encoding UTF8
$detail | Set-Content -Path (Join-Path $OutputDir '02-relevant-method-il.txt') -Encoding UTF8
$xrefs | Sort-Object -Unique | Set-Content -Path (Join-Path $OutputDir '03-calls-and-references.txt') -Encoding UTF8
$asm.Dispose()

Write-Host 'Inspection finished. No game files or saves were modified.' -ForegroundColor Green
Write-Host "Send these files from: $OutputDir"
Get-ChildItem -LiteralPath $OutputDir -File | Select-Object Name,Length | Format-Table -AutoSize
