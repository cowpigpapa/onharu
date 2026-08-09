param([string]$Exe = '..\ONHARU-ver1.0.0-multiday-preview.exe')
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe))
$itemType = $assembly.GetType('FamilyPlanner.PlannerItem')
$mainType = $assembly.GetType('FamilyPlanner.MainWindow')
$method = $mainType.GetMethod('OccursOnDate', [Reflection.BindingFlags]'Static,NonPublic')

$allDay = [Activator]::CreateInstance($itemType)
$allDay.Start = [datetime]'2026-08-01'; $allDay.End = [datetime]'2026-08-03'; $allDay.AllDay = $true
if (-not $method.Invoke($null, @($allDay, [datetime]'2026-08-01'))) { throw 'All-day event missing on start date.' }
if (-not $method.Invoke($null, @($allDay, [datetime]'2026-08-02'))) { throw 'All-day event missing on continued date.' }
if ($method.Invoke($null, @($allDay, [datetime]'2026-08-03'))) { throw 'Exclusive all-day end date must not be displayed.' }

$timed = [Activator]::CreateInstance($itemType)
$timed.Start = [datetime]'2026-08-01 10:00'; $timed.End = [datetime]'2026-08-03 12:00'
if (-not $method.Invoke($null, @($timed, [datetime]'2026-08-03'))) { throw 'Timed multi-day event missing on end date.' }
Write-Host 'ONHARU multi-day event checks passed.'
