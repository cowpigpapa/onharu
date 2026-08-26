param([string]$Exe = (Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.2-local-test.exe'))
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
    '#2563EB', '#DB2777', '#16A085', '#38A7D8', '#A78BFA', '#EF4444', [double]12, 'category', $true, $false, $true, $true, $false,
    'iso', 'monday', (New-List ([int])).PSObject.BaseObject, $false, [int]15, $calendarList.PSObject.BaseObject, $false, $false, [int]0, $true, $true, '', [int]0,
    $stringList.PSObject.BaseObject, (New-List ([string])).PSObject.BaseObject, $true, (New-List ([string])).PSObject.BaseObject, (New-List ([string])).PSObject.BaseObject, [int]8, $false,
    'border', '#CCDBEAFE', '#3B82F6', '#CCFCE7F3', 'fill', '#F59E0B',
    'local:business', $true, [int]9, [int]0, [int]30, [int]-1,
    'fade', 'last', $true, $true, [int]22, [int]7, 'screen', 'remember', $true, $true, $true, $false, $false, $true, 'classic',
    $true, $true, $true, $true, $true, $true, $true, $true
)
$settings = $settingsType.GetConstructors()[0].Invoke($settingsArgs)
if ($settings.Width -ne 620) { throw 'Settings window width changed unexpectedly.' }
$settings.Close()

$diaryEditor = $assembly.GetType('FamilyPlanner.DiaryEditorWindow', $true).GetConstructors()[0].Invoke([object[]]@([datetime]'2026-08-22', $null))
if ($diaryEditor.Width -ne 650 -or $diaryEditor.Height -ne 540) { throw 'Diary editor size changed unexpectedly.' }
$diaryType = $assembly.GetType('FamilyPlanner.DiaryEditorWindow', $true)
$diaryDate = $diaryType.GetField('dateText', [Reflection.BindingFlags]'NonPublic,Instance').GetValue($diaryEditor)
$parseDiaryDate = $diaryType.GetMethod('ParseDate', [Reflection.BindingFlags]'NonPublic,Instance')
$diaryDate.Text = '20260230'
if ($parseDiaryDate.Invoke($diaryEditor, [object[]]@($false))) { throw 'Diary date accepted an invalid calendar date.' }
$diaryDate.Text = '20260822'
if (-not $parseDiaryDate.Invoke($diaryEditor, [object[]]@($false))) { throw 'Diary date rejected valid YYYYMMDD input.' }
$diaryEditor.Close()
$diaryReader = $assembly.GetType('FamilyPlanner.DiaryReaderWindow', $true).GetConstructors()[0].Invoke([object[]]@([datetime]'2026-08-22'))
if ($diaryReader.Width -ne 920 -or $diaryReader.Height -ne 640) { throw 'Diary reader size changed unexpectedly.' }
$diaryReader.Close()

$defaults = [Activator]::CreateInstance($assembly.GetType('FamilyPlanner.PlannerSettings', $true))
$addArgs = New-Object 'object[]' 5
$addArgs[0] = [datetime]'2026-08-16'; $addArgs[1] = $null; $addArgs[2] = $calendarList.PSObject.BaseObject; $addArgs[3] = $false; $addArgs[4] = $defaults
$addType = $assembly.GetType('FamilyPlanner.AddItemWindow', $true)
$add = $addType.GetConstructors()[0].Invoke($addArgs)
$private = [Reflection.BindingFlags]'NonPublic,Instance'
$allDay = $addType.GetField('allDay', $private).GetValue($add)
$morning = $addType.GetField('morning', $private).GetValue($add)
$afternoon = $addType.GetField('afternoon', $private).GetValue($add)
$hourGrid = $addType.GetField('hourGrid', $private).GetValue($add)
$multiDay = $addType.GetField('multiDay', $private).GetValue($add)
if ($allDay.IsChecked -ne $true -or $morning.IsChecked -eq $true -or $afternoon.IsChecked -eq $true -or -not $multiDay.IsEnabled) { throw 'Initial checkbox time mode is invalid.' }
$morning.IsChecked = $true
if ($allDay.IsChecked -eq $true -or $afternoon.IsChecked -eq $true -or $morning.IsChecked -ne $true -or $multiDay.IsEnabled) { throw 'Morning checkbox did not become the exclusive timed mode.' }
if (($hourGrid.Children | Select-Object -First 1).Tag -ne 0 -or ($hourGrid.Children | Select-Object -Last 1).Tag -ne 11) { throw 'Morning hour choices are not 00-11.' }
$afternoon.IsChecked = $true
if (($hourGrid.Children | Select-Object -First 1).Tag -ne 12 -or ($hourGrid.Children | Select-Object -Last 1).Tag -ne 23) { throw 'Afternoon hour choices are not 12-23.' }
$morning.IsChecked = $true
$morning.IsChecked = $false
if ($allDay.IsChecked -ne $true -or -not $multiDay.IsEnabled) { throw 'Clearing the active time mode did not restore all-day safely.' }
$countMode = $addType.GetField('recurrenceCountMode', $private).GetValue($add)
$recurrenceEnabled = $addType.GetField('recurrenceEnabled', $private).GetValue($add)
$untilButton = $addType.GetField('recurrenceUntilButton', $private).GetValue($add)
$options = $addType.GetField('recurrenceOptions', $private).GetValue($add)
if ($countMode.IsEnabled -or $untilButton.IsEnabled) { throw 'Recurrence end controls must be disabled when recurrence is off.' }
$weekly = $options.Children | Where-Object { $_.Tag -eq 'weekly' } | Select-Object -First 1
$recurrenceEnabled.IsChecked = $true
$weekly.IsChecked = $true
if (-not $countMode.IsEnabled -or -not $untilButton.IsEnabled) { throw 'Recurrence end controls were not enabled.' }
$showDday = $addType.GetField('showDday', $private).GetValue($add)
if ($addType.GetField('anniversaryDateCard', $private) -ne $null) { throw 'General schedule editor still owns anniversary controls.' }
if ($showDday.Foreground.Color.ToString() -ne '#FF0369A1') { throw 'D-Day option does not match the sidebar D-Day palette.' }
$add.Close()

$taskSource = [Activator]::CreateInstance($assembly.GetType('FamilyPlanner.GoogleCalendarSetting', $true))
$taskSource.Id = 'tasks:test-list'; $taskSource.Name = 'Tasks · 테스트'; $taskSource.Color = '#5B8DEF'; $taskSource.Editable = $true; $taskSource.AccessRole = 'tasks'
$taskList = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($assembly.GetType('FamilyPlanner.GoogleCalendarSetting', $true)))
$taskList.Add($taskSource)
$taskDefaults = [Activator]::CreateInstance($assembly.GetType('FamilyPlanner.PlannerSettings', $true)); $taskDefaults.ShowGoogleTasks = $true
$taskArgs = New-Object 'object[]' 5
$taskArgs[0] = [datetime]'2026-08-22'; $taskArgs[1] = $null; $taskArgs[2] = $taskList; $taskArgs[3] = $true; $taskArgs[4] = $taskDefaults
$taskAdd = $addType.GetConstructors()[0].Invoke($taskArgs)
$taskOptions = $addType.GetField('categoryOptions', $private).GetValue($taskAdd)
$taskRadio = $taskOptions | Where-Object { $_.Tag -eq 'google:tasks:test-list' } | Select-Object -First 1
if ($null -eq $taskRadio) { throw ('Google Task target is missing. Targets: ' + (($taskOptions | ForEach-Object { [string]$_.Tag }) -join ', ')) }
$taskRadio.IsChecked = $true
if ($addType.GetField('timeCard', $private).GetValue($taskAdd).Visibility -ne [Windows.Visibility]::Collapsed -or
    $addType.GetField('recurrenceCard', $private).GetValue($taskAdd).Visibility -ne [Windows.Visibility]::Collapsed) {
    throw 'Google Task target does not hide unsupported time and recurrence controls.'
}
$taskAdd.Close()

$itemList = New-List $assembly.GetType('FamilyPlanner.PlannerItem', $true)
$searchArgs = New-Object 'object[]' 1; $searchArgs[0] = $itemList.PSObject.BaseObject
$search = $assembly.GetType('FamilyPlanner.SearchWindow', $true).GetConstructors()[0].Invoke($searchArgs)
if ($search.Width -ne 520 -or $search.Height -ne 540) { throw 'Search window size changed unexpectedly.' }
$search.Close()

$jump = $assembly.GetType('FamilyPlanner.MonthJumpWindow', $true).GetConstructors()[0].Invoke([object[]]@([datetime]'2026-08-01'))
if ($jump.Width -ne 390) { throw 'Month jump window width changed unexpectedly.' }
$jump.Close()
Write-Host 'ONHARU 2.1 UI construction checks passed.'
