param([string]$ExePath)
$ErrorActionPreference = 'Stop'

$mainSources = (Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'MainWindow*.cs' | ForEach-Object { Get-Content $_.FullName -Raw -Encoding UTF8 }) -join "`n"
$settingsSource = Get-Content (Join-Path $PSScriptRoot 'SettingsWindow.cs') -Raw -Encoding UTF8
$storageSource = Get-Content (Join-Path $PSScriptRoot 'LocalStorage.cs') -Raw -Encoding UTF8
if ($storageSource.Contains('Samples()') -or $storageSource.Contains('가족 저녁 식사') -or $storageSource.Contains('주간 업무 보고')) { throw 'Clean install must not create sample schedules.' }
$calendarStyleSource = Get-Content (Join-Path $PSScriptRoot 'OnharuCalendarStyle.cs') -Raw -Encoding UTF8
$deleteSource = Get-Content (Join-Path $PSScriptRoot 'LocalDataDeleteWindow.cs') -Raw -Encoding UTF8
foreach ($dataAction in @('ExportFile', 'ExportEmail', 'DeleteLocalData')) {
    if (-not $settingsSource.Contains('SettingsDataAction.' + $dataAction)) { throw "Data action is missing: $dataAction" }
}
foreach ($anniversaryStorageFeature in @('CollapseMaterializedAnniversaries', 'Store one anniversary basis record', 'ProjectItems(DateTime from, DateTime to)')) {
    if (-not (($storageSource + $mainSources).Contains($anniversaryStorageFeature))) { throw "Single-record anniversary storage is missing: $anniversaryStorageFeature" }
}
foreach ($deleteFeature in @('Google 원본은 삭제하지 않고', 'BackupBeforeDestructiveChange', 'GoogleDdayOnly')) {
    if (-not (($deleteSource + $storageSource + $settingsSource + $mainSources).Contains($deleteFeature))) { throw "Safe local data deletion is missing: $deleteFeature" }
}
foreach ($placementFeature in @('MatchWindowToPhysicalFrame(target)', 'TryGetPublishedScreenRectangle', 'SavePhysicalPlacement();')) {
    if (-not $mainSources.Contains($placementFeature) -and -not (Get-Content (Join-Path $PSScriptRoot 'ExplorerFramePublisher.cs') -Raw -Encoding UTF8).Contains($placementFeature)) {
        throw "Resolution-safe mode placement is missing: $placementFeature"
    }
}
foreach ($settledPlacementFeature in @('GetDpiForWindow', 'stableTicks < 2', 'Opacity = intendedOpacity', 'explorerFrame.Disable()')) {
    if (-not $mainSources.Contains($settledPlacementFeature)) { throw "DPI-settled WPF handoff is missing: $settledPlacementFeature" }
}
foreach ($logicalPlacementFeature in @('nativeDpi / 96.0', 'Left = frame.Left / scale', 'Width = Math.Max(MinWidth')) {
    if (-not $mainSources.Contains($logicalPlacementFeature)) { throw "Current-frame WPF placement is missing: $logicalPlacementFeature" }
}
$publisherSource = Get-Content (Join-Path $PSScriptRoot 'ExplorerFramePublisher.cs') -Raw -Encoding UTF8
foreach ($nativeFrameFeature in @('Native.GetWindowRect(handle, out nativeWindow)', 'width / window.ActualWidth', 'new Point(nativeWindow.Left, nativeWindow.Top)')) {
    if (-not $publisherSource.Contains($nativeFrameFeature)) { throw "Native HWND frame authority is missing: $nativeFrameFeature" }
}
if ((Get-Content (Join-Path $PSScriptRoot 'NoticeWindow.cs') -Raw -Encoding UTF8).Contains('"✓  일정 가져오기"')) { throw 'Notice heading must not be hard-coded to import.' }
$exe = if ([string]::IsNullOrWhiteSpace($ExePath)) { Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.2-local-test.exe' } else { $ExePath }
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build Onharu.App.exe first.' }

$assembly = [Reflection.Assembly]::LoadFrom($exe)
$backupType = $assembly.GetType('FamilyPlanner.BackupWindow', $true)
$backupLabel = $backupType.GetMethod('BackupLabel', [Reflection.BindingFlags]'NonPublic,Static')
$safeBackupName = $backupLabel.Invoke($null, @('account-before-delete-20260822-015043.json'))
if ($safeBackupName -ne '2026년 08월 22일 01:50 삭제 전 안전 백업') { throw "Unexpected safe backup label: $safeBackupName" }
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
if ($settings.Version -ne 41) { throw "Unexpected settings version: $($settings.Version)" }
if ($settings.ThemeId -ne 'classic') { throw "Theme must default to classic: $($settings.ThemeId)" }
if (-not $settings.AutomaticUpdateChecks) { throw 'Automatic update checks must default to enabled.' }
if ($settings.CalendarRangeMode -ne 'weeks' -or $settings.MonthRangeMode -ne 'monthAuto' -or $settings.UseMonthView -or $settings.VisibleWeekCount -ne 4 -or $settings.TodayRow -ne 2) { throw 'Clean-install calendar must default to a four-week view without overriding saved settings.' }
if ($settings.PositionLocked -or $settings.StartupPositionMode -ne 'editable' -or $settings.Width -ne 1120 -or $settings.Height -ne 700) { throw 'Clean-install placement defaults are invalid.' }
if ($settings.ThemeId -ne 'classic' -or $settings.FontSize -ne 12 -or $settings.Opacity -ne .95 -or $settings.SelectedPaletteIndex -ne 0) { throw 'Clean-install visual defaults are invalid.' }
if (-not $settings.MultiDayFirst -or $settings.CompletedDisplayMode -ne 'fade' -or $settings.CloseButtonAction -ne 'confirm_exit') { throw 'Clean-install behavior defaults are invalid.' }
if (-not $settings.ShowWeekNumbers -or $settings.WeekNumberRule -ne 'iso' -or $settings.WeekStartDay -ne 'sunday') { throw 'Clean-install week defaults are invalid.' }
if ($settings.SelectedDateStyle -ne 'border' -or $settings.SelectedDateBorderColor -ne '#EC4899' -or $settings.TodayStyle -ne 'icon') { throw 'Clean-install date marker defaults are invalid.' }
if (-not $settings.ShowLunar -or -not $settings.ShowSolarTerms -or -not $settings.UseRollover -or $settings.AutoSyncMinutes -ne 5) { throw 'Clean-install display and sync defaults are invalid.' }
if ($settings.BusinessColor -ne '#00A6C8' -or $settings.PersonalColor -ne '#2859C5' -or $settings.BaseballColor -ne '#38A169' -or $settings.DdayColor -ne '#E67E22' -or $settings.AnniversaryColor -ne '#C2418C' -or $settings.HolidayColor -ne '#DC2626') { throw 'Clean-install category colors are invalid.' }
if ($settings.ShowGoogleTasks) { throw 'Google Tasks must be opt-in by default.' }
if ($settings.UseTimetable) { throw 'Timetable must be opt-in by default.' }
if (-not $settings.UseDiary) { throw 'Diary must be visible by default.' }
if (-not $settings.DdayPanelVisible) { throw 'D-Day panel must default to visible.' }
if (-not $settings.CompletedLast) { throw 'CompletedLast must default to true.' }
if ($settings.DefaultCalendarKey -ne 'local:business' -or -not $settings.DefaultAllDay -or $settings.DefaultStartHour -ne 9 -or $settings.DefaultDurationMinutes -ne 30) { throw 'New-item defaults are invalid.' }
if ($settings.StartViewMode -ne 'today') { throw 'Clean-install start date must be today.' }
if (-not $settings.ReminderSound -or $settings.QuietStartHour -ne 22 -or $settings.QuietEndHour -ne 7) { throw 'Reminder defaults are invalid.' }
if ($null -eq $settings.DateBackgroundColors) { throw 'DateBackgroundColors must be initialized.' }
foreach ($typeName in @('FamilyPlanner.TimetableData', 'FamilyPlanner.TimetableSlot', 'FamilyPlanner.TimetableWindow')) {
    if ($null -eq $assembly.GetType($typeName, $false)) { throw "Timetable type is missing: $typeName" }
}
foreach ($typeName in @('FamilyPlanner.DiaryEntry', 'FamilyPlanner.DiaryStore', 'FamilyPlanner.DiaryEditorWindow', 'FamilyPlanner.DiaryReaderWindow', 'FamilyPlanner.DiaryDateHitTarget')) {
    if ($null -eq $assembly.GetType($typeName, $false)) { throw "Diary type is missing: $typeName" }
}
foreach ($diaryFeature in @('OpenDiaryEditor(date)', 'OpenDiaryEditor(selectedDate)', 'OpenDiaryReader', 'settings.UseDiary')) {
    if (-not $mainSources.Contains($diaryFeature)) { throw "Diary integration is missing: $diaryFeature" }
}
foreach ($diaryInputFeature in @('settings.UseDiary ? new DiaryDateHitTarget(date) : null', 'lunar.MouseLeftButtonDown += openDiary', 'diaryDot.MouseLeftButtonDown += openDiary', 'if (e.ClickCount == 2) AddItem(sender, e)', 'target as DiaryDateHitTarget')) {
    if (-not $mainSources.Contains($diaryInputFeature)) { throw "Diary date-only input routing is missing: $diaryInputFeature" }
}
if (-not $settingsSource.Contains('Content = "일기장 기능"')) { throw 'Diary feature setting is missing.' }
if (-not $settingsSource.Contains('Content = "Google Tasks 표시·동기화"')) { throw 'Google Tasks opt-in setting is missing.' }
foreach ($diaryToggleFeature in @('if (!settings.UseDiary)', 'settings.UseDiary && diaryDates.Contains', 'if (!settings.UseDiary && diaryReaderWindow != null) diaryReaderWindow.Close()')) {
    if (-not $mainSources.Contains($diaryToggleFeature)) { throw "Diary feature toggle is incomplete: $diaryToggleFeature" }
}
if (-not (Get-Content (Join-Path $PSScriptRoot 'DiaryWindows.cs') -Raw -Encoding UTF8).Contains('UpdateNavigationButtons()')) { throw 'Diary previous/next availability styling is missing.' }
foreach ($calendarFeature in @('OnharuCalendarStyle.Apply(calendar)', 'OnharuCalendarStyle.Create()', 'PART_HeaderButton', 'PART_MonthView')) {
    if (-not (($mainSources + $settingsSource + $calendarStyleSource + (Get-Content (Join-Path $PSScriptRoot 'AddItemWindow.cs') -Raw -Encoding UTF8) + (Get-Content (Join-Path $PSScriptRoot 'SearchWindow.cs') -Raw -Encoding UTF8) + (Get-Content (Join-Path $PSScriptRoot 'AnniversaryWindow.cs') -Raw -Encoding UTF8) + (Get-Content (Join-Path $PSScriptRoot 'DiaryWindows.cs') -Raw -Encoding UTF8)).Contains($calendarFeature))) { throw "Shared popup calendar style is missing: $calendarFeature" }
}

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
foreach ($searchPolishFeature in @('SettingsWindow.StyleComboBox(range)', 'void ScheduleRender()', 'DispatcherPriority.Background', 'StyleDatePicker(customFrom)', 'UiRound.SoftenScrollBars(resultScroller)', 'Margin = new Thickness(12, 5, 12, 5)')) {
    if (-not $searchSource.Contains($searchPolishFeature)) { throw "Search visual or responsiveness polish is missing: $searchPolishFeature" }
}
foreach ($searchLayoutFeature in @('var titleGroup = new StackPanel', 'titleGroup.Children.Add(todayButton)', 'Grid.SetColumn(range, 1)', 'Grid.SetColumn(search, 2)', 'SearchCalendarDayStyle()', 'SearchCalendarButtonStyle()', 'Margin = new Thickness(0, 7, 82, 0)')) {
    if (-not $searchSource.Contains($searchLayoutFeature)) { throw "Search layout cleanup is missing: $searchLayoutFeature" }
}
$migrationSource = Get-Content (Join-Path $PSScriptRoot 'V21Migration.cs') -Raw -Encoding UTF8
foreach ($migrationFeature in @('pre-2.1-backup', 'items-*.json', 'settings.json', 'completed.txt')) {
    if (-not $migrationSource.Contains($migrationFeature)) { throw "Pre-upgrade backup is missing: $migrationFeature" }
}

$mainSource = (Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'MainWindow*.cs' | Sort-Object Name |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join "`n"
$settingsWindowSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SettingsWindow.cs') -Raw -Encoding UTF8
foreach ($dataActionFeature in @('enum SettingsDataAction', 'RequestedDataAction = SettingsDataAction.ImportFile', 'RequestedDataAction = SettingsDataAction.ExportFile')) {
    if (-not $settingsWindowSource.Contains($dataActionFeature)) { throw "일정 데이터 작업 단일 선택 구조가 누락됐습니다: $dataActionFeature" }
}
foreach ($removedDataFlag in @('public bool ImportItemsFile', 'public bool ExportItems', 'resetDataAction')) {
    if ($settingsWindowSource.Contains($removedDataFlag)) { throw "일정 데이터 작업 bool 플래그가 다시 추가됐습니다: $removedDataFlag" }
}
foreach ($ddayCardFeature in @('(x.Start.Date - DateTime.Today).Days >= -7', 'isToday ? "D-Day"', 'Colors["D-Day"]', 'Colors["기념일"]', 'CategoryColorSystem.DetailBackground')) {
    if (-not $mainSource.Contains($ddayCardFeature)) { throw "D-Day 카드 7일 보존 규칙이 누락됐습니다: $ddayCardFeature" }
}
$sportsSource = Get-Content (Join-Path $PSScriptRoot 'SportsCalendarWindow.cs') -Raw -Encoding UTF8
$sportsApiSource = Get-Content (Join-Path $PSScriptRoot 'SportsApiWindows.cs') -Raw -Encoding UTF8
$plannerSettingsSource = Get-Content (Join-Path $PSScriptRoot 'PlannerSettings.cs') -Raw -Encoding UTF8
foreach ($sportsScaleFeature in @('SportsCalendarScale = 1.0', 'ViewScaleChanged', 'settings.SportsCalendarScale', 'Store.SaveSettings(settings)')) {
    if (-not (($plannerSettingsSource + $sportsSource + $sportsApiSource).Contains($sportsScaleFeature))) { throw "KBO view scale persistence is missing: $sportsScaleFeature" }
}
if (-not $mainSource.Contains('if (opacitySlider != null) settings.Opacity = Math.Max(opacitySlider.Minimum')) {
    throw '고정 레이어 임시 WPF Opacity가 사용자 설정으로 저장될 수 있습니다.'
}
foreach ($resizeFeature in @('if (point.X <= edge) return 5;', 'if (point.X >= surface.ActualWidth - edge) return 6;', 'if (point.Y <= edge) return 7;', 'if (point.Y >= surface.ActualHeight - edge) return 8;', 'UiCursor.ResizeHorizontal', 'UiCursor.ResizeVertical')) {
    if (-not $mainSource.Contains($resizeFeature)) { throw "Eight-direction resize is missing: $resizeFeature" }
}
foreach ($removedConflictFeature in @('desktopcal', 'dkdockhost', 'WarnDesktopCalendarConflict')) {
    if ($mainSource.Contains($removedConflictFeature)) { throw "Removed desktop-calendar warning remains: $removedConflictFeature" }
}
foreach ($positionEditorFeature in @('void ShowPositionEditor()', 'Topmost = false; ShowInTaskbar = false;', 'if (positionLocked) SchedulePublish();')) {
    if (-not $mainSource.Contains($positionEditorFeature)) { throw "Position editor foreground behavior is missing: $positionEditorFeature" }
}
$explorerLayerSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.ExplorerLayer.cs') -Raw -Encoding UTF8
foreach ($compactTitleFeature in @('void UpdateCompactHeaderTypography()', 'monthTitle.FontSize = 17')) {
    if (-not $mainSource.Contains($compactTitleFeature)) { throw "작은 화면용 고정 날짜 제목이 누락됐습니다: $compactTitleFeature" }
}
if ($explorerLayerSource -notmatch '(?s)DwmwaCloak = 13.*?void PublishAndCloak\(\).*?explorerFrame\.Publish.*?LayerHostController\.Start.*?explorerFrame\.SetActionSink.*?SetWindowCloaked\(true\).*?void ShowPreparedWpf.*?Opacity = 0;.*?SetWindowCloaked\(false\).*?BeginPreparedWpfSettle.*?Opacity = intendedOpacity;.*?explorerFrame\.Disable\(\)') {
    throw 'Fixed/edit transitions must prewarm WPF, restore its opacity, and then release the Explorer frame without animation.'
}
if ($mainSource.Contains('Topmost = true; ShowInTaskbar = true;')) { throw 'Position editor is incorrectly pinned above every application.' }
if ($mainSource.Contains('DragMove(); DesktopLayer.Lower(this);')) { throw 'Dragging the position editor still lowers it behind other windows.' }
foreach ($closeFeature in @('OpenLogoMenu(logo)', 'action == 25) { ExecuteCloseButtonAction(); return;', 'action == 28) { OpenCloseContextMenu(); return;', 'settings.CloseButtonAction == "confirm_exit"', 'ContextMenu CreateCloseContextMenu()', 'exit.Click += delegate { ExitApplication(); };', 'void CloseAuxiliaryWindows()')) {
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
foreach ($removedAnniversaryEntry in @('RegisterAsAnniversary', 'registerAnniversary', '기념일로 등록', 'anniversaryDateCard')) {
    if ($addItemSource.Contains($removedAnniversaryEntry)) { throw "일반 일정창에 제거된 기념일 등록 경로가 남았습니다: $removedAnniversaryEntry" }
}
foreach ($anniversaryConversionFeature in @('new AnniversaryWindow(existing)', 'ConvertToScheduleRequested', 'void ConvertToSchedule(', 'if (window.ConvertToScheduleRequested)')) {
    if (-not (($addItemSource + $mainSource + (Get-Content (Join-Path $PSScriptRoot 'AnniversaryWindow.cs') -Raw -Encoding UTF8)).Contains($anniversaryConversionFeature))) { throw "Anniversary conversion is missing: $anniversaryConversionFeature" }
}
foreach ($compactScheduleFeature in @('CompactScrollHeight(SystemParameters.WorkArea.Height)', 'workAreaHeight * .78 - 64', 'Grid.SetColumn(minuteGrid, 1)', 'Color.FromRgb(3, 105, 161)',
    'Height = 46', 'Content = "✓  일정 저장"', 'Content = "직접 선택"', 'SettingsWindow.StyleComboBox(customReminderUnit)')) {
    if (-not $addItemSource.Contains($compactScheduleFeature)) { throw "노트북용 일정창 압축 구성이 누락됐습니다: $compactScheduleFeature" }
}
foreach ($removedReminderShortcut in @('Name = "10분 전"', 'Name = "30분 전"', 'Name = "하루 전"')) {
    if ($addItemSource.Contains($removedReminderShortcut)) { throw "일정창에 제거된 알림 빠른 선택이 남았습니다: $removedReminderShortcut" }
}
$settingsSource = Get-Content (Join-Path $PSScriptRoot 'SettingsWindow.cs') -Raw -Encoding UTF8
if (-not $settingsSource.Contains('workAreaHeight * .70 - 60')) { throw '노트북용 설정창 높이 제한이 누락됐습니다.' }
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
$calendarSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Calendar.cs') -Raw -Encoding UTF8
foreach ($compactLaneFeature in @('laneOccupancy.FindIndex', 'occupied.All(range =>', 'var eventLaneLimit = visibleEventLanes;')) {
    if (-not $calendarSource.Contains($compactLaneFeature)) { throw "Compact calendar lane allocation is missing: $compactLaneFeature" }
}
if ($calendarSource.Contains('⌄  +') -or $calendarSource.Contains('개 일정이 셀 높이를 넘어')) { throw 'Calendar overflow summary bar must remain removed.' }
$displaySource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Display.cs') -Raw -Encoding UTF8
foreach ($googleFilterFeature in @('HorizontalAlignment = HorizontalAlignment.Left', 'var useTwoColumns = boxes.Count >= 4', 'var split = (boxes.Count + 1) / 2')) {
    if (-not $displaySource.Contains($googleFilterFeature)) { throw "Google filter hit-area or balanced columns are missing: $googleFilterFeature" }
}
$publisherSource = Get-Content (Join-Path $PSScriptRoot 'ExplorerFramePublisher.cs') -Raw -Encoding UTF8
if (-not $publisherSource.Contains('static IntPtr FindDesktopIconList()') -or -not $publisherSource.Contains('Native.EnumWindows')) { throw 'WorkerW desktop discovery is missing from frame publisher.' }
foreach ($pointerRefreshFeature in @('void SchedulePointerRefresh()', 'Mouse.Synchronize();', 'PostMessage(target, 0x0200', 'PostMessage(target, 0x0020')) {
    if (-not $mainSource.Contains($pointerRefreshFeature)) { throw "Position-mode pointer refresh is missing: $pointerRefreshFeature" }
}
foreach ($calendarTodoFeature in @('localPoint.X <= Ui(23)', 'await SetTodoCompleted(item, !item.Completed)', 'ToggleTodoFromDesktop(itemHit.Item)')) {
    if (-not $mainSource.Contains($calendarTodoFeature)) { throw "Calendar Todo checkbox interaction is missing: $calendarTodoFeature" }
}
$settingsUiSource = Get-Content (Join-Path $PSScriptRoot 'SettingsWindow.cs') -Raw -Encoding UTF8
foreach ($settingsGroupingFeature in @('Text = "화면과 동작"', 'Text = "시작 요일"', 'GroupName = "WeekStartDay"', 'weekRuleRow.Children.Add(showWeek); weekRuleRow.Children.Add(weekRules)', 'weekRules.Visibility = showWeek.IsChecked == true', 'displayGroup.Children.Add(calendarOptions)', 'displayGroup.Children.Add(otherDisplayOptions)')) {
    if (-not $settingsUiSource.Contains($settingsGroupingFeature)) { throw "Settings display grouping is missing: $settingsGroupingFeature" }
}
foreach ($independentWeekFeature in @('DayOfWeek ConfiguredFirstDay()', 'settings.WeekNumberRule == "iso" ? DayOfWeek.Monday : DayOfWeek.Sunday')) {
    if (-not $mainSource.Contains($independentWeekFeature)) { throw "Independent week-start or week-number rule is missing: $independentWeekFeature" }
}
foreach ($buttonFeedbackFeature in @('System.Windows.Controls.Button.IsPressedProperty', 'void FlashDesktopButton(Button button)', 'SettingsGlyph(Brush foreground)')) {
    if (-not $mainSource.Contains($buttonFeedbackFeature)) { throw "Header button feedback or vector settings icon is missing: $buttonFeedbackFeature" }
}
if ($mainSource.Contains('System.Windows.Controls.Button.IsMouseOverProperty')) { throw 'Default header buttons must use cursor-only hover feedback.' }
foreach ($recurrenceDurationFeature in @('durationDays <= 7 && frequency == "weekly"', 'durationDays >= 8 && frequency == "monthly"', 'UpdateEndDateButton(); UpdateRecurrenceAvailability();')) {
    if (-not $addItemSource.Contains($recurrenceDurationFeature)) { throw "Multi-day recurrence duration rule is missing: $recurrenceDurationFeature" }
}
if ($addItemSource.Contains('"✓  수정 저장"') -or $addItemSource.Contains('Content = "✓  저장"')) { throw 'Schedule save buttons must use the same schedule-save caption.' }
foreach ($ddayIndependenceFeature in @('D-Day is an independent summary view', 'Task SetTodoCompleted(PlannerItem item, bool completed)', 'ToggleTodoFromDesktop(itemHit.Item)')) {
    if (-not $mainSource.Contains($ddayIndependenceFeature)) { throw "D-Day independence or detail title toggle is missing: $ddayIndependenceFeature" }
}
foreach ($specialDayFilterFeature in @('Text = "Special Day Card"', 'var specialFilterRow = new StackPanel', 'Grid.SetRowSpan(divider, 2)', 'Width = new GridLength(118)', 'var localFilterRow = new UniformGrid { Columns = 2 }', '"야구"')) {
    if (-not $mainSource.Contains($specialDayFilterFeature)) { throw "Special Day filter grouping is missing: $specialDayFilterFeature" }
}
$detailSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Detail.cs') -Raw -Encoding UTF8
if ($detailSource.Contains('Task.Delay(230)') -or $detailSource.Contains('await SetTodoCompleted(item, !item.Completed)')) {
    throw 'Detail-card title must not toggle Todo completion; only its checkbox may do so.'
}
foreach ($detailCheckboxFeature in @('check.Click += async delegate', 'await SetTodoCompleted(item, check.IsChecked == true)', 'if (e.ClickCount != 2) return;')) {
    if (-not $detailSource.Contains($detailCheckboxFeature)) { throw "Detail-card checkbox-only interaction is missing: $detailCheckboxFeature" }
}
$timetableSource = Get-Content (Join-Path $PSScriptRoot 'TimetableWindow.cs') -Raw -Encoding UTF8
$timetableDataSource = Get-Content (Join-Path $PSScriptRoot 'TimetableData.cs') -Raw -Encoding UTF8
$timetableMainSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Timetable.cs') -Raw -Encoding UTF8
foreach ($timetableFeature in @('PeriodCount = 9', 'Height = 568', 'FontSize = 13', 'TimetableStorage.Save(data)', 'settingsPanel.Visibility = Visibility.Collapsed', 'OnharuPopupChrome.EnableDrag(this, header)', 'e.GetPosition(root).Y > 48')) {
    if (-not ($timetableSource + $timetableDataSource).Contains($timetableFeature)) { throw "Timetable nine-period/save/drag behavior is missing: $timetableFeature" }
}
if (-not $timetableMainSource.Contains('timetableWindow.Show();') -or $timetableMainSource.Contains('ShowDialog()')) { throw 'Timetable must remain an independent non-modal window.' }
$publisherSource = Get-Content (Join-Path $PSScriptRoot 'ExplorerFramePublisher.cs') -Raw -Encoding UTF8
$desktopInputSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.DesktopInput.cs') -Raw -Encoding UTF8
if (-not $publisherSource.Contains('public void UpdateOpacity(double opacity)') -or -not $desktopInputSource.Contains('explorerFrame.UpdateOpacity(settings.Opacity)')) {
    throw 'Fixed-layer opacity must use the cached-frame opacity update path.'
}
foreach ($monthlySpanFeature in @('var startsOnLastDay = selectedDate.Day == DateTime.DaysInMonth', 'if (startsOnLastDay)', '부터 같은 기간')) {
    if (-not $addItemSource.Contains($monthlySpanFeature)) { throw "Context-aware multi-day monthly option is missing: $monthlySpanFeature" }
}
foreach ($rolloverLayoutFeature in @('Text = "이월"', 'Content = "없음"', 'Content = "다음날"', 'Content = "다음주 같은 요일"', 'Content = "다음 평일"')) {
    if (-not $addItemSource.Contains($rolloverLayoutFeature)) { throw "Compact rollover row is missing: $rolloverLayoutFeature" }
}
$reminderSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Reminders.cs') -Raw -Encoding UTF8
foreach ($businessDayFeature in @('mode == "next_week" ? date.AddDays(7)', 'holiday != null && holiday(date)', 'x.Category == "국경일" && OccursOnDate(x, date)')) {
    if (-not $reminderSource.Contains($businessDayFeature)) { throw "Holiday-aware next-week rollover is missing: $businessDayFeature" }
}
foreach ($reminderAlignmentFeature in @('StyleInput(customReminderValue)', 'customReminderValue.Padding = new Thickness(4, 0, 4, 0)', 'customReminderUnit.Width = 80')) {
    if (-not $addItemSource.Contains($reminderAlignmentFeature)) { throw "Custom reminder alignment is missing: $reminderAlignmentFeature" }
}
foreach ($scheduleCardPolishFeature in @('readonly CheckBox recurrenceEnabled', 'recurrenceUntilButton.Width = 94', 'recurrenceUntilMode', 'recurrenceLine.Children.Add(recurrenceBody)', 'BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty)', 'Grid.SetColumn(reminderOptions, 1); reminderLine.Children.Add(reminderOptions)', 'StyleInput(recurrenceCountValue)')) {
    if (-not $addItemSource.Contains($scheduleCardPolishFeature)) { throw "Schedule card polish is missing: $scheduleCardPolishFeature" }
}
foreach ($timeModeLayoutFeature in @('readonly CheckBox allDay', 'readonly CheckBox morning', 'readonly CheckBox afternoon', 'durationRow.Children.Add(allDay); durationRow.Children.Add(multiDay)', 'void SelectTimeMode(CheckBox selected)', 'void EnsureTimeModeSelected()')) {
    if (-not $addItemSource.Contains($timeModeLayoutFeature)) { throw "Checkbox time-mode layout is missing: $timeModeLayoutFeature" }
}
foreach ($conditionalTimeFeature in @('hourGrid.Visibility = isAllDay ? Visibility.Collapsed : Visibility.Visible', 'minuteRow.Visibility = isAllDay ? Visibility.Collapsed : Visibility.Visible')) {
    if (-not $addItemSource.Contains($conditionalTimeFeature)) { throw "Conditional time selector is missing: $conditionalTimeFeature" }
}
$anniversaryWindowSource = Get-Content (Join-Path $PSScriptRoot 'AnniversaryWindow.cs') -Raw -Encoding UTF8
foreach ($anniversaryDateFeature in @('YYYYMMDD', 'DateTime.TryParseExact', '예: 19760916')) {
    if (-not $anniversaryWindowSource.Contains($anniversaryDateFeature)) { throw "Anniversary compact-date validation is missing: $anniversaryDateFeature" }
}
$anniversaryMainSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Anniversary.cs') -Raw -Encoding UTF8
if ($anniversaryMainSource.Contains('selectedDate = NextAnniversaryDate(start, DateTime.Today)')) { throw 'Anniversary save must not navigate the main calendar.' }
$settingsMainSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Settings.cs') -Raw -Encoding UTF8
foreach ($restoreSafetyFeature in @('var googleItems = items.Where(Store.IsGoogleItem).ToList()', 'Google 일정에는 영향을 주지 않았습니다')) {
    if (-not $settingsMainSource.Contains($restoreSafetyFeature)) { throw "Local-only backup restore safety is missing: $restoreSafetyFeature" }
}
$reminderSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Reminders.cs') -Raw -Encoding UTF8
foreach ($independentReminderFeature in @('void ShowIndependentReminder', 'SetApartmentState(ApartmentState.STA)', 'Dispatcher.BeginInvoke')) {
    if (-not $reminderSource.Contains($independentReminderFeature)) { throw "Independent reminder window is missing: $independentReminderFeature" }
}
foreach ($tenMinuteFeature in @('minuteGrid = new UniformGrid { Columns = 6', 'new[] { 0, 10, 20, 30, 40, 50 }', 'Margin = new Thickness(0, 4, 4, 4), HorizontalAlignment = HorizontalAlignment.Left')) {
    if (-not $addItemSource.Contains($tenMinuteFeature)) { throw "Ten-minute selector is missing: $tenMinuteFeature" }
}
foreach ($yearlyModeFeature in @('yearlyNth.IsChecked == true ? "yearly_nth" : "yearly_date"', '매년 같은 날짜', '매년 같은 주·요일')) {
    if (-not $addItemSource.Contains($yearlyModeFeature)) { throw "Context-aware yearly recurrence UI is missing: $yearlyModeFeature" }
}
foreach ($compactDetailFeature in @('titleText.Inlines.Add(new System.Windows.Documents.Run', 'Foreground = T("Disabled")', 'if (!item.AllDay && IsMultiDay(item))', 'Margin = new Thickness(0, 8, 0, 0)')) {
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
