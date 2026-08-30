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
foreach ($token in @('BindCalendarNavigation(previousButton, -1)', 'BindCalendarNavigation(nextButton, 1)', 'periodNavigation.Children.Add(todayButton)', 'Margin = new Thickness(-8, 0, 0, 0)', 'Grid.SetRow(opacitySlider, 0)', 'Width = 83, Height = 18', 'lowerSwitches.Children.Add(calendarRangeSwitch)', 'lowerSwitches.Children.Add(themeQuickSwitch)', 'lowerSwitches.Children.Add(positionModeSwitch)', 'monthTitle.Width = 306', 'monthTitle.HorizontalContentAlignment = HorizontalAlignment.Left', '"월 전체"', 'settings.VisibleWeekCount)) + "주"', 'brandLine.Margin = new Thickness(0)', 'new TemplateBindingExtension(Control.PaddingProperty)', 'var todayIcon =', 'settings.TodayStyle == "fill_icon"')) {
    if (-not (($layoutSource + $calendarSource) -like ('*' + $token + '*'))) { throw "Colorful header or today-highlight token is missing: $token" }
}
if (-not $layoutSource.Contains('new[] { 41.0, 55.0 }') -or -not $layoutSource.Contains('featureActions.Children.Add(searchButton)')) { throw 'View switch width and feature icons must remain in their two-level header rows.' }
if (-not $layoutSource.Contains('var lowerActions = new Grid { Width = 384') -or -not $layoutSource.Contains('Margin = new Thickness(0, 0, 6, 0)')) { throw 'The lower header row plus its right margin must fit the 390px action area without clipping the previous button.' }
if (-not $layoutSource.Contains('new[] { "이동", "고정" }') -or -not $layoutSource.Contains('Task.Delay(140)') -or -not $layoutSource.Contains('new OnharuSegmentedSwitch')) { throw 'Position mode must finish its segmented transition before locking.' }
if ($layoutSource.Contains('Button.IsMouseOverProperty')) { throw 'Default main buttons must use cursor-only hover feedback.' }
if (-not $mainSource.Contains('monthTitle.Template = ContentOnlyButtonTemplate();')) { throw 'The clickable date title must not use the Windows hover inversion.' }
foreach ($token in @('StyleLightHeaderActionButton(settingsButton, "settings")', 'HeaderGlyph(glyph, foreground)', 'StrokeThickness = glyph == "range" ? 1.2 : 1.8', 'Foreground = BrandBrush()', 'monthTitle.Foreground = BrandBrush()', 'UpdateTodayButtonStyle()', 'todayButton.Background = T("Button")', 'todayButton.Foreground = T("Text")')) {
    if (-not (($layoutSource + $mainSource + $themeSource) -like ('*' + $token + '*'))) { throw "Icon or fixed brand-style token is missing: $token" }
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
if (-not $stateColorSource.Contains('CalendarCell(string theme) { return theme == "dark" ? "#45454D"') -or -not $calendarSource.Contains('Brush(OnharuStateColors.CalendarCell(settings.ThemeId))')) { throw 'Dark calendar cells must keep the one-step-deeper charcoal surface without changing shared borders.' }
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
foreach ($token in @('InterfaceAccentColor()', 'opacitySlider.Foreground = Brush(OnharuStateColors.OpacityControl(settings.ThemeId))', 'ApplyNeutralSwitchPalette(calendarRangeSwitch)', 'ApplyNeutralSwitchPalette(positionModeSwitch)', 'ApplyDetailSwitchPalette(detailPeriodSwitch)', 'ApplyDetailSwitchPalette(detailOrderSwitch)', 'detailScroll.Resources["OnharuScrollThumb"]')) {
    if (-not (($themeSource + $layoutSource + $detailSource).Contains($token))) { throw "Neutral interface color is missing: $token" }
}
if (-not (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'MainWindow.PositionMode.cs')).Contains('ApplyNeutralSwitchPalette(positionModeSwitch)')) { throw 'Position mode refresh must preserve the neutral charcoal palette.' }
foreach ($token in @('StyleLightHeaderActionButton(searchButton, "⌕")', 'StyleLightHeaderActionButton(timetableButton, "▦")', 'StyleLightHeaderActionButton(diaryButton, "✎")', 'StyleLightHeaderActionButton(sportsButton, "⚾")', 'StyleLightHeaderActionButton(settingsButton, "settings")', 'settings.ThemeId == "dark") { StyleHeaderActionButton(button, glyph); return;', 'button.Background = Brushes.White', 'var foreground = Brush("#111827")')) {
    if (-not (($layoutSource + $themeSource).Contains($token))) { throw "Header feature buttons must keep their shared light style: $token" }
}
foreach ($token in @('ActionAccent(string theme)', 'SupportAccent(string theme)', 'GoogleSurface(string theme)', 'GoogleText(string theme)', 'GoogleButtonSurface(string theme)', 'GoogleButtonText(string theme)', 'HeaderSurface(string theme)', 'HeaderText(string theme)', 'NeutralSwitch(string theme, bool selected)', 'DetailScrollThumb(string theme, string mode)', 'ScrollThumb(string theme)', 'OpacityControl(string theme)')) {
    if (-not $stateColorSource.Contains($token)) { throw "Role-based interface color is missing: $token" }
}
foreach ($periodAccentToken in @('DetailPeriodTab(string theme, bool selected)', 'Set("#C2410C", "#FFFFFF", "#FB923C")', 'ReferenceEquals(control, detailPeriodSwitch)')) {
    if (-not (($stateColorSource + $themeSource).Contains($periodAccentToken))) { throw "Pastel detail-period accent is missing: $periodAccentToken" }
}
if (-not $themeSource.Contains('return OnharuStateColors.ActionAccent(settings.ThemeId);') -or -not $googleSource.Contains('StyleVividCheckBox(box, SupportAccentColor())')) { throw 'Action and support controls must use separate stable role colors.' }
if ($themeSource.Contains('OnharuColorPresets.RepresentativeColor(settings.SelectedPaletteIndex)')) { throw 'Preset category colors must not drive interface controls.' }
foreach ($token in @('StyleWindowControl(minimizeButton, "window_minimize", new Thickness(0))', 'StyleWindowControl(windowMaximizeButton, "window_maximize", new Thickness(0))', 'StyleWindowControl(closeWindowButton, "window_close", new Thickness(0))', 'button.Background = T("Button")', 'button.BorderBrush = T("Grid")')) {
    if (-not $layoutSource.Contains($token)) { throw "Window controls must share one neutral style: $token" }
}
if ($layoutSource.Contains('"#2563EB", "#EFF6FF"') -or $layoutSource.Contains('"#16A34A", "#F0FDF4"') -or $layoutSource.Contains('"#E11D48", "#FFF1F2"')) { throw 'Window controls must not use traffic-light colors.' }
if (-not $themeSource.Contains('todayButton.Background = T("Button")') -or -not $themeSource.Contains('OnharuStateColors.OpacityControl(settings.ThemeId)')) { throw 'Today navigation and the opacity control must keep their independent neutral colors.' }
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
foreach ($token in @('box.Template = ColorCheckBoxTemplate()', 'settings.ThemeId == "dark" ? T("Text")', 'StyleThemeCheckBox(check, ItemColor(localItem))', 'CategoryColorSystem.SelectionBackground(settings.ThemeId, settings.SelectedDateFillColor)', 'CategoryColorSystem.StrongAccent(baseColor)', 'starOutline = starFill', 'colored ? 2.05 : 1.15', 'OnharuStateColors.DetailTab')) {
    if (-not (($themeSource + $calendarSource + $detailSource).Contains($token))) { throw "Dark interaction contrast token is missing: $token" }
}
foreach ($token in @('StyleVividCheckBox(entry.Value, color)', 'StyleVividCheckBox(check, ItemColor(item))', 'OnharuColorPresets.VividColor(color)')) {
    if (-not (($themeSource + $detailSource).Contains($token))) { throw "Vivid right-side control styling is missing: $token" }
}
foreach ($token in @('"#B7ACE8"', '"#F1F5F9"', 'OnharuStateColors.DetailScrollTrack(settings.ThemeId)')) {
    if (-not (($stateColorSource + $themeSource).Contains($token))) { throw "Neutral detail scrollbar styling is missing: $token" }
}
if (-not $stateColorSource.Contains('theme == "dark" ? "#4B5563" : "#475569"')) { throw 'Light selected header switches must use blue-slate instead of charcoal.' }
if (-not $layoutSource.Contains('detailAddButton.Margin = new Thickness(8, 0, 13, 0)')) { throw 'Date and anniversary add buttons must share the same right alignment.' }
$roundSource = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'UiRound.cs')
foreach ($token in @('bar.Width = 9', "Background='{DynamicResource OnharuScrollTrack}'")) {
    if (-not $roundSource.Contains($token)) { throw "Readable scrollbar construction is missing: $token" }
}
foreach ($token in @('Set("#C2410C", "#FFFFFF", "#FB923C")', 'Set("#1D4ED8", "#FFFFFF", "#60A5FA")')) {
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
foreach ($darkDetailToken in @('timeMode ? (settings.ThemeId == "dark" ? T("Card") : Brushes.White)', 'Background = settings.ThemeId == "dark" ? T("Card") : Brushes.White', 'settings.ThemeId == "dark" ? Brushes.White : Brush("#334155")')) {
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
foreach ($token in @('panel.Children.Insert(0, SectionCard(themeGroup))', 'panel.Children.Add(SectionCard(paletteGroup))', 'paletteGroup.Children.Add(presets)', 'Action openPalette = delegate', 'if (allowPresetApply && applyPalette != null)', 'Changing skin must preserve the user', 'selectedPaletteIndexValue >= 0 && selectedPaletteIndexValue <= customPaletteIndex', 'if (index <= customPaletteIndex) presets.Children.Add(option)')) {
    if (-not $settingsSource.Contains($token)) { throw "Theme settings layout token is missing: $token" }
}
foreach ($token in @('paletteGroup.Children.Add(paletteSaveRow)', 'ApplyRandomPalettePlacement()', 'RandomizePaletteOnStartup = randomizePalette.IsChecked == true')) {
    if (($settingsSource + (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'MainWindow.Startup.cs'))).Contains($token)) { throw "Retired palette customization remains: $token" }
}
foreach ($token in @('lowerSwitches.Children.Add(themeQuickSwitch)', 'UpdateThemeQuickSwitchStyle()', 'ApplyNeutralSwitchPalette(themeQuickSwitch)', 'refreshPaletteChangeButton')) {
    if (-not (($layoutSource + $settingsSource + $themeSource).Contains($token))) { throw "Always-visible theme switch or preset button style is missing: $token" }
}
Write-Host 'ONHARU 2.2 theme palette checks passed.'
