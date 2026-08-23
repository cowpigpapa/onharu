param([string]$Exe = (Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.2-local-test.exe'))
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe))
$service = $assembly.GetType('FamilyPlanner.RecurrenceService')
$method = $service.GetMethod('NextOccurrence', [Reflection.BindingFlags]'Public,Static')
function Check($frequency, $mode, $days, [datetime]$start, [datetime]$expected) {
    $item = [Activator]::CreateInstance($assembly.GetType('FamilyPlanner.PlannerItem'))
    $item.Start = $start; $item.End = $start.AddMinutes(30); $item.RecurrenceFrequency = $frequency; $item.RecurrenceMode = $mode; $item.RecurrenceDays = $days
    $actual = [datetime]$method.Invoke($null, [object[]]@($item.PSObject.BaseObject, $start))
    if ($actual -ne $expected) { throw "$frequency/$mode expected $expected but got $actual" }
}
Check daily weekdays $null ([datetime]'2026-08-07 09:00') ([datetime]'2026-08-10 09:00')
Check weekly weekly 'MO,WE,FR' ([datetime]'2026-08-07 09:00') ([datetime]'2026-08-10 09:00')
Check monthly monthly_last $null ([datetime]'2026-01-31 09:00') ([datetime]'2026-02-28 09:00')
Check monthly monthly_nth '-1FR' ([datetime]'2026-01-30 09:00') ([datetime]'2026-02-27 09:00')
Check yearly yearly_date $null ([datetime]'2026-08-30 09:00') ([datetime]'2027-08-30 09:00')
Check yearly yearly_nth '-1SU' ([datetime]'2026-08-30 09:00') ([datetime]'2027-08-29 09:00')
$ruleMethod = $service.GetMethod('GoogleRecurrenceRule', [Reflection.BindingFlags]'Public,Static')
$googleItem = [Activator]::CreateInstance($assembly.GetType('FamilyPlanner.PlannerItem'))
$googleItem.Start = [datetime]'2026-08-08 09:00'; $googleItem.End = $googleItem.Start.AddMinutes(30); $googleItem.RecurrenceUntil = [datetime]'2026-09-30'
$googleItem.RecurrenceFrequency = 'weekly'; $googleItem.RecurrenceDays = 'MO,WE,FR'
$rule = [string]$ruleMethod.Invoke($null, [object[]]@($googleItem.PSObject.BaseObject))
if ($rule -notlike 'RRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR;UNTIL=*') { throw "Unexpected Google rule: $rule" }
$googleItem.RecurrenceCount = 12
$countRule = [string]$ruleMethod.Invoke($null, [object[]]@($googleItem.PSObject.BaseObject))
if ($countRule -ne 'RRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR;COUNT=12') { throw "Unexpected count rule: $countRule" }
$googleItem.RecurrenceCount = 0; $googleItem.RecurrenceFrequency = 'yearly'; $googleItem.RecurrenceMode = 'yearly_nth'; $googleItem.RecurrenceDays = '-1SU'
$yearlyRule = [string]$ruleMethod.Invoke($null, [object[]]@($googleItem.PSObject.BaseObject))
if ($yearlyRule -notlike 'RRULE:FREQ=YEARLY;BYMONTH=8;BYDAY=-1SU;UNTIL=*') { throw "Unexpected yearly Google rule: $yearlyRule" }
$mainSource = (Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'MainWindow*.cs' |
    Sort-Object Name | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join "`n"
foreach ($field in @('sibling.AllDay = window.Result.AllDay', 'sibling.IsTodo = window.Result.IsTodo', 'sibling.RolloverMode = window.Result.RolloverMode', 'sibling.ShowDday = window.Result.ShowDday')) {
    if (-not $mainSource.Contains($field)) { throw "Local series propagation is missing: $field" }
}
$addSource = Get-Content (Join-Path $PSScriptRoot 'AddItemWindow.cs') -Raw -Encoding UTF8
foreach ($guard in @('recurrenceCountMode.IsEnabled = recurring', 'string.IsNullOrWhiteSpace(recurrenceFrequency) ? 0 : SelectedRecurrenceCount()')) {
    if (-not $addSource.Contains($guard)) { throw "Recurrence count guard is missing: $guard" }
}
Write-Host 'Advanced recurrence checks passed.'
