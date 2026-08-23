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
foreach ($token in @('const double TopDragHeight = 60', 'EnableTopDrag(Window window)', 'IsInteractive(DependencyObject current)', 'shell.Loaded', 'FeatureTitle(string glyph, string title)', 'PrimaryButton(string text, double width)')) {
    if (-not $chrome.Contains($token)) { throw "Shared popup drag token missing: $token" }
}
$diary = Get-Content -Raw (Join-Path $root 'DiaryWindows.cs')
$timetable = Get-Content -Raw (Join-Path $root 'TimetableWindow.cs')
$sportsWindow = Get-Content -Raw (Join-Path $root 'SportsCalendarWindow.cs')
foreach ($token in @('OnharuSegmentedSwitch(new[] { "목록 보기", "한 장 보기" }', 'OnharuSegmentedSwitch(new[] { "최신순", "오래된순" }', 'FeatureTitle("✎", "나의 일기장")')) { if (-not $diary.Contains($token)) { throw "Diary tool chrome is inconsistent: $token" } }
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
