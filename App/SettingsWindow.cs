using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace FamilyPlanner
{
    public enum SettingsDataAction { None, ImportDormantLocal, RestoreBackup, ImportFile, ExportFile, ExportEmail, DeleteLocalData }

    public class SettingsWindow : Window
    {
        readonly Dictionary<string, Slider[]> sliders = new Dictionary<string, Slider[]>();
        readonly Dictionary<string, Border> previews = new Dictionary<string, Border>();
        readonly Dictionary<string, Border> editorCards = new Dictionary<string, Border>();
        readonly Dictionary<string, TextBlock> editorTitles = new Dictionary<string, TextBlock>();
        readonly Dictionary<string, TextBlock[]> editorChannels = new Dictionary<string, TextBlock[]>();
        readonly Dictionary<string, TextBlock[]> values = new Dictionary<string, TextBlock[]>();
        readonly List<CheckBox> colorSelections = new List<CheckBox>();
        readonly List<CheckBox> colorSelectionOrder = new List<CheckBox>();
        Button colorSwapButton;
        Button colorSaveMyButton;
        readonly List<StackPanel> rgbPanels = new List<StackPanel>();
        public string BusinessColor;
        public string PersonalColor;
        public string BaseballColor;
        public string DdayColor;
        public string AnniversaryColor;
        public string HolidayColor;
        public double SelectedFontSize;
        public string OrderMode;
        public bool ImportantFirst;
        public bool MultiDayFirst;
        public bool CompletedLast;
        public string CompletedDisplayMode;
        public string StartViewMode;
        public bool RemindersEnabled;
        public bool ReminderSound;
        public int QuietStartHour;
        public int QuietEndHour;
        public string ReminderPosition;
        public string StartupPositionMode;
        public bool Use24HourTime;
        public string CategoryOrderPreset;
        public List<string> CategoryOrder;
        public bool ShowWeekNumbers;
        public bool ShowLunar;
        public bool ShowSolarTerms;
        public string BackupFolder;
        public string WeekRule;
        public string WeekStartDay;
        public List<int> RestDays;
        public string SelectedDateStyle;
        public string SelectedDateFillColor;
        public string SelectedDateBorderColor;
        public string TodayColor;
        public string TodayStyle;
        public string TodayIconColor;
        public bool PastelEventStyle;
        public int AutoSyncMinutes;
        public string DefaultCalendarKey;
        public bool DefaultAllDay;
        public int DefaultStartHour;
        public int DefaultStartMinute;
        public int DefaultReminderMinutes;
        public bool ChangeGoogleAccount;
        public bool LogoutGoogleAccount;
        public SettingsDataAction RequestedDataAction;
        public string RequestedDataFormat;
        public event Action PrintRequested;
        public bool UseTimetable;
        public bool UseDiary;
        public bool UseRollover;
        public bool ShowIncompleteTodoButton;
        public bool ShowOverflowPopupWithSidebar;
        public int IncompleteTodoLookbackMonths;
        public bool ShowGoogleTasks;
        public bool AllowDragMove;
        public bool AllowLocalDragMove;
        public bool AllowGoogleDragMove;
        public bool AllowDetailCardDrag;
        public bool AllowSpecialCardDrag;
        public bool UseProBaseball;
        public bool AutomaticUpdateChecks;
        public bool ShowSearchIcon;
        public bool ShowRangeSwitch;
        public bool ShowThemeSwitch;
        public bool ShowPositionSwitch;
        public bool EnableButtonColorTool;
        public bool BusinessCategoryVisible;
        public bool PersonalCategoryVisible;
        public bool BaseballCategoryVisible;
        public bool DdayCategoryVisible;
        public bool AnniversaryCategoryVisible;
        public string ThemeId;
        public List<string> CustomPalette;
        public bool CustomPalettePastelStyle;
        public List<string> PaletteNames;
        public List<string> SavedPalettes;
        public int PaletteSelectionIndex;
        public bool RandomizePaletteOnStartup;
        bool selectedPastelStyle;
        readonly StackPanel fontOptions = new StackPanel { Orientation = Orientation.Horizontal };
        readonly List<Tuple<string, GoogleCalendarSetting>> sourceEditors = new List<Tuple<string, GoogleCalendarSetting>>();
        readonly Dictionary<string, CheckBox> editBoxes = new Dictionary<string, CheckBox>();

        public SettingsWindow(string business, string personal, string baseball, string dday, string anniversary, string holidayColor, double fontSize, string orderMode, bool importantFirst, bool multiDayFirst, bool completedLast, bool use24HourTime, bool showWeeks,
            string weekRule, string weekStartDay, List<int> restDays, bool pastelEventStyle, int autoSyncMinutes, List<GoogleCalendarSetting> sources, bool googleConnected, bool allowDragMove, bool allowLocalDragMove, bool allowGoogleDragMove, bool allowDetailCardDrag, bool allowSpecialCardDrag, int localItemCount, bool showLunar, bool showSolarTerms, string backupFolder, int backupCount, List<string> categoryOrder,
            List<string> customPalette, bool customPalettePastelStyle, List<string> paletteNames, List<string> savedPalettes, int selectedPaletteIndexValue, bool randomizePaletteOnStartup,
            string selectedDateStyle, string selectedDateFillColor, string selectedDateBorderColor, string todayColor, string todayStyle, string todayBorderColor,
            string defaultCalendarKey, bool defaultAllDay, int defaultStartHour, int defaultStartMinute, int defaultReminderMinutes,
            string completedDisplayMode, string startViewMode, bool remindersEnabled, bool reminderSound, int quietStartHour, int quietEndHour, string reminderPosition, string startupPositionMode, bool useTimetable, bool useDiary, bool useRollover, bool showIncompleteTodoButton, bool showOverflowPopupWithSidebar, int incompleteTodoLookbackMonths, bool showGoogleTasks, bool useProBaseball, bool automaticUpdateChecks, string themeId,
            bool showSearchIcon, bool showRangeSwitch, bool showThemeSwitch, bool showPositionSwitch,
            bool holidayColorVisible, bool baseballColorVisible, bool ddayColorVisible, bool anniversaryColorVisible,
            bool businessCategoryVisible, bool personalCategoryVisible, bool baseballCategoryVisible, bool ddayCategoryVisible, bool anniversaryCategoryVisible,
            bool enableButtonColorTool)
        {
            ThemeId = OnharuThemePalette.Normalize(themeId);
            ImportantFirst = importantFirst;
            selectedPastelStyle = pastelEventStyle;
            CustomPalette = customPalette == null ? new List<string>() : customPalette.ToList();
            CustomPalettePastelStyle = customPalettePastelStyle;
            PaletteNames = paletteNames == null ? new List<string>() : paletteNames.ToList();
            SavedPalettes = savedPalettes == null ? new List<string>() : savedPalettes.ToList();
            BackupFolder = backupFolder;
            Title = "온하루 설정"; Width = 620; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(26, 12, 18, 20) };
            var header = new DockPanel { Margin = new Thickness(26, 14, 12, 12) };
            OnharuPopupChrome.StyleHeader(header);
            var close = OnharuPopupChrome.CloseButton(this); close.Margin = new Thickness(0, 4, 6, 4); close.Padding = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var googleActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0) };
            var printButton = new Button { Content = "🖨", Width = 36, Height = 30, Background = Brush("#DCFCE7"),
                Foreground = Brush("#047857"), BorderBrush = Brush("#86EFAC"), BorderThickness = new Thickness(1), FontSize = 16, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, ToolTip = "현재 달력 인쇄 미리보기" };
            Round(printButton, 9);
            var aboutButton = new Button { Content = "ⓘ", Width = 36, Height = 30, Background = Brush("#EDE9FE"),
                Foreground = Brush("#5B21B6"), BorderBrush = Brush("#C4B5FD"), BorderThickness = new Thickness(1), FontSize = 17, FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, ToolTip = "제품 정보" };
            Round(aboutButton, 9);
            aboutButton.Click += delegate { new ProductInfoWindow { Owner = this }.ShowDialog(); };
            googleActions.Children.Add(printButton); googleActions.Children.Add(aboutButton);
            DockPanel.SetDock(googleActions, Dock.Right); header.Children.Add(googleActions);
            header.Children.Add(new TextBlock { Text = "⚙  온하루 설정", FontSize = 21, FontWeight = FontWeights.Bold,
                Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
            var rgbToggle = new Button { Content = "RGB ▼", Width = 58, Height = 29, Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Left, Background = Brush("#F8FAFC"),
                Foreground = Brush("#475569"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(1, 0, 1, 0) };
            Round(rgbToggle, 9);
            var paletteGroup = new StackPanel();
            var paletteHeader = new DockPanel();
            var randomizePalette = new CheckBox { Content = "시작할 때 색상 무작위 배치", IsChecked = false, Visibility = Visibility.Collapsed,
                FontSize = 11.5, Foreground = Brush("#475569"), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            DockPanel.SetDock(randomizePalette, Dock.Right); paletteHeader.Children.Add(randomizePalette);
            paletteHeader.Children.Add(new TextBlock { Text = "추천 색상 조합 · 12색 톤 팔레트", Foreground = Brush("#475569"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center });
            paletteGroup.Children.Add(paletteHeader);
            var saveMyPalette = new Button { Content = "내 설정으로 저장", Height = 32,
                Background = Brush("#EEF2FF"), Foreground = Brush("#4F46E5"), BorderBrush = Brush("#C7D2FE"),
                BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(1, 0, 1, 0), Cursor = Cursors.Hand };
            Round(saveMyPalette, 11); colorSaveMyButton = saveMyPalette; saveMyPalette.IsEnabled = false; saveMyPalette.Opacity = .45;
            var updateSelectedPalette = new Button { Content = "프리셋 변경", Height = 32,
                Background = Brush("#EDE9FE"), Foreground = Brush("#5B21B6"), BorderBrush = Brush("#A78BFA"),
                BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(1, 0, 1, 0), Cursor = Cursors.Hand };
            Round(updateSelectedPalette, 11);
            var resetPalettes = new Button { Content = "추천색 초기화", Height = 32, Background = Brush("#FFF7ED"),
                Foreground = Brush("#C2410C"), BorderBrush = Brush("#FDBA74"), BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(1, 0, 1, 0), Cursor = Cursors.Hand };
            Round(resetPalettes, 11);
            var swap = new Button { Content = "색상 2개 선택", ToolTip = "체크한 두 색상 교환", Height = 32, Background = Brush("#FCE7F3"),
                Foreground = Brush("#BE185D"), BorderBrush = Brush("#FBCFE8"), BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(1, 0, 1, 0), Cursor = Cursors.Hand };
            Round(swap, 11); colorSwapButton = swap; swap.IsEnabled = false; swap.Opacity = .45;
            var paletteSaveRow = new Grid { Margin = new Thickness(-4, 3, 0, 7) };
            paletteSaveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            paletteSaveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            paletteSaveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            paletteSaveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            paletteSaveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paletteSaveRow.Children.Add(rgbToggle); Grid.SetColumn(updateSelectedPalette, 1); paletteSaveRow.Children.Add(updateSelectedPalette);
            Grid.SetColumn(saveMyPalette, 2); paletteSaveRow.Children.Add(saveMyPalette);
            Grid.SetColumn(resetPalettes, 3); paletteSaveRow.Children.Add(resetPalettes);
            Grid.SetColumn(swap, 4); paletteSaveRow.Children.Add(swap);
            var presets = new WrapPanel { Margin = new Thickness(0, 3, 0, 8), Orientation = Orientation.Horizontal };
            var names = OnharuColorPresets.Names.Concat(new[] { "내설정", "Google" }).ToArray();
            var palettes = OnharuColorPresets.Palettes().Concat(new[] {
                new[] { business, personal, baseball, dday, anniversary, holidayColor }, new string[0] }).ToArray();
            const int presetCount = 3, customPaletteIndex = 3, googlePaletteIndex = 4;
            for (var i = 0; i < presetCount && i < SavedPalettes.Count; i++)
            {
                var saved = (SavedPalettes[i] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (saved.Length >= 6) palettes[i] = saved;
            }
            if (SavedPalettes.Count > 8)
            {
                var legacyCustom = (SavedPalettes[8] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (legacyCustom.Length >= 6) palettes[customPaletteIndex] = legacyCustom;
            }
            else if (SavedPalettes.Count > customPaletteIndex)
            {
                var savedCustom = (SavedPalettes[customPaletteIndex] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (savedCustom.Length >= 6) palettes[customPaletteIndex] = savedCustom;
            }
            if (CustomPalette.Count >= 6) palettes[customPaletteIndex] = CustomPalette.ToArray();
            var allSources = sources ?? new List<GoogleCalendarSetting>();
            var savedOrder = categoryOrder ?? new List<string>();
            var activeSources = CategoryOrderPolicy.GoogleSources(
                allSources.Where(x => showGoogleTasks || !GoogleTasks.IsSource(x.Id)), savedOrder).ToList();
            var hiddenTaskSources = allSources.Where(x => GoogleTasks.IsSource(x.Id) && !activeSources.Contains(x)).ToList();
            for (var i = 0; i < activeSources.Count; i++) sourceEditors.Add(Tuple.Create("google_" + i, activeSources[i]));
            // "내설정" is the color arrangement that was active when this
            // window opened. It is a return point after previewing presets,
            // not another palette that the user has to save explicitly.
            var currentSettingColors = new List<string> { business, personal, baseball, dday, anniversary, holidayColor };
            currentSettingColors.AddRange(activeSources.Where(x => !IsHoliday(x)).Select(x => string.IsNullOrWhiteSpace(x.Color) ? "#E9799A" : x.Color));
            palettes[customPaletteIndex] = currentSettingColors.ToArray();
            var orderEntries = new List<Tuple<string, string>> { Tuple.Create("local:business", "업무일정"), Tuple.Create("local:personal", "개인일정"), Tuple.Create("local:baseball", "야구") };
            orderEntries.AddRange(activeSources.Select(x => Tuple.Create("google:" + x.Id, "Google · " + x.Name)));
            orderEntries.Add(Tuple.Create("special:dday", "D-Day")); orderEntries.Add(Tuple.Create("special:anniversary", "기념일"));
            orderEntries = orderEntries.OrderBy(x => { var p = savedOrder.IndexOf(x.Item1); return p < 0 ? 999 : p; }).ThenBy(x => x.Item2).ToList();
            CategoryOrder = orderEntries.Select(x => x.Item1).ToList();
            var paletteOptions = new List<RadioButton>();
            var selectedPaletteIndex = selectedPaletteIndexValue >= 0 && selectedPaletteIndexValue <= customPaletteIndex
                ? selectedPaletteIndexValue : customPaletteIndex;
            Border pastelThemeCard = null;
            Action refreshPastelThemeCard = delegate { };
            Action refreshPaletteChangeButton = delegate { };
            Action<int> applyPalette = null;
            Func<string, string[], UIElement> presetContent = delegate(string label, string[] colors)
            {
                return new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("#334155"), VerticalAlignment = VerticalAlignment.Center };
            };
            var allowPresetApply = false;
            for (var i = 0; i < names.Length; i++)
            {
                var index = i; var option = new RadioButton { Content = presetContent(names[i], index == googlePaletteIndex ? new string[0] : palettes[index]),
                    GroupName = "Palette", Margin = new Thickness(0, 5, 0, 5), Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsChecked = index == selectedPaletteIndex };
                paletteOptions.Add(option);
                option.Checked += delegate
                {
                    selectedPaletteIndex = index;
                    updateSelectedPalette.Content = names[index] + " 변경";
                    updateSelectedPalette.IsEnabled = index != googlePaletteIndex;
                    updateSelectedPalette.Opacity = updateSelectedPalette.IsEnabled ? 1 : .45;
                    if (allowPresetApply && applyPalette != null) applyPalette(index);
                    refreshPastelThemeCard();
                    refreshPaletteChangeButton();
                };
                option.Margin = new Thickness(0, 5, 12, 5);
                if (index <= customPaletteIndex) presets.Children.Add(option);
            }
            paletteGroup.Children.Add(presets);
            // Current category colors are saved directly. The former RGB,
            // preset-overwrite, custom-palette and swap controls are retired.
            updateSelectedPalette.Content = names[selectedPaletteIndex] + " 변경";
            updateSelectedPalette.IsEnabled = selectedPaletteIndex != googlePaletteIndex;
            updateSelectedPalette.Opacity = updateSelectedPalette.IsEnabled ? 1 : .45;
            var rgbExpanded = false;
            rgbToggle.Click += delegate
            {
                rgbExpanded = !rgbExpanded;
                foreach (var rgbPanel in rgbPanels) rgbPanel.Visibility = rgbExpanded ? Visibility.Visible : Visibility.Collapsed;
                rgbToggle.Content = rgbExpanded ? "RGB ▲" : "RGB ▼";
            };
            Func<string, int> savedRank = key => { var rank = savedOrder.IndexOf(key); return rank < 0 ? 999 : rank; };
            var localColorGrid = new UniformGrid { Columns = 3, Margin = new Thickness(-4, 0, -4, 4) };
            var localEditors = new List<Tuple<string, string, string>> { Tuple.Create("업무일정", business, "local:business"), Tuple.Create("개인일정", personal, "local:personal") };
            if (baseballColorVisible) localEditors.Add(Tuple.Create("야구", baseball, "local:baseball"));
            foreach (var local in localEditors.OrderBy(x => savedRank(x.Item3)))
                localColorGrid.Children.Add(ColorEditor(local.Item1, local.Item2));
            paletteGroup.Children.Add(new TextBlock { Text = "온하루", Foreground = Brush("#64748B"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            paletteGroup.Children.Add(localColorGrid);
            var googleColorGrid = new UniformGrid { Columns = 3, Margin = new Thickness(-4, 0, -4, 4) };
            foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2))
                .OrderBy(x => GoogleTasks.IsSource(x.Item2.Id) ? int.MaxValue : savedRank("google:" + x.Item2.Id)))
                googleColorGrid.Children.Add(ColorEditor(editor.Item1, string.IsNullOrWhiteSpace(editor.Item2.Color) ? "#E9799A" : editor.Item2.Color, editor.Item2.Name));
            if (holidayColorVisible) googleColorGrid.Children.Add(ColorEditor("국경일", holidayColor, "휴일", false));
            if (googleColorGrid.Children.Count > 0)
            {
                paletteGroup.Children.Add(new TextBlock { Text = "Google", Foreground = Brush("#64748B"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 1, 0, 4) });
                paletteGroup.Children.Add(googleColorGrid);
            }
            var specialColorGrid = new UniformGrid { Columns = 3, Margin = new Thickness(-4, 0, -4, 4) };
            var specialEditors = new List<Tuple<string, UIElement>>();
            if (ddayColorVisible) specialEditors.Add(Tuple.Create("special:dday", ColorEditor("D-Day", dday)));
            if (anniversaryColorVisible) specialEditors.Add(Tuple.Create("special:anniversary", ColorEditor("기념일", anniversary)));
            foreach (var special in specialEditors.OrderBy(x => savedRank(x.Item1))) specialColorGrid.Children.Add(special.Item2);
            if (specialColorGrid.Children.Count > 0)
            {
                paletteGroup.Children.Add(new TextBlock { Text = "Special Day", Foreground = Brush("#64748B"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 1, 0, 4) });
                paletteGroup.Children.Add(specialColorGrid);
            }
            panel.Children.Add(SectionCard(paletteGroup));
            applyPalette = delegate(int index)
            {
                if (index == googlePaletteIndex)
                {
                    foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                        SetHex(editor.Item1, string.IsNullOrWhiteSpace(editor.Item2.OriginalColor) ? editor.Item2.Color : editor.Item2.OriginalColor);
                    return;
                }
                if (index < 0 || index >= palettes.Length || palettes[index].Length < 6) return;
                selectedPastelStyle = ThemeId == "classic";
                SetHex("업무일정", palettes[index][0]); SetHex("개인일정", palettes[index][1]);
                SetHex("야구", palettes[index][2]); SetHex("D-Day", palettes[index][3]);
                SetHex("기념일", palettes[index][4]);
                SetHex("국경일", index < presetCount ? OnharuColorPresets.HolidayColor(index) : palettes[index][5]);
                var usedColors = new HashSet<string>(palettes[index].Take(6), StringComparer.OrdinalIgnoreCase);
                var colorIndex = 6; var googleIndex = 0;
                foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                {
                    var candidate = colorIndex < palettes[index].Length
                        ? palettes[index][colorIndex++] : GooglePresetVariant(palettes[index][googleIndex % 6], googleIndex);
                    var color = UniquePresetColor(candidate, googleIndex, usedColors);
                    usedColors.Add(color); SetHex(editor.Item1, color); googleIndex++;
                }
            };
            allowPresetApply = true;
            refreshPaletteChangeButton = delegate
            {
                var editable = selectedPaletteIndex != googlePaletteIndex;
                updateSelectedPalette.IsEnabled = editable; updateSelectedPalette.Opacity = editable ? 1 : .45;
                if (!editable)
                {
                    updateSelectedPalette.Background = Brush("#F1F5F9"); updateSelectedPalette.Foreground = Brush("#64748B");
                    updateSelectedPalette.BorderBrush = Brush("#CBD5E1"); return;
                }
                var accent = "#4F46E5";
                updateSelectedPalette.Background = new SolidColorBrush(CategoryColorSystem.Background("classic", accent));
                updateSelectedPalette.Foreground = new SolidColorBrush(CategoryColorSystem.Foreground("classic", accent));
                updateSelectedPalette.BorderBrush = new SolidColorBrush(CategoryColorSystem.EditorBorder("classic", (Color)ColorConverter.ConvertFromString(accent)));
            };
            refreshPaletteChangeButton();
            Func<List<string>> captureColors = delegate
            {
                var colors = new List<string> { HexOr("업무일정", business), HexOr("개인일정", personal),
                    HexOr("야구", baseball), HexOr("D-Day", dday), HexOr("기념일", anniversary), HexOr("국경일", holidayColor) };
                colors.AddRange(sourceEditors.Where(x => !IsHoliday(x.Item2)).Select(x => Hex(x.Item1)));
                return colors;
            };
            var colorKeys = new List<string> { "업무일정", "개인일정", "야구", "D-Day", "기념일", "국경일" };
            colorKeys.AddRange(sourceEditors.Where(x => !IsHoliday(x.Item2)).Select(x => x.Item1));
            foreach (var slider in sliders.Values.SelectMany(x => x))
                slider.ValueChanged += delegate
                {
                    refreshPastelThemeCard();
                    refreshPaletteChangeButton();
                };
            saveMyPalette.Click += delegate
            {
                var currentColors = captureColors();
                var selectedKeys = new HashSet<string>(colorSelections.Where(x => x.IsChecked == true).Select(x => Convert.ToString(x.Tag)));
                if (selectedKeys.Count < 1) return;
                var customColors = palettes[customPaletteIndex].Length >= currentColors.Count
                    ? palettes[customPaletteIndex].Take(currentColors.Count).ToList()
                    : currentColors.ToList();
                while (customColors.Count < currentColors.Count) customColors.Add(currentColors[customColors.Count]);
                for (var i = 0; i < colorKeys.Count && i < currentColors.Count; i++)
                    if (selectedKeys.Contains(colorKeys[i])) customColors[i] = currentColors[i];
                while (SavedPalettes.Count < 9) SavedPalettes.Add("");
                SavedPalettes[customPaletteIndex] = string.Join(",", customColors);
                palettes[customPaletteIndex] = customColors.ToArray();
                paletteOptions[customPaletteIndex].Content = presetContent(names[customPaletteIndex], palettes[customPaletteIndex]);
                CustomPalette = customColors;
                CustomPalettePastelStyle = selectedPastelStyle;
                saveMyPalette.Content = "✓  색상 저장 완료";
                saveMyPalette.Background = Brush("#ECFDF5"); saveMyPalette.Foreground = Brush("#047857");
                saveMyPalette.BorderBrush = Brush("#A7F3D0");
                var saveNotice = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                saveNotice.Tick += delegate
                {
                    saveNotice.Stop(); saveMyPalette.Content = "내 설정으로 저장";
                    saveMyPalette.Background = Brush("#EEF2FF"); saveMyPalette.Foreground = Brush("#4F46E5");
                    saveMyPalette.BorderBrush = Brush("#C7D2FE");
                };
                saveNotice.Start();
            };
            updateSelectedPalette.Click += delegate
            {
                if (selectedPaletteIndex == googlePaletteIndex) return;
                var currentColors = captureColors();
                while (SavedPalettes.Count < 9) SavedPalettes.Add("");
                SavedPalettes[selectedPaletteIndex] = string.Join(",", currentColors);
                palettes[selectedPaletteIndex] = currentColors.ToArray();
                if (selectedPaletteIndex == customPaletteIndex) CustomPalette = currentColors.ToList();
                updateSelectedPalette.Content = "✓  " + names[selectedPaletteIndex] + " 변경 완료";
                var updateNotice = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                updateNotice.Tick += delegate { updateNotice.Stop(); updateSelectedPalette.Content = names[selectedPaletteIndex] + " 변경"; };
                updateNotice.Start();
            };
            resetPalettes.Click += delegate
            {
                var defaults = OnharuColorPresets.Palettes();
                while (SavedPalettes.Count < 9) SavedPalettes.Add("");
                for (var i = 0; i < presetCount; i++)
                {
                    SavedPalettes[i] = "";
                    palettes[i] = defaults[i].ToArray();
                    paletteOptions[i].Content = presetContent(names[i], palettes[i]);
                }
                if (selectedPaletteIndex < presetCount) applyPalette(selectedPaletteIndex);
                resetPalettes.Content = "✓  초기화 완료";
                var resetNotice = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                resetNotice.Tick += delegate { resetNotice.Stop(); resetPalettes.Content = "추천색 초기화"; };
                resetNotice.Start();
            };
            swap.Click += delegate
            {
                var selected = colorSelectionOrder.Where(x => x.IsChecked == true).Take(2).Select(x => x.Tag.ToString()).ToList();
                if (selected.Count < 2) return;
                var first = Hex(selected[0]); SetHex(selected[0], Hex(selected[1])); SetHex(selected[1], first);
                foreach (var check in colorSelections) check.IsChecked = false;
            };
            var fontRow = new Grid { Height = 24 };
            fontRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            fontRow.ColumnDefinitions.Add(new ColumnDefinition());
            fontRow.Children.Add(new TextBlock { Text = "글자 크기", Foreground = Brush("#475569"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center });
            foreach (var option in new[] { new { Name = "작게", Size = 11.0 }, new { Name = "보통", Size = 12.0 }, new { Name = "크게", Size = 14.0 } })
            {
                fontOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Size, GroupName = "FontSize",
                    IsChecked = Math.Abs(fontSize - option.Size) < .5, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            fontOptions.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(fontOptions, 1); fontRow.Children.Add(fontOptions);
            var orderRow = new Grid { Height = 32 };
            orderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            orderRow.ColumnDefinitions.Add(new ColumnDefinition());
            orderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            orderRow.Children.Add(new TextBlock { Text = "일정 표시 순서", Foreground = Brush("#475569"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center });
            var orderOptions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            orderOptions.Children.Add(new RadioButton { Content = "카테고리별", Tag = "category", GroupName = "OrderMode",
                IsChecked = orderMode != "time", Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
            orderOptions.Children.Add(new RadioButton { Content = "전체 시간순", Tag = "time", GroupName = "OrderMode", IsChecked = orderMode == "time",
                VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(orderOptions, 1); orderRow.Children.Add(orderOptions);
            var importantFirstOption = new CheckBox { Content = "중요 일정 우선", IsChecked = importantFirst,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            Grid.SetColumn(importantFirstOption, 2); orderRow.Children.Add(importantFirstOption);
            var themeOptions = new UniformGrid { Columns = 2, VerticalAlignment = VerticalAlignment.Center };
            foreach (var option in new[] { Tuple.Create("파스텔", "classic", "부드럽고 생동감 있는 기본 스킨"), Tuple.Create("블랙", "dark", "어두운 배경과 선명한 포인트") })
            {
                var previewBackground = option.Item2 == "dark" ? "#1A1A1A" : "#F6F0FF";
                var previewBorder = option.Item2 == "dark" ? "#6366F1" : "#B49CCB";
                var previewForeground = option.Item2 == "dark" ? "#FFFFFF" : "#70429B";
                var choice = new RadioButton { Tag = option.Item2, GroupName = "OnharuTheme", IsChecked = ThemeId == option.Item2,
                    Height = 34, VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, option.Item2 == "dark" ? 0 : 10, 0), Cursor = Cursors.Hand };
                var themeCard = new Border { Height = 30, Background = Brush(previewBackground), BorderBrush = Brush(previewBorder),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(9, 0, 9, 0),
                    Child = new TextBlock { Text = option.Item1 + " · " + option.Item3, Foreground = Brush(previewForeground),
                        FontWeight = FontWeights.SemiBold, FontSize = 11, VerticalAlignment = VerticalAlignment.Center } };
                choice.Content = themeCard;
                if (option.Item2 == "classic") pastelThemeCard = themeCard;
                choice.Checked += delegate
                {
                    ThemeId = option.Item2;
                    var refreshedNames = OnharuColorPresets.Names; var refreshedPalettes = OnharuColorPresets.Palettes();
                    for (var i = 0; i < presetCount; i++)
                    {
                        names[i] = refreshedNames[i]; palettes[i] = refreshedPalettes[i]; paletteOptions[i].Content = presetContent(names[i], palettes[i]);
                    }
                    while (SavedPalettes.Count <= customPaletteIndex) SavedPalettes.Add("");
                    for (var i = 0; i < presetCount; i++) SavedPalettes[i] = "";
                    foreach (var editorName in sliders.Keys.ToList()) UpdatePreview(editorName);
                    // Changing skin must preserve the user's current colors.
                    refreshPaletteChangeButton();
                };
                themeOptions.Children.Add(choice);
            }
            refreshPastelThemeCard = delegate
            {
                if (pastelThemeCard == null) return;
                var accent = "#70429B";
                pastelThemeCard.Background = new SolidColorBrush(CategoryColorSystem.Background("classic", accent));
                pastelThemeCard.BorderBrush = new SolidColorBrush(CategoryColorSystem.EditorBorder("classic", (Color)ColorConverter.ConvertFromString(accent)));
                var label = pastelThemeCard.Child as TextBlock;
                if (label != null) label.Foreground = new SolidColorBrush(CategoryColorSystem.Foreground("classic", accent));
            };
            refreshPastelThemeCard();
            var themeGroup = new Grid { Height = 36 };
            themeGroup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            themeGroup.ColumnDefinitions.Add(new ColumnDefinition());
            themeGroup.Children.Add(new TextBlock { Text = "디자인 스킨", Foreground = Brush("#475569"), FontSize = 12,
                FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(themeOptions, 1); themeGroup.Children.Add(themeOptions); panel.Children.Insert(0, SectionCard(themeGroup));

            var displayHeader = new TextBlock { Text = "메인 달력 표시 옵션", Foreground = Brush("#475569"), FontSize = 12,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 7) };
            var weekRules = new StackPanel { Orientation = Orientation.Horizontal, Visibility = showWeeks ? Visibility.Visible : Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            weekRules.Children.Add(new RadioButton { Content = "ISO 방식 (월요일이 첫째 주)", Tag = "iso", GroupName = "WeekRule",
                IsChecked = weekRule != "jan1", Margin = new Thickness(0, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center });
            weekRules.Children.Add(new RadioButton { Content = "일반 (일요일이 첫째 주)", Tag = "jan1", GroupName = "WeekRule",
                IsChecked = weekRule == "jan1", VerticalAlignment = VerticalAlignment.Center });
            var multiDayTop = new CheckBox { Content = "연속 일정은 항상 위에 표시", IsChecked = multiDayFirst,
                Margin = new Thickness(0, 0, 18, 5), ToolTip = "체크하지 않으면 카테고리 또는 시간 설정 순서를 따릅니다." };
            var completedLastOption = new CheckBox { Content = "완료 Todo는 아래로 이동", IsChecked = completedLast, Margin = new Thickness(0, 0, 30, 5) };
            var use24Hour = new CheckBox { Content = "24시간제로 시간 표시", IsChecked = use24HourTime, Margin = new Thickness(0, 0, 0, 5) };
            var showWeek = new CheckBox { Content = "주차 (Week) 표시", IsChecked = showWeeks, Margin = new Thickness(0, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center };
            var lunar = new CheckBox { Content = "음력 표시", IsChecked = showLunar, Margin = new Thickness(0, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var solarTerms = new CheckBox { Content = "24절기 표시", IsChecked = showSolarTerms, Margin = new Thickness(0, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center };
            var timetable = new CheckBox { Content = "시간표", IsChecked = useTimetable, Margin = new Thickness(0, 0, 22, 5),
                VerticalAlignment = VerticalAlignment.Center, ToolTip = "상단에 시간표 버튼을 표시합니다." };
            var diary = new CheckBox { Content = "일기장", IsChecked = useDiary, Margin = new Thickness(0, 0, 22, 5),
                VerticalAlignment = VerticalAlignment.Center, ToolTip = "일기장 아이콘, 작성 표시와 날짜·음력 더블클릭 일기 쓰기를 함께 켜거나 끕니다." };
            var proBaseball = new CheckBox { Content = "프로야구 일정", IsChecked = useProBaseball, Margin = new Thickness(0, 0, 22, 5),
                VerticalAlignment = VerticalAlignment.Center, ToolTip = "상단에 프로야구 일정 버튼을 표시합니다." };
            proBaseball.Checked += delegate
            {
                if (SportsApiKeyStore.HasKey) return;
                new SportsApiSetupWindow { Owner = this }.ShowDialog();
                if (!SportsApiKeyStore.HasKey) proBaseball.IsChecked = false;
            };
            var rollover = new CheckBox { Content = "미완료 Todo 자동 이월", IsChecked = useRollover, Margin = new Thickness(0, 0, 22, 5),
                VerticalAlignment = VerticalAlignment.Center, ToolTip = "일정 등록·수정 화면에 이월 옵션을 표시합니다." };
            var googleTasks = new CheckBox { Content = "Google Tasks", IsChecked = showGoogleTasks,
                Margin = new Thickness(0, 0, 22, 5), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Task의 제목과 날짜는 읽기 전용이며 완료 여부만 Google과 동기화합니다." };
            googleTasks.Checked += delegate
            {
                var warning = new GoogleTasksWarningWindow { Owner = this };
                if (warning.ShowDialog() != true) googleTasks.IsChecked = false;
            };
            var incompleteTodoCard = new CheckBox { Content = "미완료 일정 버튼 표시", IsChecked = showIncompleteTodoButton,
                Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "세부 달력에서 온하루·Google 일정·Google Tasks의 미완료 할 일을 모아 보는 버튼을 표시합니다." };
            var incompleteTodoRange = new ComboBox { Width = 128, Height = 26, Background = Brushes.White,
                BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            foreach (var option in new[] { Tuple.Create("최근 1개월", 1), Tuple.Create("최근 3개월", 3), Tuple.Create("최근 6개월", 6), Tuple.Create("최근 12개월", 12) })
                incompleteTodoRange.Items.Add(new ComboBoxItem { Content = option.Item1 + " 이후", Tag = option.Item2 });
            incompleteTodoRange.SelectedItem = incompleteTodoRange.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(x => (int)x.Tag == Math.Max(1, incompleteTodoLookbackMonths)) ?? incompleteTodoRange.Items[0];
            StyleComboBox(incompleteTodoRange);
            var incompleteTodoRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 2),
                VerticalAlignment = VerticalAlignment.Center };
            incompleteTodoRow.Children.Add(incompleteTodoCard); incompleteTodoRow.Children.Add(incompleteTodoRange);
            var overflowPopupOption = new CheckBox { Content = "세부 달력이 열려 있어도 더보기 팝업 표시",
                IsChecked = showOverflowPopupWithSidebar, Margin = new Thickness(0, 5, 0, 2),
                ToolTip = "체크하지 않으면 세부 달력이 열려 있을 때 더보기는 해당 날짜의 상세 일정만 표시합니다." };
            var googleDragMove = new CheckBox { Content = "드래그로 Google 일정 날짜 변경", IsChecked = allowGoogleDragMove,
                Margin = new Thickness(0, 4, 0, 1), Foreground = Brush("#475569"), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "수정 가능한 Google 일정을 달력의 다른 날짜로 옮기고 Google에 반영합니다." };
            var startDay = new StackPanel { Orientation = Orientation.Horizontal, Height = 26 };
            startDay.Children.Add(new TextBlock { Text = "시작 요일", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            foreach (var pair in new[] { Tuple.Create("월", "monday"), Tuple.Create("화", "tuesday"), Tuple.Create("수", "wednesday"),
                Tuple.Create("목", "thursday"), Tuple.Create("금", "friday"), Tuple.Create("토", "saturday"), Tuple.Create("일", "sunday") })
                startDay.Children.Add(new RadioButton { Content = pair.Item1, Tag = pair.Item2, GroupName = "WeekStartDay",
                    IsChecked = weekStartDay == pair.Item2, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = pair.Item1 + "요일부터 달력 시작" });
            var restDayRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 26 };
            restDayRow.Children.Add(new TextBlock { Text = "쉬는 날", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            var restDayBoxes = new List<CheckBox>();
            var savedRestDays = restDays == null || restDays.Count == 0 ? new List<int> { 0, 6 } : restDays;
            foreach (var pair in new[] { Tuple.Create("월", 1), Tuple.Create("화", 2), Tuple.Create("수", 3), Tuple.Create("목", 4), Tuple.Create("금", 5), Tuple.Create("토", 6), Tuple.Create("일", 0) })
            {
                var restBox = new CheckBox { Content = pair.Item1, Tag = pair.Item2, IsChecked = savedRestDays.Contains(pair.Item2),
                    Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = pair.Item1 + "요일을 쉬는 날로 표시" };
                restDayBoxes.Add(restBox); restDayRow.Children.Add(restBox);
            }
            var completedDisplay = new StackPanel { Orientation = Orientation.Horizontal, Height = 24 };
            completedDisplay.Children.Add(new TextBlock { Text = "완료 일정", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            foreach (var option in new[] { Tuple.Create("그대로", "normal"), Tuple.Create("흐리게", "fade"), Tuple.Create("숨김", "hide") })
                completedDisplay.Children.Add(new RadioButton { Content = option.Item1, Tag = option.Item2, GroupName = "CompletedDisplay",
                    IsChecked = completedDisplayMode == option.Item2, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center });
            var startView = new StackPanel { Orientation = Orientation.Horizontal, Height = 24 };
            startView.Children.Add(new TextBlock { Text = "시작 화면", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            foreach (var option in new[] { Tuple.Create("오늘", "today"), Tuple.Create("마지막으로 본 날짜", "last") })
                startView.Children.Add(new RadioButton { Content = option.Item1, Tag = option.Item2, GroupName = "StartView",
                    IsChecked = startViewMode == option.Item2, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center });
            var startupPosition = new StackPanel { Orientation = Orientation.Horizontal, Height = 24 };
            startupPosition.Children.Add(new TextBlock { Text = "시작 위치 상태", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            foreach (var option in new[] { Tuple.Create("이전 상태", "remember"), Tuple.Create("항상 고정", "locked"), Tuple.Create("항상 위치 조정", "editable") })
                startupPosition.Children.Add(new RadioButton { Content = option.Item1, Tag = option.Item2, GroupName = "StartupPosition",
                    IsChecked = startupPositionMode == option.Item2, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center });
            var calendarOptions = new StackPanel { Margin = new Thickness(0, 1, 0, 0) };
            var weekRuleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            showWeek.Margin = new Thickness(0, 0, 20, 0); weekRuleRow.Children.Add(showWeek); weekRuleRow.Children.Add(weekRules);
            calendarOptions.Children.Add(weekRuleRow);
            var otherDisplayOptions = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            lunar.Margin = new Thickness(0, 0, 24, 5); solarTerms.Margin = new Thickness(0, 0, 24, 5);
            otherDisplayOptions.Children.Add(lunar); otherDisplayOptions.Children.Add(solarTerms);
            otherDisplayOptions.Children.Add(multiDayTop); otherDisplayOptions.Children.Add(completedLastOption); otherDisplayOptions.Children.Add(use24Hour);
            var featureIconOptions = new StackPanel { Margin = new Thickness(0) };
            var featureIconRow = new WrapPanel { Margin = new Thickness(0) };
            var headerSwitchRow = new WrapPanel { Margin = new Thickness(0) };
            var searchIcon = new CheckBox { Content = "검색", IsChecked = showSearchIcon, Margin = new Thickness(0, 0, 22, 5), VerticalAlignment = VerticalAlignment.Center };
            var rangeSwitch = new CheckBox { Content = "달력 표시 기간 전환", IsChecked = showRangeSwitch, Margin = new Thickness(0, 0, 22, 5), VerticalAlignment = VerticalAlignment.Center };
            var themeSwitch = new CheckBox { Content = "스킨 전환", IsChecked = showThemeSwitch, Margin = new Thickness(0, 0, 22, 5), VerticalAlignment = VerticalAlignment.Center };
            var positionSwitch = new CheckBox { Content = "이동·고정 전환", IsChecked = showPositionSwitch, Margin = new Thickness(0, 0, 22, 5), VerticalAlignment = VerticalAlignment.Center };
            featureIconRow.Children.Add(searchIcon); featureIconRow.Children.Add(timetable); featureIconRow.Children.Add(diary); featureIconRow.Children.Add(proBaseball);
            headerSwitchRow.Children.Add(rangeSwitch); headerSwitchRow.Children.Add(themeSwitch); headerSwitchRow.Children.Add(positionSwitch);
            featureIconOptions.Children.Add(featureIconRow); featureIconOptions.Children.Add(headerSwitchRow);
            var selectionOptions = new StackPanel { Orientation = Orientation.Horizontal, Height = 24 };
            selectedDateFillColor = string.IsNullOrWhiteSpace(selectedDateFillColor) ? "#CCDBEAFE" : selectedDateFillColor;
            selectedDateBorderColor = string.IsNullOrWhiteSpace(selectedDateBorderColor) ? "#3B82F6" : selectedDateBorderColor;
            selectionOptions.Children.Add(new TextBlock { Text = "선택일 표시", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            var fillStyleOption = new RadioButton { Content = "색상", Tag = "fill", GroupName = "SelectedDateStyle",
                IsChecked = selectedDateStyle == "fill", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            var noneStyleOption = new RadioButton { Content = "", Tag = "none", GroupName = "SelectedDateStyle",
                IsChecked = selectedDateStyle == "none", Visibility = Visibility.Collapsed };
            selectionOptions.Children.Add(fillStyleOption); selectionOptions.Children.Add(noneStyleOption);
            var fillColorButton = new Button { Width = 30, Height = 14, Background = selectedDateStyle == "none" ? Brushes.White : Brush(selectedDateFillColor), Content = selectedDateStyle == "none" ? "×" : "", Foreground = Brush("#DC2626"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 18, 0), Cursor = Cursors.Hand,
                ToolTip = "선택 배경 색상", VerticalAlignment = VerticalAlignment.Center };
            Round(fillColorButton, 5);
            fillColorButton.Click += delegate
            {
                var colors = new[] { "#CCDBEAFE", "#CCECFEFF", "#CCD1FAE5", "#CCEDE9FE", "#CCFCE7F3", "#CCFEF3C7", "#CCE2E8F0" };
                var swatches = new StackPanel { Orientation = Orientation.Horizontal };
                var popup = new Popup { PlacementTarget = fillColorButton, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
                foreach (var hex in colors)
                {
                    var color = hex;
                    var swatch = new Button { Width = 28, Height = 28, Margin = new Thickness(3), Background = Brush(color),
                        BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                    Round(swatch, 10);
                    swatch.Click += delegate { selectedDateFillColor = color; fillColorButton.Background = Brush(color); fillColorButton.Content = ""; fillStyleOption.IsChecked = true; popup.IsOpen = false; };
                    swatches.Children.Add(swatch);
                }
                var clear = new Button { Content = "×", Width = 28, Height = 28, Margin = new Thickness(3), Background = Brushes.White,
                    Foreground = Brush("#DC2626"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                    ToolTip = "선택일 강조 표시 안 함" };
                Round(clear, 10); clear.Click += delegate { noneStyleOption.IsChecked = true; fillColorButton.Background = Brushes.White; fillColorButton.Content = "×"; popup.IsOpen = false; }; swatches.Children.Add(clear);
                popup.Child = new Border { Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(11), Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 0), Child = swatches };
                popup.IsOpen = true;
            };
            selectionOptions.Children.Add(fillColorButton);
            var borderStyleOption = new RadioButton { Content = "테두리", Tag = "border", GroupName = "SelectedDateStyle",
                IsChecked = selectedDateStyle == "border", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            selectionOptions.Children.Add(borderStyleOption);
            var borderColorButton = new Button { Width = 30, Height = 14, Background = selectedDateStyle == "none" ? Brushes.White : Brush(selectedDateBorderColor), Content = selectedDateStyle == "none" ? "×" : "", Foreground = Brush("#DC2626"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ToolTip = "선택 테두리 색상", VerticalAlignment = VerticalAlignment.Center };
            Round(borderColorButton, 5);
            borderColorButton.Click += delegate
            {
                var colors = new[] { "#3B82F6", "#06B6D4", "#10B981", "#8B5CF6", "#EC4899", "#F59E0B", "#64748B" };
                var swatches = new StackPanel { Orientation = Orientation.Horizontal };
                var popup = new Popup { PlacementTarget = borderColorButton, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
                foreach (var hex in colors)
                {
                    var color = hex;
                    var swatch = new Button { Width = 28, Height = 28, Margin = new Thickness(3), Background = Brush(color),
                        BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                    Round(swatch, 10);
                    swatch.Click += delegate { selectedDateBorderColor = color; borderColorButton.Background = Brush(color); borderColorButton.Content = ""; borderStyleOption.IsChecked = true; popup.IsOpen = false; };
                    swatches.Children.Add(swatch);
                }
                var clear = new Button { Content = "×", Width = 28, Height = 28, Margin = new Thickness(3), Background = Brushes.White,
                    Foreground = Brush("#DC2626"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                    ToolTip = "선택일 강조 표시 안 함" };
                Round(clear, 10); clear.Click += delegate { noneStyleOption.IsChecked = true; fillColorButton.Background = Brushes.White; fillColorButton.Content = "×"; borderColorButton.Background = Brushes.White; borderColorButton.Content = "×"; popup.IsOpen = false; }; swatches.Children.Add(clear);
                popup.Child = new Border { Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(11), Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 0), Child = swatches };
                popup.IsOpen = true;
            };
            selectionOptions.Children.Add(borderColorButton);
            var bothStyleOption = new RadioButton { Content = "색상 + 테두리", Tag = "both", GroupName = "SelectedDateStyle",
                IsChecked = selectedDateStyle == "both", Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "선택한 배경색과 테두리 색상을 함께 표시" };
            selectionOptions.Children.Add(bothStyleOption);
            fillStyleOption.Checked += delegate { fillColorButton.Background = Brush(selectedDateFillColor); fillColorButton.Content = ""; };
            borderStyleOption.Checked += delegate { borderColorButton.Background = Brush(selectedDateBorderColor); borderColorButton.Content = ""; };
            bothStyleOption.Checked += delegate { fillColorButton.Background = Brush(selectedDateFillColor); fillColorButton.Content = ""; borderColorButton.Background = Brush(selectedDateBorderColor); borderColorButton.Content = ""; };
            todayColor = string.IsNullOrWhiteSpace(todayColor) ? "#CCFCE7F3" : todayColor;
            todayStyle = string.IsNullOrWhiteSpace(todayStyle) ? (todayColor == "none" ? "none" : "fill") : todayStyle;
            if (todayStyle == "border") todayStyle = "icon";
            if (todayStyle == "both") todayStyle = "fill_icon";
            var todayIconColor = string.IsNullOrWhiteSpace(todayBorderColor) ? "#4F7BFF" : todayBorderColor;
            var todayOptions = new StackPanel { Orientation = Orientation.Horizontal, Height = 24 };
            todayOptions.Children.Add(new TextBlock { Text = "오늘 표시", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            var todayNone = new RadioButton { Tag = "none", GroupName = "TodayStyle", IsChecked = todayStyle == "none", Visibility = Visibility.Collapsed };
            var todayFill = new RadioButton { Content = "색상", Tag = "fill", GroupName = "TodayStyle", IsChecked = todayStyle == "fill", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            var todayIcon = new RadioButton { Content = "날짜 원형", Tag = "icon", GroupName = "TodayStyle", IsChecked = todayStyle == "icon", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            var todayBoth = new RadioButton { Content = "색상 + 날짜 원형", Tag = "fill_icon", GroupName = "TodayStyle", IsChecked = todayStyle == "fill_icon", Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            todayOptions.Children.Add(todayNone); todayOptions.Children.Add(todayFill);
            var todayColorButton = new Button { Width = 30, Height = 14, Background = todayStyle == "none" ? Brushes.White : Brush(todayColor),
                Content = todayStyle == "none" ? "×" : "", Foreground = Brush("#DC2626"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 18, 0), Cursor = Cursors.Hand, ToolTip = "오늘 배경색 선택", VerticalAlignment = VerticalAlignment.Center };
            Round(todayColorButton, 6);
            todayColorButton.Click += delegate
            {
                var colors = new[] { "#CCFCE7F3", "#CCDBEAFE", "#CCD1FAE5", "#CCFEF3C7", "#CCEDE9FE", "#CCFFEDD5", "#CCE2E8F0" };
                var swatches = new StackPanel { Orientation = Orientation.Horizontal };
                var popup = new Popup { PlacementTarget = todayColorButton, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
                foreach (var hex in colors)
                {
                    var color = hex;
                    var swatch = new Button { Width = 28, Height = 28, Margin = new Thickness(3), Background = Brush(color), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                    Round(swatch, 10); swatch.Click += delegate { todayColor = color; todayColorButton.Background = Brush(color); todayColorButton.Content = ""; todayFill.IsChecked = true; popup.IsOpen = false; }; swatches.Children.Add(swatch);
                }
                var clear = new Button { Content = "×", Width = 28, Height = 28, Margin = new Thickness(3), Background = Brushes.White,
                    Foreground = Brush("#DC2626"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ToolTip = "오늘 강조 표시 안 함" };
                Round(clear, 10); clear.Click += delegate { todayColor = "none"; todayNone.IsChecked = true; todayColorButton.Background = Brushes.White; todayColorButton.Content = "×"; popup.IsOpen = false; }; swatches.Children.Add(clear);
                popup.Child = new Border { Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 0), Child = swatches };
                popup.IsOpen = true;
            };
            todayOptions.Children.Add(todayColorButton);
            todayOptions.Children.Add(todayIcon);
            var todayIconButton = new Button { Width = 30, Height = 14, Background = todayStyle == "none" ? Brushes.White : Brush(todayIconColor),
                Content = todayStyle == "none" ? "×" : "", Foreground = Brush("#DC2626"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, ToolTip = "오늘 날짜 원형 색상", VerticalAlignment = VerticalAlignment.Center };
            Round(todayIconButton, 6);
            todayIconButton.Click += delegate
            {
                var colors = new[] { "#4F7BFF", "#06B6D4", "#10B981", "#8B5CF6", "#EC4899", "#F59E0B", "#64748B" };
                var swatches = new StackPanel { Orientation = Orientation.Horizontal };
                var popup = new Popup { PlacementTarget = todayIconButton, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
                foreach (var hex in colors)
                {
                    var color = hex; var swatch = new Button { Width = 28, Height = 28, Margin = new Thickness(3), Background = Brush(color), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                    Round(swatch, 10); swatch.Click += delegate { todayIconColor = color; todayIconButton.Background = Brush(color); todayIconButton.Content = ""; todayIcon.IsChecked = true; popup.IsOpen = false; }; swatches.Children.Add(swatch);
                }
                var clear = new Button { Content = "×", Width = 28, Height = 28, Margin = new Thickness(3), Background = Brushes.White, Foreground = Brush("#DC2626"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                Round(clear, 10); clear.Click += delegate { todayColor = "none"; todayNone.IsChecked = true; todayColorButton.Background = Brushes.White; todayColorButton.Content = "×"; todayIconButton.Background = Brushes.White; todayIconButton.Content = "×"; popup.IsOpen = false; }; swatches.Children.Add(clear);
                popup.Child = new Border { Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 0), Child = swatches }; popup.IsOpen = true;
            };
            todayOptions.Children.Add(todayIconButton); todayOptions.Children.Add(todayBoth);
            todayFill.Checked += delegate { if (todayColor == "none") todayColor = "#CCFCE7F3"; todayColorButton.Background = Brush(todayColor); todayColorButton.Content = ""; };
            todayIcon.Checked += delegate { todayIconButton.Background = Brush(todayIconColor); todayIconButton.Content = ""; };
            todayBoth.Checked += delegate { if (todayColor == "none") todayColor = "#CCFCE7F3"; todayColorButton.Background = Brush(todayColor); todayColorButton.Content = ""; todayIconButton.Background = Brush(todayIconColor); todayIconButton.Content = ""; };
            showWeek.Click += delegate { weekRules.Visibility = showWeek.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; };
            var displayGroup = new StackPanel(); displayGroup.Children.Add(displayHeader);
            displayGroup.Children.Add(calendarOptions);
            displayGroup.Children.Add(otherDisplayOptions);
            displayGroup.Children.Add(orderRow);
            var localBusinessCategory = new CheckBox { Content = "업무", IsChecked = businessCategoryVisible, Margin = new Thickness(0, 0, 22, 0) };
            var localPersonalCategory = new CheckBox { Content = "개인", IsChecked = personalCategoryVisible, Margin = new Thickness(0, 0, 22, 0) };
            var localBaseballCategory = new CheckBox { Content = "야구", IsChecked = baseballCategoryVisible };
            var ddayCategory = new CheckBox { Content = "D-Day", IsChecked = ddayCategoryVisible, Margin = new Thickness(0, 0, 22, 0) };
            var anniversaryCategory = new CheckBox { Content = "기념일", IsChecked = anniversaryCategoryVisible };
            displayGroup.Children.Add(new Border { Height = 1, Background = Brush("#E2E8F0"), Margin = new Thickness(0, 4, 0, 7) });
            displayGroup.Children.Add(new TextBlock { Text = "화면과 동작", Foreground = Brush("#64748B"), FontSize = 11,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            displayGroup.Children.Add(startDay); displayGroup.Children.Add(restDayRow); displayGroup.Children.Add(completedDisplay); displayGroup.Children.Add(startView);
            displayGroup.Children.Add(startupPosition); displayGroup.Children.Add(selectionOptions); displayGroup.Children.Add(todayOptions);
            fontRow.Margin = new Thickness(0, 2, 0, 0); displayGroup.Children.Add(fontRow);
            panel.Children.Add(SectionCard(displayGroup));

            var sourceVisibleBoxes = new Dictionary<string, CheckBox>();
            var detailGroup = new StackPanel();
            detailGroup.Children.Add(new TextBlock { Text = "세부 달력 표시 옵션", Foreground = Brush("#475569"), FontSize = 12,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 7) });
            var detailCategories = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            detailCategories.ColumnDefinitions.Add(new ColumnDefinition()); detailCategories.ColumnDefinitions.Add(new ColumnDefinition());
            var localDetail = new StackPanel();
            localDetail.Children.Add(new TextBlock { Text = "온하루", Foreground = Brush("#64748B"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            var localDetailOptions = new WrapPanel();
            localBusinessCategory.Margin = new Thickness(0, 0, 20, 2); localPersonalCategory.Margin = new Thickness(0, 0, 20, 2); localBaseballCategory.Margin = new Thickness(0, 0, 0, 2);
            localDetailOptions.Children.Add(localBusinessCategory); localDetailOptions.Children.Add(localPersonalCategory); localDetailOptions.Children.Add(localBaseballCategory);
            localDetail.Children.Add(localDetailOptions); detailCategories.Children.Add(localDetail);
            var specialDetail = new StackPanel();
            specialDetail.Children.Add(new TextBlock { Text = "Special Day", Foreground = Brush("#64748B"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            var specialDetailOptions = new WrapPanel(); ddayCategory.Margin = new Thickness(0, 0, 20, 2); anniversaryCategory.Margin = new Thickness(0, 0, 0, 2);
            specialDetailOptions.Children.Add(ddayCategory); specialDetailOptions.Children.Add(anniversaryCategory); specialDetail.Children.Add(specialDetailOptions);
            Grid.SetColumn(specialDetail, 1); detailCategories.Children.Add(specialDetail); detailGroup.Children.Add(detailCategories);
            detailGroup.Children.Add(incompleteTodoRow);
            detailGroup.Children.Add(overflowPopupOption);
            if (sourceEditors.Count > 0 || hiddenTaskSources.Count > 0)
            {
                detailGroup.Children.Add(new Border { Height = 1, Background = Brush("#E2E8F0"), Margin = new Thickness(0, 2, 0, 7) });
                detailGroup.Children.Add(new TextBlock { Text = "Google", Foreground = Brush("#64748B"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
                var googleDetailGrid = new UniformGrid { Columns = 2 };
                foreach (var editor in sourceEditors)
                {
                    var source = editor.Item2; var holiday = IsHoliday(source); var taskSource = GoogleTasks.IsSource(source.Id);
                    if (taskSource) continue;
                    var canWrite = (source.AccessRole == "owner" || source.AccessRole == "writer") && !holiday;
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 27, Margin = new Thickness(0, 0, 12, 2) };
                    var visibleBox = new CheckBox { Content = source.Name, IsChecked = source.Visible, VerticalAlignment = VerticalAlignment.Center, ToolTip = source.Name };
                    sourceVisibleBoxes[editor.Item1] = visibleBox; row.Children.Add(visibleBox);
                    if (canWrite)
                    {
                        var editGroup = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                        var editCaption = new TextBlock { Text = "(수정", FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center };
                        var editBox = new CheckBox { IsChecked = source.Editable, Margin = new Thickness(3, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };
                        var editClose = new TextBlock { Text = ")", FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center };
                        Action refreshEditTone = delegate { var tone = editBox.IsChecked == true ? Brush("#111827") : Brush("#94A3B8"); editCaption.Foreground = tone; editClose.Foreground = tone; editBox.Opacity = editBox.IsChecked == true ? 1 : .55; };
                        editBox.Checked += delegate { refreshEditTone(); }; editBox.Unchecked += delegate { refreshEditTone(); }; refreshEditTone();
                        editGroup.Children.Add(editCaption); editGroup.Children.Add(editBox); editGroup.Children.Add(editClose);
                        editBoxes[editor.Item1] = editBox; row.Children.Add(editGroup);
                    }
                    else
                    {
                        var readOnly = new TextBlock { Text = "읽기 전용", Foreground = Brush("#94A3B8"), FontSize = 10, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                        Grid.SetColumn(readOnly, 1); row.Children.Add(readOnly);
                    }
                    googleDetailGrid.Children.Add(row);
                }
                var taskRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 27, Margin = new Thickness(0, 0, 12, 2) };
                googleTasks.Margin = new Thickness(0); taskRow.Children.Add(googleTasks);
                taskRow.Children.Add(new TextBlock { Text = " · 읽기 전용, 완료 체크만 가능", Foreground = Brush("#94A3B8"), FontSize = 10,
                    Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                googleDetailGrid.Children.Add(taskRow);
                detailGroup.Children.Add(googleDetailGrid);
            }
            panel.Children.Add(SectionCard(detailGroup));
            var syncGroup = new StackPanel { Orientation = Orientation.Horizontal, Height = 28 };
            syncGroup.Children.Add(new TextBlock { Text = "Google 자동 동기화", Width = 120, Foreground = Brush("#475569"), FontSize = 12,
                FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var syncOptions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (var option in new[] { new { Name = "사용 안 함", Minutes = 0 }, new { Name = "5분", Minutes = 5 },
                new { Name = "15분", Minutes = 15 }, new { Name = "30분", Minutes = 30 }, new { Name = "60분", Minutes = 60 } })
                syncOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Minutes, GroupName = "AutoSync",
                    IsChecked = autoSyncMinutes == option.Minutes, Margin = new Thickness(0, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center });
            syncGroup.Children.Add(syncOptions);
            panel.Children.Add(SectionCard(syncGroup));
            var defaultsGroup = new StackPanel();
            defaultsGroup.Children.Add(new TextBlock { Text = "입력 화면 · 새 일정", Foreground = Brush("#475569"), FontSize = 12,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            var defaultsRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 30 };
            var defaultCalendar = new ComboBox { Width = 145, Height = 26, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            Action refreshDefaultCalendars = delegate
            {
                var selected = defaultCalendar.SelectedItem as ComboBoxItem;
                var selectedKey = selected == null ? defaultCalendarKey : Convert.ToString(selected.Tag);
                defaultCalendar.Items.Clear();
                if (localBusinessCategory.IsChecked == true) defaultCalendar.Items.Add(new ComboBoxItem { Content = "온하루 · 업무일정", Tag = "local:business" });
                if (localPersonalCategory.IsChecked == true) defaultCalendar.Items.Add(new ComboBoxItem { Content = "온하루 · 개인일정", Tag = "local:personal" });
                if (localBaseballCategory.IsChecked == true) defaultCalendar.Items.Add(new ComboBoxItem { Content = "온하루 · 야구", Tag = "local:baseball" });
                foreach (var source in activeSources.Where(x => !IsHoliday(x) && !GoogleTasks.IsSource(x.Id)))
                {
                    var editor = sourceEditors.FirstOrDefault(x => x.Item2 == source);
                    var editable = editor != null && editBoxes.ContainsKey(editor.Item1) ? editBoxes[editor.Item1].IsChecked == true : source.Editable;
                    if (editable) defaultCalendar.Items.Add(new ComboBoxItem { Content = "Google · " + source.Name, Tag = "google:" + source.Id });
                }
                if (defaultCalendar.Items.Count == 0) defaultCalendar.Items.Add(new ComboBoxItem { Content = "등록 가능한 일정 없음", Tag = "local:business", IsEnabled = false });
                defaultCalendar.SelectedItem = defaultCalendar.Items.OfType<ComboBoxItem>().FirstOrDefault(x => Convert.ToString(x.Tag) == selectedKey && x.IsEnabled)
                    ?? defaultCalendar.Items.OfType<ComboBoxItem>().FirstOrDefault(x => x.IsEnabled) ?? defaultCalendar.Items[0];
            };
            refreshDefaultCalendars();
            localBusinessCategory.Click += delegate { refreshDefaultCalendars(); };
            localPersonalCategory.Click += delegate { refreshDefaultCalendars(); };
            localBaseballCategory.Click += delegate { refreshDefaultCalendars(); };
            StyleComboBox(defaultCalendar); defaultsRow.Children.Add(defaultCalendar);
            var defaultAllDayOption = new RadioButton { Content = "하루종일", GroupName = "DefaultTimeMode", IsChecked = defaultAllDay, Margin = new Thickness(12, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            var defaultTimedOption = new RadioButton { Content = "시간 지정", GroupName = "DefaultTimeMode", IsChecked = !defaultAllDay, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            defaultsRow.Children.Add(defaultAllDayOption); defaultsRow.Children.Add(defaultTimedOption);
            var defaultTime = new ComboBox { Width = 76, Height = 26, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            for (var hour = 0; hour < 24; hour++) for (var minute = 0; minute < 60; minute += 15)
                defaultTime.Items.Add(new ComboBoxItem { Content = string.Format("{0:00}:{1:00}", hour, minute), Tag = hour * 60 + minute });
            defaultTime.SelectedItem = defaultTime.Items.OfType<ComboBoxItem>().OrderBy(x => Math.Abs((int)x.Tag - (defaultStartHour * 60 + defaultStartMinute))).First();
            StyleComboBox(defaultTime); defaultsRow.Children.Add(defaultTime); defaultsGroup.Children.Add(defaultsRow);
            rollover.Margin = new Thickness(0, 3, 0, 2); defaultsGroup.Children.Add(rollover);
            Action updateDefaultTime = delegate { defaultTime.IsEnabled = defaultTimedOption.IsChecked == true; };
            defaultAllDayOption.Checked += delegate { updateDefaultTime(); }; defaultTimedOption.Checked += delegate { updateDefaultTime(); }; updateDefaultTime();
            panel.Children.Add(SectionCard(defaultsGroup));

            var defaultReminderMode = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
            var noDefaultReminder = new RadioButton { Content = "없음", GroupName = "DefaultReminder", IsChecked = defaultReminderMinutes < 0,
                Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
            var customDefaultReminder = new RadioButton { Content = "직접 선택", GroupName = "DefaultReminder", IsChecked = defaultReminderMinutes >= 0,
                Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            var reminderMultiplier = defaultReminderMinutes > 0 && defaultReminderMinutes % 1440 == 0 ? 1440 : defaultReminderMinutes > 0 && defaultReminderMinutes % 60 == 0 ? 60 : 1;
            var defaultReminderValue = new TextBox { Width = 48, Height = 26, Text = Math.Max(1, defaultReminderMinutes / reminderMultiplier).ToString(),
                Padding = new Thickness(5, 0, 5, 0), VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center,
                Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), VerticalAlignment = VerticalAlignment.Center };
            UiRound.StyleTextBox(defaultReminderValue, 9);
            var defaultReminderUnit = new ComboBox { Width = 72, Height = 26, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"),
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
            foreach (var option in new[] { Tuple.Create("분 전", 1), Tuple.Create("시간 전", 60), Tuple.Create("일 전", 1440) })
                defaultReminderUnit.Items.Add(new ComboBoxItem { Content = option.Item1, Tag = option.Item2 });
            defaultReminderUnit.SelectedItem = defaultReminderUnit.Items.OfType<ComboBoxItem>().First(x => (int)x.Tag == reminderMultiplier);
            StyleComboBox(defaultReminderUnit);
            Action updateDefaultReminder = delegate { var enabled = customDefaultReminder.IsChecked == true; defaultReminderValue.IsEnabled = enabled; defaultReminderUnit.IsEnabled = enabled; };
            noDefaultReminder.Checked += delegate { updateDefaultReminder(); }; customDefaultReminder.Checked += delegate { updateDefaultReminder(); };
            defaultReminderMode.Children.Add(noDefaultReminder); defaultReminderMode.Children.Add(customDefaultReminder);
            defaultReminderMode.Children.Add(defaultReminderValue); defaultReminderMode.Children.Add(defaultReminderUnit); updateDefaultReminder();
            var reminderGroup = new StackPanel { Orientation = Orientation.Horizontal, Height = 28 };
            var remindersEnabledOption = new CheckBox { Content = "알림 사용", IsChecked = remindersEnabled, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
            var reminderSoundOption = new CheckBox { Content = "소리 사용", IsChecked = reminderSound, Margin = new Thickness(0, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center };
            reminderGroup.Children.Add(remindersEnabledOption); reminderGroup.Children.Add(reminderSoundOption);
            reminderGroup.Children.Add(new TextBlock { Text = "조용한 시간", Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            var quietStart = new ComboBox { Width = 64, Height = 25, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            var quietEnd = new ComboBox { Width = 64, Height = 25, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            for (var hour = 0; hour < 24; hour++)
            {
                quietStart.Items.Add(new ComboBoxItem { Content = hour.ToString("00") + "시", Tag = hour });
                quietEnd.Items.Add(new ComboBoxItem { Content = hour.ToString("00") + "시", Tag = hour });
            }
            quietStart.SelectedIndex = Math.Max(0, Math.Min(23, quietStartHour)); quietEnd.SelectedIndex = Math.Max(0, Math.Min(23, quietEndHour));
            StyleComboBox(quietStart); StyleComboBox(quietEnd); reminderGroup.Children.Add(quietStart);
            reminderGroup.Children.Add(new TextBlock { Text = "~", Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) });
            reminderGroup.Children.Add(quietEnd);
            var reminderPositionGroup = new StackPanel { Orientation = Orientation.Horizontal, Height = 28 };
            reminderPositionGroup.Children.Add(new TextBlock { Text = "알림 위치", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            foreach (var option in new[] { Tuple.Create("주 모니터 중앙", "screen"), Tuple.Create("온하루 위", "onharu") })
                reminderPositionGroup.Children.Add(new RadioButton { Content = option.Item1, Tag = option.Item2, GroupName = "ReminderPosition",
                    IsChecked = reminderPosition == option.Item2, Margin = new Thickness(0, 0, 24, 0), VerticalAlignment = VerticalAlignment.Center });
            var defaultReminderRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 30, Margin = new Thickness(0, 1, 0, 0) };
            defaultReminderRow.Children.Add(new TextBlock { Text = "새 일정 기본 알림", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            defaultReminderMode.Margin = new Thickness(0); defaultReminderRow.Children.Add(defaultReminderMode);
            var reminderCard = new StackPanel();
            reminderCard.Children.Add(new TextBlock { Text = "알림", Foreground = Brush("#475569"), FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            reminderCard.Children.Add(reminderGroup); reminderCard.Children.Add(defaultReminderRow); reminderCard.Children.Add(reminderPositionGroup);
            panel.Children.Add(SectionCard(reminderCard));
            Action updateQuietHours = delegate
            {
                var enabled = remindersEnabledOption.IsChecked == true;
                reminderSoundOption.IsEnabled = enabled; reminderPositionGroup.IsEnabled = enabled;
                var quietEnabled = enabled && reminderSoundOption.IsChecked == true;
                quietStart.IsEnabled = quietEnabled; quietEnd.IsEnabled = quietEnabled;
                quietStart.Opacity = quietEnabled ? 1 : .45; quietEnd.Opacity = quietEnabled ? 1 : .45;
            };
            remindersEnabledOption.Checked += delegate { updateQuietHours(); };
            remindersEnabledOption.Unchecked += delegate { updateQuietHours(); };
            reminderSoundOption.Checked += delegate { updateQuietHours(); };
            reminderSoundOption.Unchecked += delegate { updateQuietHours(); };
            updateQuietHours();
            var updateOption = new CheckBox { Content = "새 버전 자동 확인 · 설치 전 항상 확인",
                IsChecked = automaticUpdateChecks, Foreground = Brush("#475569"), FontSize = 12,
                Margin = new Thickness(0, 1, 0, 1), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "하루에 한 번 GitHub Release를 확인합니다. 동의 없이 설치하지 않습니다." };
            var buttonColorTool = new CheckBox { Content = "ONHARU 버튼 색상 도구", IsChecked = enableButtonColorTool,
                Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 3, 0, 1),
                ToolTip = "켜면 버튼을 마우스 오른쪽 버튼으로 눌러 배경과 글씨 색상을 직접 조정할 수 있습니다." };
            var dragMaster = new CheckBox { Content = "드래그로 일정 옮기기", IsChecked = allowDragMove, Foreground = Brush("#475569"), FontSize = 12,
                Margin = new Thickness(0, 5, 0, 3), VerticalAlignment = VerticalAlignment.Center };
            var localDrag = new CheckBox { Content = "온하루 일정", IsChecked = allowLocalDragMove, Margin = new Thickness(18, 0, 18, 1) };
            googleDragMove.Content = "Google 일정"; googleDragMove.Margin = new Thickness(0, 0, 18, 1);
            var detailCardDrag = new CheckBox { Content = "세부 일정 카드", IsChecked = allowDetailCardDrag, Margin = new Thickness(0, 0, 18, 1) };
            var specialCardDrag = new CheckBox { Content = "Special Day 카드", IsChecked = allowSpecialCardDrag, Margin = new Thickness(0, 0, 0, 1) };
            var dragChildren = new WrapPanel(); dragChildren.Children.Add(localDrag); dragChildren.Children.Add(googleDragMove); dragChildren.Children.Add(detailCardDrag); dragChildren.Children.Add(specialCardDrag);
            Action updateDragOptions = delegate { dragChildren.IsEnabled = dragMaster.IsChecked == true; dragChildren.Opacity = dragMaster.IsChecked == true ? 1 : .45; };
            dragMaster.Checked += delegate { updateDragOptions(); }; dragMaster.Unchecked += delegate { updateDragOptions(); }; updateDragOptions();
            var generalOptions = new StackPanel(); generalOptions.Children.Add(updateOption); generalOptions.Children.Add(buttonColorTool); generalOptions.Children.Add(dragMaster); generalOptions.Children.Add(dragChildren);
            panel.Children.Add(SectionCard(generalOptions));
            var featureGroup = new StackPanel();
            featureGroup.Children.Add(new TextBlock { Text = "상단 기능 아이콘", Foreground = Brush("#475569"), FontSize = 12,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            featureGroup.Children.Add(featureIconOptions); panel.Children.Add(SectionCard(featureGroup));
            var save = OnharuPopupChrome.ActionButton("✓  설정 저장", double.NaN); save.Height = 44; save.FontSize = 14; save.Margin = new Thickness(0, 10, 0, 0);
            Round(save, 13);
            save.Click += delegate
            {
                BusinessColor = HexOr("업무일정", business); PersonalColor = HexOr("개인일정", personal);
                BaseballColor = HexOr("야구", baseball); DdayColor = HexOr("D-Day", dday);
                AnniversaryColor = HexOr("기념일", anniversary); HolidayColor = HexOr("국경일", holidayColor);
                foreach (var editor in sourceEditors)
                {
                    if (!IsHoliday(editor.Item2)) editor.Item2.Color = Hex(editor.Item1);
                    if (sourceVisibleBoxes.ContainsKey(editor.Item1)) editor.Item2.Visible = sourceVisibleBoxes[editor.Item1].IsChecked == true;
                    editor.Item2.Editable = !GoogleTasks.IsSource(editor.Item2.Id) && editBoxes.ContainsKey(editor.Item1) && editBoxes[editor.Item1].IsChecked == true;
                }
                foreach (var source in hiddenTaskSources) source.Editable = false;
                CategoryOrder = localColorGrid.Children.Cast<FrameworkElement>().Select(x => x.Tag as string == "업무일정" ? "local:business" : x.Tag as string == "야구" ? "local:baseball" : "local:personal")
                    .Concat(googleColorGrid.Children.Cast<FrameworkElement>().Where(x => (string)x.Tag != "국경일")
                        .Select(x => "google:" + sourceEditors.First(y => y.Item1 == (string)x.Tag).Item2.Id))
                    .Concat(activeSources.Where(IsHoliday).Select(x => "google:" + x.Id))
                    .Concat(specialColorGrid.Children.Cast<FrameworkElement>().Select(x => (string)x.Tag == "D-Day" ? "special:dday" : "special:anniversary")).ToList();
                SelectedFontSize = (double)fontOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                OrderMode = orderOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                ImportantFirst = importantFirstOption.IsChecked == true;
                MultiDayFirst = multiDayTop.IsChecked == true;
                CompletedLast = completedLastOption.IsChecked == true;
                CompletedDisplayMode = completedDisplay.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                StartViewMode = startView.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                StartupPositionMode = startupPosition.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                Use24HourTime = use24Hour.IsChecked == true;
                ShowWeekNumbers = showWeek.IsChecked == true;
                ShowLunar = lunar.IsChecked == true;
                ShowSolarTerms = solarTerms.IsChecked == true;
                UseTimetable = timetable.IsChecked == true;
                ShowSearchIcon = searchIcon.IsChecked == true;
                ShowRangeSwitch = rangeSwitch.IsChecked == true;
                ShowThemeSwitch = themeSwitch.IsChecked == true;
                ShowPositionSwitch = positionSwitch.IsChecked == true;
                BusinessCategoryVisible = localBusinessCategory.IsChecked == true;
                PersonalCategoryVisible = localPersonalCategory.IsChecked == true;
                BaseballCategoryVisible = localBaseballCategory.IsChecked == true;
                DdayCategoryVisible = ddayCategory.IsChecked == true;
                AnniversaryCategoryVisible = anniversaryCategory.IsChecked == true;
                UseDiary = diary.IsChecked == true;
                UseRollover = rollover.IsChecked == true;
                ShowIncompleteTodoButton = incompleteTodoCard.IsChecked == true;
                ShowOverflowPopupWithSidebar = overflowPopupOption.IsChecked == true;
                IncompleteTodoLookbackMonths = (int)((ComboBoxItem)incompleteTodoRange.SelectedItem).Tag;
                ShowGoogleTasks = googleTasks.IsChecked == true;
                AllowDragMove = dragMaster.IsChecked == true;
                AllowLocalDragMove = localDrag.IsChecked == true;
                AllowGoogleDragMove = googleDragMove.IsChecked == true;
                AllowDetailCardDrag = detailCardDrag.IsChecked == true;
                AllowSpecialCardDrag = specialCardDrag.IsChecked == true;
                UseProBaseball = proBaseball.IsChecked == true;
                AutomaticUpdateChecks = updateOption.IsChecked == true;
                EnableButtonColorTool = buttonColorTool.IsChecked == true;
                ThemeId = themeOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                PaletteSelectionIndex = selectedPaletteIndex;
                RandomizePaletteOnStartup = false;
                if (selectedPaletteIndex == customPaletteIndex)
                {
                    var customColors = new List<string> { BusinessColor, PersonalColor, BaseballColor, DdayColor, AnniversaryColor, HolidayColor };
                    customColors.AddRange(sourceEditors.Where(x => !IsHoliday(x.Item2)).Select(x => Hex(x.Item1)));
                    CustomPalette = customColors;
                }
                SelectedDateStyle = selectionOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                SelectedDateFillColor = selectedDateFillColor;
                SelectedDateBorderColor = selectedDateBorderColor;
                TodayColor = todayColor;
                TodayStyle = todayOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                TodayIconColor = todayIconColor;
                WeekRule = weekRules.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                WeekStartDay = startDay.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                RestDays = restDayBoxes.Where(x => x.IsChecked == true).Select(x => (int)x.Tag).ToList();
                PastelEventStyle = selectedPastelStyle;
                AutoSyncMinutes = (int)syncOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                DefaultCalendarKey = ((ComboBoxItem)defaultCalendar.SelectedItem).Tag.ToString();
                DefaultAllDay = defaultAllDayOption.IsChecked == true;
                var timeValue = (int)((ComboBoxItem)defaultTime.SelectedItem).Tag;
                DefaultStartHour = timeValue / 60; DefaultStartMinute = timeValue % 60;
                int reminderValue; if (!int.TryParse(defaultReminderValue.Text, out reminderValue)) reminderValue = 10;
                reminderValue = Math.Max(1, Math.Min(999, reminderValue));
                DefaultReminderMinutes = noDefaultReminder.IsChecked == true ? -1 : reminderValue * (int)((ComboBoxItem)defaultReminderUnit.SelectedItem).Tag;
                RemindersEnabled = remindersEnabledOption.IsChecked == true;
                ReminderSound = reminderSoundOption.IsChecked == true;
                QuietStartHour = (int)((ComboBoxItem)quietStart.SelectedItem).Tag; QuietEndHour = (int)((ComboBoxItem)quietEnd.SelectedItem).Tag;
                ReminderPosition = reminderPositionGroup.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                DialogResult = true;
            };
            printButton.Click += delegate { if (PrintRequested != null) PrintRequested(); };
            var dataGroup = new StackPanel();
            dataGroup.Children.Add(new TextBlock { Text = "일정 관리  (JSON·ICS는 로컬 일정 · Excel CSV는 Google 포함 전체 일정)", Foreground = Brush("#475569"), FontSize = 12,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            Func<string, string, UIElement> actionCaption = delegate(string first, string second)
            {
                var caption = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                caption.Children.Add(new TextBlock { Text = first, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11.5, FontWeight = FontWeights.SemiBold });
                caption.Children.Add(new TextBlock { Text = second, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 10.5 });
                return caption;
            };
            var dataActions = new Grid();
            for (var i = 0; i < 5; i++) dataActions.ColumnDefinitions.Add(new ColumnDefinition());
            var restore = new Button { Content = actionCaption("↶  백업 복원", backupCount > 0 ? backupCount + "개 보관" : "백업 없음"), Height = 46,
                Background = Brush("#EEF2FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 3, 0),
                Cursor = Cursors.Hand, IsEnabled = backupCount > 0, Opacity = backupCount > 0 ? 1 : .45,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11.5 };
            restore.IsEnabled = backupCount > 0 || (googleConnected && localItemCount > 0); restore.Opacity = restore.IsEnabled ? 1 : .45;
            Round(restore, 10); restore.Click += delegate
            {
                var choice = new RecoveryChoiceWindow(backupCount, googleConnected ? localItemCount : 0) { Owner = this };
                if (choice.ShowDialog() != true) return;
                RequestedDataAction = choice.SelectedAction == "local" ? SettingsDataAction.ImportDormantLocal : SettingsDataAction.RestoreBackup;
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            restore.Padding = new Thickness(1);
            dataActions.Children.Add(restore);
            var import = new Button { Content = actionCaption("⇧  가져오기", "온하루 일정"), Height = 46,
                Background = Brush("#FFF7ED"), Foreground = Brush("#C2410C"), BorderThickness = new Thickness(0), Margin = new Thickness(3, 0, 3, 0), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11.5 };
            Round(import, 10); import.Click += delegate
            {
                var choice = new DataFormatChoiceWindow("⇧  일정 가져오기", false) { Owner = this };
                if (choice.ShowDialog() != true) return; RequestedDataFormat = choice.SelectedFormat;
                RequestedDataAction = SettingsDataAction.ImportFile; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            import.Padding = new Thickness(1);
            Grid.SetColumn(import, 1); dataActions.Children.Add(import);
            var export = new Button { Content = actionCaption("⇩  PC 저장", "형식 선택"), Height = 46,
                Background = Brush("#ECFDF5"), Foreground = Brush("#047857"), BorderThickness = new Thickness(0), Margin = new Thickness(3, 0, 3, 0), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11.5 };
            Round(export, 10); export.Click += delegate
            {
                var choice = new DataFormatChoiceWindow("⇩  PC로 내보내기", false) { Owner = this };
                if (choice.ShowDialog() != true) return; RequestedDataFormat = choice.SelectedFormat;
                RequestedDataAction = SettingsDataAction.ExportFile; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            export.Padding = new Thickness(1);
            Grid.SetColumn(export, 2); dataActions.Children.Add(export);
            var email = new Button { Content = actionCaption("✉  메일 보내기", "형식 선택"), Height = 46,
                Background = Brush("#EEF2FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0), Margin = new Thickness(3, 0, 0, 0), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11.5,
                ToolTip = "선택한 형식의 일정 파일을 내 이메일로 보내기" };
            Round(email, 10); email.Click += delegate
            {
                var choice = new DataFormatChoiceWindow("✉  메일로 보내기", true) { Owner = this };
                if (choice.ShowDialog() != true) return; RequestedDataFormat = choice.SelectedFormat;
                RequestedDataAction = SettingsDataAction.ExportEmail; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            email.Padding = new Thickness(1);
            Grid.SetColumn(email, 3); dataActions.Children.Add(email);
            var deleteData = new Button { Content = actionCaption("🗑  일정 삭제", "선택 관리"), Height = 46,
                Background = Brush("#FFF1F2"), Foreground = Brush("#BE123C"), BorderThickness = new Thickness(0), Margin = new Thickness(3, 0, 0, 0), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 10.5,
                ToolTip = "Google 원본은 삭제하지 않고 ONHARU 로컬 데이터만 관리" };
            Round(deleteData, 10); deleteData.Click += delegate { RequestedDataAction = SettingsDataAction.DeleteLocalData; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            deleteData.Padding = new Thickness(1);
            Grid.SetColumn(deleteData, 4); dataActions.Children.Add(deleteData);
            dataGroup.Children.Add(dataActions); panel.Children.Add(SectionCard(dataGroup));
            panel.Children.Add(save);
            Func<double, double> compactScrollHeight = delegate(double workAreaHeight)
            { return Math.Max(360, Math.Min(650, workAreaHeight * .70 - 60)); };
            var contentScroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = compactScrollHeight(SystemParameters.WorkArea.Height), Opacity = 0 };
            var popupLayout = new Grid(); popupLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); popupLayout.RowDefinitions.Add(new RowDefinition());
            popupLayout.Children.Add(header); Grid.SetRow(contentScroll, 1); popupLayout.Children.Add(contentScroll);
            Loaded += delegate
            {
                contentScroll.MaxHeight = compactScrollHeight(Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea.Height);
                contentScroll.ApplyTemplate();
                UiRound.SoftenScrollBars(contentScroll);
                contentScroll.Opacity = 1;
            };
            Content = OnharuPopupChrome.Shell(popupLayout);
        }

        static string GooglePresetVariant(string hex, int ordinal)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var delta = (ordinal / 6) % 2 == 0 ? 18 : -18;
            Func<byte, byte> shift = value => (byte)Math.Max(0, Math.Min(255, value + delta));
            return string.Format("#{0:X2}{1:X2}{2:X2}", shift(color.R), shift(color.G), shift(color.B));
        }

        static string UniquePresetColor(string hex, int ordinal, HashSet<string> usedColors)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var candidate = string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
            if (!usedColors.Contains(candidate)) return candidate;
            for (var attempt = 1; attempt <= 64; attempt++)
            {
                var r = (byte)((color.R + 17 * attempt + 3 * ordinal) % 256);
                var g = (byte)((color.G + 29 * attempt + 5 * ordinal) % 256);
                var b = (byte)((color.B + 43 * attempt + 7 * ordinal) % 256);
                candidate = string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b);
                if (!usedColors.Contains(candidate)) return candidate;
            }
            return candidate;
        }

        UIElement ColorEditor(string name, string hex, string displayName = null, bool allowColorChange = true)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 3) };
            var title = new DockPanel { Height = 21, LastChildFill = false, Margin = new Thickness(0, 0, 0, 1), VerticalAlignment = VerticalAlignment.Center };
            var preview = new Border { Width = 38, Height = 17, CornerRadius = new CornerRadius(5),
                Background = PaletteEditorPreview(color), VerticalAlignment = VerticalAlignment.Center };
            previews[name] = preview; DockPanel.SetDock(preview, Dock.Right); title.Children.Add(preview);
            var select = new TextBlock { Tag = name, Text = displayName ?? name, FontWeight = FontWeights.SemiBold,
                FontSize = 12, Foreground = PaletteEditorForeground(color), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0), Cursor = Cursors.Hand };
            editorTitles[name] = select;
            DockPanel.SetDock(select, Dock.Left); title.Children.Add(select); box.Children.Add(title);
            var rgbPanel = new StackPanel { Visibility = Visibility.Collapsed };
            rgbPanels.Add(rgbPanel); box.Children.Add(rgbPanel);
            var rgb = new[] { color.R, color.G, color.B }; var set = new Slider[3]; var labels = new TextBlock[3]; var channels = new TextBlock[3];
            for (var i = 0; i < 3; i++)
            {
                var row = new Grid { Height = 16 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                var channel = new TextBlock { Text = new[] { "R", "G", "B" }[i], Foreground = PaletteEditorForeground(color) }; row.Children.Add(channel); channels[i] = channel;
                var slider = new Slider { Minimum = 0, Maximum = 255, Value = rgb[i], Tag = name, Height = 15 }; Grid.SetColumn(slider, 1); row.Children.Add(slider); set[i] = slider;
                var value = new TextBlock { Text = rgb[i].ToString(), Foreground = PaletteEditorForeground(color), HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetColumn(value, 2); row.Children.Add(value); labels[i] = value;
                slider.ValueChanged += delegate { UpdatePreview(name); }; rgbPanel.Children.Add(row);
            }
            sliders[name] = set; values[name] = labels; editorChannels[name] = channels;
            var card = new Border { Background = PaletteEditorBackground(color), BorderBrush = PaletteEditorBorder(color), Tag = name,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(4, 0, 4, 4), Child = box, AllowDrop = true, Cursor = Cursors.Arrow };
            Action openPalette = delegate
            {
                var menu = new ContextMenu { PlacementTarget = card, Placement = PlacementMode.Bottom, StaysOpen = false };
                var palettes = OnharuColorPresets.Palettes();
                for (var row = 0; row < palettes.Length; row++)
                {
                    var header = new MenuItem { Header = OnharuColorPresets.Names[row], IsEnabled = false, FontWeight = FontWeights.SemiBold };
                    menu.Items.Add(header);
                    foreach (var hexValue in palettes[row])
                    {
                        var selectedHex = hexValue;
                        var swatch = new Border { Width = 164, Height = 24, CornerRadius = new CornerRadius(7),
                            Background = new SolidColorBrush(CategoryColorSystem.Background(ThemeId, selectedHex)),
                            BorderBrush = new SolidColorBrush(CategoryColorSystem.EditorBorder(ThemeId, (Color)ColorConverter.ConvertFromString(selectedHex))),
                            BorderThickness = new Thickness(1), Padding = new Thickness(8, 0, 8, 0),
                            Child = new TextBlock { Text = selectedHex, VerticalAlignment = VerticalAlignment.Center,
                                Foreground = new SolidColorBrush(CategoryColorSystem.Foreground(ThemeId, selectedHex)) } };
                        var item = new MenuItem { Header = swatch, Padding = new Thickness(2), Tag = selectedHex };
                        item.Click += delegate { SetHex(name, selectedHex); };
                        menu.Items.Add(item);
                    }
                    if (row < palettes.Length - 1) menu.Items.Add(new Separator());
                }
                card.ContextMenu = menu; menu.IsOpen = true;
            };
            if (allowColorChange)
            {
                select.MouseLeftButtonUp += delegate { openPalette(); };
                preview.Cursor = Cursors.Hand;
                preview.MouseLeftButtonUp += delegate { openPalette(); };
            }
            else
            {
                select.Cursor = Cursors.Arrow;
                preview.Cursor = Cursors.Arrow;
                card.ToolTip = "공휴일은 프리셋별 빨강 계열로 유지됩니다.";
            }
            card.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (e.LeftButton != MouseButtonState.Pressed || HasColorEditorControl(e.OriginalSource as DependencyObject)) return;
                DragDrop.DoDragDrop(card, card, DragDropEffects.Move);
            };
            card.DragOver += delegate(object sender, DragEventArgs e)
            {
                var dragged = e.Data.GetData(typeof(Border)) as Border;
                e.Effects = dragged != null && dragged.Parent == card.Parent ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            card.Drop += delegate(object sender, DragEventArgs e)
            {
                var dragged = e.Data.GetData(typeof(Border)) as Border;
                var parent = card.Parent as Panel;
                if (dragged == null || parent == null || dragged.Parent != parent || dragged == card) return;
                var target = parent.Children.IndexOf(card); parent.Children.Remove(dragged); parent.Children.Insert(target, dragged);
            };
            editorCards[name] = card; UpdatePreview(name); return card;
        }

        static bool HasColorEditorControl(DependencyObject source)
        {
            for (var current = source; current != null; current = VisualTreeHelper.GetParent(current))
                if (current is CheckBox || current is Slider || current is Button) return true;
            return false;
        }

        void UpdateColorSelectionAvailability()
        {
            var selected = colorSelections.Where(x => x.IsChecked == true).ToList();
            if (colorSaveMyButton != null)
            {
                colorSaveMyButton.IsEnabled = selected.Count >= 1;
                colorSaveMyButton.Opacity = colorSaveMyButton.IsEnabled ? 1 : .45;
            }
            if (colorSwapButton == null) return;
            var swapPair = colorSelectionOrder.Where(x => x.IsChecked == true).ToList();
            colorSwapButton.IsEnabled = swapPair.Count == 2;
            colorSwapButton.Opacity = colorSwapButton.IsEnabled ? 1 : .45;
            colorSwapButton.Content = swapPair.Count == 2
                ? SelectionName(swapPair[0]) + " ⇄ " + SelectionName(swapPair[1])
                : "색상 2개 선택";
        }

        static string SelectionName(CheckBox check)
        {
            var text = check == null ? "" : Convert.ToString(check.Content);
            return text.EndsWith(" 색상", StringComparison.Ordinal) ? text.Substring(0, text.Length - 3) : text;
        }

        void UpdatePreview(string name)
        {
            if (!sliders.ContainsKey(name)) return;
            var s = sliders[name]; var c = Color.FromRgb((byte)s[0].Value, (byte)s[1].Value, (byte)s[2].Value);
            previews[name].Background = PaletteEditorPreview(c);
            if (editorCards.ContainsKey(name))
            {
                editorCards[name].Background = PaletteEditorBackground(c);
                editorCards[name].BorderBrush = PaletteEditorBorder(c);
            }
            var foreground = PaletteEditorForeground(c);
            if (editorTitles.ContainsKey(name)) editorTitles[name].Foreground = foreground;
            for (var i = 0; i < 3; i++)
            {
                values[name][i].Text = ((int)s[i].Value).ToString(); values[name][i].Foreground = foreground;
                if (editorChannels.ContainsKey(name)) editorChannels[name][i].Foreground = foreground;
            }
        }
        string Hex(string name)
        {
            var s = sliders[name]; return string.Format("#{0:X2}{1:X2}{2:X2}", (byte)s[0].Value, (byte)s[1].Value, (byte)s[2].Value);
        }
        string HexOr(string name, string fallback)
        {
            return sliders.ContainsKey(name) ? Hex(name) : fallback;
        }
        void SetHex(string name, string hex)
        {
            if (!sliders.ContainsKey(name)) return;
            var color = (Color)ColorConverter.ConvertFromString(hex); var s = sliders[name];
            s[0].Value = color.R; s[1].Value = color.G; s[2].Value = color.B; UpdatePreview(name);
        }
        static Border SectionCard(UIElement child)
        {
            return new Border { Background = Brush(OnharuPopupChrome.ContentSurfaceColor), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 0, 6), Child = child };
        }

        internal static void StyleComboBox(ComboBox combo, double maxDropHeight = 220)
        {
            var grid = new FrameworkElementFactory(typeof(Grid));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ComboBox.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(ComboBox.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            grid.AppendChild(border);
            var value = new FrameworkElementFactory(typeof(ContentPresenter));
            value.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
            value.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
            value.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            value.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 0, 24, 0)); grid.AppendChild(value);
            var arrow = new FrameworkElementFactory(typeof(TextBlock));
            arrow.SetValue(TextBlock.TextProperty, "▾"); arrow.SetValue(TextBlock.FontSizeProperty, 11.5);
            arrow.SetValue(TextBlock.ForegroundProperty, Brush("#64748B")); arrow.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetValue(TextBlock.WidthProperty, 24.0); arrow.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            arrow.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center); arrow.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 2, 0));
            arrow.SetValue(TextBlock.IsHitTestVisibleProperty, false); grid.AppendChild(arrow);
            var toggle = new FrameworkElementFactory(typeof(ToggleButton));
            toggle.SetValue(ToggleButton.BackgroundProperty, Brushes.Transparent); toggle.SetValue(ToggleButton.BorderThicknessProperty, new Thickness(0));
            toggle.SetValue(ToggleButton.FocusableProperty, false); toggle.SetValue(ToggleButton.CursorProperty, Cursors.Hand);
            var toggleRoot = new FrameworkElementFactory(typeof(Border)); toggleRoot.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            toggle.SetValue(ToggleButton.TemplateProperty, new ControlTemplate(typeof(ToggleButton)) { VisualTree = toggleRoot });
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen") { RelativeSource = RelativeSource.TemplatedParent, Mode = BindingMode.TwoWay });
            grid.AppendChild(toggle);
            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom); popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.StaysOpenProperty, false);
            popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen") { RelativeSource = RelativeSource.TemplatedParent, Mode = BindingMode.TwoWay });
            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty, Brushes.White); popupBorder.SetValue(Border.BorderBrushProperty, Brush("#D5D8DE"));
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1)); popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            popupBorder.SetValue(Border.MarginProperty, new Thickness(0, 3, 0, 0));
            popupBorder.SetBinding(Border.WidthProperty, new Binding("ActualWidth") { RelativeSource = RelativeSource.TemplatedParent });
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer)); scroll.SetValue(ScrollViewer.MaxHeightProperty, maxDropHeight); scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled); scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            var items = new FrameworkElementFactory(typeof(ItemsPresenter)); scroll.AppendChild(items); popupBorder.AppendChild(scroll); popup.AppendChild(popupBorder); grid.AppendChild(popup);
            combo.Template = new ControlTemplate(typeof(ComboBox)) { VisualTree = grid };
            var itemRoot = new FrameworkElementFactory(typeof(Border));
            itemRoot.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ComboBoxItem.BackgroundProperty));
            var itemContent = new FrameworkElementFactory(typeof(ContentPresenter)); itemContent.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 5, 10, 5));
            itemRoot.AppendChild(itemContent);
            var itemTemplate = new ControlTemplate(typeof(ComboBoxItem)) { VisualTree = itemRoot };
            var hover = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
            hover.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, Brush(OnharuPopupChrome.SupportSurfaceColor)));
            var selected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, Brush(OnharuPopupChrome.SelectionSurfaceColor)));
            itemTemplate.Triggers.Add(hover); itemTemplate.Triggers.Add(selected);
            var itemStyle = new Style(typeof(ComboBoxItem)); itemStyle.Setters.Add(new Setter(ComboBoxItem.TemplateProperty, itemTemplate));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brush("#334155"))); combo.ItemContainerStyle = itemStyle;
            combo.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!combo.IsEnabled) return;
                if (ItemsControl.ContainerFromElement(combo, e.OriginalSource as DependencyObject) is ComboBoxItem) return;
                combo.Focus(); combo.IsDropDownOpen = !combo.IsDropDownOpen; e.Handled = true;
            };
            combo.DropDownOpened += delegate
            {
                combo.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(delegate
                {
                    var popupPart = combo.Template.FindName("PART_Popup", combo) as Popup;
                    if (popupPart != null && popupPart.Child != null) UiRound.SoftenScrollBars(popupPart.Child);
                }));
            };
        }

        static bool IsHoliday(GoogleCalendarSetting source)
        {
            return (source.Name ?? "").Contains("휴일") || (source.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        static void Round(Button button, double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }
        Brush PaletteEditorBackground(Color color)
        {
            return new SolidColorBrush(CategoryColorSystem.Background(ThemeId, color));
        }
        Brush PaletteEditorBorder(Color color)
        {
            return new SolidColorBrush(CategoryColorSystem.EditorBorder(ThemeId, color));
        }
        Brush PaletteEditorForeground(Color color)
        {
            return new SolidColorBrush(CategoryColorSystem.Foreground(ThemeId, color));
        }
        Brush PaletteEditorPreview(Color color)
        {
            return new SolidColorBrush(CategoryColorSystem.Background("classic", color));
        }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }
}
