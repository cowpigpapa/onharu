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
$chrome = Get-Content -Raw (Join-Path $root 'OnharuPopupChrome.cs')
foreach ($token in @('const double TopDragHeight = 60', 'EnableTopDrag(Window window)', 'IsInteractive(DependencyObject current)', 'shell.Loaded', 'FeatureTitle(string glyph, string title)', 'PrimaryButton(string text, double width)', 'ActionButton(string text, double width)', 'SurfaceColor = "#FFF7F7FA"', 'HeaderSurfaceColor = "#E8E9ED"', 'ContentSurfaceColor = "#FFFFFF"', 'SupportSurfaceColor = "#F3F1EF"', 'BorderColor = "#A9AFBA"', 'PrimarySurfaceColor = "#DDF3F1"', 'SelectionSurfaceColor = "#FBE8DE"', 'StyleSegment(OnharuSegmentedSwitch control)', 'StyleHeader(Panel header)', 'new TemplateBindingExtension(Control.BorderBrushProperty)')) {
    if (-not $chrome.Contains($token)) { throw "Shared popup drag token missing: $token" }
}
$sharedShellWindows = @(
    'AnniversaryWindow.cs','BackupWindow.cs','CalendarPrintWindow.cs','DataManagementChoiceWindow.cs',
    'DiaryWindows.cs','EmailBackupWindow.cs','ExitConfirmWindow.cs','GoogleAccountActionWindow.cs',
    'GoogleTasksWarningWindow.cs','LocalDataDeleteWindow.cs','LocalImportWindow.cs','LocalItemsOfferWindow.cs',
    'MonthJumpWindow.cs','NoticeWindow.cs','PendingSyncWindow.cs','ProductInfoWindow.cs','ReminderWindow.cs',
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
foreach ($token in @('SetListToggleIcon()', 'SetSortIcon()', 'DiaryTitle()')) { if (-not $diary.Contains($token)) { throw "Diary tool chrome is inconsistent: $token" } }
if (-not $timetable.Contains('FeatureTitle("▦", "나의 시간표")')) { throw 'Timetable tool title must use shared feature chrome.' }
foreach ($token in @('FeatureTitle("⚾", "KBO 경기 일정")', 'Button("«", 23', 'Button("»", 23')) { if (-not $sportsWindow.Contains($token)) { throw "KBO tool chrome is inconsistent: $token" } }
$mainLayout = Get-Content -Raw (Join-Path $root 'MainWindow.Layout.cs')
foreach ($token in @('glyph == "▦" || glyph == "✎" || glyph == "⚾"', 'Width = featureIcon ? 21 : 17', 'Height = featureIcon ? 21 : 17')) { if (-not $mainLayout.Contains($token)) { throw "Main feature icon rule is missing: $token" } }
$delayedScrollStyle = Get-ChildItem -LiteralPath $root -Filter '*.cs' | Where-Object {
    (Get-Content -Raw $_.FullName) -match '(?s)Dispatcher\.BeginInvoke.{0,220}SoftenScrollBars'
}
if ($delayedScrollStyle) { throw ('Popup scrollbar styling must not be deferred past the first frame: ' + (($delayedScrollStyle.Name) -join ', ')) }
if (-not $desktop.Contains('if (ActivateBlockingDialog()) return;')) { throw 'Explorer fixed-layer actions must be blocked by the active main dialog.' }
if (-not $sports.Contains('if (sportsWindow != null)') -or -not $sports.Contains('sportsWindow.Show(); sportsWindow.Activate();')) { throw 'KBO must remain a single-instance independent tool window.' }
if ($settings.Contains('Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { UiRound.SoftenScrollBars(contentScroll)')) { throw 'Settings scrollbars must be styled before the first visible frame.' }
if (-not $settings.Contains('contentScroll.ApplyTemplate();') -or -not $settings.Contains('contentScroll.Opacity = 1;')) { throw 'Settings must reveal its content only after ONHARU scrollbar styling.' }

Write-Host 'ONHARU popup policy checks passed.'
