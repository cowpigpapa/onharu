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
        readonly Dictionary<string, CheckBox> editorTitles = new Dictionary<string, CheckBox>();
        readonly Dictionary<string, TextBlock[]> editorChannels = new Dictionary<string, TextBlock[]>();
        readonly Dictionary<string, TextBlock[]> values = new Dictionary<string, TextBlock[]>();
        readonly List<CheckBox> colorSelections = new List<CheckBox>();
        readonly List<StackPanel> rgbPanels = new List<StackPanel>();
        public string BusinessColor;
        public string PersonalColor;
        public string BaseballColor;
        public string DdayColor;
        public string AnniversaryColor;
        public string HolidayColor;
        public double SelectedFontSize;
        public string OrderMode;
        public bool MultiDayFirst;
        public bool CompletedLast;
        public string CompletedDisplayMode;
        public string StartViewMode;
        public bool ReminderSound;
        public int QuietStartHour;
        public int QuietEndHour;
        public string StartupPositionMode;
        public string CloseButtonAction;
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
        public string CalendarRangeMode;
        public int VisibleWeekCount;
        public int TodayRow;
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
        public int DefaultDurationMinutes;
        public int DefaultReminderMinutes;
        public bool ChangeGoogleAccount;
        public bool LogoutGoogleAccount;
        public SettingsDataAction RequestedDataAction;
        public string RequestedDataFormat;
        public event Action PrintRequested;
        public bool UseTimetable;
        public bool UseDiary;
        public bool UseRollover;
        public bool ShowGoogleTasks;
        public bool UseProBaseball;
        public bool AutomaticUpdateChecks;
        public bool ShowThemeQuickSwitch;
        public string ThemeId;
        public List<string> CustomPalette;
        public bool CustomPalettePastelStyle;
        public List<string> PaletteNames;
        public List<string> SavedPalettes;
        public int PaletteSelectionIndex;
        bool selectedPastelStyle;
        readonly StackPanel fontOptions = new StackPanel { Orientation = Orientation.Horizontal };
        readonly List<Tuple<string, GoogleCalendarSetting>> sourceEditors = new List<Tuple<string, GoogleCalendarSetting>>();
        readonly Dictionary<string, CheckBox> editBoxes = new Dictionary<string, CheckBox>();

        public SettingsWindow(string business, string personal, string baseball, string dday, string anniversary, string holidayColor, double fontSize, string orderMode, bool multiDayFirst, bool completedLast, bool use24HourTime, bool showWeeks,
            string weekRule, string weekStartDay, List<int> restDays, bool pastelEventStyle, int autoSyncMinutes, List<GoogleCalendarSetting> sources, bool googleConnected, int localItemCount, bool showLunar, bool showSolarTerms, string backupFolder, int backupCount, List<string> categoryOrder,
            List<string> customPalette, bool customPalettePastelStyle, List<string> paletteNames, List<string> savedPalettes, int selectedPaletteIndexValue,
            string calendarRangeMode, int visibleWeekCount, int todayRow, string selectedDateStyle, string selectedDateFillColor, string selectedDateBorderColor, string todayColor, string todayStyle, string todayBorderColor,
            string defaultCalendarKey, bool defaultAllDay, int defaultStartHour, int defaultStartMinute, int defaultDurationMinutes, int defaultReminderMinutes,
            string completedDisplayMode, string startViewMode, bool reminderSound, int quietStartHour, int quietEndHour, string startupPositionMode, string closeButtonAction, bool useTimetable, bool useDiary, bool useRollover, bool showGoogleTasks, bool useProBaseball, bool automaticUpdateChecks, bool showThemeQuickSwitch, string themeId,
            bool holidayColorVisible, bool baseballColorVisible, bool ddayColorVisible, bool anniversaryColorVisible)
        {
            ThemeId = OnharuThemePalette.Normalize(themeId);
            ShowThemeQuickSwitch = showThemeQuickSwitch;
            selectedPastelStyle = pastelEventStyle;
            CustomPalette = customPalette == null ? new List<string>() : customPalette.ToList();
            CustomPalettePastelStyle = customPalettePastelStyle;
            PaletteNames = paletteNames == null ? new List<string>() : paletteNames.ToList();
            SavedPalettes = savedPalettes == null ? new List<string>() : savedPalettes.ToList();
            BackupFolder = backupFolder;
            Title = "온하루 설정"; Width = 620; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent;
            var saveGradient = new LinearGradientBrush();
            saveGradient.StartPoint = new Point(0, .5); saveGradient.EndPoint = new Point(1, .5);
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3977E8"), 0));
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C5CE5"), 1));
            var panel = new StackPanel { Margin = new Thickness(26, 12, 18, 20) };
            var header = new DockPanel { Margin = new Thickness(26, 14, 12, 12) };
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"),
                Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            Round(close, 10);
            close.Click += delegate { DialogResult = false; };
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var account = new Button { Content = googleConnected ? "G 계정" : "G 연결", Width = 62, Height = 30, Background = Brush("#EEF2FF"),
                Foreground = Brush("#4F46E5"), BorderThickness = new Thickness(0), FontSize = 12, FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand, ToolTip = googleConnected ? "Google 계정 변경 또는 로그아웃" : "Google 계정 연결" };
            Round(account, 9);
            var googleActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0) };
            var printButton = new Button { Content = "🖨", Width = 34, Height = 30, Background = Brush("#ECFDF5"),
                Foreground = Brush("#047857"), BorderThickness = new Thickness(0), FontSize = 12, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, ToolTip = "현재 달력 인쇄 미리보기" };
            Round(printButton, 9);
            var aboutButton = new Button { Content = "ⓘ", Width = 30, Height = 30, Background = Brush("#F5F3FF"),
                Foreground = Brush("#6D28D9"), BorderThickness = new Thickness(0), FontSize = 15, FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, ToolTip = "제품 정보" };
            Round(aboutButton, 9);
            aboutButton.Click += delegate { new ProductInfoWindow { Owner = this }.ShowDialog(); };
            googleActions.Children.Add(printButton); googleActions.Children.Add(aboutButton);
            googleActions.Children.Add(account);
            DockPanel.SetDock(googleActions, Dock.Right); header.Children.Add(googleActions);
            var topSave = new Button { Content = "✓  설정 저장", Width = 96, Height = 30, Background = saveGradient,
                Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand, Visibility = Visibility.Collapsed };
            Round(topSave, 9); DockPanel.SetDock(topSave, Dock.Right); header.Children.Add(topSave);
            header.Children.Add(new TextBlock { Text = "⚙  온하루 설정", FontSize = 21, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
            var rgbToggle = new Button { Content = "▦  RGB 조절  펼치기  ▾", Width = 158, Height = 29,
                HorizontalAlignment = HorizontalAlignment.Right, Background = Brush("#F8FAFC"),
                Foreground = Brush("#475569"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(11, 0, 8, 0) };
            Round(rgbToggle, 9);
            var paletteHeader = new Grid(); paletteHeader.ColumnDefinitions.Add(new ColumnDefinition()); paletteHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            paletteHeader.Children.Add(new TextBlock { Text = "추천 색상 조합 · 스킨에 맞는 5가지 기본 팔레트", Foreground = Brush("#475569"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(rgbToggle, 1); paletteHeader.Children.Add(rgbToggle); panel.Children.Add(paletteHeader);
            var updateSelectedPalette = new Button { Content = "선택 프리셋 변경", Height = 36,
                Background = Brush("#EDE9FE"), Foreground = Brush("#5B21B6"), BorderBrush = Brush("#A78BFA"),
                BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
            Round(updateSelectedPalette, 11);
            var saveMyPalette = new Button { Content = "내 설정으로 저장", Height = 36,
                Background = Brush("#EEF2FF"), Foreground = Brush("#4F46E5"), BorderBrush = Brush("#C7D2FE"),
                BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand };
            Round(saveMyPalette, 11);
            var resetPalettes = new Button { Content = "↺  색상 설정 초기화", Height = 36, Background = Brush("#FFF7ED"),
                Foreground = Brush("#C2410C"), BorderBrush = Brush("#FDBA74"), BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand };
            Round(resetPalettes, 11);
            resetPalettes.IsEnabled = PaletteNames.Any(x => !string.IsNullOrWhiteSpace(x)) || SavedPalettes.Any(x => !string.IsNullOrWhiteSpace(x)) || CustomPalette.Count >= 2;
            resetPalettes.Opacity = resetPalettes.IsEnabled ? 1 : .45;
            var swap = new Button { Content = "☑  ⇄  ☑  색상 교환", ToolTip = "체크한 두 색상 교환", Height = 36, Background = Brush("#FCE7F3"),
                Foreground = Brush("#BE185D"), BorderBrush = Brush("#FBCFE8"), BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 0, 10, 0), Cursor = Cursors.Hand };
            Round(swap, 11);
            var paletteSaveRow = new Grid { Margin = new Thickness(0, 7, 0, 5) };
            paletteSaveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paletteSaveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paletteSaveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paletteSaveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paletteSaveRow.Children.Add(updateSelectedPalette); Grid.SetColumn(saveMyPalette, 1); paletteSaveRow.Children.Add(saveMyPalette);
            Grid.SetColumn(swap, 2); paletteSaveRow.Children.Add(swap); Grid.SetColumn(resetPalettes, 3); paletteSaveRow.Children.Add(resetPalettes);
            var presets = new Grid { Margin = new Thickness(0, 3, 0, 8) };
            for (var i = 0; i < 13; i++) presets.ColumnDefinitions.Add(new ColumnDefinition {
                Width = i % 2 == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
            var names = OnharuColorPresets.Names.Concat(new[] { "내설정", "Google" }).ToArray();
            var palettes = OnharuColorPresets.Palettes().Concat(new[] {
                new[] { business, personal, baseball, dday, anniversary, holidayColor }, new string[0] }).ToArray();
            const int presetCount = 5, customPaletteIndex = 5, googlePaletteIndex = 6;
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
            var activeSources = allSources.Where(x => showGoogleTasks || !GoogleTasks.IsSource(x.Id))
                .OrderBy(x => IsHoliday(x) ? 2 : x.Primary ? 0 : 1).ThenBy(x => x.Name).ToList();
            var hiddenTaskSources = allSources.Where(x => GoogleTasks.IsSource(x.Id) && !activeSources.Contains(x)).ToList();
            var hiddenTaskEditBoxes = new Dictionary<GoogleCalendarSetting, CheckBox>();
            for (var i = 0; i < activeSources.Count; i++) sourceEditors.Add(Tuple.Create("google_" + i, activeSources[i]));
            if (CustomPalette.Count < 6)
            {
                var currentColors = new List<string> { business, personal, baseball, dday, anniversary, holidayColor };
                currentColors.AddRange(activeSources.Where(x => !IsHoliday(x)).Select(x => string.IsNullOrWhiteSpace(x.Color) ? "#E9799A" : x.Color));
                palettes[customPaletteIndex] = currentColors.ToArray();
            }
            var orderEntries = new List<Tuple<string, string>> { Tuple.Create("local:business", "업무일정"), Tuple.Create("local:personal", "개인일정") };
            orderEntries.AddRange(activeSources.Select(x => Tuple.Create("google:" + x.Id, "Google · " + x.Name)));
            var savedOrder = categoryOrder ?? new List<string>();
            orderEntries = orderEntries.OrderBy(x => { var p = savedOrder.IndexOf(x.Item1); return p < 0 ? 999 : p; }).ThenBy(x => x.Item2).ToList();
            CategoryOrder = orderEntries.Select(x => x.Item1).ToList();
            var paletteOptions = new List<RadioButton>();
            var selectedPaletteIndex = selectedPaletteIndexValue == 8 ? customPaletteIndex
                : selectedPaletteIndexValue == 9 ? googlePaletteIndex
                : selectedPaletteIndexValue >= presetCount ? 0 : Math.Max(0, selectedPaletteIndexValue);
            var applyingPalette = false;
            Action<int> applyPalette = null;
            Func<string, string[], UIElement> presetContent = delegate(string label, string[] colors)
            {
                return new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("#334155"), VerticalAlignment = VerticalAlignment.Center };
            };
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
                    var googleDefault = index == googlePaletteIndex;
                    updateSelectedPalette.Content = googleDefault ? "Google · 변경 불가" : names[index];
                    updateSelectedPalette.IsEnabled = !googleDefault; updateSelectedPalette.Opacity = googleDefault ? .45 : 1;
                    if (applyPalette != null) applyPalette(index);
                };
                Grid.SetColumn(option, index * 2); presets.Children.Add(option);
            }
            updateSelectedPalette.Content = selectedPaletteIndex == googlePaletteIndex
                ? "Google · 변경 불가" : names[selectedPaletteIndex];
            updateSelectedPalette.IsEnabled = selectedPaletteIndex != googlePaletteIndex;
            updateSelectedPalette.Opacity = updateSelectedPalette.IsEnabled ? 1 : .45;
            panel.Children.Add(paletteSaveRow);
            panel.Children.Add(presets);
            var rgbExpanded = false;
            rgbToggle.Click += delegate
            {
                rgbExpanded = !rgbExpanded;
                foreach (var rgbPanel in rgbPanels) rgbPanel.Visibility = rgbExpanded ? Visibility.Visible : Visibility.Collapsed;
                rgbToggle.Content = rgbExpanded ? "▦  RGB 조절  접기    ▴" : "▦  RGB 조절  펼치기  ▾";
            };
            var colorGrid = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 0, -4, 4) };
            colorGrid.Children.Add(ColorEditor("업무일정", business));
            colorGrid.Children.Add(ColorEditor("개인일정", personal));
            foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                colorGrid.Children.Add(ColorEditor(editor.Item1, string.IsNullOrWhiteSpace(editor.Item2.Color) ? "#E9799A" : editor.Item2.Color, editor.Item2.Name));
            panel.Children.Add(colorGrid);
            var specialColorGrid = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 0, -4, 4) };
            var holidayEditor = ColorEditor("국경일", holidayColor, "휴일");
            var baseballEditor = ColorEditor("야구", baseball);
            var ddayEditor = ColorEditor("D-Day", dday);
            var anniversaryEditor = ColorEditor("기념일", anniversary);
            if (holidayColorVisible) specialColorGrid.Children.Add(holidayEditor);
            if (baseballColorVisible) specialColorGrid.Children.Add(baseballEditor);
            if (ddayColorVisible) specialColorGrid.Children.Add(ddayEditor);
            if (anniversaryColorVisible) specialColorGrid.Children.Add(anniversaryEditor);
            if (specialColorGrid.Children.Count > 0)
            {
                panel.Children.Add(new TextBlock { Text = "Special Day 색상", Foreground = Brush("#64748B"), FontSize = 11,
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 2, 0, 5) });
                panel.Children.Add(specialColorGrid);
            }
            applyPalette = delegate(int index)
            {
                if (index == googlePaletteIndex)
                {
                    foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                        SetHex(editor.Item1, string.IsNullOrWhiteSpace(editor.Item2.OriginalColor) ? editor.Item2.Color : editor.Item2.OriginalColor);
                    return;
                }
                if (index < 0 || index >= palettes.Length || palettes[index].Length < 6) return;
                applyingPalette = true;
                try
                {
                    selectedPastelStyle = ThemeId == "classic";
                    SetHex("업무일정", palettes[index][0]); SetHex("개인일정", palettes[index][1]);
                    SetHex("야구", palettes[index][2]); SetHex("D-Day", palettes[index][3]);
                    SetHex("기념일", palettes[index][4]); SetHex("국경일", palettes[index][5]);
                    var usedColors = new HashSet<string>(palettes[index].Take(6), StringComparer.OrdinalIgnoreCase);
                    var colorIndex = 6; var googleIndex = 0;
                    foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                    {
                        var candidate = colorIndex < palettes[index].Length
                            ? palettes[index][colorIndex++] : GooglePresetVariant(palettes[index][googleIndex % 6], googleIndex);
                        var color = UniquePresetColor(candidate, googleIndex, usedColors);
                        usedColors.Add(color); SetHex(editor.Item1, color); googleIndex++;
                    }
                }
                finally { applyingPalette = false; }
            };
            applyPalette(selectedPaletteIndex);
            Func<List<string>> captureColors = delegate
            {
                var colors = new List<string> { Hex("업무일정"), Hex("개인일정"), Hex("야구"), Hex("D-Day"), Hex("기념일"), Hex("국경일") };
                colors.AddRange(sourceEditors.Where(x => !IsHoliday(x.Item2)).Select(x => Hex(x.Item1)));
                return colors;
            };
            foreach (var slider in sliders.Values.SelectMany(x => x))
                slider.ValueChanged += delegate
                {
                    resetPalettes.IsEnabled = true; resetPalettes.Opacity = 1;
                    if (!applyingPalette && selectedPaletteIndex != googlePaletteIndex)
                        updateSelectedPalette.Content = names[selectedPaletteIndex] + " 변경";
                };
            updateSelectedPalette.Click += delegate
            {
                if (selectedPaletteIndex == googlePaletteIndex) return;
                var currentColors = captureColors();
                while (SavedPalettes.Count < 9) SavedPalettes.Add("");
                SavedPalettes[selectedPaletteIndex] = string.Join(",", currentColors);
                palettes[selectedPaletteIndex] = currentColors.ToArray();
                paletteOptions[selectedPaletteIndex].Content = presetContent(names[selectedPaletteIndex], palettes[selectedPaletteIndex]);
                if (selectedPaletteIndex == customPaletteIndex) CustomPalette = currentColors.ToList();
                CustomPalettePastelStyle = selectedPastelStyle;
                var normalText = names[selectedPaletteIndex];
                updateSelectedPalette.Content = "✓  " + names[selectedPaletteIndex] + " 변경 완료";
                resetPalettes.IsEnabled = true; resetPalettes.Opacity = 1;
                var updateNotice = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                updateNotice.Tick += delegate { updateNotice.Stop(); updateSelectedPalette.Content = normalText; };
                updateNotice.Start();
            };
            saveMyPalette.Click += delegate
            {
                var currentColors = captureColors();
                while (SavedPalettes.Count < 9) SavedPalettes.Add("");
                SavedPalettes[customPaletteIndex] = string.Join(",", currentColors);
                palettes[customPaletteIndex] = currentColors.ToArray();
                paletteOptions[customPaletteIndex].Content = presetContent(names[customPaletteIndex], palettes[customPaletteIndex]);
                CustomPalette = currentColors.ToList();
                CustomPalettePastelStyle = selectedPastelStyle;
                saveMyPalette.Content = "✓  색상 저장 완료";
                saveMyPalette.Background = Brush("#ECFDF5"); saveMyPalette.Foreground = Brush("#047857");
                saveMyPalette.BorderBrush = Brush("#A7F3D0");
                resetPalettes.IsEnabled = true; resetPalettes.Opacity = 1;
                var saveNotice = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                saveNotice.Tick += delegate
                {
                    saveNotice.Stop(); saveMyPalette.Content = "내 설정으로 저장";
                    saveMyPalette.Background = Brush("#EEF2FF"); saveMyPalette.Foreground = Brush("#4F46E5");
                    saveMyPalette.BorderBrush = Brush("#C7D2FE");
                };
                saveNotice.Start();
            };
            resetPalettes.Click += delegate
            {
                PaletteNames.Clear(); SavedPalettes.Clear(); CustomPalette.Clear(); CustomPalettePastelStyle = ThemeId == "classic";
                var defaultNames = OnharuColorPresets.Names; var defaultPalettes = OnharuColorPresets.Palettes();
                for (var i = 0; i < presetCount; i++) { names[i] = defaultNames[i]; palettes[i] = defaultPalettes[i].ToArray(); paletteOptions[i].Content = presetContent(names[i], palettes[i]); }
                palettes[customPaletteIndex] = defaultPalettes[0].ToArray();
                paletteOptions[0].IsChecked = false; paletteOptions[0].IsChecked = true;
                resetPalettes.Content = "✓  초기화 완료";
                resetPalettes.IsEnabled = false; resetPalettes.Opacity = .45;
                var resetNotice = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                resetNotice.Tick += delegate { resetNotice.Stop(); resetPalettes.Content = "↺  색상 초기화"; };
                resetNotice.Start();
            };
            swap.Click += delegate
            {
                var selected = colorSelections.Where(x => x.IsChecked == true).Select(x => x.Tag.ToString()).ToList();
                if (selected.Count != 2) return;
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
            orderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
            orderRow.Children.Add(new TextBlock { Text = "일정 표시 순서", Foreground = Brush("#475569"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center });
            var orderOptions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            orderOptions.Children.Add(new RadioButton { Content = "카테고리별 · 하루종일 우선", Tag = "category", GroupName = "OrderMode",
                IsChecked = orderMode != "time", Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
            orderOptions.Children.Add(new RadioButton { Content = "전체 시간순", Tag = "time", GroupName = "OrderMode", IsChecked = orderMode == "time",
                VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(orderOptions, 1); orderRow.Children.Add(orderOptions);
            var categoryOrderButton = new Button { Content = "☷  카테고리 순서 설정", Height = 28, Background = Brush("#E0E7FF"), Foreground = Brush("#4338CA"),
                BorderBrush = Brush("#C7D2FE"), BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Round(categoryOrderButton, 10);
            categoryOrderButton.Click += delegate
            {
                var ordered = CategoryOrder.Select(key => orderEntries.First(x => x.Item1 == key)).ToList();
                var window = new CategoryOrderWindow(ordered) { Owner = this };
                if (window.ShowDialog() == true) CategoryOrder = window.Result;
            };
            Grid.SetColumn(categoryOrderButton, 2); orderRow.Children.Add(categoryOrderButton); panel.Children.Add(SectionCard(orderRow));
            var themeOptions = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 2) };
            foreach (var option in new[] { Tuple.Create("파스텔", "classic", "부드럽고 생동감 있는 기본 스킨"), Tuple.Create("블랙", "dark", "어두운 배경과 선명한 포인트") })
            {
                var previewBackground = option.Item2 == "dark" ? "#1A1A1A" : "#E7E9FF";
                var previewBorder = option.Item2 == "dark" ? "#6366F1" : "#B9C1FF";
                var previewForeground = option.Item2 == "dark" ? "#FFFFFF" : "#4338CA";
                var choice = new RadioButton { Tag = option.Item2, GroupName = "OnharuTheme", IsChecked = ThemeId == option.Item2,
                    Height = 34, VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, option.Item2 == "dark" ? 0 : 10, 0), Cursor = Cursors.Hand };
                choice.Content = new Border { Height = 30, Background = Brush(previewBackground), BorderBrush = Brush(previewBorder),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(9, 0, 9, 0),
                    Child = new TextBlock { Text = option.Item1 + " · " + option.Item3, Foreground = Brush(previewForeground),
                        FontWeight = FontWeights.SemiBold, FontSize = 11, VerticalAlignment = VerticalAlignment.Center } };
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
                    updateSelectedPalette.Content = selectedPaletteIndex == googlePaletteIndex
                        ? "Google · 변경 불가" : names[selectedPaletteIndex];
                    foreach (var editorName in sliders.Keys.ToList()) UpdatePreview(editorName);
                    if (applyPalette != null) applyPalette(selectedPaletteIndex);
                };
                themeOptions.Children.Add(choice);
            }
            var themeGroup = new StackPanel();
            var themeQuickSwitchOption = new CheckBox { Content = "상단 스킨 전환 버튼 표시", IsChecked = showThemeQuickSwitch,
                Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
            var themeHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 7), LastChildFill = true };
            DockPanel.SetDock(themeQuickSwitchOption, Dock.Right); themeHeader.Children.Add(themeQuickSwitchOption);
            themeHeader.Children.Add(new TextBlock { Text = "디자인 스킨", Foreground = Brush("#475569"), FontSize = 12,
                FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            themeGroup.Children.Add(themeHeader); themeGroup.Children.Add(themeOptions); panel.Children.Add(SectionCard(themeGroup));

            var displayHeader = new TextBlock { Text = "표시 옵션", Foreground = Brush("#475569"), FontSize = 12,
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
            var diary = new CheckBox { Content = "일기장 기능", IsChecked = useDiary, Margin = new Thickness(0, 0, 22, 5),
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
            var googleTasks = new CheckBox { Content = "Google Tasks 표시·동기화", IsChecked = showGoogleTasks,
                Margin = new Thickness(0, 0, 22, 5), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Google Tasks의 제한 사항을 확인한 뒤 오른쪽 목록과 동기화를 사용합니다." };
            googleTasks.Checked += delegate
            {
                var warning = new GoogleTasksWarningWindow { Owner = this };
                if (warning.ShowDialog() != true) googleTasks.IsChecked = false;
            };
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
            var closeAction = new StackPanel { Orientation = Orientation.Horizontal, Height = 24 };
            closeAction.Children.Add(new TextBlock { Text = "× 버튼 동작", Width = 120, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            foreach (var option in new[] { Tuple.Create("트레이로 최소화", "minimize"), Tuple.Create("종료 확인", "confirm_exit") })
                closeAction.Children.Add(new RadioButton { Content = option.Item1, Tag = option.Item2, GroupName = "CloseButtonAction",
                    IsChecked = closeButtonAction == option.Item2, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center });
            var calendarOptions = new StackPanel { Margin = new Thickness(0, 1, 0, 0) };
            showWeek.Margin = new Thickness(0, 0, 0, 5); calendarOptions.Children.Add(showWeek);
            var weekRuleRow = new Grid { Margin = new Thickness(18, 0, 0, 5), Visibility = showWeeks ? Visibility.Visible : Visibility.Collapsed };
            weekRuleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(102) }); weekRuleRow.ColumnDefinitions.Add(new ColumnDefinition());
            weekRuleRow.Children.Add(new TextBlock { Text = "주차 방식", Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(weekRules, 1); weekRuleRow.Children.Add(weekRules);
            var otherDisplayOptions = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            lunar.Margin = new Thickness(0, 0, 24, 5); solarTerms.Margin = new Thickness(0, 0, 24, 5);
            otherDisplayOptions.Children.Add(lunar); otherDisplayOptions.Children.Add(solarTerms);
            otherDisplayOptions.Children.Add(multiDayTop); otherDisplayOptions.Children.Add(completedLastOption); otherDisplayOptions.Children.Add(rollover); otherDisplayOptions.Children.Add(use24Hour);
            otherDisplayOptions.Children.Add(googleTasks);
            var featureIconOptions = new WrapPanel { Margin = new Thickness(0) };
            featureIconOptions.Children.Add(timetable); featureIconOptions.Children.Add(diary); featureIconOptions.Children.Add(proBaseball);
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
            showWeek.Click += delegate { weekRuleRow.Visibility = showWeek.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; };
            var displayGroup = new StackPanel(); displayGroup.Children.Add(displayHeader);
            displayGroup.Children.Add(new TextBlock { Text = "달력 내 표시", Foreground = Brush("#64748B"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            displayGroup.Children.Add(calendarOptions);
            displayGroup.Children.Add(weekRuleRow); displayGroup.Children.Add(otherDisplayOptions);
            displayGroup.Children.Add(new Border { Height = 1, Background = Brush("#E2E8F0"), Margin = new Thickness(0, 3, 0, 7) });
            displayGroup.Children.Add(new TextBlock { Text = "상단 기능 아이콘", Foreground = Brush("#64748B"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            displayGroup.Children.Add(featureIconOptions);
            panel.Children.Add(SectionCard(displayGroup));
            var behaviorGroup = new StackPanel();
            behaviorGroup.Children.Add(new TextBlock { Text = "화면과 동작", Foreground = Brush("#475569"), FontSize = 12,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            behaviorGroup.Children.Add(startDay); behaviorGroup.Children.Add(restDayRow); behaviorGroup.Children.Add(completedDisplay); behaviorGroup.Children.Add(startView);
            behaviorGroup.Children.Add(startupPosition); behaviorGroup.Children.Add(closeAction); behaviorGroup.Children.Add(selectionOptions); behaviorGroup.Children.Add(todayOptions);
            fontRow.Margin = new Thickness(0, 2, 0, 0); behaviorGroup.Children.Add(fontRow);
            panel.Children.Add(SectionCard(behaviorGroup));
            var rangeGroup = new StackPanel();
            rangeGroup.Children.Add(new TextBlock { Text = "달력 표시 범위", Foreground = Brush("#475569"), FontSize = 12 });
            var rangeRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 24 };
            rangeRow.Children.Add(new TextBlock { Text = "월전체 (1일부터)", Width = 120, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#64748B") });
            var monthFive = new RadioButton { Content = "5주", Tag = "month5", GroupName = "MonthCalendarRange", IsChecked = calendarRangeMode == "month5", Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
            var monthSix = new RadioButton { Content = "6주", Tag = "month6", GroupName = "MonthCalendarRange", IsChecked = calendarRangeMode == "month6", Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
            var monthAuto = new RadioButton { Content = "자동 (4~6주)", Tag = "monthAuto", GroupName = "MonthCalendarRange", IsChecked = calendarRangeMode != "month5" && calendarRangeMode != "month6", Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
            monthAuto.ToolTip = "해당 월에 필요한 4~6주만 표시";
            rangeRow.Children.Add(monthFive); rangeRow.Children.Add(monthSix); rangeRow.Children.Add(monthAuto); rangeGroup.Children.Add(rangeRow);
            var weekChoiceRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 24, Margin = new Thickness(0, -3, 0, 0) };
            weekChoiceRow.Children.Add(new TextBlock { Text = "사용자 지정", Width = 120, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#64748B") });
            var customWeeks = new List<RadioButton>();
            for (var count = 1; count <= 6; count++)
            {
                var option = new RadioButton { Content = count + "주", Tag = "weeks:" + count, GroupName = "CustomCalendarRange",
                    IsChecked = visibleWeekCount == count, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
                customWeeks.Add(option); weekChoiceRow.Children.Add(option);
            }
            todayRow = Math.Max(1, Math.Min(Math.Max(1, visibleWeekCount), todayRow > 0 ? todayRow : DefaultTodayRow(visibleWeekCount)));
            var todayLabel = new TextBlock { Text = "이번 주", FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(3, -17, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            var todayRowOption = new ComboBox { Width = 108, Height = 22, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"),
                Foreground = Brush("#4338CA"), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand,
                VerticalContentAlignment = VerticalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2) };
            StyleComboBox(todayRowOption);
            Action<int, int> fillTodayRows = delegate(int count, int selected)
            {
                todayRowOption.Items.Clear();
                for (var row = 1; row <= 6; row++) todayRowOption.Items.Add(new ComboBoxItem { Content = "위에서 " + row + "번째", IsEnabled = row <= count });
                todayRowOption.SelectedIndex = Math.Max(0, Math.Min(count - 1, selected - 1));
            };
            fillTodayRows(Math.Max(1, visibleWeekCount), todayRow);
            var todayPicker = new Grid { Width = 108, Height = 24, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ClipToBounds = false };
            todayPicker.Children.Add(todayRowOption); todayPicker.Children.Add(todayLabel);
            weekChoiceRow.Children.Add(todayPicker);
            Action updateRange = delegate
            {
                todayLabel.IsEnabled = true; todayRowOption.IsEnabled = true; todayRowOption.Opacity = 1;
            };
            foreach (var option in customWeeks)
            {
                var selectedOption = option;
                selectedOption.Checked += delegate
                {
                    var count = int.Parse(selectedOption.Tag.ToString().Substring(6));
                    todayRow = DefaultTodayRow(count); fillTodayRows(count, todayRow); updateRange();
                };
            }
            monthFive.Checked += delegate { updateRange(); }; monthSix.Checked += delegate { updateRange(); };
            monthAuto.Checked += delegate { updateRange(); };
            rangeGroup.Children.Add(weekChoiceRow);
            panel.Children.Add(SectionCard(rangeGroup));
            updateRange();
            var defaultsGroup = new StackPanel();
            defaultsGroup.Children.Add(new TextBlock { Text = "새 일정 기본값", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
            var defaultsRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 30 };
            var defaultCalendar = new ComboBox { Width = 145, Height = 26, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            defaultCalendar.Items.Add(new ComboBoxItem { Content = "온하루 · 업무일정", Tag = "local:business" });
            defaultCalendar.Items.Add(new ComboBoxItem { Content = "온하루 · 개인일정", Tag = "local:personal" });
            foreach (var source in activeSources.Where(x => x.Editable && !IsHoliday(x)))
                defaultCalendar.Items.Add(new ComboBoxItem { Content = "Google · " + source.Name, Tag = "google:" + source.Id });
            defaultCalendar.SelectedItem = defaultCalendar.Items.OfType<ComboBoxItem>().FirstOrDefault(x => (x.Tag ?? "").ToString() == defaultCalendarKey) ?? defaultCalendar.Items[0];
            StyleComboBox(defaultCalendar); defaultsRow.Children.Add(defaultCalendar);
            var defaultAllDayOption = new RadioButton { Content = "하루종일", GroupName = "DefaultTimeMode", IsChecked = defaultAllDay, Margin = new Thickness(12, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            var defaultTimedOption = new RadioButton { Content = "시간 지정", GroupName = "DefaultTimeMode", IsChecked = !defaultAllDay, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            defaultsRow.Children.Add(defaultAllDayOption); defaultsRow.Children.Add(defaultTimedOption);
            var defaultTime = new ComboBox { Width = 76, Height = 26, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            for (var hour = 0; hour < 24; hour++) for (var minute = 0; minute < 60; minute += 15)
                defaultTime.Items.Add(new ComboBoxItem { Content = string.Format("{0:00}:{1:00}", hour, minute), Tag = hour * 60 + minute });
            defaultTime.SelectedItem = defaultTime.Items.OfType<ComboBoxItem>().OrderBy(x => Math.Abs((int)x.Tag - (defaultStartHour * 60 + defaultStartMinute))).First();
            StyleComboBox(defaultTime); defaultsRow.Children.Add(defaultTime); defaultsGroup.Children.Add(defaultsRow);
            var defaultsDetail = new StackPanel { Orientation = Orientation.Horizontal, Height = 30, Margin = new Thickness(0, 2, 0, 0) };
            defaultsDetail.Children.Add(new TextBlock { Text = "소요 시간", Width = 66, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            var defaultDuration = new ComboBox { Width = 88, Height = 26, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            foreach (var option in new[] { Tuple.Create("30분", 30), Tuple.Create("1시간", 60), Tuple.Create("1시간 30분", 90), Tuple.Create("2시간", 120) })
                defaultDuration.Items.Add(new ComboBoxItem { Content = option.Item1, Tag = option.Item2 });
            defaultDuration.SelectedItem = defaultDuration.Items.OfType<ComboBoxItem>().FirstOrDefault(x => (int)x.Tag == defaultDurationMinutes) ?? defaultDuration.Items[0];
            StyleComboBox(defaultDuration); defaultsDetail.Children.Add(defaultDuration);
            defaultsDetail.Children.Add(new TextBlock { Text = "기본 알림", Width = 70, Margin = new Thickness(18, 0, 0, 0), Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            var defaultReminder = new ComboBox { Width = 105, Height = 26, Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            foreach (var option in new[] { Tuple.Create("없음", -1), Tuple.Create("정시", 0), Tuple.Create("10분 전", 10), Tuple.Create("30분 전", 30), Tuple.Create("1시간 전", 60), Tuple.Create("하루 전", 1440) })
                defaultReminder.Items.Add(new ComboBoxItem { Content = option.Item1, Tag = option.Item2 });
            defaultReminder.SelectedItem = defaultReminder.Items.OfType<ComboBoxItem>().FirstOrDefault(x => (int)x.Tag == defaultReminderMinutes) ?? defaultReminder.Items[0];
            StyleComboBox(defaultReminder); defaultsDetail.Children.Add(defaultReminder); defaultsGroup.Children.Add(defaultsDetail);
            Action updateDefaultTime = delegate { defaultTime.IsEnabled = defaultTimedOption.IsChecked == true; defaultDuration.IsEnabled = defaultTimedOption.IsChecked == true; };
            defaultAllDayOption.Checked += delegate { updateDefaultTime(); }; defaultTimedOption.Checked += delegate { updateDefaultTime(); }; updateDefaultTime();
            panel.Children.Add(SectionCard(defaultsGroup));
            var reminderGroup = new StackPanel { Orientation = Orientation.Horizontal, Height = 28 };
            reminderGroup.Children.Add(new TextBlock { Text = "알림", Width = 120, Foreground = Brush("#475569"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            var reminderSoundOption = new CheckBox { Content = "소리 사용", IsChecked = reminderSound, Margin = new Thickness(0, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center };
            reminderGroup.Children.Add(reminderSoundOption);
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
            reminderGroup.Children.Add(quietEnd); panel.Children.Add(SectionCard(reminderGroup));
            Action updateQuietHours = delegate
            {
                var enabled = reminderSoundOption.IsChecked == true;
                quietStart.IsEnabled = enabled; quietEnd.IsEnabled = enabled;
                quietStart.Opacity = enabled ? 1 : .45; quietEnd.Opacity = enabled ? 1 : .45;
            };
            reminderSoundOption.Checked += delegate { updateQuietHours(); };
            reminderSoundOption.Unchecked += delegate { updateQuietHours(); };
            updateQuietHours();
            var syncGroup = new StackPanel { Orientation = Orientation.Horizontal, Height = 28 };
            syncGroup.Children.Add(new TextBlock { Text = "Google 자동 동기화", Width = 120, Foreground = Brush("#475569"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center });
            var syncOptions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (var option in new[] { new { Name = "사용 안 함", Minutes = 0 }, new { Name = "5분", Minutes = 5 },
                new { Name = "15분", Minutes = 15 }, new { Name = "30분", Minutes = 30 }, new { Name = "60분", Minutes = 60 } })
                syncOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Minutes, GroupName = "AutoSync",
                    IsChecked = autoSyncMinutes == option.Minutes, Margin = new Thickness(0, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center });
            syncGroup.Children.Add(syncOptions); panel.Children.Add(SectionCard(syncGroup));
            var updateOption = new CheckBox { Content = "새 버전 자동 확인 · 설치 전 항상 확인",
                IsChecked = automaticUpdateChecks, Foreground = Brush("#475569"), FontSize = 12,
                Margin = new Thickness(0, 1, 0, 1), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "하루에 한 번 GitHub Release를 확인합니다. 동의 없이 설치하지 않습니다." };
            panel.Children.Add(SectionCard(updateOption));
            if (activeSources.Count > 0 || hiddenTaskSources.Count > 0)
            {
                var permissionGroup = new StackPanel();
                permissionGroup.Children.Add(new TextBlock { Text = "Google 일정 수정 권한", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
                var permissionGrid = new UniformGrid { Columns = 2 };
                foreach (var editor in sourceEditors)
                {
                    var source = editor.Item2; var holiday = IsHoliday(source); var taskSource = GoogleTasks.IsSource(source.Id);
                    var canWrite = taskSource || source.AccessRole == "owner" || source.AccessRole == "writer";
                    var label = taskSource ? source.Name + " · ONHARU 등록 Task" : source.Name + (holiday || !canWrite ? " · 읽기 전용" : " · 수정 가능");
                    var box = new CheckBox { Content = label,
                        IsChecked = source.Editable && canWrite && !holiday, IsEnabled = canWrite && !holiday, Margin = new Thickness(0, 0, 10, 7),
                        ToolTip = taskSource ? "체크하면 온하루에서 이 목록에 Task를 만들고 수정할 수 있습니다." : label,
                        Visibility = taskSource && !showGoogleTasks ? Visibility.Collapsed : Visibility.Visible };
                    if (taskSource)
                    {
                        googleTasks.Checked += delegate { if (googleTasks.IsChecked == true) box.Visibility = Visibility.Visible; };
                        googleTasks.Unchecked += delegate { box.Visibility = Visibility.Collapsed; };
                    }
                    editBoxes[editor.Item1] = box; permissionGrid.Children.Add(box);
                }
                foreach (var source in hiddenTaskSources)
                {
                    var box = new CheckBox { Content = source.Name + " · ONHARU 등록 Task",
                        IsChecked = source.Editable, Margin = new Thickness(0, 0, 10, 7),
                        ToolTip = "체크하면 온하루에서 이 목록에 Task를 만들고 수정할 수 있습니다.",
                        Visibility = Visibility.Collapsed };
                    googleTasks.Checked += delegate { if (googleTasks.IsChecked == true) { box.Visibility = Visibility.Visible; box.IsChecked = true; } };
                    googleTasks.Unchecked += delegate { box.Visibility = Visibility.Collapsed; };
                    hiddenTaskEditBoxes[source] = box; permissionGrid.Children.Add(box);
                }
                permissionGroup.Children.Add(permissionGrid); panel.Children.Add(SectionCard(permissionGroup));
            }
            var save = new Button { Content = "✓  설정 저장", Height = 44, Background = saveGradient, Foreground = Brushes.White,
                BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 10, 0, 0), Cursor = Cursors.Hand };
            Round(save, 13);
            save.Click += delegate
            {
                BusinessColor = Hex("업무일정"); PersonalColor = Hex("개인일정");
                BaseballColor = Hex("야구"); DdayColor = Hex("D-Day");
                AnniversaryColor = Hex("기념일"); HolidayColor = Hex("국경일");
                foreach (var editor in sourceEditors)
                {
                    if (!IsHoliday(editor.Item2)) editor.Item2.Color = Hex(editor.Item1);
                    editor.Item2.Editable = editBoxes.ContainsKey(editor.Item1) && editBoxes[editor.Item1].IsChecked == true;
                }
                foreach (var entry in hiddenTaskEditBoxes) entry.Key.Editable = entry.Value.IsChecked == true;
                SelectedFontSize = (double)fontOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                OrderMode = orderOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                MultiDayFirst = multiDayTop.IsChecked == true;
                CompletedLast = completedLastOption.IsChecked == true;
                CompletedDisplayMode = completedDisplay.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                StartViewMode = startView.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                StartupPositionMode = startupPosition.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                CloseButtonAction = closeAction.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                Use24HourTime = use24Hour.IsChecked == true;
                ShowWeekNumbers = showWeek.IsChecked == true;
                ShowLunar = lunar.IsChecked == true;
                ShowSolarTerms = solarTerms.IsChecked == true;
                UseTimetable = timetable.IsChecked == true;
                UseDiary = diary.IsChecked == true;
                UseRollover = rollover.IsChecked == true;
                ShowGoogleTasks = googleTasks.IsChecked == true;
                UseProBaseball = proBaseball.IsChecked == true;
                AutomaticUpdateChecks = updateOption.IsChecked == true;
                ShowThemeQuickSwitch = themeQuickSwitchOption.IsChecked == true;
                ThemeId = themeOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                PaletteSelectionIndex = selectedPaletteIndex;
                if (selectedPaletteIndex == customPaletteIndex)
                {
                    var customColors = new List<string> { Hex("업무일정"), Hex("개인일정"), Hex("야구"), Hex("D-Day"), Hex("기념일"), Hex("국경일") };
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
                CalendarRangeMode = rangeRow.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                var selectedCustomRange = customWeeks.First(x => x.IsChecked == true).Tag.ToString();
                VisibleWeekCount = int.Parse(selectedCustomRange.Substring(6));
                TodayRow = Math.Max(1, Math.Min(VisibleWeekCount, todayRowOption.SelectedIndex + 1));
                PastelEventStyle = selectedPastelStyle;
                AutoSyncMinutes = (int)syncOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                DefaultCalendarKey = ((ComboBoxItem)defaultCalendar.SelectedItem).Tag.ToString();
                DefaultAllDay = defaultAllDayOption.IsChecked == true;
                var timeValue = (int)((ComboBoxItem)defaultTime.SelectedItem).Tag;
                DefaultStartHour = timeValue / 60; DefaultStartMinute = timeValue % 60;
                DefaultDurationMinutes = (int)((ComboBoxItem)defaultDuration.SelectedItem).Tag;
                DefaultReminderMinutes = (int)((ComboBoxItem)defaultReminder.SelectedItem).Tag;
                ReminderSound = reminderSoundOption.IsChecked == true;
                QuietStartHour = (int)((ComboBoxItem)quietStart.SelectedItem).Tag; QuietEndHour = (int)((ComboBoxItem)quietEnd.SelectedItem).Tag;
                DialogResult = true;
            };
            topSave.Click += delegate { save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            printButton.Click += delegate { if (PrintRequested != null) PrintRequested(); };
            account.Click += delegate
            {
                if (!googleConnected) { ChangeGoogleAccount = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return; }
                var primaryAccount = (sources ?? new List<GoogleCalendarSetting>()).FirstOrDefault(x => x.Primary);
                var chooser = new GoogleAccountActionWindow(primaryAccount == null ? null : primaryAccount.Name) { Owner = this };
                if (chooser.ShowDialog() != true) return;
                ChangeGoogleAccount = chooser.SelectedAction == "change";
                LogoutGoogleAccount = chooser.SelectedAction == "logout";
                if (ChangeGoogleAccount || LogoutGoogleAccount) save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            var dataGroup = new StackPanel();
            dataGroup.Children.Add(new TextBlock { Text = "일정 관리  (JSON·ICS는 로컬 일정 · Excel CSV는 Google 포함 전체 일정)", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
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
            Action updateTopSave = delegate { topSave.Visibility = contentScroll.ComputedVerticalScrollBarVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed; };
            contentScroll.ScrollChanged += delegate { updateTopSave(); };
            contentScroll.SizeChanged += delegate { updateTopSave(); };
            var popupLayout = new Grid(); popupLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); popupLayout.RowDefinitions.Add(new RowDefinition());
            popupLayout.Children.Add(header); Grid.SetRow(contentScroll, 1); popupLayout.Children.Add(contentScroll);
            Loaded += delegate
            {
                contentScroll.MaxHeight = compactScrollHeight(Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea.Height);
                contentScroll.ApplyTemplate();
                UiRound.SoftenScrollBars(contentScroll);
                updateTopSave();
                contentScroll.Opacity = 1;
            };
            var shell = new Border { Background = Brush("#FFF8FAFC"), CornerRadius = new CornerRadius(18),
                BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Child = popupLayout };
            Content = UiRound.EmphasizePopup(shell);
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

        UIElement ColorEditor(string name, string hex, string displayName = null)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 3) };
            var title = new DockPanel { Height = 24, LastChildFill = true, Margin = new Thickness(0, 0, 0, 2), VerticalAlignment = VerticalAlignment.Center };
            var preview = new Border { Width = 42, Height = 20, CornerRadius = new CornerRadius(6),
                Background = PaletteEditorPreview(color), VerticalAlignment = VerticalAlignment.Center };
            previews[name] = preview; DockPanel.SetDock(preview, Dock.Right); title.Children.Add(preview);
            var select = new CheckBox { Tag = name, Content = (displayName ?? name) + " 색상", FontWeight = FontWeights.SemiBold,
                FontSize = 14, Foreground = PaletteEditorForeground(color), VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 7, 0), Cursor = Cursors.Hand };
            editorTitles[name] = select;
            colorSelections.Add(select);
            select.Checked += delegate { UpdateColorSelectionAvailability(); };
            select.Unchecked += delegate { UpdateColorSelectionAvailability(); };
            DockPanel.SetDock(select, Dock.Left); title.Children.Add(select); box.Children.Add(title);
            var rgbPanel = new StackPanel { Visibility = Visibility.Collapsed };
            rgbPanels.Add(rgbPanel); box.Children.Add(rgbPanel);
            var rgb = new[] { color.R, color.G, color.B }; var set = new Slider[3]; var labels = new TextBlock[3]; var channels = new TextBlock[3];
            for (var i = 0; i < 3; i++)
            {
                var row = new Grid { Height = 18 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                var channel = new TextBlock { Text = new[] { "R", "G", "B" }[i], Foreground = PaletteEditorForeground(color) }; row.Children.Add(channel); channels[i] = channel;
                var slider = new Slider { Minimum = 0, Maximum = 255, Value = rgb[i], Tag = name, Height = 17 }; Grid.SetColumn(slider, 1); row.Children.Add(slider); set[i] = slider;
                var value = new TextBlock { Text = rgb[i].ToString(), Foreground = PaletteEditorForeground(color), HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetColumn(value, 2); row.Children.Add(value); labels[i] = value;
                slider.ValueChanged += delegate { UpdatePreview(name); }; rgbPanel.Children.Add(row);
            }
            sliders[name] = set; values[name] = labels; editorChannels[name] = channels;
            var card = new Border { Background = PaletteEditorBackground(color), BorderBrush = PaletteEditorBorder(color),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(12, 5, 12, 4),
                Margin = new Thickness(4, 0, 4, 5), Child = box };
            editorCards[name] = card; UpdatePreview(name); return card;
        }

        void UpdateColorSelectionAvailability()
        {
            var full = colorSelections.Count(x => x.IsChecked == true) >= 2;
            foreach (var check in colorSelections)
            {
                var available = !full || check.IsChecked == true;
                check.IsEnabled = available; check.Opacity = available ? 1 : .38;
            }
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
        void SetHex(string name, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex); var s = sliders[name];
            s[0].Value = color.R; s[1].Value = color.G; s[2].Value = color.B; UpdatePreview(name);
        }
        static int DefaultTodayRow(int count) { return count <= 2 ? 1 : 2; }

        static Border SectionCard(UIElement child)
        {
            return new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1),
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
            arrow.SetValue(TextBlock.TextProperty, "▾"); arrow.SetValue(TextBlock.FontSizeProperty, 10.0);
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
            popupBorder.SetValue(Border.BackgroundProperty, Brushes.White); popupBorder.SetValue(Border.BorderBrushProperty, Brush("#C7D2FE"));
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
            hover.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, Brush("#F1F5F9")));
            var selected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, Brush("#EEF2FF")));
            itemTemplate.Triggers.Add(hover); itemTemplate.Triggers.Add(selected);
            var itemStyle = new Style(typeof(ComboBoxItem)); itemStyle.Setters.Add(new Setter(ComboBoxItem.TemplateProperty, itemTemplate));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brush("#334155"))); combo.ItemContainerStyle = itemStyle;
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
