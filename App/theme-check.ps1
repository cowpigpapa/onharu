param([string]$Exe = (Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.2-local-test.exe'))
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
$themeSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.Theme.cs') -Raw
$layoutSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.Layout.cs') -Raw
$mainSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.cs') -Raw
$googleSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.Display.cs') -Raw
$calendarSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.Calendar.cs') -Raw
$colorSystemSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'CategoryColorSystem.cs') -Raw
$presetSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'OnharuColorPresets.cs') -Raw
$stateColorSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'OnharuStateColors.cs') -Raw
$detailSource = Get-Content -Raw (Join-Path $PSScriptRoot 'MainWindow.Detail.cs')
foreach ($token in @('FilterColor(entry.Key, entry.Value)', 'StyleThemeCheckBox(entry.Value, color)', 'EventBackgroundBrush(ItemColor(item)', 'EventTextBrush(ItemColor(item)')) {
    if (-not (($themeSource + $layoutSource + $googleSource + $calendarSource) -like ('*' + $token + '*'))) { throw "Colorful skin coverage token is missing: $token" }
}
if (-not $themeSource.Contains('CategoryColorSystem.CheckBoxBackground(settings.ThemeId, color)')) { throw 'Sidebar checkboxes must use the stronger category checkbox color.' }
if (-not $colorSystemSource.Contains('return Background(theme, hex);') -or -not $colorSystemSource.Contains('return Foreground(theme, hex);')) { throw 'Detail cards must share the exact calendar event fill and text calculations.' }
if (-not $mainSource.Contains('OnharuColorPresets.DefaultCategories()')) { throw 'Default category colors must come from OnharuColorPresets.' }
if (-not $layoutSource.Contains('Tag = Colors[category]') -or -not $googleSource.Contains('Tag = color') -or -not $googleSource.Contains('StyleThemeCheckBox(box, color)')) { throw 'All sidebar filters must carry and immediately apply their category color.' }
if (-not $themeSource.Contains("Width='14' Height='14'") -or -not $themeSource.Contains("ContentPresenter Margin='4,0,0,0'")) { throw 'Colorful checkbox footprint must remain compatible with the 2.1 sidebar layout.' }
if (-not $layoutSource.Contains('Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top')) { throw 'Special Day filters must align with the first local filter row.' }
foreach ($token in @('BindCalendarNavigation(previousButton, -1)', 'BindCalendarNavigation(nextButton, 1)', 'brandNavigation.Children.Add(todayButton)', 'monthTitle.Width = 306', 'monthTitle.HorizontalContentAlignment = HorizontalAlignment.Left', 'upperActions.Children.Add(opacitySlider)', 'lowerActions.Children.Add(positionModeSwitch)', '"월 전체"', 'settings.VisibleWeekCount)) + "주"', 'brandLine.Margin = new Thickness(0)', 'new TemplateBindingExtension(Control.PaddingProperty)', 'var todayIcon =', 'settings.TodayStyle == "fill_icon"')) {
    if (-not (($layoutSource + $calendarSource) -like ('*' + $token + '*'))) { throw "Colorful header or today-highlight token is missing: $token" }
}
if (-not $layoutSource.Contains('new[] { 41.0, 55.0 }') -or -not $layoutSource.Contains('featureActions.Children.Add(searchButton)')) { throw 'View switch width and feature icons must remain in their two-level header rows.' }
if (-not $layoutSource.Contains('new[] { "이동", "고정" }') -or -not $layoutSource.Contains('Task.Delay(140)') -or -not $layoutSource.Contains('new OnharuSegmentedSwitch')) { throw 'Position mode must finish its segmented transition before locking.' }
if ($layoutSource.Contains('Button.IsMouseOverProperty')) { throw 'Default main buttons must use cursor-only hover feedback.' }
if (-not $mainSource.Contains('monthTitle.Template = ContentOnlyButtonTemplate();')) { throw 'The clickable date title must not use the Windows hover inversion.' }
foreach ($token in @('SettingsGlyph(T("Icon"))', 'button.Foreground = T("Icon")', 'HeaderGlyph(glyph, T("Icon"))', 'StrokeThickness = 1.8', 'Foreground = BrandBrush()', 'monthTitle.Foreground = BrandBrush()', 'UpdateTodayButtonStyle()', 'todayButton.Foreground = Brushes.White')) {
    if (-not (($layoutSource + $mainSource + $themeSource) -like ('*' + $token + '*'))) { throw "Icon or fixed brand-style token is missing: $token" }
}
$sportsSource = Get-Content -Raw (Join-Path $PSScriptRoot 'SportsCalendarWindow.cs')
if (($sportsSource | Select-String -Pattern 'new OnharuSegmentedSwitch' -AllMatches).Matches.Count -lt 3) { throw 'Sports view, range, and size controls must use segmented switches.' }
$segmentSource = Get-Content -Raw (Join-Path $PSScriptRoot 'OnharuSegmentedSwitch.cs')
if (-not $segmentSource.Contains('button.Template = new ControlTemplate') -or -not $segmentSource.Contains('Border.BackgroundProperty, Brushes.Transparent')) { throw 'Segment buttons must not show the Windows hover inversion.' }
foreach ($token in @('LabelWidth(labels[i]) + 4', 'Padding = new Thickness(2, 0, 2, 0)', 'new TemplateBindingExtension(Control.PaddingProperty)')) {
    if (-not $segmentSource.Contains($token)) { throw "Segment width or padding rule is missing: $token" }
}
if (-not $segmentSource.Contains('FontSize = 12.5') -or -not $layoutSource.Contains('new[] { "이동", "고정" }')) { throw 'Segment text visibility or position-mode labels are missing.' }
$layerSource = Get-Content -Raw (Join-Path $PSScriptRoot 'MainWindow.ExplorerLayer.cs')
$placementSource = Get-Content -Raw (Join-Path $PSScriptRoot 'MainWindow.Placement.cs')
if (-not $layerSource.Contains('if (RestoreBlockingDialog()) { UpdateModeButtons(); return; }') -or -not $placementSource.Contains('if (RestoreBlockingDialog()) { UpdateModeButtons(); return; }') -or $layerSource.Contains('if (IsEnabled) return false;')) { throw 'Visible ONHARU dialogs must block and roll back both position-mode transitions.' }
$settingsSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SettingsWindow.cs') -Raw
if ($settingsSource.Contains('static string[][] ThemePresetPalettes') -or $settingsSource.Contains('static string[] ThemePresetNames')) { throw 'Preset policy must not return to SettingsWindow.' }
foreach ($presetToken in @('오션블루','핫핑크','라임펄스','바이올렛','앰버선셋','#DC2626')) {
    if (-not $presetSource.Contains($presetToken)) { throw "Preset module is missing: $presetToken" }
}
foreach ($token in @('OnharuColorPresets.Names', 'OnharuColorPresets.Palettes()', '"내 설정으로 저장"', 'ColorEditor("야구"', 'ColorEditor("D-Day"', 'ColorEditor("기념일"', 'ColorEditor("국경일"', 'Tuple.Create("파스텔", "classic"')) {
    if (-not $settingsSource.Contains($token)) { throw "Theme preset or local color editor is missing: $token" }
}
foreach ($token in @('ActionAccentColor()', 'opacitySlider.Foreground = ActionAccentBrush()', 'calendarRangeSwitch.SetAccent(ActionAccentBrush(), Brushes.White)', 'OnharuStateColors.DetailTab(settings.ThemeId, entry.Item2, ActionAccentColor())')) {
    if (-not (($themeSource + $layoutSource + $detailSource).Contains($token))) { throw "Preset representative action color is missing: $token" }
}
if (-not $themeSource.Contains('return OnharuColorPresets.RepresentativeColor(settings.SelectedPaletteIndex);')) { throw 'Action accent must use the fixed preset representative color.' }
foreach ($token in @('refreshPastelThemeCard', 'CategoryColorSystem.Background("classic", accent)', 'PaletteEditorBackground(c)', 'PaletteEditorForeground(c)')) {
    if (-not $settingsSource.Contains($token)) { throw "Pastel settings preview rule is missing: $token" }
}
foreach ($token in @('specialColorGrid.Children.Add(holidayEditor)', 'PaletteEditorBackground(c)', 'PaletteEditorForeground(c)', 'CategoryColorSystem.Foreground(ThemeId, color)')) {
    if (-not $settingsSource.Contains($token)) { throw "Theme-aware editable holiday palette token is missing: $token" }
}
if (-not $settingsSource.Contains('const int presetCount = 5, customPaletteIndex = 5, googlePaletteIndex = 6;') -or -not $settingsSource.Contains('selectedPaletteIndexValue == 8 ? customPaletteIndex')) { throw 'Five-preset layout or legacy palette selection mapping is missing.' }
if (-not $settingsSource.Contains('GooglePresetVariant(palettes[index][googleIndex % 6], googleIndex)')) { throw 'Default Google colors must not duplicate the six local preset colors.' }
foreach ($token in @('UniquePresetColor(candidate, googleIndex, usedColors)', 'applyPalette(selectedPaletteIndex);')) {
    if (-not $settingsSource.Contains($token)) { throw "Selected presets must initialize and keep every rendered calendar color distinct: $token" }
}
$settingsType = $assembly.GetType('FamilyPlanner.SettingsWindow', $true)
$presetType = $assembly.GetType('FamilyPlanner.OnharuColorPresets', $true)
$presetNamesField = $presetType.GetField('Names', [Reflection.BindingFlags]'Public,Static')
$presetColorsMethod = $presetType.GetMethod('Palettes', [Reflection.BindingFlags]'Public,Static')
$presetRows = @($presetColorsMethod.Invoke($null, @()))
if (@($presetRows | Where-Object { $_[2] -ne '#38A169' }).Count) { throw 'Baseball must keep the field-green preset color.' }
if (@($presetRows | Where-Object { $_[5] -ne '#DC2626' }).Count) { throw 'Holiday must keep the clear red preset color.' }
if (@($presetRows | ForEach-Object { $_[3] } | Select-Object -Unique).Count -ne 5) { throw 'D-Day must vary across all five presets.' }
if (@($presetRows | ForEach-Object { $_[4] } | Select-Object -Unique).Count -ne 5) { throw 'Anniversary must vary across all five presets.' }
if (-not $settingsSource.Contains('Text = label, FontSize = 12')) { throw 'Preset option labels must use the standard settings font size.' }
foreach ($layoutToken in @('for (var i = 0; i < 13; i++) presets.ColumnDefinitions.Add', 'i % 2 == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star)', 'Grid.SetColumn(option, index * 2)', 'Margin = new Thickness(0, 5, 0, 5), Padding = new Thickness(0)')) {
    if (-not $settingsSource.Contains($layoutToken)) { throw "Preset radio groups must use content-width sets with equal flexible gaps: $layoutToken" }
}
foreach ($id in @('classic','dark')) {
    $presetNames = @($presetNamesField.GetValue($null))
    $presetColors = @($presetColorsMethod.Invoke($null, @()))
    if ($presetNames.Count -ne 5 -or @($presetNames | Where-Object { $_.Length -gt 5 -or $_ -match '\s' }).Count) { throw "Preset names must be five compact labels: $id" }
    foreach ($colors in $presetColors) {
        if ($colors.Count -lt 12 -or @($colors | Select-Object -Unique).Count -ne $colors.Count) { throw "Every preset must provide distinct local and Google colors: $id" }
        if ($colors[2] -ne '#38A169' -or $colors[5] -ne '#DC2626') { throw "Baseball and holiday base hues must remain stable across presets: $id" }
    }
}
if ($settingsSource.Contains('paletteOptions[customPaletteIndex].IsChecked = true;')) { throw 'RGB edits or My Settings save must not silently change the selected preset.' }
foreach ($token in @('Content = "날짜 원형"', 'Content = "색상 + 날짜 원형"', 'Tag = "icon"', 'Tag = "fill_icon"')) {
    if (-not $settingsSource.Contains($token)) { throw "Today display option is missing: $token" }
}
if ($settingsSource.Contains('GroupName = "TodayStyle", IsChecked = todayStyle == "border"') -or $settingsSource.Contains('GroupName = "TodayStyle", IsChecked = todayStyle == "both"')) { throw 'Legacy today-border choices must not be shown.' }
foreach ($token in @('CategoryColorSystem.Foreground(settings.ThemeId, itemColor)', 'CategoryColorSystem.Background(settings.ThemeId, itemColor)')) {
    if (-not $themeSource.Contains($token)) { throw "Shared category color system is missing from the calendar: $token" }
}
foreach ($stateHex in @('#6366F1','#4F46E5','#BE185D','#F472B6','#F1F5F9','#94A3B8')) {
    if ($detailSource.Contains($stateHex)) { throw "State color policy must not return to MainWindow.Detail: $stateHex" }
    if (-not $stateColorSource.Contains($stateHex)) { throw "State color module is missing: $stateHex" }
}
foreach ($token in @('box.Template = ColorCheckBoxTemplate()', 'settings.ThemeId == "dark" ? T("Text")', 'StyleThemeCheckBox(check, ItemColor(item))', 'CategoryColorSystem.SelectionBackground(settings.ThemeId, settings.SelectedDateFillColor)', 'OnharuStateColors.ImportantDay', 'OnharuStateColors.DetailTab')) {
    if (-not (($themeSource + $calendarSource + $detailSource).Contains($token))) { throw "Dark interaction contrast token is missing: $token" }
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
$anniversarySource = Get-Content -Raw (Join-Path $PSScriptRoot 'MainWindow.Anniversary.cs')
foreach ($token in @('CategoryColorSystem.DetailBackground(settings.ThemeId, groupColor)', 'CategoryColorSystem.DetailForeground(settings.ThemeId, groupColor)', 'Colors["D-Day"]', 'Colors["기념일"]')) {
    if (-not (($detailSource + $anniversarySource).Contains($token))) { throw "Right detail card palette token is missing: $token" }
}
$settings = [Activator]::CreateInstance($assembly.GetType('FamilyPlanner.PlannerSettings', $true))
if ($settings.Version -ne 41 -or $settings.ThemeId -ne 'classic' -or -not $settings.ImportantFirst -or $settings.LockPalettePlacement) { throw 'Theme settings defaults are invalid.' }
$themeDefinitionSource = Get-Content -Raw (Join-Path $PSScriptRoot 'OnharuTheme.cs')
if ($themeDefinitionSource.Contains('colorful') -or $settingsSource.Contains('"컬러"')) { throw 'Removed colorful skin code must not remain.' }
foreach ($token in @('ShowThemeQuickSwitch', '상단 스킨 전환 버튼 표시', '선택 프리셋 변경', '색상 설정 초기화')) {
    if (($layoutSource + $settingsSource + $themeSource).Contains($token)) { throw "Removed theme/color control remains: $token" }
}
foreach ($token in @('panel.Children.Insert(0, SectionCard(themeGroup))', 'panel.Children.Add(SectionCard(paletteGroup))', 'paletteGroup.Children.Add(presets)', 'paletteGroup.Children.Add(paletteSaveRow)', 'names[selectedPaletteIndex] + " 변경"', '내 설정으로 저장', 'selectedKeys.Count < 1', 'swapPair.Count == 2', 'colorSelectionOrder', '추천색 초기화', 'SelectionName(swapPair[0]) + " ⇄ " + SelectionName(swapPair[1])')) {
    if (-not $settingsSource.Contains($token)) { throw "Theme settings layout token is missing: $token" }
}
foreach ($token in @('현재 색상 배치 고정', 'RandomizeRecommendedPalettePlacement()', 'settings.SelectedPaletteIndex >= 5', 'primary.Color = representative', 'settings.DdayColor = next()', 'settings.AnniversaryColor = next()')) {
    if (-not (($settingsSource + $themeSource + (Get-Content -Raw (Join-Path $PSScriptRoot 'MainWindow.Startup.cs'))).Contains($token))) { throw "Palette placement token is missing: $token" }
}
foreach ($token in @('lowerActions.Children.Add(themeQuickSwitch)', 'UpdateThemeQuickSwitchStyle()', 'CategoryColorSystem.Background("classic", ActionAccentColor())', 'refreshPaletteChangeButton')) {
    if (-not (($layoutSource + $settingsSource + $themeSource).Contains($token))) { throw "Always-visible theme switch or preset button style is missing: $token" }
}
Write-Host 'ONHARU 2.2 theme palette checks passed.'
