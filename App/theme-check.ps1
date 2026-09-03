param([Alias('ExePath')][string]$Exe = (Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.2-local-test.exe'))
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Exe).Path)
$paletteType = $assembly.GetType('FamilyPlanner.OnharuThemePalette', $true)
$for = $paletteType.GetMethod('For', [Reflection.BindingFlags]'Public,Static')
$normalize = $paletteType.GetMethod('Normalize', [Reflection.BindingFlags]'Public,Static')
$indexer = $paletteType.GetProperty('Item')
$seen = @{}
foreach ($id in @('classic','dark')) {
    $palette = $for.Invoke($null, @($id))
    foreach ($role in @('Shell','Calendar','Sidebar','Grid','Text','Muted','Accent','Button','Icon')) {
        $value = $indexer.GetValue($palette, @($role))
        if ($value -notmatch '^#[0-9A-Fa-f]{6,8}$') { throw "Invalid theme color: $id/$role=$value" }
    }
    $seen[$id] = $indexer.GetValue($palette, @('Shell'))
}
if (($seen.Values | Select-Object -Unique).Count -ne 2) { throw 'Theme shells must be visually distinct.' }
if ($normalize.Invoke($null, @('unknown')) -ne 'classic') { throw 'Unknown themes must safely fall back to classic.' }
# 2026-09-03: 팔레트 인덱서는 없는 키에 마젠타 `#FF00FF`를 돌려준다. 누락을 눈에 띄게 하려는 의도지만,
# `T("Card")`가 정의되지 않은 채로 오래 남아 블랙 스킨의 시간순·미완료 카드가 형광 분홍으로 보였다.
# 파스텔은 같은 자리에서 흰색을 직접 써서 드러나지 않았고 자동 검사도 문자열만 봐서 놓쳤다.
# 소스가 실제로 쓰는 모든 키를 팔레트에 물어보고 마젠타가 나오면 실패시킨다.
$usedRoles = @{}
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs') {
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($hit in [regex]::Matches($text, 'T\("([A-Za-z]+)"\)')) { $usedRoles[$hit.Groups[1].Value] = $true }
}
if ($usedRoles.Count -lt 10) { throw '테마 역할색 사용처를 찾지 못했습니다. 검사 정규식을 확인하세요.' }
foreach ($id in @('classic','dark')) {
    $palette = $for.Invoke($null, @($id))
    foreach ($role in $usedRoles.Keys) {
        $value = $indexer.GetValue($palette, @($role))
        if ($value -eq '#FF00FF') { throw "테마 팔레트에 없는 역할색을 쓰고 있습니다: $id/$role" }
        if ($value -notmatch '^#[0-9A-Fa-f]{6,8}$') { throw "Invalid theme color: $id/$role=$value" }
    }
}
$mainType = $assembly.GetType('FamilyPlanner.MainWindow', $true)
$templateMethod = $mainType.GetMethod('ColorCheckBoxTemplate', [Reflection.BindingFlags]'NonPublic,Static')
if ($null -eq $templateMethod -or $null -eq $templateMethod.Invoke($null, @())) { throw 'Colorful checkbox template could not be created.' }
$themeSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.Theme.cs') -Raw -Encoding UTF8
$layoutSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.Layout.cs') -Raw -Encoding UTF8
$mainSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.cs') -Raw -Encoding UTF8
$googleSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.Display.cs') -Raw -Encoding UTF8
$calendarSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.Calendar.cs') -Raw -Encoding UTF8
$colorSystemSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'CategoryColorSystem.cs') -Raw -Encoding UTF8
$presetSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'OnharuColorPresets.cs') -Raw -Encoding UTF8
$stateColorSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'OnharuStateColors.cs') -Raw -Encoding UTF8
$detailSource = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'MainWindow.Detail.cs')
foreach ($token in @('FilterColor(entry.Key, entry.Value)', 'StyleVividCheckBox(entry.Value, color)', 'EventBackgroundBrush(item)', 'EventTextBrush(item)')) {
    if (-not (($themeSource + $layoutSource + $googleSource + $calendarSource) -like ('*' + $token + '*'))) { throw "Colorful skin coverage token is missing: $token" }
}
if (-not $themeSource.Contains('CategoryColorSystem.CheckBoxBackground(settings.ThemeId, color)')) { throw 'Sidebar checkboxes must use the stronger category checkbox color.' }
if (-not $colorSystemSource.Contains('return Background(theme, hex);') -or -not $colorSystemSource.Contains('return Foreground(theme, hex);')) { throw 'Detail cards must share the exact calendar event fill and text calculations.' }
if (-not $mainSource.Contains('OnharuColorPresets.DefaultCategories()')) { throw 'Default category colors must come from OnharuColorPresets.' }
if (-not $layoutSource.Contains('Tag = Colors[category]') -or -not $googleSource.Contains('Tag = color') -or -not $googleSource.Contains('StyleVividCheckBox(box, color)')) { throw 'All sidebar filters must carry and immediately apply their category color.' }
if (-not $themeSource.Contains("Width='14' Height='14'") -or -not $themeSource.Contains("ContentPresenter Margin='4,0,0,0'")) { throw 'Colorful checkbox footprint must remain compatible with the 2.1 sidebar layout.' }
foreach ($unavailableToken in @("LineHeight='14'", "LineStackingStrategy='BlockLineHeight'", "TargetName='Box' Property='BorderBrush' Value='White'")) {
    if (-not $themeSource.Contains($unavailableToken)) { throw "Unavailable detail checkboxes must keep a centered cross and visible border: $unavailableToken" }
}
if (-not $layoutSource.Contains('Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top')) { throw 'Special Day filters must align with the first local filter row.' }
# 2026-09-02: 오늘 버튼이 periodNavigation의 42x23 텍스트 버튼에서
# monthArrows 안의 점 버튼(더블클릭으로 오늘 이동)으로 바뀌었다. 기능은 남아 있고 형태만 달라졌다.
# 같은 개편에서 사라진 'Margin = new Thickness(-8, 0, 0, 0)' 토큰도 함께 뺐다. 현재 소스에 없는 값이다.
# 2026-09-02: 헤더 개편으로 바뀐 세 토큰을 현재 구조로 갱신했다.
#  - 투명도 슬라이더가 별도 Grid 행에서 브랜드 줄 `brandNavigation`으로 옮겨졌다. 폭도 83에서 78로 줄었다.
#  - 월 제목의 고정 폭 306이 사라지고 `compactHeaderWidth`(245) 기준의 `monthNavigation` Grid가 폭을 정한다.
# 2026-09-03: 연·월 제목 옆 `‹ • ›` 이동 버튼 셋을 없앴다. 월 이동은 제목 클릭으로 열리는
# 날짜 선택기(OpenMonthJump)가, 오늘로 이동은 상세 `선택 날짜` 탭 더블클릭이 담당한다.
# 그 두 경로가 살아 있는지를 대신 단언한다.
foreach ($token in @('lowerActions.Children.Add(opacitySlider)', 'Width = 78, Height = 18', 'lowerSwitches.Children.Add(calendarRangeSwitch)', 'lowerSwitches.Children.Add(themeQuickSwitch)', 'lowerSwitches.Children.Add(positionModeSwitch)', 'var monthNavigation = new Grid { Width = compactHeaderWidth }', 'monthTitle.HorizontalContentAlignment = HorizontalAlignment.Left', '"월 전체"', 'settings.VisibleWeekCount)) + "주"', 'brandLine.Margin = new Thickness(0)', 'new TemplateBindingExtension(Control.PaddingProperty)', 'var todayIcon =', 'settings.TodayStyle == "fill_icon"')) {
    if (-not (($layoutSource + $calendarSource) -like ('*' + $token + '*'))) { throw "Colorful header or today-highlight token is missing: $token" }
}
if (-not $layoutSource.Contains('new[] { 41.0, 55.0 }') -or -not $layoutSource.Contains('featureActions.Children.Add(searchButton)')) { throw 'View switch width and feature icons must remain in their two-level header rows.' }
if (-not $layoutSource.Contains('var lowerActions = new Grid { Width = 384') -or -not $layoutSource.Contains('Margin = new Thickness(0, 0, 6, 0)')) { throw 'The lower header row plus its right margin must fit the 390px action area without clipping the previous button.' }
if (-not $layoutSource.Contains('new[] { "이동", "고정" }') -or -not $layoutSource.Contains('Task.Delay(140)') -or -not $layoutSource.Contains('new OnharuSegmentedSwitch')) { throw 'Position mode must finish its segmented transition before locking.' }
if ($layoutSource.Contains('Button.IsMouseOverProperty')) { throw 'Default main buttons must use cursor-only hover feedback.' }
if (-not $mainSource.Contains('monthTitle.Template = ContentOnlyButtonTemplate();')) { throw 'The clickable date title must not use the Windows hover inversion.' }
# 2026-09-02: 아이콘 획 두께 판정은 `MainWindow.Layout.cs`에서 `OnharuIcons.cs`로 옮겨졌고
# 값도 1.8에서 1.6으로 줄었다(2026-09-01 기록). 검사도 새 위치의 소스를 함께 읽는다.
# 상단 오늘 버튼 `todayButton`은 `monthArrows` 안의 점 버튼으로 대체돼 이름 자체가 사라졌다.
# 기능은 남아 있으므로 `UpdateTodayButtonStyle`·`todayButton.*` 대신 점 버튼과 더블클릭 이동을 단언한다.
$iconSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'OnharuIcons.cs') -Raw -Encoding UTF8
foreach ($token in @('StyleLightHeaderActionButton(settingsButton, "settings")', 'HeaderGlyph(glyph, foreground)', 'const double DefaultThickness = 1.6', 'glyph == "range" ? 1.2 : DefaultThickness', 'Foreground = BrandBrush()', 'monthTitle.Foreground = BrandBrush()', 'monthTitle.Click += OpenMonthJump', 'if (e.ClickCount < 2) return; GoToday(); e.Handled = true;')) {
    if (-not (($layoutSource + $mainSource + $themeSource + $iconSource) -like ('*' + $token + '*'))) { throw "Icon or fixed brand-style token is missing: $token" }
}
foreach ($token in @('HeaderMonthButton(', 'monthArrows')) {
    if ($layoutSource.Contains($token)) { throw "제거한 연·월 이동 버튼이 되살아났습니다: $token" }
}
$sportsSource = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'SportsCalendarWindow.cs')
if (($sportsSource | Select-String -Pattern 'new OnharuSegmentedSwitch' -AllMatches).Matches.Count -lt 3) { throw 'Sports view, range, and size controls must use segmented switches.' }
$segmentSource = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'OnharuSegmentedSwitch.cs')
if (-not $segmentSource.Contains('button.Template = new ControlTemplate') -or -not $segmentSource.Contains('Border.BackgroundProperty, Brushes.Transparent')) { throw 'Segment buttons must not show the Windows hover inversion.' }
foreach ($token in @('LabelWidth(labels[i]) + 4', 'Padding = new Thickness(2, 0, 2, 0)', 'new TemplateBindingExtension(Control.PaddingProperty)')) {
    if (-not $segmentSource.Contains($token)) { throw "Segment width or padding rule is missing: $token" }
}
if (-not $segmentSource.Contains('FontSize = 12.5') -or -not $layoutSource.Contains('new[] { "이동", "고정" }')) { throw 'Segment text visibility or position-mode labels are missing.' }
$layerSource = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'MainWindow.ExplorerLayer.cs')
$placementSource = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'MainWindow.Placement.cs')
if (-not $layerSource.Contains('if (RestoreBlockingDialog()) { UpdateModeButtons(); return; }') -or -not $placementSource.Contains('if (RestoreBlockingDialog()) { UpdateModeButtons(); return; }') -or $layerSource.Contains('if (IsEnabled) return false;')) { throw 'Visible ONHARU dialogs must block and roll back both position-mode transitions.' }
$settingsSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SettingsWindow.cs') -Raw -Encoding UTF8
if (-not $stateColorSource.Contains('return theme == "dark" ? palette["Card"] : palette["CardBorder"];') -or -not $calendarSource.Contains('Brush(OnharuStateColors.CalendarCell(settings.ThemeId))')) { throw 'Dark calendar cells must keep the one-step-deeper charcoal surface without changing shared borders.' }
if ($settingsSource.Contains('static string[][] ThemePresetPalettes') -or $settingsSource.Contains('static string[] ThemePresetNames')) { throw 'Preset policy must not return to SettingsWindow.' }
foreach ($presetToken in @('맑고 선명한 조합','차분한 중간톤','밝고 산뜻한 조합')) {
    if (-not $presetSource.Contains($presetToken)) { throw "Preset module is missing: $presetToken" }
}
foreach ($token in @('OnharuColorPresets.Names', 'OnharuColorPresets.Palettes()', '"내 설정으로 저장"', 'Tuple.Create("야구", baseball, "local:baseball")', 'ColorEditor("D-Day"', 'ColorEditor("기념일"', 'ColorEditor("국경일"', 'Tuple.Create("파스텔", "classic"')) {
    if (-not $settingsSource.Contains($token)) { throw "Theme preset or local color editor is missing: $token" }
}
foreach ($token in @('Tuple.Create("special:dday", "D-Day")', 'specialEditors.OrderBy(x => savedRank(x.Item1))', 'special:anniversary')) {
    if (-not $settingsSource.Contains($token)) { throw "Special Day palette order persistence is missing: $token" }
}
foreach ($token in @('var filterCategories =', 'CategoryOrderPolicy.Rank(settings.CategoryOrder', '"special:dday"', '"special:anniversary"')) {
    if (-not $layoutSource.Contains($token)) { throw "Local or Special Day sidebar order synchronization is missing: $token" }
}
if (-not $googleSource.Contains('void ApplySidebarCategoryOrder()') -or -not $googleSource.Contains('ReorderFilterPanel(localFilterRow') -or -not $googleSource.Contains('ReorderFilterPanel(specialFilterRow')) { throw 'Saved local and Special Day palette order must immediately reorder the existing sidebar controls.' }
if (-not $googleSource.Contains('void NormalizeSpecialFilterSpacing()') -or -not $googleSource.Contains('i == boxes.Count - 1 ? 0 : 7')) { throw 'Special Day checkbox spacing must follow visual order instead of category name.' }
if (-not (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'MainWindow.Settings.cs')).Contains('ApplySidebarCategoryOrder();')) { throw 'Settings save must apply sidebar category order immediately.' }
foreach ($token in @('InterfaceAccentColor()', 'opacitySlider.Foreground = OpacitySliderBrush()', 'ApplyNeutralSwitchPalette(calendarRangeSwitch)', 'ApplyNeutralSwitchPalette(positionModeSwitch)', 'ApplyDetailSwitchPalette(detailPeriodSwitch)', 'StyleDetailHeaderActionButtons()', 'detailScroll.Resources["OnharuScrollThumb"]')) {
    if (-not (($themeSource + $layoutSource + $detailSource).Contains($token))) { throw "Neutral interface color is missing: $token" }
}
if (-not (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'MainWindow.PositionMode.cs')).Contains('ApplyNeutralSwitchPalette(positionModeSwitch)')) { throw 'Position mode refresh must preserve the neutral charcoal palette.' }
foreach ($token in @('StyleLightHeaderActionButton(searchButton, "⌕")', 'StyleLightHeaderActionButton(timetableButton, "▦")', 'StyleLightHeaderActionButton(diaryButton, "◴")', 'StyleLightHeaderActionButton(sportsButton, "⚾")', 'StyleLightHeaderActionButton(settingsButton, "settings")', 'settings.ThemeId == "dark") { StyleHeaderActionButton(button, glyph); return;', 'button.Background = Brushes.White', 'var foreground = Brush("#111827")')) {
    if (-not (($layoutSource + $themeSource).Contains($token))) { throw "Header feature buttons must keep their shared light style: $token" }
}
foreach ($token in @('ActionAccent(string theme)', 'SupportAccent(string theme)', 'GoogleSurface(string theme)', 'GoogleText(string theme)', 'GoogleButtonSurface(string theme)', 'GoogleButtonText(string theme)', 'HeaderSurface(string theme)', 'HeaderText(string theme)', 'NeutralSwitch(string theme, bool selected)', 'DetailScrollThumb(string theme, string mode)', 'ScrollThumb(string theme)')) {
    if (-not $stateColorSource.Contains($token)) { throw "Role-based interface color is missing: $token" }
}
foreach ($periodAccentToken in @('DetailPeriodTab(string theme, bool selected)', 'Set("#3B82F6", "#FFFFFF", "#60A5FA")', 'ReferenceEquals(control, detailPeriodSwitch)')) {
    if (-not (($stateColorSource + $themeSource).Contains($periodAccentToken))) { throw "Pastel detail-period accent is missing: $periodAccentToken" }
}
if (-not $themeSource.Contains('return OnharuStateColors.ActionAccent(settings.ThemeId);') -or -not $googleSource.Contains('StyleVividCheckBox(box, SupportAccentColor())')) { throw 'Action and support controls must use separate stable role colors.' }
if ($themeSource.Contains('OnharuColorPresets.RepresentativeColor(settings.SelectedPaletteIndex)')) { throw 'Preset category colors must not drive interface controls.' }
foreach ($token in @('StyleWindowControl(minimizeButton, "window_minimize", new Thickness(0))', 'StyleWindowControl(windowMaximizeButton, "window_maximize", new Thickness(0))', 'StyleWindowControl(closeWindowButton, "window_close", new Thickness(0))', 'button.Background = T("Button")', 'button.BorderBrush = T("Grid")')) {
    if (-not $layoutSource.Contains($token)) { throw "Window controls must share one neutral style: $token" }
}
if ($layoutSource.Contains('"#2563EB", "#EFF6FF"') -or $layoutSource.Contains('"#16A34A", "#F0FDF4"') -or $layoutSource.Contains('"#E11D48", "#FFF1F2"')) { throw 'Window controls must not use traffic-light colors.' }
# 2026-09-02: 상단 오늘 텍스트 버튼 `todayButton`은 `monthArrows`의 점 버튼으로 대체돼 이름이 사라졌다.
# 투명도 슬라이더 색은 사용자가 `현재 기준을 적용`으로 확정했다. 브랜드 그라데이션은 `온하루 · ONHARU`와
# 연·월 제목 전용이고, 슬라이더는 조작 컨트롤이므로 인터페이스 강조색(`OnharuStateColors.ActionAccent`)을 쓴다.
# 생성 시점과 테마 적용 시점 두 곳이 어긋나면 첫 화면과 스킨 전환 후 색이 달라지므로 둘 다 단언한다.
if (-not $themeSource.Contains('opacitySlider.Foreground = OpacitySliderBrush()')) { throw '투명도 슬라이더 색은 OpacitySliderBrush 한 곳에서 정한다.' }
if (-not $layoutSource.Contains('ToolTip = "달력 투명도", Foreground = OpacitySliderBrush()')) { throw '투명도 슬라이더는 생성 시점에도 같은 색 함수를 쓴다.' }
if (($themeSource | Select-String -Pattern 'opacitySlider\.Foreground =' -AllMatches).Matches.Count -ne 1) { throw '투명도 슬라이더 색은 MainWindow.Theme.cs 한 곳에서만 정한다.' }
foreach ($token in @('refreshPastelThemeCard', 'CategoryColorSystem.Background("classic", accent)', 'PaletteEditorBackground(c)', 'PaletteEditorForeground(c)')) {
    if (-not $settingsSource.Contains($token)) { throw "Pastel settings preview rule is missing: $token" }
}
foreach ($pastelPreviewToken in @('option.Item2 == "dark" ? "#1A1A1A" : "#F6F0FF"', 'var accent = "#70429B";')) {
    if (-not $settingsSource.Contains($pastelPreviewToken)) { throw "Bright and fresh pastel skin preview is missing: $pastelPreviewToken" }
}
foreach ($token in @('googleColorGrid.Children.Add(ColorEditor("국경일", holidayColor, "휴일", false))', 'OnharuColorPresets.HolidayColor(index)', 'HolidayColor = HexOr("국경일", holidayColor)')) {
    if (-not $settingsSource.Contains($token)) { throw "Preset-aware red holiday palette token is missing: $token" }
}
if (-not (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'OnharuColorPresets.cs')).Contains('return foregrounds[index][5];')) { throw 'Holiday preset color must use the sixth local color.' }
if (-not $settingsSource.Contains('const int presetCount = 3, customPaletteIndex = 3, googlePaletteIndex = 4;')) { throw 'Three tone-preset layout is missing.' }
if (-not $settingsSource.Contains('selectedPaletteIndexValue >= 0 && selectedPaletteIndexValue <= customPaletteIndex')) { throw 'Saved palette selection is not restored when settings reopens.' }
if (-not $settingsSource.Contains('GooglePresetVariant(palettes[index][googleIndex % 6], googleIndex)')) { throw 'Default Google colors must not duplicate the six local preset colors.' }
foreach ($token in @('UniquePresetColor(candidate, googleIndex, usedColors)', 'applyPalette(selectedPaletteIndex);')) {
    if (-not $settingsSource.Contains($token)) { throw "Selected presets must initialize and keep every rendered calendar color distinct: $token" }
}
$settingsType = $assembly.GetType('FamilyPlanner.SettingsWindow', $true)
$presetType = $assembly.GetType('FamilyPlanner.OnharuColorPresets', $true)
$presetNamesField = $presetType.GetField('Names', [Reflection.BindingFlags]'Public,Static')
$presetColorsMethod = $presetType.GetMethod('Palettes', [Reflection.BindingFlags]'Public,Static')
$softWorkspaceMethod = $presetType.GetMethod('SoftWorkspacePalette', [Reflection.BindingFlags]'Public,Static')
$presetRows = @($presetColorsMethod.Invoke($null, @()))
$softWorkspace = @($softWorkspaceMethod.Invoke($null, @()))
if ($softWorkspace.Count -ne 12 -or @($softWorkspace | Select-Object -Unique).Count -ne 12) { throw 'Soft Workspace custom palette must provide twelve distinct colors.' }
if ((@($presetNamesField.GetValue($null)) -join '|') -ne '차분한 중간톤|밝고 산뜻한 조합|맑고 선명한 조합') { throw 'Tone preset order is invalid.' }
if (@($presetRows | Where-Object { $_.Count -ne 12 -or @($_ | Select-Object -Unique).Count -ne 12 }).Count) { throw 'Every tone preset must contain twelve distinct colors.' }
$expectedLocalRoles = @(
    '#3F5F85|#884B60|#35684B|#705A18|#674F80|#9F3F46',
    '#3457A4|#A92E5C|#16704B|#755C00|#70429B|#B4234D',
    '#1D4ED8|#BE185D|#087A4B|#806000|#6D28D9|#C62828')
