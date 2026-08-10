param([string]$ExePath)
$ErrorActionPreference = 'Stop'

$exe = if ([string]::IsNullOrWhiteSpace($ExePath)) { Join-Path $PSScriptRoot 'FamilyPlanner.exe' } else { $ExePath }
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build FamilyPlanner.exe first.' }

$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $exe))
$google = $assembly.GetType('FamilyPlanner.GoogleCalendar', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'

$reminder = $google.GetMethod('NormalizeReminderMinutes', $flags)
if ($reminder.Invoke($null, @([int]2147483647, $false)) -ne 10) { throw 'Unsafe timed reminder was not normalized.' }
if ($reminder.Invoke($null, @([int]-2147483648, $true)) -ne -1) { throw 'Unsafe all-day reminder was not normalized.' }
if ($reminder.Invoke($null, @([int]1440, $false)) -ne 1440) { throw 'Supported reminder changed unexpectedly.' }

$rollover = $google.GetMethod('NormalizeRolloverMode', $flags)
if ($null -ne $rollover.Invoke($null, @('unknown'))) { throw 'Unknown rollover mode was accepted.' }
if ($rollover.Invoke($null, @('next_weekday')) -ne 'next_weekday') { throw 'Supported rollover mode changed unexpectedly.' }

$frequency = $google.GetMethod('NormalizeRecurrenceFrequency', $flags)
if ($null -ne $frequency.Invoke($null, @('invalid'))) { throw 'Unknown recurrence frequency was accepted.' }

$days = $google.GetMethod('NormalizeRecurrenceDays', $flags)
$safeDays = $days.Invoke($null, @('weekly', 'weekly', 'MO,XX,WE', [datetime]'2026-08-10'))
if ($safeDays -ne 'MO,WE') { throw "Weekly recurrence days were not normalized: $safeDays" }
$safeNth = $days.Invoke($null, @('monthly', 'monthly_nth', '999XX', [datetime]'2026-08-10'))
if ($safeNth -ne '2MO') { throw "Monthly recurrence fallback is invalid: $safeNth" }

$pageUrl = $google.GetMethod('PageUrl', $flags).Invoke($null, @('https://example.test/items?max=1', 'a+b/c='))
if ($pageUrl -ne 'https://example.test/items?max=1&pageToken=a%2Bb%2Fc%3D') { throw "Page token encoding failed: $pageUrl" }

foreach ($typeName in @('FamilyPlanner.GoogleEvents', 'FamilyPlanner.GoogleCalendarList')) {
    $type = $assembly.GetType($typeName, $true)
    if ($null -eq $type.GetField('NextPageToken')) { throw "$typeName does not expose nextPageToken." }
}

$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'GoogleCalendarService.cs') -Raw -Encoding UTF8
if ($source.IndexOf('var remoteEvents = await ReadEventsAsync') -lt 0 -or
    $source.IndexOf('var remoteEvents = await ReadEventsAsync') -gt $source.IndexOf('local.RemoveAll')) {
    throw 'Remote deletion can occur before all event pages are loaded.'
}

Write-Host 'ONHARU sync security checks passed.'
