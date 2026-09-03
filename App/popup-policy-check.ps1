param([string]$Exe)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$direct = Get-ChildItem -LiteralPath $root -Filter 'MainWindow*.cs' | Where-Object {
    $_.Name -notin @('MainWindow.Dialogs.cs','MainWindow.Reminders.cs') -and
    ((Get-Content -Raw $_.FullName) -replace 'printWindow\.ShowDialog\(\)', '') -match '\.ShowDialog\(\)'
}
if ($direct) { throw ('Main blocking dialog bypasses popup policy: ' + (($direct.Name) -join ', ')) }

$dialogs = Get-Content -Raw (Join-Path $root 'MainWindow.Dialogs.cs')
$desktop = Get-Content -Raw (Join-Path $root 'MainWindow.DesktopInput.cs')
$sports = Get-Content -Raw (Join-Path $root 'SportsApiWindows.cs')
$settings = Get-Content -Raw (Join-Path $root 'SettingsWindow.cs')
foreach ($token in @('blockingDialogDepth', 'ShowBlockingDialog(Window window)', 'ShowBlockingFileDialog(Forms.CommonDialog dialog)')) {
    if (-not $dialogs.Contains($token)) { throw "Popup coordinator token missing: $token" }
}
if ($dialogs.Contains('IsEnabled = false')) { throw 'Blocking a popup must not switch the calendar into the visually disabled state.' }
if (-not $dialogs.Contains('IsHitTestVisible = false')) { throw 'Main popup input blocking must preserve the calendar visual state.' }
if (-not $dialogs.Contains('IsBlockingDialogOrChild(window)')) { throw 'Independent tool windows must not steal focus from the active blocking dialog chain.' }
if (-not $dialogs.Contains('OnharuPopupChrome.EnableTopDrag(window)')) { throw 'Main popup windows must share the top drag rule.' }
# 2026-09-03: 알람은 독립 도구 창이다. `Owner`를 붙이면 고정 상태에서 메인 창이 cloak될 때
# WPF가 소유된 창을 함께 숨겨 창이 뜨지 않는다. 시간표·KBO와 같이 Owner 없이 PlaceCalendarDialog로 배치한다.
$alarm = Get-Content -Raw (Join-Path $root 'AlarmWindow.cs')
if ($alarm.Contains('new AlarmWindow { Owner = this }')) { throw '알람 창에 Owner를 붙이면 고정 상태에서 뜨지 않습니다.' }
if (-not $alarm.Contains('PlaceCalendarDialog(alarmWindow);')) { throw '알람 창은 다른 독립 도구 창과 같은 배치 함수를 씁니다.' }
$chrome = Get-Content -Raw (Join-Path $root 'OnharuPopupChrome.cs')
foreach ($token in @('const double TopDragHeight = 60', 'EnableTopDrag(Window window)', 'IsInteractive(DependencyObject current)', 'shell.Loaded', 'PrimaryButton(string text, double width)', 'ActionButton(string text, double width)', 'SurfaceColor = "#FFF7F7FA"', 'HeaderSurfaceColor = "#E8E9ED"', 'ContentSurfaceColor = "#FFFFFF"', 'SupportSurfaceColor = "#F3F1EF"', 'BorderColor = "#A9AFBA"', 'PrimarySurfaceColor = "#DDF3F1"', 'SelectionSurfaceColor = "#FBE8DE"', 'StyleSegment(OnharuSegmentedSwitch control)', 'StyleHeader(Panel header)', 'new TemplateBindingExtension(Control.BorderBrushProperty)')) {
    if (-not $chrome.Contains($token)) { throw "Shared popup drag token missing: $token" }
}
$sharedShellWindows = @(
    'AnniversaryWindow.cs','BackupWindow.cs','CalendarPrintWindow.cs','DataManagementChoiceWindow.cs',
    'DiaryWindows.cs','EmailBackupWindow.cs','ExitConfirmWindow.cs','GoogleAccountActionWindow.cs',
    'GoogleTasksWarningWindow.cs','LocalDataDeleteWindow.cs','LocalImportWindow.cs','LocalItemsOfferWindow.cs',
    'MonthJumpWindow.cs','NoticeWindow.cs','ProductInfoWindow.cs','ReminderWindow.cs',
    'RepeatDeleteWindow.cs','SearchWindow.cs','SportsApiWindows.cs','SportsCalendarWindow.cs','TimetableWindow.cs',
    'UpdateAvailableWindow.cs'
)
foreach ($name in $sharedShellWindows) {
    $source = Get-Content -Raw (Join-Path $root $name)
    if (-not $source.Contains('OnharuPopupChrome.Shell(')) { throw "Popup does not use the shared ONHARU shell: $name" }
}
$diary = Get-Content -Raw (Join-Path $root 'DiaryWindows.cs')
$timetable = Get-Content -Raw (Join-Path $root 'TimetableWindow.cs')
$sportsWindow = Get-Content -Raw (Join-Path $root 'SportsCalendarWindow.cs')
# 2026-09-01: 팝업 제목을 FeatureHeading 하나로 통일했다. 검색·설정·시간표·KBO가 모두 이것을 쓴다.
# 2026-09-03: 죽은 정의였던 FeatureTitle(34px 아이콘 박스)과 DisclosureButton·SetDisclosure를 지웠다.
# 근거는 AGENTS.md·CLAUDE.md·design-onharu.md 8.6·POPUP_POLICY.md에 함께 갱신했다.
# 일기장은 기능에서 제외해 창 검사를 뺐다. 소스는 재사용을 위해 남겨 둔다.
foreach ($token in @('SetListToggleIcon()', 'SetSortIcon()')) { if (-not $diary.Contains($token)) { throw "Diary tool chrome is inconsistent: $token" } }
if (-not $timetable.Contains('FeatureHeading("▦", "나의 시간표")')) { throw 'Timetable tool title must use shared feature chrome.' }
# KBO 이동 버튼은 2026-09-01 design-onharu 적용에서 글자형 «/» 대신 벡터 버튼으로 교체했다.
foreach ($token in @('FeatureHeading("⚾", "KBO 경기 일정")')) { if (-not $sportsWindow.Contains($token)) { throw "KBO tool chrome is inconsistent: $token" } }
$mainLayout = Get-Content -Raw (Join-Path $root 'MainWindow.Layout.cs')
$icons = Get-Content -Raw (Join-Path $root 'OnharuIcons.cs')
# 2026-09-01: 아이콘 도형을 OnharuIcons.cs 한 곳으로 모았다. 메인 헤더가 벡터, 팝업 제목이 글꼴 기호라
# 같은 기능인데 그림이 달랐던 문제를 없애기 위해서다. 부가기능 21px 규칙은 그대로이고 위치만 옮겼다.
# 메인은 HeaderGlyph가 OnharuIcons.Draw로 위임하고, 팝업 제목은 FeatureHeading이 같은 도형을 쓴다.
foreach ($token in @('glyph == "▦" || glyph == "✎" || glyph == "◴" || glyph == "⚾"', 'glyph == "print" || glyph == "info" || glyph == "calendar" ? 21 : 17')) { if (-not $icons.Contains($token)) { throw "Main feature icon rule is missing: $token" } }
# 2026-09-02: 설정 톱니·인쇄·정보를 OnharuIcons로 모았다. 이전에는 설정창이 FeatureHeading("⚙")를 불렀는데
# OnharuIcons가 "⚙"를 몰라 Segoe UI Symbol 글꼴로 떨어졌고, 그래서 제목 아이콘만 작고 얇게 보였다.
# 인쇄는 SettingsWindow 안에 경로가 하드코딩돼 있었고 정보는 Segoe UI 글자 `i`였다.
foreach ($token in @('case "⚙": case "settings":', 'case "print":', 'case "info":', 'case "calendar":')) {
    if (-not $icons.Contains($token)) { throw "설정·인쇄·정보 도형이 OnharuIcons에 없습니다: $token" }
}
$settingsWindow = Get-Content -Raw (Join-Path $root 'SettingsWindow.cs')
foreach ($token in @('HeaderToolButton("print"', 'HeaderToolButton("info"', 'FeatureHeading("settings", "온하루 설정")')) {
    if (-not $settingsWindow.Contains($token)) { throw "설정창 헤더 아이콘이 공통 도형을 쓰지 않습니다: $token" }
}
# 글꼴 문자나 별도 경로로 되돌아가면 여기서 걸린다.
foreach ($token in @('Text = "i"', 'M5,3 L13,3 L13,7 L15,7')) {
    if ($settingsWindow.Contains($token)) { throw "설정창 아이콘이 다시 글꼴 문자나 하드코딩 경로로 돌아갔습니다: $token" }
}
# 이 파일은 BOM이 없어 Windows PowerShell 5.1이 .ps1과 .cs를 같은 ANSI 코드페이지로 읽는다.
# 양쪽이 똑같이 깨지므로 한글 토큰은 비교가 되지만, 비ASCII 문자가 문자열 끝에 오면 뒤따르는 따옴표까지
# 삼켜 버려 토큰이 어긋난다. 그래서 톱니 버튼은 `없어야 할 글꼴 문자`가 아니라
# `있어야 할 도형 호출`을 ASCII 토큰으로 단언한다.
if (-not $timetable.Contains('settingsButton.Content = OnharuIcons.Draw("settings"')) { throw 'Timetable settings button must draw the shared gear shape.' }
if (-not $sportsWindow.Contains('optionsButton.Content = OnharuIcons.Draw("settings"')) { throw 'KBO options button must draw the shared gear shape.' }
if (-not $mainLayout.Contains('return OnharuIcons.Draw(glyph, foreground);')) { throw 'Main header must draw icons through OnharuIcons.' }
$chrome = Get-Content -Raw (Join-Path $root 'OnharuPopupChrome.cs')
if (-not $chrome.Contains('OnharuIcons.Draw(glyph, Brush("#334155"), 21)')) { throw 'Popup feature title must use the shared icon geometry.' }
# 부가기능 아이콘은 글꼴 기호로 떨어지지 않도록 네 개 모두 도형이 있어야 한다.
foreach ($featureGlyph in @('case "⌕":', 'case "▦":', 'case "◴":', 'case "⚾":', 'case "settings":')) { if (-not $icons.Contains($featureGlyph)) { throw "Feature icon geometry is missing: $featureGlyph" } }
# 설정 톱니는 24×24 채움 도형인 SettingsGlyph로 따로 그려져 선 아이콘들 사이에서 혼자 무거웠다.
# 같은 18×18 선 도형으로 옮겼으므로 별도 함수가 되살아나지 않게 막는다.
if ($mainLayout.Contains('SettingsGlyph') -or (Get-Content -Raw (Join-Path $root 'MainWindow.Theme.cs')).Contains('SettingsGlyph')) { throw 'Settings icon must be drawn through OnharuIcons, not a separate SettingsGlyph.' }
$delayedScrollStyle = Get-ChildItem -LiteralPath $root -Filter '*.cs' | Where-Object {
    (Get-Content -Raw $_.FullName) -match '(?s)Dispatcher\.BeginInvoke.{0,220}SoftenScrollBars'
}
if ($delayedScrollStyle) { throw ('Popup scrollbar styling must not be deferred past the first frame: ' + (($delayedScrollStyle.Name) -join ', ')) }
if (-not $desktop.Contains('if (ActivateBlockingDialog()) return;')) { throw 'Explorer fixed-layer actions must be blocked by the active main dialog.' }
if (-not $sports.Contains('if (sportsWindow != null)') -or -not $sports.Contains('sportsWindow.Show(); sportsWindow.Activate();')) { throw 'KBO must remain a single-instance independent tool window.' }
if ($settings.Contains('Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { UiRound.SoftenScrollBars(contentScroll)')) { throw 'Settings scrollbars must be styled before the first visible frame.' }
if (-not $settings.Contains('contentScroll.ApplyTemplate();') -or -not $settings.Contains('contentScroll.Opacity = 1;')) { throw 'Settings must reveal its content only after ONHARU scrollbar styling.' }

Write-Host 'ONHARU popup policy checks passed.'
