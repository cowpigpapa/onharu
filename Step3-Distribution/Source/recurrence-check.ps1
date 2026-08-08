param([string]$Exe = '..\ONHARU-step3-oauth4.exe')
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
$ruleMethod = $service.GetMethod('GoogleRecurrenceRule', [Reflection.BindingFlags]'Public,Static')
$googleItem = [Activator]::CreateInstance($assembly.GetType('FamilyPlanner.PlannerItem'))
$googleItem.Start = [datetime]'2026-08-08 09:00'; $googleItem.End = $googleItem.Start.AddMinutes(30); $googleItem.RecurrenceUntil = [datetime]'2026-09-30'
$googleItem.RecurrenceFrequency = 'weekly'; $googleItem.RecurrenceDays = 'MO,WE,FR'
$rule = [string]$ruleMethod.Invoke($null, [object[]]@($googleItem.PSObject.BaseObject))
if ($rule -notlike 'RRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR;UNTIL=*') { throw "Unexpected Google rule: $rule" }
Write-Host 'Advanced recurrence checks passed.'