for ($i = 0; $i -lt 3; $i++) {
    if ((@($presetRows[$i])[0..5] -join '|') -ne $expectedLocalRoles[$i]) { throw "Mainstream local role palette is invalid: $i" }
}
if (-not $settingsSource.Contains('Text = label, FontSize = 12')) { throw 'Preset option labels must use the standard settings font size.' }
foreach ($layoutToken in @('var presets = new WrapPanel', 'option.Margin = new Thickness(0, 5, 12, 5)', 'Padding = new Thickness(0)')) {
    if (-not $settingsSource.Contains($layoutToken)) { throw "Long tone-preset labels must wrap cleanly without clipping: $layoutToken" }
}
foreach ($id in @('classic','dark')) {
    $presetNames = @($presetNamesField.GetValue($null))
    $presetColors = @($presetColorsMethod.Invoke($null, @()))
    if ($presetNames.Count -ne 3 -or @($presetNames | Select-Object -Unique).Count -ne 3) { throw "Preset names must be three distinct tone labels: $id" }
    foreach ($colors in $presetColors) {
        if ($colors.Count -lt 12 -or @($colors | Select-Object -Unique).Count -ne $colors.Count) { throw "Every preset must provide distinct local and Google colors: $id" }
    }
}
if ($settingsSource.Contains('paletteOptions[customPaletteIndex].IsChecked = true;')) { throw 'RGB edits or My Settings save must not silently change the selected preset.' }
foreach ($token in @('Content = "날짜 원형"', 'Content = "색상 + 날짜 원형"', 'Tag = "icon"', 'Tag = "fill_icon"')) {
    if (-not $settingsSource.Contains($token)) { throw "Today display option is missing: $token" }
}
if ($settingsSource.Contains('GroupName = "TodayStyle", IsChecked = todayStyle == "border"') -or $settingsSource.Contains('GroupName = "TodayStyle", IsChecked = todayStyle == "both"')) { throw 'Legacy today-border choices must not be shown.' }
foreach ($token in @('CategoryColorSystem.Foreground(settings.ThemeId, ItemColor(item))', 'CategoryColorSystem.Background(settings.ThemeId, ItemColor(item))')) {
    if (-not $themeSource.Contains($token)) { throw "Shared category color system is missing from the calendar: $token" }
}
foreach ($token in @('CategoryColorSystem.ReadableEmphasisForeground(background, preferred)', 'SafeColor(item.ImportantBackgroundColor', 'SafeColor(item.ImportantTextColor')) {
    if (-not $themeSource.Contains($token)) { throw "Important event colors must pass through the shared contrast guard: $token" }
}
foreach ($stateHex in @('#7462CF','#6D5CC6','#D8D2F3','#493A91','#B6AAE3','#4FAFA5','#3B8F89','#2B2342','#EEEAFE','#5A43A4','#D8CCFF','#303744','#2A2E36','#555E6D','#4A505B','#0E7490','#22D3EE','#1D4ED8','#60A5FA','#8C8C96','#B0B0B8','#BE185D','#F472B6','#F1F5F9','#B7ACE8')) {
    if ($detailSource.Contains($stateHex)) { throw "State color policy must not return to MainWindow.Detail: $stateHex" }
    if (-not $stateColorSource.Contains($stateHex)) { throw "State color module is missing: $stateHex" }
}
foreach ($token in @('OnharuStateColors.MoreButton(settings.ThemeId)', 'more.BorderThickness = new Thickness(1)', 'more.Padding = new Thickness(6, 0, 6, 0)')) {
    if (-not $calendarSource.Contains($token)) { throw "Calendar overflow button styling is missing: $token" }
}
if (-not $calendarSource.Contains('bar.Opacity = .66') -or -not $detailSource.Contains('row.Opacity = .66')) { throw 'Completed-item fade opacity must remain readable.' }
foreach ($token in @('box.Template = ColorCheckBoxTemplate()', 'settings.ThemeId == "dark" ? T("Text")', 'StylePopupCheckBox(check, ItemColor(localItem))', 'CategoryColorSystem.SelectionBackground(settings.ThemeId, settings.SelectedDateFillColor)', 'CategoryColorSystem.StrongAccent(baseColor)', 'starOutline = starFill', 'colored ? 2.05 : 1.15', 'OnharuStateColors.DetailTab')) {
    if (-not (($themeSource + $calendarSource + $detailSource).Contains($token))) { throw "Dark interaction contrast token is missing: $token" }
}
foreach ($token in @('StyleVividCheckBox(entry.Value, color)', 'StyleVividCheckBox(check, ItemColor(item))', 'OnharuColorPresets.VividColor(color)')) {
    if (-not (($themeSource + $detailSource).Contains($token))) { throw "Vivid right-side control styling is missing: $token" }
}
foreach ($token in @('"#B7ACE8"', '"#F1F5F9"', 'OnharuStateColors.DetailScrollTrack(settings.ThemeId)')) {
    if (-not (($stateColorSource + $themeSource).Contains($token))) { throw "Neutral detail scrollbar styling is missing: $token" }
}
if (-not $stateColorSource.Contains('theme == "dark" ? "#4B5563" : "#475569"')) { throw 'Light selected header switches must use blue-slate instead of charcoal.' }
# 2026-09-02: 오른쪽 정렬을 고정 Margin(8,0,13,0)으로 맞추던 방식이 DockPanel 도킹으로 바뀌었다.
# 같은 줄의 기념일 추가 버튼은 현재 없다. 구조로 보장되는 도킹 자체를 단언한다.
if (-not $layoutSource.Contains('DockPanel.SetDock(detailAddButton, Dock.Right)')) { throw '상세 헤더의 일정 추가 버튼은 오른쪽에 도킹해야 한다.' }
$roundSource = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'UiRound.cs')
foreach ($token in @('bar.Width = 9', "Background='{DynamicResource OnharuScrollTrack}'")) {
    if (-not $roundSource.Contains($token)) { throw "Readable scrollbar construction is missing: $token" }
}
# 2026-09-03: 상세 범위 탭 선택색을 두 스킨 모두 밝은 블루 `#3B82F6`으로 통일했다.
# 이전 피치 `#C2410C`는 블랙에서 KBO 갈색과, 파스텔 보라는 회보라 카드와 계열이 겹쳤다.
foreach ($token in @('Set("#3B82F6", "#FFFFFF", "#60A5FA")', 'Set("#1D4ED8", "#FFFFFF", "#60A5FA")')) {
    if (-not $stateColorSource.Contains($token)) { throw "Fixed vivid detail-button palette is missing: $token" }
}
$colorType = $assembly.GetType('FamilyPlanner.CategoryColorSystem', $true)
$flags = [Reflection.BindingFlags]'Public,Static'
$backgroundMethod = $colorType.GetMethod('Background', $flags, $null, [Type[]]@([string], [string]), $null)
$foregroundMethod = $colorType.GetMethod('Foreground', $flags, $null, [Type[]]@([string], [string]), $null)
$detailBackgroundMethod = $colorType.GetMethod('DetailBackground', $flags)
$detailForegroundMethod = $colorType.GetMethod('DetailForeground', $flags)
$selectionBackgroundMethod = $colorType.GetMethod('SelectionBackground', $flags)
$selectionForegroundMethod = $colorType.GetMethod('SelectionForeground', $flags)
$contrastMethod = $colorType.GetMethod('ContrastRatio', $flags)
$pairMethod = $presetType.GetMethod('TryPastelPair', [Reflection.BindingFlags]'Public,Static')
foreach ($colors in $presetColors) {
    foreach ($hex in $colors) {
        $pairArgs = @($hex, $null, $null)
        if (-not $pairMethod.Invoke($null, $pairArgs)) { throw "Authored pastel pair is missing: $hex" }
        $actual = $backgroundMethod.Invoke($null, @('classic', $hex))
        $actualText = $foregroundMethod.Invoke($null, @('classic', $hex))
        if ($actual.ToString().ToUpperInvariant() -notlike ('*' + $pairArgs[1].Substring(1))) { throw "Authored pastel background changed at runtime: $hex=$actual" }
        if ($actualText.ToString().ToUpperInvariant() -notlike ('*' + $pairArgs[2].Substring(1))) { throw "Authored pastel text changed at runtime: $hex=$actualText" }
    }
}
foreach ($id in @('classic','dark')) {
    foreach ($colors in @($presetColorsMethod.Invoke($null, @()))) {
        foreach ($hex in $colors) {
            $background = $backgroundMethod.Invoke($null, @($id, $hex))
            $foreground = $foregroundMethod.Invoke($null, @($id, $hex))
            $contrast = [double]$contrastMethod.Invoke($null, @($background, $foreground))
            if ($contrast -lt 4.5) { throw "Category text contrast is below 4.5:1: $id/$hex=$contrast" }
            $detailBackground = $detailBackgroundMethod.Invoke($null, @($id, $hex))
            $detailForeground = $detailForegroundMethod.Invoke($null, @($id, $hex))
            $detailContrast = [double]$contrastMethod.Invoke($null, @($detailBackground, $detailForeground))
            if ($detailContrast -lt 4.5) { throw "Detail card text contrast is below 4.5:1: $id/$hex=$detailContrast" }
            if ($detailBackground.ToString() -ne $background.ToString() -or $detailForeground.ToString() -ne $foreground.ToString()) {
                throw "Detail and calendar event colors must be identical: $id/$hex"
            }
            $selectionBackground = $selectionBackgroundMethod.Invoke($null, @($id, $hex))
            $selectionForeground = $selectionForegroundMethod.Invoke($null, @($id, $hex))
            $selectionContrast = [double]$contrastMethod.Invoke($null, @($selectionBackground, $selectionForeground))
            if ($selectionContrast -lt 4.5) { throw "Selected-date contrast is below 4.5:1: $id/$hex=$selectionContrast" }
        }
    }
}
$anniversarySource = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'MainWindow.Anniversary.cs')
foreach ($token in @('CategoryColorSystem.DetailBackground(settings.ThemeId, groupColor)', 'CategoryColorSystem.DetailForeground(settings.ThemeId, groupColor)', 'Colors["D-Day"]', 'Colors["기념일"]')) {
    if (-not (($detailSource + $anniversarySource).Contains($token))) { throw "Right detail card palette token is missing: $token" }
}
# 2026-09-03: `Card`를 팔레트에 넣어 두 스킨이 같은 역할색을 쓴다. 스킨 분기가 필요 없어졌다.
foreach ($darkDetailToken in @('timeMode ? T("Card")', 'var surface = new Border { Background = T("Card"),', 'settings.ThemeId == "dark" ? Brushes.White : Brush("#334155")')) {
    if (-not $detailSource.Contains($darkDetailToken)) { throw "Dark detail-card contrast is missing: $darkDetailToken" }
}
$settings = [Activator]::CreateInstance($assembly.GetType('FamilyPlanner.PlannerSettings', $true))
if ($settings.Version -ne 48 -or $settings.PaletteDefinitionVersion -ne 4 -or $settings.ThemeId -ne 'classic' -or -not $settings.ImportantFirst) { throw 'Theme settings defaults are invalid.' }
if (@($settings.CustomPalette).Count -ne 12) { throw 'My Settings must start with the Soft Workspace reference palette.' }
$themeDefinitionSource = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'OnharuTheme.cs')
if ($themeDefinitionSource.Contains('colorful') -or $settingsSource.Contains('"컬러"')) { throw 'Removed colorful skin code must not remain.' }
foreach ($token in @('ShowThemeQuickSwitch', '상단 스킨 전환 버튼 표시', '선택 프리셋 변경', '색상 설정 초기화')) {
    if (($layoutSource + $settingsSource + $themeSource).Contains($token)) { throw "Removed theme/color control remains: $token" }
}
# 2026-09-02: 카드 순서를 매직 인덱스에서 배열 하나로 옮겼다. 디자인 스킨이 첫 카드, 색상 조합이
# 둘째 카드라는 규칙은 그대로이므로 배열의 첫 두 줄을 단언한다.
foreach ($token in @('themeGroup,       // 디자인 스킨', 'paletteGroup,     // 추천 색상 조합', 'paletteGroup.Children.Add(presets)', 'Action openPalette = delegate', 'if (allowPresetApply && applyPalette != null)', 'Changing skin must preserve the user', 'selectedPaletteIndexValue >= 0 && selectedPaletteIndexValue <= customPaletteIndex', 'if (index <= customPaletteIndex) presets.Children.Add(option)')) {
    if (-not $settingsSource.Contains($token)) { throw "Theme settings layout token is missing: $token" }
}
foreach ($token in @('paletteGroup.Children.Add(paletteSaveRow)', 'ApplyRandomPalettePlacement()', 'RandomizePaletteOnStartup = randomizePalette.IsChecked == true')) {
    if (($settingsSource + (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'MainWindow.Startup.cs'))).Contains($token)) { throw "Retired palette customization remains: $token" }
}
foreach ($token in @('lowerSwitches.Children.Add(themeQuickSwitch)', 'UpdateThemeQuickSwitchStyle()', 'ApplyNeutralSwitchPalette(themeQuickSwitch)', 'refreshPaletteChangeButton')) {
    if (-not (($layoutSource + $settingsSource + $themeSource).Contains($token))) { throw "Always-visible theme switch or preset button style is missing: $token" }
}
# 더보기 팝업은 항상 흰 팝업 위에 그린다. 달력용 색(EventTextBrush 등)을 그대로 쓰면
# 블랙 스킨의 밝은 글자색이 흰 배경에 얹혀 글씨가 사라진다. 팝업 전용 색만 쓰게 막는다.
$overflowStart = $calendarSource.IndexOf('void ShowDayOverflowPopup(')
if ($overflowStart -lt 0) { throw 'ShowDayOverflowPopup is missing.' }
$overflowEnd = $calendarSource.IndexOf('void PlaceDayOverflowDialog(')
if ($overflowEnd -lt $overflowStart) { throw 'PlaceDayOverflowDialog must follow ShowDayOverflowPopup.' }
$overflowBody = $calendarSource.Substring($overflowStart, $overflowEnd - $overflowStart)
# 앞의 공백까지 함께 본다. 그러지 않으면 Popup 접두사가 붙은 팝업 전용 이름까지 걸린다.
foreach ($skinColorLeak in @(' EventTextBrush(', ' EventBackgroundBrush(', ' StyleThemeCheckBox(', 'T("Text")', 'T("Disabled")')) {
    if ($overflowBody.Contains($skinColorLeak)) { throw "Day overflow popup must not use skin colors on its white surface: $skinColorLeak" }
}
foreach ($popupColorFeature in @('const string PopupThemeId = "classic";', 'Brush PopupEventTextBrush(PlannerItem item)', 'Brush PopupEventBackgroundBrush(PlannerItem item)', 'void StylePopupCheckBox(CheckBox box, string color)')) {
    if (-not $themeSource.Contains($popupColorFeature)) { throw "Popup color helper is missing: $popupColorFeature" }
}

Write-Host 'ONHARU 2.2 theme palette checks passed.'
