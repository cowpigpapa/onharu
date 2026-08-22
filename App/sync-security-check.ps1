param([string]$ExePath)
$ErrorActionPreference = 'Stop'

$exe = if ([string]::IsNullOrWhiteSpace($ExePath)) { Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.1-local-test.exe' } else { $ExePath }
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the local test executable first.' }

$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $exe))
$google = $assembly.GetType('FamilyPlanner.GoogleCalendar', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'

$reminder = $google.GetMethod('NormalizeReminderMinutes', $flags)
if ($reminder.Invoke($null, @([int]2147483647, $false)) -ne 10) { throw 'Unsafe timed reminder was not normalized.' }
if ($reminder.Invoke($null, @([int]-2147483648, $true)) -ne -1) { throw 'Unsafe all-day reminder was not normalized.' }
if ($reminder.Invoke($null, @([int]1440, $false)) -ne 1440) { throw 'Supported reminder changed unexpectedly.' }
if ($reminder.Invoke($null, @([int]120, $false)) -ne 120) { throw 'Custom hour reminder changed unexpectedly.' }
if ($reminder.Invoke($null, @([int]2880, $true)) -ne 2880) { throw 'Custom all-day reminder changed unexpectedly.' }

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

$birthdayRestriction = $google.GetMethod('IsBirthdayRestriction', $flags)
[Exception]$birthdayError = New-Object InvalidOperationException("eventTypeRestriction: 'birthday' event type must not have private extended properties")
if (-not $birthdayRestriction.Invoke($null, [object[]]@($birthdayError))) { throw 'Google birthday restriction was not recognized.' }
[Exception]$otherError = New-Object InvalidOperationException('ordinary Google error')
if ($birthdayRestriction.Invoke($null, [object[]]@($otherError))) { throw 'Ordinary Google error was treated as a birthday restriction.' }

$itemType = $assembly.GetType('FamilyPlanner.PlannerItem', $true)
if ($null -eq $itemType.GetField('GoogleEventType')) { throw 'Google event type is not persisted.' }
$tasks = $assembly.GetType('FamilyPlanner.GoogleTasks', $true)
$taskFlags = [Reflection.BindingFlags]'NonPublic,Static'
if (-not $tasks.GetMethod('IsSource').Invoke($null, @('tasks:list-id')) -or $tasks.GetMethod('IsSource').Invoke($null, @('calendar-id'))) { throw 'Google Tasks source boundary is invalid.' }
$due = [datetime]::MinValue
$dueArgs = [object[]]@('2026-08-22T00:00:00.000Z', $due)
if (-not $tasks.GetMethod('TryDueDate', $taskFlags).Invoke($null, $dueArgs) -or ([datetime]$dueArgs[1]).Date -ne [datetime]'2026-08-22') { throw 'Google Task due date parsing failed.' }
$eventJson = $google.GetMethod('EventJson', $flags)
$timed = [Activator]::CreateInstance($itemType)
$timed.Title = 'timed'; $timed.Start = [datetime]'2026-08-18 19:00'; $timed.End = [datetime]'2026-08-18 19:30'; $timed.AllDay = $false
$timed.Important = $true; $timed.ShowDday = $true; $timed.AnniversaryDate = [datetime]'1990-08-18'
$timedJson = $eventJson.Invoke($null, @($timed)) | ConvertFrom-Json
if ($timedJson.start.PSObject.Properties.Name -notcontains 'date' -or $null -ne $timedJson.start.date) { throw 'Timed event does not clear the previous all-day date.' }
if ($timedJson.extendedProperties.private.onharuAnniversaryDate -ne '1990-08-18') { throw 'Anniversary base date is not included in Google metadata.' }
if ($timedJson.extendedProperties.private.PSObject.Properties.Name -contains 'onharuCompleted') { throw 'Calendar event completion leaked into Google metadata.' }

$allDay = [Activator]::CreateInstance($itemType)
$allDay.Title = 'all-day'; $allDay.Start = [datetime]'2026-08-18'; $allDay.End = [datetime]'2026-08-19'; $allDay.AllDay = $true
$allDayJson = $eventJson.Invoke($null, @($allDay)) | ConvertFrom-Json
if ($allDayJson.start.PSObject.Properties.Name -notcontains 'dateTime' -or $null -ne $allDayJson.start.dateTime -or $null -ne $allDayJson.start.timeZone) { throw 'All-day event does not clear the previous timed fields.' }

$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'GoogleCalendarService.cs') -Raw -Encoding UTF8
$tasksSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'GoogleTasksService.cs') -Raw -Encoding UTF8
if (-not $source.Contains('https://www.googleapis.com/auth/tasks') -or -not $source.Contains('TasksScopePath')) { throw 'Google Tasks OAuth scope migration is missing.' }
foreach ($taskSyncFeature in @('showCompleted=true&showHidden=true&showAssigned=true', '\"status\":\"completed\"', '\"status\":\"needsAction\"', 'public static async Task UpsertAsync', 'public static async Task DeleteAsync')) {
    if (-not $tasksSource.Contains($taskSyncFeature)) { throw "Google Tasks sync feature is missing: $taskSyncFeature" }
}
if ($source.IndexOf('var remoteEvents = await read.Item3') -lt 0 -or
    $source.IndexOf('var remoteEvents = await read.Item3') -gt $source.IndexOf('local.RemoveAll')) {
    throw 'Remote deletion can occur before all event pages are loaded.'
}
$mainSource = (Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'MainWindow*.cs' | Sort-Object Name |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join "`n"
if (-not $mainSource.Contains('googleSyncing = false; googleButton.IsEnabled = true;') -or
    -not $mainSource.Contains('if (!showSuccess) UpdateAccountStatus();') -or
    -not $mainSource.Contains('if (positionLocked) SchedulePublish();')) {
    throw 'Google sync completion does not refresh the fixed desktop frame.'
}
if (-not $mainSource.Contains('syncWatch.Elapsed.TotalSeconds.ToString("0.0")')) {
    throw 'Manual Google sync does not report its elapsed time.'
}
foreach ($completionBoundary in @('GoogleTasks.IsTask(item)', 'GoogleTasks.SetCompletedAsync(item, completed)')) {
    if (-not $mainSource.Contains($completionBoundary)) { throw "Google completion boundary is missing: $completionBoundary" }
}
foreach ($taskOptInBoundary in @('if (settings.ShowGoogleTasks && !GoogleCalendar.HasTasksPermission)', 'else if (settings.ShowGoogleTasks)', 'if (GoogleTasks.IsTask(item)) await GoogleTasks.UpsertAsync(item)')) {
    if (-not $mainSource.Contains($taskOptInBoundary)) { throw "Google Tasks opt-in boundary is missing: $taskOptInBoundary" }
}
foreach ($accountSwitchBoundary in @('LoadConnectedAccountItems();', 'Store.SetAccount(accountId);', 'items.Clear(); items.AddRange(Store.Load());')) {
    if (-not $mainSource.Contains($accountSwitchBoundary)) { throw "Account-local data reload is missing: $accountSwitchBoundary" }
}
$addSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AddItemWindow.cs') -Raw -Encoding UTF8
foreach ($taskEditorBoundary in @('var taskTarget = selectedSource != null && GoogleTasks.IsSource(selectedSource.Id)', 'var recurrenceFrequency = taskTarget ? ""', 'ReminderMinutes = taskTarget ? -1')) {
    if (-not $addSource.Contains($taskEditorBoundary)) { throw "Google Task editor restriction is missing: $taskEditorBoundary" }
}
$detailSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.Detail.cs') -Raw -Encoding UTF8
if ($detailSource.Contains('await SaveGoogleItem(item);')) { throw 'Calendar event completion is still sent to Google Calendar.' }
if ($source.IndexOf('calendarReads.Add(Tuple.Create') -lt 0 -or $source.IndexOf('calendarReads.Add(Tuple.Create') -gt $source.IndexOf('foreach (var read in calendarReads)')) {
    throw 'Google calendar downloads are not started concurrently.'
}

Write-Host 'ONHARU sync security checks passed.'
