param([string]$Exe = '..\Release\ONHARU.exe')
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe))
$itemType = $assembly.GetType('FamilyPlanner.PlannerItem')
$listType = [Collections.Generic.List``1].MakeGenericType($itemType)
$items = [Activator]::CreateInstance($listType)
$item = [Activator]::CreateInstance($itemType)
$item.Id = 'export-check'; $item.Title = 'Meeting, "Review"'; $item.Notes = "First line`nSecond line"
$item.Category = 'Business'; $item.Start = [datetime]'2026-08-10 09:00'; $item.End = [datetime]'2026-08-10 10:00'
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
    $csv = Join-Path $folder 'check.csv'; $ics = Join-Path $folder 'check.ics'; $json = Join-Path $folder 'check.json'
    $service.GetMethod('Csv').Invoke($null, [object[]]@([string]$csv, $items.PSObject.BaseObject)) | Out-Null
    $service.GetMethod('Ics').Invoke($null, [object[]]@([string]$ics, $items.PSObject.BaseObject)) | Out-Null
    $service.GetMethod('Json').Invoke($null, [object[]]@([string]$json, $items.PSObject.BaseObject)) | Out-Null
    $csvText = [IO.File]::ReadAllText($csv); $icsText = [IO.File]::ReadAllText($ics)
    if (-not $csvText.Contains('"Meeting, ""Review"""')) { throw 'CSV quote escaping failed.' }
    if (-not $csvText.Contains('"Family Team"')) { throw 'Google calendar category was not exported to CSV.' }
    if (-not $csvText.Contains('"''=HYPERLINK(""https://example.invalid"",""click"")"')) { throw 'CSV formula prefix was not neutralized.' }
    if (-not $csvText.Contains('"''  @SUM(1+1)"')) { throw 'CSV formula prefix after whitespace was not neutralized.' }
    if (-not $icsText.Contains('BEGIN:VCALENDAR') -or -not $icsText.Contains('SUMMARY:Meeting\, "Review"')) { throw 'ICS format validation failed.' }
    if (-not $icsText.Contains('CATEGORIES:Family Team')) { throw 'Google calendar category was not exported to ICS.' }
    $jsonText = [IO.File]::ReadAllText($json)
    if (-not $jsonText.Contains('"Category":"Family Team"')) { throw 'Google calendar category was not exported to JSON.' }
}
finally { if ([IO.Directory]::Exists($folder)) { [IO.Directory]::Delete($folder, $true) } }

Write-Host 'ONHARU JSON, CSV and ICS export checks passed.'
