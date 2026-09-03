param([string]$ExePath)
$ErrorActionPreference = 'Stop'

$mainSources = (Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'MainWindow*.cs' | ForEach-Object { Get-Content $_.FullName -Raw -Encoding UTF8 }) -join "`n"
$settingsSource = Get-Content (Join-Path $PSScriptRoot 'SettingsWindow.cs') -Raw -Encoding UTF8
$addItemSource = Get-Content (Join-Path $PSScriptRoot 'AddItemWindow.cs') -Raw -Encoding UTF8
$storageSource = Get-Content (Join-Path $PSScriptRoot 'LocalStorage.cs') -Raw -Encoding UTF8
if ($storageSource.Contains('Samples()') -or $storageSource.Contains('가족 저녁 식사') -or $storageSource.Contains('주간 업무 보고')) { throw 'Clean install must not create sample schedules.' }
$calendarStyleSource = Get-Content (Join-Path $PSScriptRoot 'OnharuCalendarStyle.cs') -Raw -Encoding UTF8
$deleteSource = Get-Content (Join-Path $PSScriptRoot 'LocalDataDeleteWindow.cs') -Raw -Encoding UTF8
$cursorSource = Get-Content (Join-Path $PSScriptRoot 'UiCursor.cs') -Raw -Encoding UTF8
foreach ($undoFeature in @('UiCursor.DragCopy', 'GetAsyncKeyState(0x11)', 'UiCursor.ControlDown', 'TimeSpan.FromMilliseconds(25)', 'dragCursorTimer.Start()', 'QueryContinueDrag', 'RegisterUndo(copy ? "일정 복사" : "일정 이동"', 'RegisterCreateUndo(window.Result)', 'RegisterEditUndo(originalItem)', 'RegisterUnsupportedUndo("반복 일정', 'RegisterDeleteUndo(deletedItems', 'await UndoCalendarAction()')) {
    if (-not (($mainSources + $cursorSource).Contains($undoFeature))) { throw "Calendar copy cursor or undo support is missing: $undoFeature" }
}
if ($mainSources.Contains('action.Name + "을 되돌')) { throw 'Undo result message uses an invalid fixed Korean object particle.' }
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
foreach ($settledPlacementFeature in @('GetDpiForWindow', 'if (hasTarget) MatchWindowToPhysicalFrame(target);', 'Opacity = intendedOpacity', 'explorerFrame.Disable()')) {
    if (-not $mainSources.Contains($settledPlacementFeature)) { throw "Candidate-17 WPF handoff is missing: $settledPlacementFeature" }
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
if ($settings.Version -ne 48) { throw "Unexpected settings version: $($settings.Version)" }
if ($settings.IncompleteTodoLookbackMonths -ne 1) { throw 'Incomplete To-Do lookback must default to one month.' }
if ($settings.ThemeId -ne 'classic') { throw "Theme must default to classic: $($settings.ThemeId)" }
if (-not $settings.AutomaticUpdateChecks) { throw 'Automatic update checks must default to enabled.' }
if ($settings.UseMonthView -or $settings.VisibleWeekCount -ne 4) { throw 'Clean-install calendar must default to a four-week view.' }
if ($settingsType.GetField('CalendarRangeMode') -or $settingsType.GetField('MonthRangeMode') -or $settingsType.GetField('TodayRow')) { throw 'Removed calendar-range settings must not remain.' }
if ($settings.PositionLocked -or $settings.StartupPositionMode -ne 'editable' -or $settings.Width -ne 1120 -or $settings.Height -ne 700) { throw 'Clean-install placement defaults are invalid.' }
if ($settings.ThemeId -ne 'classic' -or $settings.FontSize -ne 12 -or $settings.Opacity -ne .95 -or $settings.SelectedPaletteIndex -ne 0) { throw 'Clean-install visual defaults are invalid.' }
if (-not $settings.MultiDayFirst -or $settings.CompletedDisplayMode -ne 'fade' -or -not $settings.RemindersEnabled) { throw 'Clean-install behavior defaults are invalid.' }
if (-not $settings.ShowWeekNumbers -or $settings.WeekNumberRule -ne 'iso' -or $settings.WeekStartDay -ne 'sunday') { throw 'Clean-install week defaults are invalid.' }
if ($settings.SelectedDateStyle -ne 'border' -or $settings.SelectedDateBorderColor -ne '#EC4899' -or $settings.TodayStyle -ne 'icon') { throw 'Clean-install date marker defaults are invalid.' }
if (-not $settings.ShowLunar -or -not $settings.ShowSolarTerms -or -not $settings.UseRollover -or $settings.AutoSyncMinutes -ne 5) { throw 'Clean-install display and sync defaults are invalid.' }
if ($settings.BusinessColor -ne '#00A6C8' -or $settings.PersonalColor -ne '#2859C5' -or $settings.BaseballColor -ne '#38A169' -or $settings.DdayColor -ne '#E67E22' -or $settings.AnniversaryColor -ne '#C2418C' -or $settings.HolidayColor -ne '#DC2626') { throw 'Clean-install category colors are invalid.' }
if ($settings.ShowGoogleTasks) { throw 'Google Tasks must be opt-in by default.' }
if ($settings.UseTimetable) { throw 'Timetable must be opt-in by default.' }
if (-not $settings.UseDiary) { throw 'Diary must be visible by default.' }
if (-not $settings.DdayPanelVisible) { throw 'D-Day panel must default to visible.' }
if (-not $settings.CompletedLast) { throw 'CompletedLast must default to true.' }
if ($settings.DefaultCalendarKey -ne 'local:business' -or -not $settings.DefaultAllDay -or $settings.DefaultStartHour -ne 9) { throw 'New-item defaults are invalid.' }
if ($settingsType.GetField('DefaultDurationMinutes')) { throw 'Removed default-duration setting must not remain.' }
if ($settings.StartViewMode -ne 'today') { throw 'Clean-install start date must be today.' }
if (-not $settings.ReminderSound -or $settings.QuietStartHour -ne 22 -or $settings.QuietEndHour -ne 7) { throw 'Reminder defaults are invalid.' }
if ($null -eq $settings.DateBackgroundColors) { throw 'DateBackgroundColors must be initialized.' }
foreach ($typeName in @('FamilyPlanner.TimetableData', 'FamilyPlanner.TimetableSlot', 'FamilyPlanner.TimetableWindow')) {
    if ($null -eq $assembly.GetType($typeName, $false)) { throw "Timetable type is missing: $typeName" }
}
foreach ($typeName in @('FamilyPlanner.DiaryEntry', 'FamilyPlanner.DiaryStore', 'FamilyPlanner.DiaryEditorWindow', 'FamilyPlanner.DiaryReaderWindow')) {
    if ($null -eq $assembly.GetType($typeName, $false)) { throw "Diary type is missing: $typeName" }
}
# 일기장은 기능에서 제외하고 소스는 재사용을 위해 남겨 둔다. 남은 진입점만 검사한다.
# 2026-09-01: 제외 결정으로 두 번째 호출 지점 'OpenDiaryEditor(date)'가 사라져 토큰에서 뺐다.
$diarySource = Get-Content (Join-Path $PSScriptRoot 'DiaryWindows.cs') -Raw -Encoding UTF8
# 2026-09-03: 일기 쓰기는 읽기 창이 직접 연다(DiaryWindows.cs). 메인의 OpenDiaryEditor는
# 죽은 히트 타깃 분기에서만 불리던 것이라 함께 지웠다.
foreach ($diaryFeature in @('new DiaryEditorWindow(date, entry)', 'OpenDiaryReader', 'settings.UseDiary')) {
    if (-not ($mainSources + $diarySource).Contains($diaryFeature)) { throw "Diary integration is missing: $diaryFeature" }
}
# 2026-09-02: 일기장을 기능에서 제외하며 달력 날짜 숫자·점의 일기 열기 연결을 제거했다.
# 2026-09-03: 남아 있던 DiaryDateHitTarget 판정은 그 표식을 다는 곳이 없는 죽은 분기여서 지웠다.
# 바탕화면 더블클릭으로 일정을 추가하는 경로는 살아 있어 계속 검사한다.
if (-not $mainSources.Contains('if (e.ClickCount == 2) AddItem(sender, e)')) {
    throw 'Diary date-only input routing is missing: if (e.ClickCount == 2) AddItem(sender, e)'
}
if ($mainSources.Contains('lunar.MouseLeftButtonDown += openDiary')) { throw 'Lunar text must not open the diary editor.' }
foreach ($logoutConfirmFeature in @('if (!ConfirmGoogleLogout()) return;', 'GoogleLogoutConfirmWindow', 'Google 계정에서 로그아웃하시겠습니까?', '온하루 로컬 일정은 그대로 유지됩니다.')) {
    if (-not (($mainSources + (Get-Content (Join-Path $PSScriptRoot 'GoogleAccountActionWindow.cs') -Raw -Encoding UTF8)).Contains($logoutConfirmFeature))) { throw "Google logout confirmation is missing: $logoutConfirmFeature" }
}
# 2026-09-02: 설정창의 일기장 항목은 기능 제외와 함께 사라졌다.
foreach ($taskReadOnlyFeature in @('Content = "Google Tasks"', 'Text = " · 읽기 전용, 완료 체크만 가능"', 'googleDetailGrid.Children.Add(taskRow)', 'GoogleTasks.IsTask(item) || !source.Editable', 'item.GoogleReadOnly = true')) {
    if (-not (($settingsSource + $mainSources + (Get-Content (Join-Path $PSScriptRoot 'GoogleTasksService.cs') -Raw -Encoding UTF8)).Contains($taskReadOnlyFeature))) { throw "Google Tasks read-only opt-in is missing: $taskReadOnlyFeature" }
}
$desktopHookSource = Get-Content (Join-Path (Split-Path -Parent $PSScriptRoot) 'ExplorerLayer\DesktopHook.cpp') -Raw -Encoding UTF8
foreach ($fixedUndoFeature in @('g_onharuOwnsUndo = insideOnharu', 'WH_KEYBOARD', 'WM_KILLFOCUS', "wParam == 'Z'", 'SetFocus(hwnd)', 'PostDesktopAction(110, 0)', 'action == 110', 'UndoCalendarAction()')) {
    if (-not (($desktopHookSource + $mainSources).Contains($fixedUndoFeature))) { throw "Fixed-mode undo feature missing: $fixedUndoFeature" }
}
if ($desktopHookSource.Contains('SetForegroundWindow(GetAncestor(hwnd, GA_ROOT))')) { throw 'Fixed-layer clicks must not foreground Explorer; this repaints the desktop during mode transitions.' }
if ($settingsSource.Contains('syncCard.Children.Add(googleTasks)')) { throw 'Google Tasks must not be placed in automatic sync settings.' }
foreach ($groupFilterFeature in @('HeaderAllFilter("온하루 일정 전체 선택/해제"', 'HeaderAllFilter("Google 일정 전체 선택/해제"', 'HeaderAllFilter("Special Day Card 전체 선택/해제"', 'SetLocalFilters(bool visible)', 'SetGoogleFilters(bool visible)', 'SetSpecialFilters(bool visible)')) {
    if (-not $mainSources.Contains($groupFilterFeature)) { throw "Detail group all-filter is missing: $groupFilterFeature" }
}
foreach ($addItemAlignmentFeature in @('showDday.Height = important.Height = 20', 'importantColors.Height = 20', 'titleCaption.VerticalAlignment = VerticalAlignment.Center')) {
    if (-not $addItemSource.Contains($addItemAlignmentFeature)) { throw "Add-item D-Day/important alignment is missing: $addItemAlignmentFeature" }
}
foreach ($detailBorderRule in @('importantCard ? EventTextBrush(categoryItems[0])', 'settings.ThemeId == "dark" ? T("Grid") : Brush("#CBD5E1")', 'CategoryColorSystem.DetailBorder(settings.ThemeId, groupColor)', 'BorderThickness = new Thickness(1)')) {
    if (-not $mainSources.Contains($detailBorderRule)) { throw "Detail-card border rule is missing: $detailBorderRule" }
}
foreach ($dragFeature in @('EnableItemDrag(bar, item)', 'EnableItemDrag(row, item)', 'IsInteractiveDragContent', 'EnableCalendarDrop()', 'MoveItemToDate(item, targetDate.Value', 'DragDropEffects.Move | DragDropEffects.Copy', 'CopyItem(item)')) {
    if (-not $mainSources.Contains($dragFeature)) { throw "Calendar item drag-and-drop is missing: $dragFeature" }
}
if (-not $settingsSource.Contains('드래그로 일정 옮기기') -or -not $mainSources.Contains('settings.AllowGoogleDragMove')) { throw 'Google drag-move opt-in is missing.' }
if (-not $mainSources.Contains('IsAutomaticSportsItem(item)') -or -not $mainSources.Contains('settings.BaseballVisible')) { throw 'KBO drag protection or independent category visibility is missing.' }
if (-not $mainSources.Contains('item.GoogleEventType == "birthday"') -or -not $mainSources.Contains('반복 일정 원본은 이동할 수 없습니다.')) { throw 'Google recurring-instance drag policy is invalid.' }
foreach ($dragFeedback in @('ONHARU_BLOCKED_ITEM_DRAG', 'DragRestriction(item)')) {
    if (-not $mainSources.Contains($dragFeedback)) { throw "Drag feedback or baseball activation is missing: $dragFeedback" }
}
foreach ($detailOrderFeature in @('EnableDetailCardOrder(groupHeaderSurface, detailCard, groupKey)', 'Mouse.Capture(card)', 'ReorderDetailGroup(groupName, (string)targetCard.Tag', 'settings.DetailCategoryOrder')) {
    if (-not $mainSources.Contains($detailOrderFeature)) { throw "Detail card drag order is missing: $detailOrderFeature" }
}
# 2026-09-02: 달력의 일기 표시와 리더 자동 닫기는 기능 제외로 사라졌다. 설정 플래그 확인만 남긴다.
foreach ($diaryToggleFeature in @('if (!settings.UseDiary)')) {
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
# 2026-09-01: 검색 범위를 콤보에서 공통 슬라이딩 버튼으로 바꿔 StyleComboBox 의존이 사라졌고,
# 결과 행은 54px 카드에서 42px 목록 행이 되어 12,5,12,5 여백을 쓰지 않는다.
foreach ($searchPolishFeature in @('new OnharuSegmentedSwitch(labels', 'void ScheduleRender()', 'DispatcherPriority.Background', 'StyleDatePicker(customFrom)', 'UiRound.SoftenScrollBars(resultScroller)', 'Height = 42, HorizontalContentAlignment')) {
    if (-not $searchSource.Contains($searchPolishFeature)) { throw "Search visual or responsiveness polish is missing: $searchPolishFeature" }
}
foreach ($comboFeature in @('FontSizeProperty, 11.5', 'combo.PreviewMouseLeftButtonDown', 'ItemsControl.ContainerFromElement(combo', 'combo.IsDropDownOpen = !combo.IsDropDownOpen', 'OnharuPopupChrome.SelectionSurfaceColor')) {
    if (-not $settingsSource.Contains($comboFeature)) { throw "Shared combo box behavior is missing: $comboFeature" }
}
# 2026-09-02: BrandGradient()를 OnharuPopupChrome.BrandGradientBrush()로 올려 검색·시간표·알람·KBO가 같은 정의를 쓴다.
# 2026-09-01: 검색 배치를 '목록 밀도'안으로 확정했다. 제목·입력·오늘·닫기를 한 행에 모으고,
# 범위는 항상 펼친 슬라이딩 버튼, 결과는 42px 목록 행으로 둔다. 결과 목록은 Grid 4행 중 '*' 행이라
# 창 크기와 사용자 지정 행 표시 여부에 관계없이 남은 공간을 채운다.
foreach ($searchLayoutFeature in @('var titleGroup = new StackPanel', 'DockPanel.SetDock(todayButton, Dock.Right)', 'OnharuCalendarStyle.Create()',
    'range.SetPalette(', 'panel.RowDefinitions.Add(new RowDefinition())', 'Grid.SetRow(listShell, 3)',
    'picker.CalendarStyle = OnharuCalendarStyle.Create()', 'todayButton.Background = OnharuPopupChrome.BrandGradientBrush()')) {
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
# 2026-09-02: `OnharuDetailCard:...` 자동화 ID와 `ddayColorChanged`를 요구하던 단언 두 개를 제거했다.
# 두 토큰은 저장소 전체 이력의 어떤 C# 소스에도 존재한 적이 없다. `git log -S`로 확인하면 커밋
# 8902881에서 이 검사 파일에만 추가됐고, 대응하는 운영 코드는 들어온 적이 없다.
# 즉 회귀를 잡던 검사가 낡은 것이 아니라, 구현되지 않은 계획을 단언으로 적어 둔 것이었다.
# 요소별 색상 오버라이드 맵 자체가 이 설계에 없으므로 되돌릴 오버라이드도 없다.
# 상세 카드 색이 한 모듈에서만 계산된다는 실제 불변식은 바로 위 `CategoryColorSystem.DetailBackground`
# 단언이 이미 지킨다. 아이디어 자체는 `TODO_NEXT.md`에 남겼다.
$detailCardSource = $mainSources + (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'CategoryColorSystem.cs') -Raw -Encoding UTF8)
foreach ($detailCardColorFeature in @('CategoryColorSystem.DetailBackground', 'CategoryColorSystem.DetailBorder', 'IsDetailCardNonDragControl')) {
    if (-not $detailCardSource.Contains($detailCardColorFeature)) { throw "상세 카드 색상·입력 판정이 공통 모듈을 벗어났습니다: $detailCardColorFeature" }
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
$chromeSource = Get-Content (Join-Path $PSScriptRoot 'OnharuPopupChrome.cs') -Raw -Encoding UTF8
$themeSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Theme.cs') -Raw -Encoding UTF8
$layoutSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Layout.cs') -Raw -Encoding UTF8
# 기능 아이콘 줄은 오른쪽에 붙어 왼쪽으로 늘어난다. 전부 켰을 때만 기간 스위치와 왼쪽 선이 맞는다.
# 보이는 첫 아이콘을 기준으로 삼으면 하나만 켜도 줄이 왼쪽으로 끌려간다.
foreach ($featureRowFeature in @('double FullFeatureRowWidth()', 'var delta = areaRight - fullWidth - switchLeft;', 'const double MaxFeatureRowShift = 38;', 'Math.Abs(delta) > MaxFeatureRowShift) return;')) {
    if (-not $layoutSource.Contains($featureRowFeature)) { throw "Feature icon row anchoring is missing: $featureRowFeature" }
}
if ($layoutSource.Contains('FirstVisibleFeatureButton')) { throw 'Feature icon row must not align to the first visible icon.' }
# 아이콘만 있는 헤더 버튼은 도구 설명을 자동화 이름으로 삼는다. 그러지 않으면 이름이 빈 칸이다.
foreach ($iconNameFeature in @('internal static void NameFromToolTip(FrameworkElement element)', 'AutomationProperties.SetName(element, text);')) {
    if (-not $chromeSource.Contains($iconNameFeature)) { throw "Icon button accessible name helper is missing: $iconNameFeature" }
}
foreach ($iconNameCaller in @('void StyleHeaderActionButton(Button button, string glyph)
        {
            if (button == null) return;
            OnharuPopupChrome.NameFromToolTip(button);', 'OnharuPopupChrome.NameFromToolTip(button);
            if (settings.ThemeId == "dark")')) {
    if (-not $themeSource.Contains($iconNameCaller)) { throw 'Header icon buttons must take their accessible name from the tooltip.' }
}
# 2026-09-03: 여덟 방향 판정은 팝업 공통 함수 하나가 기준이다. 메인 창 사본은 지웠다.
foreach ($resizeFeature in @('if (point.X <= edge) return 5;', 'if (point.X >= surface.ActualWidth - edge) return 6;', 'if (point.Y <= edge) return 7;', 'if (point.Y >= surface.ActualHeight - edge) return 8;', 'UiCursor.ResizeHorizontal', 'UiCursor.ResizeVertical')) {
    if (-not $chromeSource.Contains($resizeFeature)) { throw "Eight-direction resize is missing: $resizeFeature" }
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
if ($explorerLayerSource -notmatch '(?s)DwmwaCloak = 13.*?void PublishAndCloak\(\).*?explorerFrame\.Publish.*?LayerHostController\.Start.*?explorerFrame\.SetActionSink.*?SetWindowCloaked\(true\).*?void ShowPreparedWpf.*?Opacity = 0;.*?BeginPreparedWpfSettle.*?Opacity = intendedOpacity;.*?SetWindowCloaked\(false\);.*?explorerFrame\.Disable\(\)') {
    throw 'Fixed/edit transitions must prepare the visible WPF surface while cloaked, then uncloak it before releasing the Explorer frame.'
}
if ($mainSource.Contains('Topmost = true; ShowInTaskbar = true;')) { throw 'Position editor is incorrectly pinned above every application.' }
if ($mainSource.Contains('DragMove(); DesktopLayer.Lower(this);')) { throw 'Dragging the position editor still lowers it behind other windows.' }
foreach ($closeFeature in @('action == 25) { MinimizeToTray(); return;', 'ContextMenu CreateCloseContextMenu()', 'exit.Click += delegate { ExitApplication(); };', 'void CloseAuxiliaryWindows()')) {
    if (-not $mainSource.Contains($closeFeature)) { throw "Logo menu close behavior is missing: $closeFeature" }
}
foreach ($exitRestoreFeature in @('var wasMinimized = calendarMinimized;', 'calendarMinimized = false; UpdateTrayVisibilityText();', 'if (wasMinimized) { MinimizeToTray(); return; }', 'if (!wasLocked) ShowPositionEditor();')) {
    if (-not $mainSource.Contains($exitRestoreFeature)) { throw "Exit cancellation state restoration is missing: $exitRestoreFeature" }
}
# 2026-09-03: 메인 창이 들고 있던 사본을 지우고 팝업 공통 판정 하나로 합쳤다.
$chromeType = $assembly.GetType('FamilyPlanner.OnharuPopupChrome', $false)
if ($null -eq $chromeType) { throw 'OnharuPopupChrome type is missing.' }
$resizeAt = $chromeType.GetMethod('ResizeEdgeAt', [Reflection.BindingFlags]'NonPublic,Static')
if ($null -eq $resizeAt) { throw 'Shared resize edge detection is missing.' }
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
    'Height = 46', 'OnharuPopupChrome.Button("✓  일정 저장"', 'Content = "직접 선택"', 'SettingsWindow.StyleComboBox(customReminderUnit)')) {
    if (-not $addItemSource.Contains($compactScheduleFeature)) { throw "노트북용 일정창 압축 구성이 누락됐습니다: $compactScheduleFeature" }
}
# 2026-09-02: 창의 대표 실행 버튼은 브랜드 그라데이션으로 통일한다. 알람 `시작`, 시간표 `저장`·`적용`,
# KBO `선택 경기 등록`, 검색 `오늘`이 같은 브러시를 쓴다. 일정 등록 창만 평면 인디고를 쓰고 있었다.
$representativeActionSources = @{ 'AddItemWindow.cs' = 'save'; 'AlarmWindow.cs' = 'startButton'; 'TimetableWindow.cs' = 'save'; 'SportsCalendarWindow.cs' = 'registerButton'; 'SearchWindow.cs' = 'todayButton' }
foreach ($entry in $representativeActionSources.GetEnumerator()) {
    $windowSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot $entry.Key) -Raw -Encoding UTF8
    if (-not $windowSource.Contains($entry.Value + '.Background = OnharuPopupChrome.BrandGradientBrush()')) {
        throw ('창의 대표 실행 버튼은 브랜드 그라데이션을 사용한다: ' + $entry.Key + ' / ' + $entry.Value)
    }
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
foreach ($detailOrderFeature in @('IEnumerable<List<PlannerItem>> DetailGroups', 'settings.DetailOrderMode != "time"', 'foreach (var categoryItems in DetailGroups(dayItems))')) {
    if (-not $mainSource.Contains($detailOrderFeature)) { throw "Detail ordering is missing: $detailOrderFeature" }
}
$calendarSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Calendar.cs') -Raw -Encoding UTF8
foreach ($compactLaneFeature in @('laneOccupancy.FindIndex', 'occupied.All(range =>', 'var eventLaneLimit = visibleEventLanes;')) {
    if (-not $calendarSource.Contains($compactLaneFeature)) { throw "Compact calendar lane allocation is missing: $compactLaneFeature" }
}
if ($calendarSource.Contains('⌄  +') -or $calendarSource.Contains('개 일정이 셀 높이를 넘어')) { throw 'Calendar overflow summary bar must remain removed.' }
$displaySource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Display.cs') -Raw -Encoding UTF8
foreach ($googleFilterFeature in @('HorizontalAlignment = HorizontalAlignment.Stretch', 'new ColumnDefinition { Width = new GridLength(17) }', 'var split = (boxes.Count + 1) / 2')) {
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
# 2026-09-02: 설정 톱니는 `MainWindow.Layout.cs`의 `SettingsGlyph` 전용 함수에서 `OnharuIcons`의
# `settings` 도형으로 통합했다(2026-09-01 기록). 그 작업이 `popup-policy-check.ps1`에 `SettingsGlyph`가
# 되살아나면 실패하는 단언을 넣었으므로, 같은 이름의 존재를 요구하던 여기 단언은 서로 모순이었다.
# 벡터 설정 아이콘이라는 원래 목적은 `popup-policy-check.ps1`이 `OnharuIcons` 쪽에서 지킨다.
foreach ($buttonFeedbackFeature in @('System.Windows.Controls.Button.IsPressedProperty', 'void FlashDesktopButton(Button button)', 'HeaderGlyph("settings"')) {
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
foreach ($specialDayFilterFeature in @('Text = "Special Day"', 'specialFilterRow = new StackPanel', 'Grid.SetRowSpan(divider, 2)', 'Width = new GridLength(118)', 'localFilterRow = new UniformGrid { Columns = 2 }', '"야구"')) {
    if (-not $mainSource.Contains($specialDayFilterFeature)) { throw "Special Day filter grouping is missing: $specialDayFilterFeature" }
}
$detailSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Detail.cs') -Raw -Encoding UTF8
if ($detailSource.Contains('Task.Delay(230)') -or $detailSource.Contains('await SetTodoCompleted(item, !item.Completed)')) {
    throw 'Detail-card title must not toggle Todo completion; only its checkbox may do so.'
}
foreach ($detailCheckboxFeature in @('check.Click += async delegate', 'await SetTodoCompleted(item, check.IsChecked == true)', 'if (e.ClickCount != 2) return;')) {
    if (-not $detailSource.Contains($detailCheckboxFeature)) { throw "Detail-card checkbox-only interaction is missing: $detailCheckboxFeature" }
}
# 세부 머리글의 별 버튼을 Collapsed로 감추면 머리글 높이가 줄어 날짜 제목과 도구 아이콘이
# 함께 2px 움직인다. 시간순·카테고리순·미완료를 오갈 때마다 글자가 흔들려 보인다.
if (-not $detailSource.Contains('dateColorButton.Visibility = rangeTitle ? Visibility.Hidden : Visibility.Visible;')) {
    throw 'Detail header star must keep its slot so the date title does not shift between modes.'
}
# 고정 상태에서 눌리려면 적중 지도에 담길 표식이 있어야 한다. 미완료 카드의 일정 글자는
# 표식이 없어 이동에서만 열렸다. 세부 카드의 일정 글자와 같은 표식을 쓴다.
if (-not $detailSource.Contains('label.Tag = new ItemHitTarget { Item = item, SegmentStart = item.Start.Date, SegmentEnd = item.Start.Date,')) {
    throw 'Incomplete-todo rows must carry a fixed-layer hit target so they open in both modes.'
}
# 세부 제목의 날짜 표기는 세 탭과 세 보기가 모두 같은 양식이어야 한다. 짧은 형태가 남아 있으면 걸린다.
if (-not $detailSource.Contains('return DetailDateValue(start) + " ~ " + DetailDateValue(end);')) {
    throw 'Detail range titles must use the shared dated format.'
}
if ($detailSource.Contains('start.ToString("MM/dd"')) {
    throw 'Detail range titles must not fall back to the year-less short format.'
}
if ($detailSource.Contains('rangeTitle ? Visibility.Collapsed')) {
    throw 'Detail header star must not collapse its slot.'
}
# 기념일 머리글 높이는 접힘 여부에 따라 달라진다. `+`가 가운데 정렬이면 상태마다 위아래로 움직인다.
$anniversarySource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Anniversary.cs') -Raw -Encoding UTF8
foreach ($anniversaryPlusFeature in @('make.VerticalAlignment = VerticalAlignment.Top;', 'AutomationProperties.SetName(make, "기념일 만들기")')) {
    if (-not $anniversarySource.Contains($anniversaryPlusFeature)) { throw "Anniversary add button placement is missing: $anniversaryPlusFeature" }
}
$timetableSource = Get-Content (Join-Path $PSScriptRoot 'TimetableWindow.cs') -Raw -Encoding UTF8
$timetableDataSource = Get-Content (Join-Path $PSScriptRoot 'TimetableData.cs') -Raw -Encoding UTF8
$timetableMainSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Timetable.cs') -Raw -Encoding UTF8
# 2026-09-02: 두 토큰을 현재 구조로 갱신했다.
#  - `Height = 568` 고정값 → 표 크기로 계산한 뒤 작업 영역에 맞추는 `ApplyPreferredSize`.
#    교시 수와 요일 수가 사용자 설정이라 고정 높이는 애초에 유지될 수 없는 단언이었다.
#  - 인라인 접이식 `settingsPanel` → 별도 자식 차단 팝업 `TimetableSettingsWindow`.
#    POPUP_POLICY 3장 분류에 맞춘 변경이며 소스 주석에 근거가 남아 있다.
foreach ($timetableFeature in @('PeriodCount = 9', 'Height = Math.Min(height, Math.Max(MinHeight, area.Height - 48))', 'FontSize = 13', 'TimetableStorage.Save(data)', 'new TimetableSettingsWindow(data) { Owner = this }', 'OnharuPopupChrome.EnableDrag(this, header)', 'e.GetPosition(root).Y > 48')) {
    if (-not ($timetableSource + $timetableDataSource).Contains($timetableFeature)) { throw "Timetable nine-period/save/drag behavior is missing: $timetableFeature" }
}
if (-not $timetableMainSource.Contains('timetableWindow.Show();') -or $timetableMainSource.Contains('ShowDialog()')) { throw 'Timetable must remain an independent non-modal window.' }
# 짧은 값만 받는 시각 칸은 가운데로 세운다. 왼쪽 정렬이면 오른쪽 여백만 커져 같은 줄의 라벨·버튼과 눈높이가 어긋난다.
$alarmSource = Get-Content (Join-Path $PSScriptRoot 'AlarmWindow.cs') -Raw -Encoding UTF8
foreach ($centeredTimeInput in @('readonly TextBox timerMinutes', 'readonly TextBox alarmTime')) {
    $line = @(($alarmSource -split "`r?`n") | Where-Object { $_.Contains($centeredTimeInput) }) -join ''
    if (-not $line.Contains('TextAlignment = TextAlignment.Center')) { throw "Alarm time input must stay centered: $centeredTimeInput" }
}
if (-not $timetableSource.Contains('BorderThickness = new Thickness(1), TextAlignment = TextAlignment.Center')) {
    throw 'Timetable start-time input must stay centered.'
}
# 수기 입력칸은 누르는 즉시 기존 값을 통째로 선택해야 지우지 않고 바로 새 값을 칠 수 있다.
# 공통 진입점은 UiRound다. 목록에 없는 칸이 생기면 여기서 걸린다.
$roundSource = Get-Content (Join-Path $PSScriptRoot 'UiRound.cs') -Raw -Encoding UTF8
foreach ($selectAllFeature in @('public static void SelectAllOnFocus(TextBox input, bool alwaysSelect)', 'if (alwaysSelect || !input.AcceptsReturn) input.SelectAll();', 'if (input.IsKeyboardFocusWithin) return;', 'if (!alwaysSelect && input.AcceptsReturn) return;')) {
    if (-not $roundSource.Contains($selectAllFeature)) { throw "Shared select-all-on-focus helper is missing: $selectAllFeature" }
}
if (-not $roundSource.Contains("input.Template = new ControlTemplate(typeof(TextBox)) { VisualTree = border };`r`n            SelectAllOnFocus(input);")) {
    throw 'Styled text boxes must get select-all-on-focus from StyleTextBox.'
}
foreach ($selectAllCaller in @(@{ File = 'TimetableWindow.cs'; Token = 'UiRound.SelectAllOnFocus(editor, true);' }, @{ File = 'SearchWindow.cs'; Token = 'UiRound.SelectAllOnFocus(query);' })) {
    $callerSource = Get-Content (Join-Path $PSScriptRoot $selectAllCaller.File) -Raw -Encoding UTF8
    if (-not $callerSource.Contains($selectAllCaller.Token)) { throw ('Text box outside StyleTextBox must opt into select-all-on-focus: ' + $selectAllCaller.File) }
}
$publisherSource = Get-Content (Join-Path $PSScriptRoot 'ExplorerFramePublisher.cs') -Raw -Encoding UTF8
$desktopInputSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.DesktopInput.cs') -Raw -Encoding UTF8
if (-not $publisherSource.Contains('public void UpdateOpacity(double opacity)') -or -not $desktopInputSource.Contains('explorerFrame.UpdateOpacity(settings.Opacity)')) {
    throw 'Fixed-layer opacity must use the cached-frame opacity update path.'
}
# 접힘·펼침 버튼의 세로 위치는 배치가 끝난 뒤에만 받아들이고, 고정 상태에서는 다시 그린다.
# 다시 그리지 않으면 캐시된 프레임에 시작 직후의 옛 자리가 그대로 남는다.
foreach ($sidebarToggleFeature in @('void AlignSidebarToggleToDetailSwitch(bool measuringWhileHidden)', 'void EnsureSidebarTogglePlacement()', 'AlignSidebarToggleToDetailSwitch(true);', 'if (detailPeriodSwitch.ActualHeight <= 0 || bodyGrid.ActualHeight <= 0) return;', 'if (top <= 0 || Math.Abs(sidebarToggleTop - top) < .5) return;', 'sidebarToggleTop = top; UpdateSidebarFloatButton(collapseSidebarButton.IsMouseOver);
            if (positionLocked) SchedulePublish();')) {
    if (-not $layoutSource.Contains($sidebarToggleFeature)) { throw "Sidebar toggle alignment guard is missing: $sidebarToggleFeature" }
}
$startupSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Startup.cs') -Raw -Encoding UTF8
if (-not $startupSource.Contains('EnsureSidebarTogglePlacement();')) {
    throw 'Startup must place the sidebar toggle before the first fixed-layer publish.'
}
# 사이드바 그룹 머리글은 Button이 아니라 TextBlock이다. 고정 화면의 적중 지도가 담지 않으면
# 눌러도 아무 일이 없다. 태그·수집·처리 세 곳이 함께 있어야 동작한다.
$publisherHitSource = Get-Content (Join-Path $PSScriptRoot 'ExplorerFramePublisher.cs') -Raw -Encoding UTF8
if (($layoutSource.Split(@('Tag = "toggle_section"'), [StringSplitOptions]::None).Length - 1) -lt 4) {
    throw 'All sidebar group headers and the shared indicator must carry the toggle_section tag.'
}
# 화살표는 Border여야 투명 배경이 칠해져 상자 전체가 눌린다. ContentControl은 획만 눌려 잡기 어려웠다.
foreach ($indicatorFeature in @('Border SectionToggleIndicator(bool expanded)', 'Width = 20, Height = 20, Background = Brushes.Transparent', 'var localIndicator = SectionToggleIndicator(true);', 'var googleIndicator = SectionToggleIndicator(true);')) {
    if (-not $layoutSource.Contains($indicatorFeature)) { throw "Sidebar group toggle indicator hit area is missing: $indicatorFeature" }
}
if (-not $publisherHitSource.Contains('element.Tag as string == "toggle_section"')) {
    throw 'Fixed-layer hit map must collect sidebar group headers.'
}
foreach ($toggleSectionFeature in @('element.Tag as string == "toggle_section") return child;', 'if (target as string == "toggle_section" && !doubleClick && targetElement != null)', 'RoutedEvent = UIElement.MouseLeftButtonUpEvent')) {
    if (-not $desktopInputSource.Contains($toggleSectionFeature)) { throw "Fixed-mode sidebar group toggle is missing: $toggleSectionFeature" }
}
if (-not $desktopInputSource.Contains('action == 20) { ToggleSidebar(null, null);')) {
    throw 'Fixed-layer sidebar action must use the same layout update path as WPF mode.'
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
foreach ($reminderPositionFeature in @('settings.ReminderPosition == "onharu"', 'new Rect(Left, Top', 'SystemParameters.WorkArea', 'reminderBounds')) {
    if (-not $reminderSource.Contains($reminderPositionFeature)) { throw "Reminder placement option is missing: $reminderPositionFeature" }
}
$reminderWindowSource = Get-Content (Join-Path $PSScriptRoot 'ReminderWindow.cs') -Raw -Encoding UTF8
if (-not $reminderWindowSource.Contains('WindowStartupLocation.Manual') -or -not $reminderWindowSource.Contains('targetBounds.Left')) { throw 'Reminder window must support explicit primary-screen and ONHARU placement.' }
foreach ($tenMinuteFeature in @('minuteGrid = new UniformGrid { Columns = 6', 'new[] { 0, 10, 20, 30, 40, 50 }', 'Margin = new Thickness(0, 4, 4, 4), HorizontalAlignment = HorizontalAlignment.Left')) {
    if (-not $addItemSource.Contains($tenMinuteFeature)) { throw "Ten-minute selector is missing: $tenMinuteFeature" }
}
foreach ($yearlyModeFeature in @('yearlyNth.IsChecked == true ? "yearly_nth" : "yearly_date"', '매년 같은 날짜', '매년 같은 주·요일')) {
    if (-not $addItemSource.Contains($yearlyModeFeature)) { throw "Context-aware yearly recurrence UI is missing: $yearlyModeFeature" }
}
foreach ($compactDetailFeature in @('titleText.Inlines.Add(new System.Windows.Documents.Run', 'Foreground = T("Disabled")', 'if (!item.AllDay && IsMultiDay(item))', 'Margin = new Thickness(0, 8, 0, 0)')) {
    if (-not $mainSource.Contains($compactDetailFeature)) { throw "Compact detail layout is missing: $compactDetailFeature" }
}
foreach ($fixedWeekPickerFeature in @('button.Tag = "week_count:" + selectedCount', 'navigation.StartsWith("week_count:"', 'ApplyWeekCount(weekCount)')) {
    if (-not $mainSources.Contains($fixedWeekPickerFeature)) { throw "Fixed-layer week picker is missing: $fixedWeekPickerFeature" }
}
# 2026-09-03: 고정 상태에서 기간 스위치를 다시 눌러도 주 수 목록이 닫히지 않았다.
# 바깥 클릭으로 먼저 닫은 뒤 이어지는 Click이 다시 열었기 때문이다. 스위치 자신은 예외로 둔다.
if (-not $mainSources.Contains('&& !IsInside(targetElement, calendarRangeSwitch)) CloseWeekCountOverlay();')) {
    throw '고정 상태에서 기간 스위치의 주 수 목록 토글이 깨집니다.'
}
# 빠르게 두 번 누르면 두 번째 클릭이 WM_LBUTTONDBLCLK으로 와서 삼켜졌다. 스위치는 흘려보낸다.
if (-not $mainSources.Contains('if (IsInside(targetButton, calendarRangeSwitch))')) {
    throw '고정 상태에서 기간 스위치의 빠른 두 번째 클릭이 삼켜집니다.'
}
foreach ($fixedWeekHitFeature in @('TryApplyWeekCountAt(root, point)', 'button.TransformToAncestor(root)', 'ApplyWeekCount(count)')) {
    if (-not $mainSources.Contains($fixedWeekHitFeature)) { throw "Fixed-layer week option direct hit handling is missing: $fixedWeekHitFeature" }
}
$anniversaryCardSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Anniversary.cs') -Raw -Encoding UTF8
if (-not $anniversaryCardSource.Contains('Margin = new Thickness(0, 4, 0, 7)')) { throw 'D-Day and anniversary cards must use the common detail-card outer spacing.' }
foreach ($detailRhythm in @('groupCollapsed ? 0 : 5', 'Margin = new Thickness(0, 4, 0, 7)', 'Margin = new Thickness(3), IsHitTestVisible = false')) {
    if (-not $mainSources.Contains($detailRhythm)) { throw "Detail spacing rhythm is missing: $detailRhythm" }
}
if (-not $mainSources.Contains('new Border { Height = 8, Background = Brushes.Transparent }')) { throw 'Date header to first-card spacing is invalid.' }
if ($anniversaryCardSource.Contains('ddaySectionCollapsed ? 17') -or $anniversaryCardSource.Contains('anniversarySectionCollapsed ? 17')) { throw 'Collapsed special-card title height still clips text descenders.' }
foreach ($categoryMoveFeature in @('DragDrop.DoDragDrop(card, card, DragDropEffects.Move)', 'dragged.Parent != parent', 'CategoryOrder = localColorGrid.Children')) {
    if (-not $settingsSource.Contains($categoryMoveFeature)) { throw "In-group color-card ordering is missing: $categoryMoveFeature" }
}
foreach ($colorEditorLayoutFeature in @('localColorGrid = new UniformGrid { Columns = 3', 'googleColorGrid = new UniformGrid { Columns = 3', 'specialColorGrid = new UniformGrid { Columns = 3', 'Text = displayName ?? name', 'Cursor = Cursors.Arrow', 'local:baseball', 'ColorEditor("국경일", holidayColor, "휴일", false)', 'Action openPalette = delegate', 'OnharuColorPresets.Palettes()')) {
    if (-not $settingsSource.Contains($colorEditorLayoutFeature)) { throw "Three-column grouped color editor is missing: $colorEditorLayoutFeature" }
}
# 2026-09-02: 제목 없던 카드 `generalOptions`를 해체했다. `드래그로 일정 옮기기`는 제목 있는 `일정 조작`
# 카드로, `새 버전 자동 확인`은 `앱` 카드로 나눴다. 서로 무관한 두 항목이 한 카드에 있어 제목을 붙일 수 없었다.
foreach ($googleSettingsOrderFeature in @('CategoryOrderPolicy.GoogleSources(', 'googleDetailGrid.Children.Add(taskRow);', 'dragGroup.Children.Add(dragMaster); dragGroup.Children.Add(dragChildren);', 'appGroup.Children.Add(updateOption);')) {
    if (-not $settingsSource.Contains($googleSettingsOrderFeature)) { throw "Google settings order is missing: $googleSettingsOrderFeature" }
}
# 카드 순서는 배열 한 곳에서만 정한다. 이전에는 `Insert(0, ...)`·`Insert(2, ...)` 매직 인덱스를 써서
# 카드를 하나만 추가해도 순서가 어긋났다. 인덱스 방식이 되살아나면 여기서 걸린다.
if (-not $settingsSource.Contains('}) panel.Children.Add(SectionCard(section));')) { throw '설정창 카드 순서는 배열 한 곳에서 정의해야 합니다.' }
if ($settingsSource -match 'panel\.Children\.Insert\(') { throw '설정창 카드 순서를 매직 인덱스로 넣고 있습니다.' }
# 라벨 열 폭과 다열 격자. 이전에는 92·96·78·120이 섞이고 WrapPanel 자유 흐름이라 열이 맞지 않았다.
foreach ($settingsLayoutFeature in @('const double LabelColumn = 120', 'new GridLength(LabelColumn)', 'Width = LabelColumn', 'new UniformGrid { Columns = 3', 'OnharuPopupChrome.SetOptionsEnabled(')) {
    if (-not $settingsSource.Contains($settingsLayoutFeature)) { throw "설정창 정렬 규칙이 누락됐습니다: $settingsLayoutFeature" }
}
# 2026-09-03: 선택일 표시·오늘 표시를 라벨 열 + 격자로 바꾸면서 라디오가 두 단계 안쪽으로 들어갔는데
# 저장 처리는 여전히 패널의 직접 자식만 훑고 있었다. 결과가 비어 `First`가 예외를 던졌고
# 설정 저장을 누르면 창이 닫혔다. 사용자가 실제로 겪은 크래시이며 자동 검사는 문자열만 봐서 놓쳤다.
# 중첩을 견디는 `CheckedRadioTag`로 바꿨다. 직접 자식 읽기로 되돌아가면 여기서 걸린다.
# 2026-09-03: 재귀 조회와 비활성 헬퍼를 OnharuPopupChrome으로 올려 설정창과 일정 등록·수정 창이 함께 쓴다.
$chromeSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'OnharuPopupChrome.cs') -Raw -Encoding UTF8
foreach ($token in @('internal static string CheckedRadioTag(DependencyObject root, string fallback)', 'internal static RadioButton CheckedRadio(DependencyObject root)', 'internal static void SetOptionsEnabled(bool enabled, params UIElement[] targets)')) {
    if (-not $chromeSource.Contains($token)) { throw "공통 폼 헬퍼가 누락됐습니다: $token" }
}
foreach ($token in @('CheckedRadioTag(selectionOptions,', 'CheckedRadioTag(todayOptions,', 'CheckedRadioTag(syncOptions,')) {
    if (-not $settingsSource.Contains($token)) { throw "중첩 라디오 조회가 누락됐습니다: $token" }
}
# 일정 등록·수정 창도 같은 함정을 피해야 한다. 저장 경로에서 직접 자식 조회가 되살아나면 걸린다.
$addItemSourceNow = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AddItemWindow.cs') -Raw -Encoding UTF8
foreach ($token in @('CheckedRadioTag(hourGrid,', 'CheckedRadioTag(minuteGrid,', 'CheckedRadioTag(recurrenceOptions,', 'CheckedRadio(reminderOptions)')) {
    if (-not $addItemSourceNow.Contains($token)) { throw "일정 창의 중첩 라디오 조회가 누락됐습니다: $token" }
}
foreach ($token in @('selectionOptions.Children.OfType<RadioButton>()', 'todayOptions.Children.OfType<RadioButton>()', 'syncOptions.Children.OfType<RadioButton>()')) {
    if ($settingsSource.Contains($token)) { throw "격자 안의 라디오를 직접 자식으로 읽고 있습니다: $token" }
}
if ($addItemSourceNow.Contains('Children.OfType<RadioButton>().First(')) { throw '일정 창이 다시 직접 자식 조회로 돌아갔습니다.' }
# 팝업 제목은 모든 창이 FeatureHeading을 쓴다. 이 창만 자체 TextBlock이었다.
if (-not $addItemSourceNow.Contains('OnharuPopupChrome.FeatureHeading(existing == null ? "add" : "✎"')) { throw '일정 창 제목이 공통 FeatureHeading을 쓰지 않습니다.' }
foreach ($token in @('📅', '"✦  새 일정"')) {
    if ($addItemSourceNow.Contains($token)) { throw "일정 창에 글꼴 기호·이모지가 남아 있습니다: $token" }
}
# 대표 실행 버튼과 역할색. design-onharu 3.4를 따른다.
if (-not $settingsSource.Contains('save.Background = OnharuPopupChrome.BrandGradientBrush()')) { throw '설정 저장 버튼은 브랜드 그라데이션을 사용한다.' }
foreach ($retiredDataColor in @('Brush("#FFF7ED"), Foreground = Brush("#C2410C")', 'Brush("#ECFDF5"), Foreground = Brush("#047857")')) {
    if ($settingsSource.Contains($retiredDataColor)) { throw "일정 관리 버튼이 다시 장식 의미색으로 돌아갔습니다: $retiredDataColor" }
}
foreach ($visibleGroupFilterFeature in @('VisibleFilterKeys(new[] { "업무일정", "개인일정", "야구" })', 'filters[key].Visibility == Visibility.Visible', 'SetHeaderAllState(localAllFilter, VisibleFilterKeys')) {
    if (-not $mainSources.Contains($visibleGroupFilterFeature)) { throw "Visible-category group filter rule is missing: $visibleGroupFilterFeature" }
}
foreach ($compactOverflowFeature in @('+ "개 더보기"', 'visibleLaneLimit', 'detailMode = "selected"')) {
    if (-not $mainSources.Contains($compactOverflowFeature)) { throw "Calendar overflow navigation is missing: $compactOverflowFeature" }
}
foreach ($collapsedFilterSpacing in @('filterGroups.Margin = new Thickness(0, 0, 0, visible ? 14 : 6)', 'googleHeader.Margin = new Thickness(0, 0, 0, visible ? 7 : 10)')) {
    if (-not $mainSources.Contains($collapsedFilterSpacing)) { throw "Collapsed sidebar spacing is missing: $collapsedFilterSpacing" }
}
foreach ($todoSummaryFeature in @('ShowIncompleteTodoButton', '미완료 To-Do', '기한 지남', '다가오는 할 일', 'detailIncompleteMode')) {
    if (-not (($settingsSource + $mainSources + (Get-Content (Join-Path $PSScriptRoot 'PlannerSettings.cs') -Raw -Encoding UTF8)).Contains($todoSummaryFeature))) { throw "Incomplete To-Do card is missing: $todoSummaryFeature" }
}
if (-not $settingsSource.Contains('var incompleteTodoRange = new ComboBox { Width = 128')) { throw 'Incomplete To-Do range labels can be clipped.' }
if (-not $mainSource.Contains('var rangeTitle = detailMode != "selected" || detailIncompleteMode;')) { throw 'Incomplete To-Do date range glyph is missing.' }
foreach ($miniCalendarFeature in @('VisibleCalendarRange()', 'OnharuCalendarStyle.Apply(picker)', '현재 표시  ')) {
    if (-not $mainSources.Contains($miniCalendarFeature)) { throw "Header mini-calendar navigation is missing: $miniCalendarFeature" }
}
if (-not $mainSources.Contains('CornerRadius = new CornerRadius(continuesBefore ? 0 : 4')) { throw 'Multi-day week-boundary continuation styling is missing.' }
$addItemSource = Get-Content (Join-Path $PSScriptRoot 'AddItemWindow.cs') -Raw -Encoding UTF8
$orderPolicySource = Get-Content (Join-Path $PSScriptRoot 'CategoryOrderPolicy.cs') -Raw -Encoding UTF8
foreach ($sharedOrderFeature in @('CategoryOrderPolicy.Rank(localOrder, x.Item2)', 'CategoryOrderPolicy.GoogleSources(')) {
    if (-not $addItemSource.Contains($sharedOrderFeature)) { throw "New-item calendar order does not use the shared policy: $sharedOrderFeature" }
}
foreach ($sharedOrderFeature in @('ItemKey(PlannerItem item)', 'GoogleSources(IEnumerable<GoogleCalendarSetting> sources')) {
    if (-not $orderPolicySource.Contains($sharedOrderFeature)) { throw "Shared category order policy is missing: $sharedOrderFeature" }
}
$sportsCalendarSource = Get-Content (Join-Path $PSScriptRoot 'SportsCalendarWindow.cs') -Raw -Encoding UTF8
$sportsMainSource = Get-Content (Join-Path $PSScriptRoot 'SportsApiWindows.cs') -Raw -Encoding UTF8
$googleCalendarSource = Get-Content (Join-Path $PSScriptRoot 'GoogleCalendarService.cs') -Raw -Encoding UTF8
foreach ($sportsGoogleFeature in @('ONHARU 로컬 일정', 'Google · ', 'x.Editable && !GoogleTasks.IsSource', 'StyleComboBox(registrationTarget)', 'item.GoogleCalendarId = target.Id', 'await RegistrationRequested(SelectedItems)')) {
    if (-not $sportsCalendarSource.Contains($sportsGoogleFeature)) { throw "KBO local/Google registration target is missing: $sportsGoogleFeature" }
}
foreach ($sportsIdentityFeature in @('RegistrationId(SportsGame game)', 'onharuSportsGameId', 'CollapseDuplicateSportsItems')) {
    if (-not (($sportsCalendarSource + $sportsMainSource + $googleCalendarSource + $mainSources + (Get-Content (Join-Path $PSScriptRoot 'SportsApi.cs') -Raw -Encoding UTF8)).Contains($sportsIdentityFeature))) { throw "Stable KBO identity is missing: $sportsIdentityFeature" }
}
foreach ($legacySportsIdentityFeature in @('!item.Notes.StartsWith("KBO 경기 일정", StringComparison.Ordinal)', 'var stableId = SportsApi.RegistrationId(item);', 'item.SportsGameId = stableId; changed = true;')) {
    if (-not (($sportsMainSource + (Get-Content (Join-Path $PSScriptRoot 'SportsApi.cs') -Raw -Encoding UTF8)).Contains($legacySportsIdentityFeature))) { throw "Legacy KBO duplicate repair is missing: $legacySportsIdentityFeature" }
}
foreach ($localCategoryFeature in @('세부 달력 표시 옵션', 'BusinessCategoryVisible', 'PersonalCategoryVisible', 'BaseballCategoryVisible', 'DdayCategoryVisible', 'AnniversaryCategoryVisible')) {
    if (-not (($settingsSource + $mainSources).Contains($localCategoryFeature))) { throw "Independent local category visibility setting is missing: $localCategoryFeature" }
}
foreach ($hiddenCategorySaveFeature in @('HexOr("업무일정", business)', 'HexOr("개인일정", personal)', 'HexOr("야구", baseball)', 'HexOr("D-Day", dday)', 'HexOr("기념일", anniversary)')) {
    if (-not $settingsSource.Contains($hiddenCategorySaveFeature)) { throw "Hidden category color must survive settings save: $hiddenCategorySaveFeature" }
}
if (-not $settingsSource.Contains('if (!sliders.ContainsKey(name)) return;')) { throw 'Preset changes must skip hidden category color editors.' }
foreach ($immediateCategoryRefresh in @('Content = BuildLayout();', 'RenderAll();', 'if (positionLocked) PublishAndHide();')) {
    if (-not $mainSources.Contains($immediateCategoryRefresh)) { throw "Settings category changes must refresh immediately: $immediateCategoryRefresh" }
}
foreach ($detailColorOrderFeature in @('IsSpecialDetailGroup(string groupKey)', 'IsSpecialDetailGroup((string)x.Card.Tag) ? 1', 'ReadableEmphasisForeground')) {
    if (-not $mainSources.Contains($detailColorOrderFeature)) { throw "Detail special-card order or important color rule is missing: $detailColorOrderFeature" }
}
if (-not $mainSources.Contains('item.Category == "야구"') -or -not $mainSources.Contains('Tag = "Unavailable"')) { throw 'Detail cards must show an unavailable checkbox for baseball and non-actionable all-day events.' }
if (-not $googleCalendarSource.Contains('IsAlreadyDeleted(error)')) { throw 'Google delete must accept an already-deleted remote event and clear the local cache.' }
foreach ($googleConvergenceFeature in @('uploadedEventIds', '!uploadedEventIds.Contains(x.GoogleEventId)', '!remoteIds.Contains(x.GoogleEventId)')) {
    if (-not $googleCalendarSource.Contains($googleConvergenceFeature)) { throw "Google calendar convergence protection is missing: $googleConvergenceFeature" }
}
if (-not $mainSources.Contains("Property='Tag' Value='Unavailable'") -or -not $mainSources.Contains("Property='Text' Value='×'")) { throw 'Unavailable detail checkbox X marker is missing.' }
if (-not $sportsMainSource.Contains('foreach (var item in googleItems) await SaveGoogleItem(item)')) { throw 'KBO Google registration must use the existing Google save path.' }
foreach ($sportsCategoryFeature in @('"onharuCategory"', 'value == "야구" ? "야구"')) {
    if (-not $googleCalendarSource.Contains($sportsCategoryFeature)) { throw "KBO Google category round-trip is missing: $sportsCategoryFeature" }
}
$printSource = Get-Content (Join-Path $PSScriptRoot 'CalendarPrintWindow.cs') -Raw -Encoding UTF8
$updateSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Updates.cs') -Raw -Encoding UTF8
$startupSource = Get-Content (Join-Path $PSScriptRoot 'MainWindow.Startup.cs') -Raw -Encoding UTF8
if (-not $printSource.Contains('dialog.PrintVisual(page')) { throw 'Calendar print dispatch is missing.' }
if ($mainSources.Contains('new CalendarPrintWindow(Content as System.Windows.Media.Visual, delegate')) { throw 'Printing must not switch the fixed Explorer layer while the spooler owns the UI thread.' }
foreach ($fixedPrintFeature in @('Application.Current.MainWindow = this', 'Application.Current.MainWindow = mainWindow', 'if (HasBlockingDialog)')) {
    if (-not (($printSource + $mainSources).Contains($fixedPrintFeature))) { throw "Fixed-mode native print ownership is missing: $fixedPrintFeature" }
}
if (-not $updateSource.Contains('SaveWindowSettings();') -or -not $updateSource.Contains('preservePlacementOnExit = true')) { throw 'Update launch must preserve the current monitor placement.' }
if (-not $startupSource.Contains('if (preservePlacementOnExit) Store.SaveSettings(settings)')) { throw 'Update exit must not overwrite the preserved placement.' }
$layerControllerSource = Get-Content (Join-Path $PSScriptRoot 'LayerHostController.cs') -Raw -Encoding UTF8
foreach ($layerShutdownFeature in @('hostPath = ResolveHostPath()', 'Process.GetProcessesByName("Onharu.LayerHost")', 'host.WaitForExit(4000)', 'host.Kill()')) {
    if (-not $layerControllerSource.Contains($layerShutdownFeature)) { throw "LayerHost shutdown verification is missing: $layerShutdownFeature" }
}
$completion = $addItem.GetMethod('UsesCompletionCheck', [Reflection.BindingFlags]'NonPublic,Static')
if (-not $completion.Invoke($null, @($false, $false))) { throw 'Timed items must use completion checks.' }
if (-not $completion.Invoke($null, @($true, $true))) { throw 'Local all-day completion check was not enabled.' }
if ($completion.Invoke($null, @($true, $false))) { throw 'Google all-day item must not enable local completion checks.' }

Write-Host 'Feature pack checks passed.'
