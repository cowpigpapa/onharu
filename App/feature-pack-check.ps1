param([string]$ExePath)
$ErrorActionPreference = 'Stop'

$exe = if ([string]::IsNullOrWhiteSpace($ExePath)) { Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.1-local-test.exe' } else { $ExePath }
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build OnharuV3.App.exe first.' }

$assembly = [Reflection.Assembly]::LoadFrom($exe)
$main = $assembly.GetType('FamilyPlanner.MainWindow', $true)
$solar = $main.GetMethod('SolarTerm', [Reflection.BindingFlags]'Public,Static')

$cases = @(
    @{ Date = [datetime]'2021-02-03'; Expected = ([string][char]0xC785 + [char]0xCD98) },
    @{ Date = [datetime]'2021-06-21'; Expected = ([string][char]0xD558 + [char]0xC9C0) },
    @{ Date = [datetime]'2021-12-22'; Expected = ([string][char]0xB3D9 + [char]0xC9C0) },
    @{ Date = [datetime]'2021-06-20'; Expected = $null }
)
foreach ($case in $cases) {
    $actual = $solar.Invoke($null, @($case.Date))
    if ($actual -ne $case.Expected) { throw "Solar term mismatch: $($case.Date.ToString('yyyy-MM-dd')) expected '$($case.Expected)', got '$actual'." }
}

$settingsType = $assembly.GetType('FamilyPlanner.PlannerSettings', $true)
$settings = [Activator]::CreateInstance($settingsType)
if ($settings.Version -ne 16) { throw "Unexpected settings version: $($settings.Version)" }
if (-not $settings.DdayPanelVisible) { throw 'D-Day panel must default to visible.' }
if (-not $settings.CompletedLast) { throw 'CompletedLast must default to true.' }
if ($settings.DefaultCalendarKey -ne 'local:business' -or -not $settings.DefaultAllDay -or $settings.DefaultStartHour -ne 9 -or $settings.DefaultDurationMinutes -ne 30) { throw 'New-item defaults are invalid.' }
if ($settings.CompletedDisplayMode -ne 'normal' -or $settings.StartViewMode -ne 'today' -or $settings.StartupPositionMode -ne 'remember' -or $settings.CloseButtonAction -ne 'minimize') { throw '2.1 display defaults are invalid.' }
if (-not $settings.ReminderSound -or $settings.QuietStartHour -ne 22 -or $settings.QuietEndHour -ne 7) { throw 'Reminder defaults are invalid.' }
if ($null -eq $settings.DateBackgroundColors) { throw 'DateBackgroundColors must be initialized.' }

$itemType = $assembly.GetType('FamilyPlanner.PlannerItem', $true)
if ($null -eq $itemType.GetField('RecurrenceCount') -or $null -eq $itemType.GetField('ShowDday') -or $null -eq $itemType.GetField('AnniversaryDate')) { throw '2.1 planner item fields are missing.' }

$elapsed = $main.GetMethod('AnniversaryElapsedDays', [Reflection.BindingFlags]'NonPublic,Static')
$nextAnniversary = $main.GetMethod('NextAnniversaryDate', [Reflection.BindingFlags]'NonPublic,Static')
$remaining = $main.GetMethod('AnniversaryRemainingDays', [Reflection.BindingFlags]'NonPublic,Static')
$visibleAnniversaries = $main.GetMethod('AnniversaryVisibleCount', [Reflection.BindingFlags]'NonPublic,Static')
if ($elapsed.Invoke($null, [object[]]@([datetime]'2020-01-01', [datetime]'2020-01-11')) -ne 10) { throw 'Anniversary elapsed-day calculation is invalid.' }
if ($nextAnniversary.Invoke($null, [object[]]@([datetime]'2000-02-29', [datetime]'2026-03-01')) -ne [datetime]'2027-02-28') { throw 'Leap-day anniversary calculation is invalid.' }
if ($remaining.Invoke($null, [object[]]@([datetime]'2000-08-20', [datetime]'2026-08-17')) -ne 3) { throw 'Anniversary remaining-day calculation is invalid.' }
if ($visibleAnniversaries.Invoke($null, [object[]]@(8, $false)) -ne 5 -or $visibleAnniversaries.Invoke($null, [object[]]@(8, $true)) -ne 8) { throw 'Anniversary card display limit is invalid.' }

$storageSource = Get-Content (Join-Path $PSScriptRoot 'LocalStorage.cs') -Raw -Encoding UTF8
foreach ($normalization in @('"normal", "fade", "hide"', '"today" && settings.StartViewMode != "last"', 'settings.LastShownDate.Year < 1900', '"remember", "locked", "editable"', '11.0, 12.0, 14.0', '0, 5, 15, 30, 60', 'settings.WeekNumberRule != "jan1"', 'settings.QuietStartHour = Math.Max')) {
    if (-not $storageSource.Contains($normalization)) { throw "Settings normalization is missing: $normalization" }
}
$searchSource = Get-Content (Join-Path $PSScriptRoot 'SearchWindow.cs') -Raw -Encoding UTF8
foreach ($searchFeature in @('Tuple.Create(', '"custom")', 'customFrom.SelectedDate', 'customTo.SelectedDate', '.AddDays(1)')) {
    if (-not $searchSource.Contains($searchFeature)) { throw "Custom search range is missing: $searchFeature" }
}
$migrationSource = Get-Content (Join-Path $PSScriptRoot 'V21Migration.cs') -Raw -Encoding UTF8
foreach ($migrationFeature in @('pre-2.1-backup', 'items-*.json', 'settings.json', 'completed.txt')) {
    if (-not $migrationSource.Contains($migrationFeature)) { throw "Pre-upgrade backup is missing: $migrationFeature" }
}

$mainSource = (Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'MainWindow*.cs' | Sort-Object Name |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join "`n"
foreach ($resizeFeature in @('if (point.X <= edge) return 5;', 'if (point.X >= surface.ActualWidth - edge) return 6;', 'if (point.Y <= edge) return 7;', 'if (point.Y >= surface.ActualHeight - edge) return 8;', 'UiCursor.ResizeHorizontal', 'UiCursor.ResizeVertical')) {
    if (-not $mainSource.Contains($resizeFeature)) { throw "Eight-direction resize is missing: $resizeFeature" }
}
foreach ($removedConflictFeature in @('desktopcal', 'dkdockhost', 'WarnDesktopCalendarConflict')) {
    if ($mainSource.Contains($removedConflictFeature)) { throw "Removed desktop-calendar warning remains: $removedConflictFeature" }
}
foreach ($positionEditorFeature in @('void ShowPositionEditor()', 'Topmost = false; ShowInTaskbar = true;', 'Topmost = false; ShowInTaskbar = false; SchedulePublish();')) {
    if (-not $mainSource.Contains($positionEditorFeature)) { throw "Position editor foreground behavior is missing: $positionEditorFeature" }
}
if ($mainSource.Contains('Topmost = true; ShowInTaskbar = true;')) { throw 'Position editor is incorrectly pinned above every application.' }
if ($mainSource.Contains('DragMove(); DesktopLayer.Lower(this);')) { throw 'Dragging the position editor still lowers it behind other windows.' }
foreach ($closeFeature in @('ExecuteCloseButtonAction(); }, 38', 'action == 25) { ExecuteCloseButtonAction(); return;', 'action == 28) { OpenCloseContextMenu(); return;', 'settings.CloseButtonAction == "confirm_exit"', 'ContextMenu CreateCloseContextMenu()', 'exit.Click += delegate { Close(); };')) {
    if (-not $mainSource.Contains($closeFeature)) { throw "Configurable close behavior is missing: $closeFeature" }
}
foreach ($exitRestoreFeature in @('var wasMinimized = calendarMinimized;', 'calendarMinimized = false; UpdateTrayVisibilityText();', 'if (wasMinimized) { MinimizeToTray(); return; }', 'if (!wasLocked) ShowPositionEditor();')) {
    if (-not $mainSource.Contains($exitRestoreFeature)) { throw "Exit cancellation state restoration is missing: $exitRestoreFeature" }
}
$resizeAt = $main.GetMethod('ResizeEdgeAt', [Reflection.BindingFlags]'NonPublic,Static')
$surface = New-Object System.Windows.Controls.Border
$surface.Measure((New-Object Windows.Size(200, 120)))
$surface.Arrange((New-Object Windows.Rect(0, 0, 200, 120)))
[Windows.FrameworkElement]$testSurface = $surface
$resizeCases = @(
    @((New-Object Windows.Point(1, 1)), 1), @((New-Object Windows.Point(199, 1)), 2),
    @((New-Object Windows.Point(1, 119)), 3), @((New-Object Windows.Point(199, 119)), 4),
    @((New-Object Windows.Point(1, 60)), 5), @((New-Object Windows.Point(199, 60)), 6),
    @((New-Object Windows.Point(100, 1)), 7), @((New-Object Windows.Point(100, 119)), 8)
)
foreach ($case in $resizeCases) {
    [Windows.Point]$point = $case[0]
    $actualEdge = $resizeAt.Invoke($null, [object[]]@($point, $testSurface))
    if ($actualEdge -ne $case[1]) { throw "Resize edge mismatch: expected $($case[1]), got $actualEdge" }
}

$addItem = $assembly.GetType('FamilyPlanner.AddItemWindow', $true)
$addItemSource = Get-Content (Join-Path $PSScriptRoot 'AddItemWindow.cs') -Raw -Encoding UTF8
foreach ($anniversaryEntryFeature in @('public bool RegisterAsAnniversary;', 'registerAnniversary = new CheckBox', 'if (showDday.IsChecked == true) showDday.IsChecked = false;', 'if (registerAnniversary.IsChecked == true) registerAnniversary.IsChecked = false;', 'Result.AnniversaryType = MainWindow.InferAnniversaryType(Result.Title);')) {
    if (-not $addItemSource.Contains($anniversaryEntryFeature)) { throw "Schedule anniversary entry is missing: $anniversaryEntryFeature" }
}
foreach ($anniversaryConversionFeature in @('Result.ShowDday = true;', 'ConvertToScheduleRequested', 'void ConvertToSchedule(', 'if (window.ConvertToScheduleRequested)', 'if (!RegisterAsAnniversary && selectedSource != null)')) {
    if (-not (($addItemSource + $mainSource + (Get-Content (Join-Path $PSScriptRoot 'AnniversaryWindow.cs') -Raw -Encoding UTF8)).Contains($anniversaryConversionFeature))) { throw "Anniversary conversion is missing: $anniversaryConversionFeature" }
}
foreach ($anniversaryRepairFeature in @('x.AnniversaryType) && x.CreatedInOnharu', 'anniversary.GoogleCalendarId = null;', 'anniversary.GoogleCalendarColor = null;')) {
    if (-not $mainSource.Contains($anniversaryRepairFeature)) { throw "Local anniversary repair is missing: $anniversaryRepairFeature" }
}
foreach ($startupLayoutFeature in @('UpdateLayout(); RenderAll(); UpdateLayout();', 'selectedTitle.FontSize = Ui(16);')) {
    if (-not $mainSource.Contains($startupLayoutFeature)) { throw "Startup layout stabilization is missing: $startupLayoutFeature" }
}
foreach ($exitPopupFeature in @('new ExitConfirmWindow { Topmost = wasLocked, ShowInTaskbar = false }', 'if (!wasLocked) ShowPositionEditor();')) {
    if (-not $mainSource.Contains($exitPopupFeature)) { throw "Flicker-free exit popup is missing: $exitPopupFeature" }
}
if ($mainSource.Contains('if (wasLocked) ShowForDialog();')) { throw 'Exit popup still flashes the full WPF calendar.' }
foreach ($ddayCardFeature in @('void AddDdayCards()', 'string.IsNullOrWhiteSpace(x.AnniversaryType)', 'D-Day (')) {
    if (-not $mainSource.Contains($ddayCardFeature)) { throw "Separate D-Day cards are missing: $ddayCardFeature" }
}
foreach ($ddayFilterFeature in @('settings.DdayPanelVisible;', 'settings.DdayPanelVisible = filters["D-Day"].IsChecked == true;', 'if (positionLocked) SchedulePublish();')) {
    if (-not $mainSource.Contains($ddayFilterFeature)) { throw "D-Day filter interaction is missing: $ddayFilterFeature" }
}
foreach ($detailOrderFeature in @('IEnumerable<List<PlannerItem>> DetailGroups', 'settings.CalendarOrderMode != "time"', 'foreach (var categoryItems in DetailGroups(dayItems))')) {
    if (-not $mainSource.Contains($detailOrderFeature)) { throw "Detail ordering is missing: $detailOrderFeature" }
}
foreach ($compactDetailFeature in @('titleText.Inlines.Add(new System.Windows.Documents.Run', 'Foreground = Brush("#94A3B8")', 'if (!item.AllDay && IsMultiDay(item))', 'Margin = new Thickness(0, 8, 0, 0)')) {
    if (-not $mainSource.Contains($compactDetailFeature)) { throw "Compact detail layout is missing: $compactDetailFeature" }
}
$categoryOrderSource = Get-Content (Join-Path $PSScriptRoot 'CategoryOrderWindow.cs') -Raw -Encoding UTF8
foreach ($categoryMoveFeature in @('movedCard.BringIntoView();', 'Mouse.Capture(null); Mouse.Synchronize();')) {
    if (-not $categoryOrderSource.Contains($categoryMoveFeature)) { throw "Category-order pointer synchronization is missing: $categoryMoveFeature" }
}
$completion = $addItem.GetMethod('UsesCompletionCheck', [Reflection.BindingFlags]'NonPublic,Static')
if (-not $completion.Invoke($null, @($false, $false))) { throw 'Timed items must use completion checks.' }
if (-not $completion.Invoke($null, @($true, $true))) { throw 'Local all-day completion check was not enabled.' }
if ($completion.Invoke($null, @($true, $false))) { throw 'Google all-day item must not enable local completion checks.' }

Write-Host 'Feature pack checks passed.'
