param([string]$Exe = '..\Tests\LocalTest\ONHARU-2.2-local-test.exe')
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe))
$itemType = $assembly.GetType('FamilyPlanner.PlannerItem')
$listType = [Collections.Generic.List``1].MakeGenericType($itemType)
$items = [Activator]::CreateInstance($listType)
$item = [Activator]::CreateInstance($itemType)
$item.Id = 'export-check'; $item.Title = 'Meeting, "Review"'; $item.Notes = "First line`nSecond line"
$item.Category = 'Business'; $item.Start = [datetime]'2026-08-10 09:00'; $item.End = $item.Start
$item.RecurrenceFrequency = 'weekly'; $item.RecurrenceCount = 999; $item.ReminderConfigured = $true; $item.ReminderMinutes = 120; $item.ShowDday = $true; $item.Important = $true; $item.AnniversaryDate = [datetime]'2010-08-10'
$item.SnoozeUntil = [datetime]'2000-01-01'; $item.RecurrenceUntil = [datetime]'2026-08-10'
$items.Add($item)
$googleItem = [Activator]::CreateInstance($itemType)
$googleItem.Id = 'google-category-check'; $googleItem.Title = 'Family dinner'; $googleItem.Category = 'Personal'
$googleItem.GoogleCalendarId = 'family'; $googleItem.GoogleCalendarName = 'Family Team'; $googleItem.AllDay = $true
$googleItem.Start = [datetime]'2026-08-11'; $googleItem.End = [datetime]'2026-08-12'
$googleItem.SnoozeUntil = [datetime]'2000-01-01'; $googleItem.RecurrenceUntil = [datetime]'2026-08-11'
$items.Add($googleItem)
$formulaItem = [Activator]::CreateInstance($itemType)
$formulaItem.Id = 'csv-formula-check'; $formulaItem.Title = '=HYPERLINK("https://example.invalid","click")'
$formulaItem.Notes = '  @SUM(1+1)'; $formulaItem.Category = 'Business'
$formulaItem.Start = [datetime]'2026-08-12 09:00'; $formulaItem.End = [datetime]'2026-08-12 10:00'
$formulaItem.SnoozeUntil = [datetime]'2000-01-01'; $formulaItem.RecurrenceUntil = [datetime]'2026-08-12'
$items.Add($formulaItem)
$service = $assembly.GetType('FamilyPlanner.ExportService')
$folder = Join-Path ([IO.Path]::GetTempPath()) ('onharu-export-check-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder) | Out-Null

try {
    $csv = Join-Path $folder 'check.csv'; $json = Join-Path $folder 'check.json'; $ics = Join-Path $folder 'check.ics'
    $service.GetMethod('Csv').Invoke($null, [object[]]@([string]$csv, $items.PSObject.BaseObject)) | Out-Null
    $store = $assembly.GetType('FamilyPlanner.Store'); $localOnly = $store.GetMethod('LocalOnly', [Reflection.BindingFlags]'Static,NonPublic')
    $args = New-Object 'object[]' 1; $args[0] = $items; $localItems = $localOnly.Invoke($null, $args)
    $service.GetMethod('Json').Invoke($null, [object[]]@([string]$json, $localItems)) | Out-Null
    $exchange = $assembly.GetType('FamilyPlanner.CalendarExchangeService')
    $exchange.GetMethod('Ics').Invoke($null, [object[]]@([string]$ics, $localItems)) | Out-Null
    $csvText = [IO.File]::ReadAllText($csv)
    if (-not $csvText.Contains('"Meeting, ""Review"""')) { throw 'CSV quote escaping failed.' }
    if (-not $csvText.Contains('"Family Team"')) { throw 'Google calendar category was not exported to CSV.' }
    if (-not $csvText.Contains('"''=HYPERLINK(""https://example.invalid"",""click"")"')) { throw 'CSV formula prefix was not neutralized.' }
    if (-not $csvText.Contains('"''  @SUM(1+1)"')) { throw 'CSV formula prefix after whitespace was not neutralized.' }
    if (-not $csvText.Contains('D-Day') -or -not $csvText.Contains('"2010-08-10"') -or -not $csvText.Contains('"120"')) { throw '2.1 fields were not exported to CSV.' }
    $csvImported = $exchange.GetMethod('ReadCsv').Invoke($null, [object[]]@([string]$csv))
    $csvItem = $csvImported | Where-Object { $_.Id -eq 'export-check' } | Select-Object -First 1
    if ($null -eq $csvItem -or $csvItem.Title -ne 'Meeting, "Review"' -or $csvItem.RecurrenceFrequency -ne 'weekly') { throw 'CSV round-trip failed.' }
    $icsText = [IO.File]::ReadAllText($ics)
    if (-not $icsText.Contains('BEGIN:VCALENDAR') -or -not $icsText.Contains('RRULE:FREQ=WEEKLY;COUNT=999')) { throw 'ICS export failed.' }
    $icsImported = $exchange.GetMethod('ReadIcs').Invoke($null, [object[]]@([string]$ics))
    $icsItem = $icsImported | Where-Object { $_.Id -eq 'export-check' } | Select-Object -First 1
    if ($null -eq $icsItem -or $icsItem.Title -ne 'Meeting, "Review"' -or $icsItem.ReminderMinutes -ne 120 -or -not $icsItem.ShowDday) { throw 'ICS round-trip failed.' }
    $sameContent = $assembly.GetType('FamilyPlanner.LocalImportWindow').GetMethod('SameContent', [Reflection.BindingFlags]'Static,NonPublic')
    $sameClone = [Activator]::CreateInstance($itemType); foreach ($field in $itemType.GetFields()) { $field.SetValue($sameClone, $field.GetValue($icsItem)) }
    $sameClone.ExportSource = '온하루 · 로컬'; $sameClone.SnoozeUntil = [datetime]'2099-01-01'
    if (-not $sameContent.Invoke($null, @($sameClone.PSObject.BaseObject, $icsItem.PSObject.BaseObject))) { throw 'Internal defaults incorrectly changed import classification.' }
    $jsonText = [IO.File]::ReadAllText($json)
    if ($jsonText.Contains('google-category-check') -or -not $jsonText.Contains('export-check')) { throw 'ONHARU JSON must contain local schedules only.' }
    $readImport = $store.GetMethods() | Where-Object { $_.Name -eq 'ReadImportFile' -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1
    $normalized = $readImport.Invoke($null, [object[]]@([string]$json))
    $normalizedItem = $normalized | Where-Object { $_.Id -eq 'export-check' } | Select-Object -First 1
    if ($normalizedItem.End -ne $normalizedItem.Start.AddMinutes(30)) { throw 'Invalid item duration was not repaired.' }
    if ($normalizedItem.RecurrenceCount -ne 500) { throw 'Recurrence count was not clamped.' }
    if ($normalizedItem.ReminderMinutes -ne 120) { throw 'Custom reminder changed during import.' }
}
finally { if ([IO.Directory]::Exists($folder)) { [IO.Directory]::Delete($folder, $true) } }

Write-Host 'ONHARU JSON, ICS, and Excel CSV import/export checks passed.'
