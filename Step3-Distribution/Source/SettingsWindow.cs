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
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace FamilyPlanner
{
    public class SettingsWindow : Window
    {
        readonly Dictionary<string, Slider[]> sliders = new Dictionary<string, Slider[]>();
        readonly Dictionary<string, Border> previews = new Dictionary<string, Border>();
        readonly Dictionary<string, Border> editorCards = new Dictionary<string, Border>();
        readonly Dictionary<string, TextBlock[]> values = new Dictionary<string, TextBlock[]>();
        readonly List<CheckBox> colorSelections = new List<CheckBox>();
        public string BusinessColor;
        public string PersonalColor;
        public double SelectedFontSize;
        public string OrderMode;
        public bool MultiDayFirst;
        public string CategoryOrderPreset;
        public List<string> CategoryOrder;
        public bool ShowWeekNumbers;
        public bool ShowLunar;
        public string WeekRule;
        public bool PastelEventStyle;
        public int AutoSyncMinutes;
        public bool ChangeGoogleAccount;
        public bool LogoutGoogleAccount;
        public bool ImportLocalItems;
        public bool RestoreBackup;
        public bool ExportItems;
        bool selectedPastelStyle;
        readonly StackPanel fontOptions = new StackPanel { Orientation = Orientation.Horizontal };
        readonly List<Tuple<string, GoogleCalendarSetting>> sourceEditors = new List<Tuple<string, GoogleCalendarSetting>>();
        readonly Dictionary<string, CheckBox> editBoxes = new Dictionary<string, CheckBox>();

        public SettingsWindow(string business, string personal, double fontSize, string orderMode, bool multiDayFirst, bool showWeeks,
            string weekRule, bool pastelEventStyle, int autoSyncMinutes, List<GoogleCalendarSetting> sources, bool googleConnected, int localItemCount, bool showLunar, int backupCount, List<string> categoryOrder)
        {
            selectedPastelStyle = pastelEventStyle;
            Title = "온하루 설정"; Width = 620; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(26, 12, 18, 20) };
            var header = new DockPanel { Margin = new Thickness(26, 14, 12, 12) };
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"),
                Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            Round(close, 10);
            close.Click += delegate { DialogResult = false; };
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(new TextBlock { Text = "⚙  온하루 설정", FontSize = 21, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
            panel.Children.Add(new TextBlock { Text = "추천 색상 조합", Foreground = Brush("#475569"), FontSize = 12 });
            panel.Children.Add(new TextBlock { Text = "위 5개 · 선명한 조합     아래 4개 · 파스텔 조합     Google 기본 · 원래 색상",
                Foreground = Brush("#94A3B8"), FontSize = 10, Margin = new Thickness(0, 3, 0, 2) });
            var presets = new UniformGrid { Columns = 5, Margin = new Thickness(0, 3, 0, 14) };
            var names = new[] { "오션", "핫 핑크", "라임 블루", "선셋", "네온 베리", "로즈 밀크", "라벤더", "민트", "피치 스카이", "Google 기본" };
            var palettes = new[] {
                new[] { "#2563EB", "#DB2777", "#059669", "#D97706", "#0F766E", "#7C3AED", "#0284C7", "#C2410C", "#4F46E5", "#BE185D" },
                new[] { "#F20D7A", "#FF3D9A", "#7C3AED", "#EC4899", "#2563EB", "#E11D48", "#9333EA", "#0891B2", "#DB2777", "#EA580C" },
                new[] { "#65A30D", "#0284C7", "#7C3AED", "#EA580C", "#0891B2", "#DB2777", "#0F766E", "#4F46E5", "#CA8A04", "#C026D3" },
                new[] { "#E11D48", "#F97316", "#7C2D12", "#C026D3", "#0F766E", "#2563EB", "#CA8A04", "#9333EA", "#0891B2", "#BE123C" },
                new[] { "#FF1493", "#6D28D9", "#00A6A6", "#FF6B00", "#2563EB", "#E11D48", "#65A30D", "#C026D3", "#0891B2", "#D97706" },
                new[] { "#E8798E", "#F2A65A", "#69A6A6", "#8196D1", "#B58AC8", "#D98CA3", "#78B6A4", "#E0B36A", "#8EA8D8", "#C394B7" },
                new[] { "#A78BFA", "#F0A6CA", "#7EA6E0", "#F4A27C", "#8FCB9B", "#D7A1E5", "#79C8C3", "#E8BD73", "#9CB7E8", "#E58FAE" },
                new[] { "#64B5A6", "#8FC7B5", "#78A7C8", "#D9A66C", "#B795C9", "#E29A9A", "#8BBE87", "#D6B66D", "#89A6D5", "#C58AAF" },
                new[] { "#F4A38C", "#F7C58B", "#8EC5D6", "#B7A0D8", "#8FCB9B", "#E78DB0", "#78BFB3", "#DDA76D", "#91A9DC", "#C58FC2" } };
            var activeSources = (sources ?? new List<GoogleCalendarSetting>())
                .OrderBy(x => IsHoliday(x) ? 2 : x.Primary ? 0 : 1).ThenBy(x => x.Name).ToList();
            for (var i = 0; i < activeSources.Count; i++) sourceEditors.Add(Tuple.Create("google_" + i, activeSources[i]));
            var orderEntries = new List<Tuple<string, string>> { Tuple.Create("local:business", "업무일정"), Tuple.Create("local:personal", "개인일정") };
            orderEntries.AddRange(activeSources.Select(x => Tuple.Create("google:" + x.Id, "Google · " + x.Name)));
            var savedOrder = categoryOrder ?? new List<string>();
            orderEntries = orderEntries.OrderBy(x => { var p = savedOrder.IndexOf(x.Item1); return p < 0 ? 999 : p; }).ThenBy(x => x.Item2).ToList();
            CategoryOrder = orderEntries.Select(x => x.Item1).ToList();
            for (var i = 0; i < names.Length; i++)
            {
                var index = i; var option = new RadioButton { Content = names[i], GroupName = "Palette", Margin = new Thickness(2, 5, 8, 5) };
                option.Checked += delegate
                {
                    if (index == names.Length - 1)
                    {
                        foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                            SetHex(editor.Item1, string.IsNullOrWhiteSpace(editor.Item2.OriginalColor) ? editor.Item2.Color : editor.Item2.OriginalColor);
                        return;
                    }
                    selectedPastelStyle = index >= 5;
                    SetHex("업무일정", palettes[index][0]);
                    SetHex("개인일정", palettes[index][1]);
                    var colorIndex = 2;
                    foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                        SetHex(editor.Item1, palettes[index][colorIndex++ % palettes[index].Length]);
                };
                presets.Children.Add(option);
            }
            panel.Children.Add(presets);
            var colorGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 4) };
            colorGrid.Children.Add(ColorEditor("업무일정", business));
            colorGrid.Children.Add(ColorEditor("개인일정", personal));
            foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                colorGrid.Children.Add(ColorEditor(editor.Item1, string.IsNullOrWhiteSpace(editor.Item2.Color) ? "#E9799A" : editor.Item2.Color, editor.Item2.Name));
            panel.Children.Add(colorGrid);
            var swap = new Button { Content = "선택한 두 색상 교환", Height = 32, Background = Brush("#FCE7F3"),
                Foreground = Brush("#BE185D"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 2, 0, 10), Cursor = Cursors.Hand };
            Round(swap, 9);
            swap.Click += delegate
            {
                var selected = colorSelections.Where(x => x.IsChecked == true).Select(x => x.Tag.ToString()).ToList();
                if (selected.Count != 2) return;
                var first = Hex(selected[0]); SetHex(selected[0], Hex(selected[1])); SetHex(selected[1], first);
                foreach (var check in colorSelections) check.IsChecked = false;
            };
            panel.Children.Add(swap);
            foreach (var editor in sourceEditors.Where(x => IsHoliday(x.Item2))) panel.Children.Add(FixedHolidayColor(editor.Item2.Name));
            panel.Children.Add(new TextBlock { Text = "글자 크기", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
            foreach (var option in new[] { new { Name = "작게", Size = 11.0 }, new { Name = "보통", Size = 12.0 }, new { Name = "크게", Size = 14.0 } })
            {
                fontOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Size, GroupName = "FontSize",
                    IsChecked = Math.Abs(fontSize - option.Size) < .5, Margin = new Thickness(0, 0, 22, 12) });
            }
            panel.Children.Add(fontOptions);
            panel.Children.Add(new TextBlock { Text = "일정 표시 순서", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 3, 0, 7) });
            var orderRow = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            orderRow.ColumnDefinitions.Add(new ColumnDefinition()); orderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            var orderOptions = new StackPanel { Orientation = Orientation.Horizontal };
            orderOptions.Children.Add(new RadioButton { Content = "카테고리별 · 하루 종일 우선", Tag = "category", GroupName = "OrderMode",
                IsChecked = orderMode != "time", Margin = new Thickness(0, 0, 20, 0) });
            orderOptions.Children.Add(new RadioButton { Content = "전체 시간순", Tag = "time", GroupName = "OrderMode", IsChecked = orderMode == "time" });
            orderRow.Children.Add(orderOptions);
            var categoryOrderButton = new Button { Content = "☷  카테고리 순서 설정", Height = 32, Background = Brush("#E0E7FF"), Foreground = Brush("#4338CA"),
                BorderBrush = Brush("#C7D2FE"), BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand, Margin = new Thickness(8, -7, 0, 0) };
            Round(categoryOrderButton, 10);
            categoryOrderButton.Click += delegate
            {
                var ordered = CategoryOrder.Select(key => orderEntries.First(x => x.Item1 == key)).ToList();
                var window = new CategoryOrderWindow(ordered) { Owner = this };
                if (window.ShowDialog() == true) CategoryOrder = window.Result;
            };
            Grid.SetColumn(categoryOrderButton, 1); orderRow.Children.Add(categoryOrderButton); panel.Children.Add(orderRow);
            var multiDayTop = new CheckBox { Content = "연속 일정은 항상 위에 표시", IsChecked = multiDayFirst,
                Margin = new Thickness(0, -5, 0, 14), ToolTip = "체크하지 않으면 카테고리 또는 시간 설정 순서를 따릅니다." };
            panel.Children.Add(multiDayTop);
            panel.Children.Add(new TextBlock { Text = "표시 옵션", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 7) });
            var displayOptions = new Grid(); displayOptions.ColumnDefinitions.Add(new ColumnDefinition()); displayOptions.ColumnDefinitions.Add(new ColumnDefinition());
            var showWeek = new CheckBox { Content = "달력 왼쪽에 주차 표시", IsChecked = showWeeks, Margin = new Thickness(0, 0, 0, 7) };
            var lunar = new CheckBox { Content = "날짜 아래에 음력 표시", IsChecked = showLunar, Margin = new Thickness(0, 0, 0, 7) };
            displayOptions.Children.Add(showWeek); Grid.SetColumn(lunar, 1); displayOptions.Children.Add(lunar); panel.Children.Add(displayOptions);
            var weekRules = new StackPanel { Orientation = Orientation.Horizontal, IsEnabled = showWeeks };
            weekRules.Children.Add(new RadioButton { Content = "ISO · 월요일 시작", Tag = "iso", GroupName = "WeekRule",
                IsChecked = weekRule != "jan1", Margin = new Thickness(18, 0, 22, 0) });
            weekRules.Children.Add(new RadioButton { Content = "일반 · 일요일 시작", Tag = "jan1", GroupName = "WeekRule", IsChecked = weekRule == "jan1" });
            showWeek.Click += delegate { weekRules.IsEnabled = showWeek.IsChecked == true; };
            weekRules.Margin = new Thickness(0, 0, 0, 14); panel.Children.Add(weekRules);
            panel.Children.Add(new TextBlock { Text = "Google 자동 동기화", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 7) });
            var syncOptions = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            foreach (var option in new[] { new { Name = "사용 안 함", Minutes = 0 }, new { Name = "5분", Minutes = 5 },
                new { Name = "15분", Minutes = 15 }, new { Name = "30분", Minutes = 30 }, new { Name = "60분", Minutes = 60 } })
                syncOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Minutes, GroupName = "AutoSync",
                    IsChecked = autoSyncMinutes == option.Minutes, Margin = new Thickness(0, 0, 22, 5) });
            panel.Children.Add(syncOptions);
            if (activeSources.Count > 0)
            {
                panel.Children.Add(new TextBlock { Text = "Google 일정 수정 권한", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 7) });
                var permissionGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 3) };
                foreach (var editor in sourceEditors)
                {
                    var source = editor.Item2; var holiday = IsHoliday(source);
                    var canWrite = source.AccessRole == "owner" || source.AccessRole == "writer";
                    var box = new CheckBox { Content = source.Name + (holiday || !canWrite ? " · 읽기 전용" : " · 수정 가능"),
                        IsChecked = source.Editable && canWrite && !holiday, IsEnabled = canWrite && !holiday, Margin = new Thickness(0, 0, 10, 7),
                        ToolTip = source.Name + (holiday || !canWrite ? " · 읽기 전용" : " · 수정 가능") };
                    editBoxes[editor.Item1] = box; permissionGrid.Children.Add(box);
                }
                panel.Children.Add(permissionGrid);
            }
            var saveGradient = new LinearGradientBrush();
            saveGradient.StartPoint = new Point(0, .5); saveGradient.EndPoint = new Point(1, .5);
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3977E8"), 0));
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C5CE5"), 1));
            var save = new Button { Content = "✓  설정 저장", Height = 44, Background = saveGradient, Foreground = Brushes.White,
                BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 10, 0, 0), Cursor = Cursors.Hand };
            Round(save, 13);
            save.Click += delegate
            {
                BusinessColor = Hex("업무일정"); PersonalColor = Hex("개인일정");
                foreach (var editor in sourceEditors)
                {
                    editor.Item2.Color = IsHoliday(editor.Item2) ? "#CF2B36" : Hex(editor.Item1);
                    editor.Item2.Editable = editBoxes.ContainsKey(editor.Item1) && editBoxes[editor.Item1].IsChecked == true;
                }
                SelectedFontSize = (double)fontOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                OrderMode = orderOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                MultiDayFirst = multiDayTop.IsChecked == true;
                ShowWeekNumbers = showWeek.IsChecked == true;
                ShowLunar = lunar.IsChecked == true;
                WeekRule = weekRules.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                PastelEventStyle = selectedPastelStyle;
                AutoSyncMinutes = (int)syncOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                DialogResult = true;
            };
            var account = new Button { Content = "Google 계정 변경", Height = 44, Background = Brush("#E2E8F0"),
                Foreground = Brush("#334155"), BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 10, 4, 0), Cursor = Cursors.Hand };
            Round(account, 13);
            account.Click += delegate { ChangeGoogleAccount = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            if (googleConnected && localItemCount > 0)
            {
                var importLocal = new Button { Content = "로컬 일정 가져오기  ·  " + localItemCount + "개", Height = 34,
                    Background = Brush("#EEF2FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 8, 0, 0), Cursor = Cursors.Hand };
                Round(importLocal, 10);
                importLocal.Click += delegate { ImportLocalItems = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
                panel.Children.Add(importLocal);
            }
            if (backupCount > 0)
            {
                var restore = new Button { Content = "↶  백업 복원  ·  최근 " + backupCount + "개", Height = 34,
                    Background = Brush("#EEF2FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 6, 0, 0), Cursor = Cursors.Hand };
                Round(restore, 10); restore.Click += delegate { RestoreBackup = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }; panel.Children.Add(restore);
            }
            var export = new Button { Content = "⇩  일정 내보내기  ·  JSON · CSV · ICS", Height = 34,
                Background = Brush("#ECFDF5"), Foreground = Brush("#047857"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 6, 0, 0), Cursor = Cursors.Hand };
            Round(export, 10); export.Click += delegate { ExportItems = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }; panel.Children.Add(export);
            var logout = new Button { Content = "로그아웃", Height = 44, Background = Brush("#F1F5F9"), Foreground = Brush("#64748B"),
                BorderThickness = new Thickness(0), Margin = new Thickness(0, 10, 4, 0), Cursor = Cursors.Hand };
            Round(logout, 13);
            logout.Click += delegate { LogoutGoogleAccount = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            save.Margin = new Thickness(4, 10, 0, 0);
            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.72, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.14, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.14, GridUnitType.Star) });
            actions.Children.Add(logout); Grid.SetColumn(account, 1); actions.Children.Add(account); Grid.SetColumn(save, 2); actions.Children.Add(save);
            panel.Children.Add(actions);
            var contentScroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Min(930, Math.Max(360, SystemParameters.WorkArea.Height - 104)) };
            var popupLayout = new Grid(); popupLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); popupLayout.RowDefinitions.Add(new RowDefinition());
            popupLayout.Children.Add(header); Grid.SetRow(contentScroll, 1); popupLayout.Children.Add(contentScroll);
            Loaded += delegate
            {
                contentScroll.MaxHeight = Math.Min(930, Math.Max(360, Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea.Height - 104));
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { UiRound.SoftenScrollBars(contentScroll); }));
            };
            var shell = new Border { Background = Brush("#FFF8FAFC"), CornerRadius = new CornerRadius(18),
                BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Child = popupLayout };
            Content = shell;
        }

        UIElement FixedHolidayColor(string name)
        {
            var row = new DockPanel();
            var swatch = new Border { Width = 42, Height = 24, CornerRadius = new CornerRadius(7), Background = Brush("#CF2B36") };
            DockPanel.SetDock(swatch, Dock.Right); row.Children.Add(swatch);
            row.Children.Add(new TextBlock { Text = name + " 색상 · 빨간색 고정", FontWeight = FontWeights.SemiBold,
                FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#991B1B") });
            return new Border { Background = Brush("#FEF2F2"), BorderBrush = Brush("#FECACA"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 8), Child = row };
        }

        UIElement ColorEditor(string name, string hex, string displayName = null)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            var title = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var preview = new Border { Width = 42, Height = 24, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(color) };
            previews[name] = preview; DockPanel.SetDock(preview, Dock.Right); title.Children.Add(preview);
            var select = new CheckBox { Tag = name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) };
            select.Checked += delegate { if (colorSelections.Count(x => x.IsChecked == true) > 2) select.IsChecked = false; };
            colorSelections.Add(select); DockPanel.SetDock(select, Dock.Left); title.Children.Add(select);
            title.Children.Add(new TextBlock { Text = (displayName ?? name) + " 색상", FontWeight = FontWeights.SemiBold, FontSize = 14 }); box.Children.Add(title);
            var rgb = new[] { color.R, color.G, color.B }; var set = new Slider[3]; var labels = new TextBlock[3];
            for (var i = 0; i < 3; i++)
            {
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                var channel = new TextBlock { Text = new[] { "R", "G", "B" }[i], Foreground = Brush("#64748B") }; row.Children.Add(channel);
                var slider = new Slider { Minimum = 0, Maximum = 255, Value = rgb[i], Tag = name }; Grid.SetColumn(slider, 1); row.Children.Add(slider); set[i] = slider;
                var value = new TextBlock { Text = rgb[i].ToString(), HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetColumn(value, 2); row.Children.Add(value); labels[i] = value;
                slider.ValueChanged += delegate { UpdatePreview(name); }; box.Children.Add(row);
            }
            sliders[name] = set; values[name] = labels;
            var card = new Border { Background = Pastel(color, .88), BorderBrush = Pastel(color, .65),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(12, 8, 12, 7),
                Margin = new Thickness(4, 0, 4, 8), Child = box };
            editorCards[name] = card; UpdatePreview(name); return card;
        }

        void UpdatePreview(string name)
        {
            if (!sliders.ContainsKey(name)) return;
            var s = sliders[name]; var c = Color.FromRgb((byte)s[0].Value, (byte)s[1].Value, (byte)s[2].Value);
            previews[name].Background = new SolidColorBrush(c);
            if (editorCards.ContainsKey(name))
            {
                editorCards[name].Background = Pastel(c, .88);
                editorCards[name].BorderBrush = Pastel(c, .65);
            }
            for (var i = 0; i < 3; i++) values[name][i].Text = ((int)s[i].Value).ToString();
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
        static Brush Pastel(Color color, double whiteRatio)
        {
            return new SolidColorBrush(Color.FromRgb(
                (byte)(color.R + (255 - color.R) * whiteRatio),
                (byte)(color.G + (255 - color.G) * whiteRatio),
                (byte)(color.B + (255 - color.B) * whiteRatio)));
        }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }
}
