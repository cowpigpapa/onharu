param([string]$Exe = (Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.1-local-test.exe'))
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Exe).Path)
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'UI construction check requires STA.' }

function New-List([Type]$elementType) {
    return ,([Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($elementType)))
}

$calendarList = New-List $assembly.GetType('FamilyPlanner.GoogleCalendarSetting', $true)
$stringList = New-List ([string])
$settingsType = $assembly.GetType('FamilyPlanner.SettingsWindow', $true)
$settingsArgs = [object[]]@(
    '#2563EB', '#DB2777', [double]12, 'category', $false, $true, $true, $false,
    'iso', $false, [int]15, $calendarList.PSObject.BaseObject, $false, [int]0, $true, $true, '', [int]0,
    $stringList.PSObject.BaseObject, (New-List ([string])).PSObject.BaseObject, $true, (New-List ([string])).PSObject.BaseObject, (New-List ([string])).PSObject.BaseObject,
    'monthAuto', [int]4, [int]2, 'border', '#CCDBEAFE', '#3B82F6',
    'local:business', $true, [int]9, [int]0, [int]30, [int]-1,
    'fade', 'last', $true, [int]22, [int]7, 'remember', 'minimize'
)
$settings = $settingsType.GetConstructors()[0].Invoke($settingsArgs)
if ($settings.Width -ne 620) { throw 'Settings window width changed unexpectedly.' }
$settings.Close()

$defaults = [Activator]::CreateInstance($assembly.GetType('FamilyPlanner.PlannerSettings', $true))
$addArgs = New-Object 'object[]' 5
$addArgs[0] = [datetime]'2026-08-16'; $addArgs[1] = $null; $addArgs[2] = $calendarList.PSObject.BaseObject; $addArgs[3] = $false; $addArgs[4] = $defaults
$addType = $assembly.GetType('FamilyPlanner.AddItemWindow', $true)
$add = $addType.GetConstructors()[0].Invoke($addArgs)
$private = [Reflection.BindingFlags]'NonPublic,Instance'
$countMode = $addType.GetField('recurrenceCountMode', $private).GetValue($add)
$untilButton = $addType.GetField('recurrenceUntilButton', $private).GetValue($add)
$options = $addType.GetField('recurrenceOptions', $private).GetValue($add)
if ($countMode.IsEnabled -or $untilButton.IsEnabled) { throw 'Recurrence end controls must be disabled when recurrence is off.' }
$weekly = $options.Children | Where-Object { $_.Tag -eq 'weekly' } | Select-Object -First 1
$weekly.IsChecked = $true
if (-not $countMode.IsEnabled -or -not $untilButton.IsEnabled) { throw 'Recurrence end controls were not enabled.' }
$important = $addType.GetField('important', $private).GetValue($add)
$showDday = $addType.GetField('showDday', $private).GetValue($add)
$anniversaryCard = $addType.GetField('anniversaryDateCard', $private).GetValue($add)
if ($anniversaryCard.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Anniversary date must be hidden while D-Day is off.' }
$showDday.IsChecked = $true
if ($anniversaryCard.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'General schedule editor must not show anniversary fields.' }
$add.Close()

$itemList = New-List $assembly.GetType('FamilyPlanner.PlannerItem', $true)
$searchArgs = New-Object 'object[]' 1; $searchArgs[0] = $itemList.PSObject.BaseObject
$search = $assembly.GetType('FamilyPlanner.SearchWindow', $true).GetConstructors()[0].Invoke($searchArgs)
if ($search.Width -ne 520 -or $search.Height -ne 540) { throw 'Search window size changed unexpectedly.' }
$search.Close()

$jump = $assembly.GetType('FamilyPlanner.MonthJumpWindow', $true).GetConstructors()[0].Invoke([object[]]@([datetime]'2026-08-01'))
if ($jump.Width -ne 390) { throw 'Month jump window width changed unexpectedly.' }
$jump.Close()
Write-Host 'ONHARU 2.1 UI construction checks passed.'
