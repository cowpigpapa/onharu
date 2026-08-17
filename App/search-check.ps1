param([string]$Exe = (Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.1-local-test.exe'))
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Exe).Path)
$type = $assembly.GetType('FamilyPlanner.SearchWindow', $true)
$method = $type.GetMethod('RangeBounds', [Reflection.BindingFlags]'Public,Static')
$today = [datetime]'2026-08-16'
function Bounds([string]$mode, [datetime]$first, [datetime]$last) {
    return $method.Invoke($null, [object[]]@($mode, $today, $first, $last))
}
$past = Bounds 'past' $today $today
if ($past[0] -ne [datetime]'2025-08-16' -or $past[1] -ne [datetime]'2026-08-17') { throw "Past range mismatch: $($past[0]) / $($past[1])" }
$future = Bounds 'future' $today $today
if ($future[0] -ne [datetime]'2026-08-16' -or $future[1] -ne [datetime]'2027-08-17') { throw "Future range mismatch: $($future[0]) / $($future[1])" }
$custom = Bounds 'custom' ([datetime]'2026-09-10') ([datetime]'2026-09-01')
if ($custom[0] -ne [datetime]'2026-09-01' -or $custom[1] -ne [datetime]'2026-09-11') { throw "Custom range mismatch: $($custom[0]) / $($custom[1])" }
$around = Bounds 'around' $today $today
if ($around[0] -ne [datetime]'2025-08-16' -or $around[1] -ne [datetime]'2027-08-17') { throw "Around range mismatch: $($around[0]) / $($around[1])" }
Write-Host 'ONHARU search range checks passed.'
