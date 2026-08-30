using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class TimetableWindow : Window
    {
        readonly TimetableData data;
        readonly List<TextBox> timeEditors = new List<TextBox>();
        readonly Dictionary<string, TextBox> slotEditors = new Dictionary<string, TextBox>();
        readonly Grid table = new Grid();
        readonly StackPanel settingsPanel = new StackPanel();
        readonly List<CheckBox> dayBoxes = new List<CheckBox>();
        readonly ComboBox periodCount = new ComboBox { Width = 68, Height = 28 };
        readonly TextBox startTime = Box(62);
        readonly ComboBox lessonMinutes = new ComboBox { Width = 72, Height = 28 };
        readonly ComboBox breakMinutes = new ComboBox { Width = 72, Height = 28 };
        readonly StackPanel fontOptions = new StackPanel { Orientation = Orientation.Horizontal };

        public TimetableWindow()
        {
            data = TimetableStorage.Load();
            Title = "온하루 시간표"; Width = 900; Height = 568; MinWidth = 720; MinHeight = 500;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Malgun Gothic");
            var root = new Grid { Margin = new Thickness(22, 16, 18, 18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8), Background = Brushes.Transparent };
            OnharuPopupChrome.StyleHeader(header);
            var close = OnharuPopupChrome.ToolCloseButton(this); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var save = OnharuPopupChrome.ActionButton("✓  시간표 저장", 118);
            save.Height = 32; save.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(save, Dock.Right); header.Children.Add(save);
            var toggle = OnharuPopupChrome.DisclosureButton("시간표 설정", 112, false); toggle.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(toggle, Dock.Right); header.Children.Add(toggle);
            header.Children.Add(OnharuPopupChrome.FeatureTitle("▦", "나의 시간표"));
            OnharuPopupChrome.EnableDrag(this, header);
            root.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.GetPosition(root).Y > 48 || HasInteractiveParent(e.OriginalSource as DependencyObject)) return;
                if (Mouse.LeftButton == MouseButtonState.Pressed) { DragMove(); e.Handled = true; }
            };
            root.Children.Add(header);
            BuildSettings(); settingsPanel.Visibility = Visibility.Collapsed; Grid.SetRow(settingsPanel, 1); root.Children.Add(settingsPanel);
            toggle.Click += delegate { var open = settingsPanel.Visibility != Visibility.Visible; settingsPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed; OnharuPopupChrome.SetDisclosure(toggle, "시간표 설정", open); };
            save.Click += delegate
            {
                if (settingsPanel.Visibility == Visibility.Visible) Apply(); else Capture();
                Capture(); TimetableStorage.Save(data);
                settingsPanel.Visibility = Visibility.Collapsed; OnharuPopupChrome.SetDisclosure(toggle, "시간표 설정", false);
                save.Content = "✓  저장 완료";
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                timer.Tick += delegate { timer.Stop(); save.Content = "✓  시간표 저장"; };
                timer.Start();
            };
            var scroll = new ScrollViewer { Content = table, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, PanningMode = PanningMode.Both };
            scroll.Resources["OnharuScrollThumb"] = Brush("#B7ACE8");
            scroll.Resources["OnharuScrollTrack"] = Brush("#F1F5F9");
            scroll.ScrollChanged += delegate { FreezeTableHeaders(scroll); };
            scroll.Loaded += delegate { UiRound.SoftenScrollBars(scroll); };
            var tableShell = new Border { Background = Brush("#FAFAFF"), BorderBrush = Brush("#D5D8DE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(0), Child = scroll };
            tableShell.SizeChanged += delegate
            {
                scroll.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, scroll.ActualWidth), Math.Max(0, scroll.ActualHeight)), 10, 10);
            };
            Grid.SetRow(tableShell, 2); root.Children.Add(tableShell); Rebuild();
            Content = OnharuPopupChrome.Shell(root);
        }

        void BuildSettings()
        {
            settingsPanel.Margin = new Thickness(0, 0, 0, 10); var panel = new StackPanel();
            var days = new StackPanel { Orientation = Orientation.Horizontal, Height = 32 }; days.Children.Add(Label("표시 요일", 84));
            var names = new[] { "월", "화", "수", "목", "금", "토", "일" };
            for (var day = 0; day < 7; day++) { var box = new CheckBox { Content = names[day], Tag = day, IsChecked = data.VisibleDays.Contains(day), Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = day == 6 ? Brush("#DC2626") : day == 5 ? Brush("#2563EB") : Brush("#334155") }; dayBoxes.Add(box); days.Children.Add(box); }
            panel.Children.Add(days);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 34 }; row.Children.Add(Label("시간 설정", 84)); row.Children.Add(Label("교시", 31));
            for (var i = 1; i <= 12; i++) periodCount.Items.Add(Item(i + "개", i)); periodCount.SelectedIndex = data.PeriodCount - 1; SettingsWindow.StyleComboBox(periodCount); row.Children.Add(periodCount);
            row.Children.Add(Gap()); row.Children.Add(Label("시작", 34)); startTime.Text = data.StartHour.ToString("00") + ":" + data.StartMinute.ToString("00"); row.Children.Add(startTime);
            row.Children.Add(Gap()); row.Children.Add(Label("수업", 34)); foreach (var v in new[] { 30, 40, 45, 50, 60, 75, 90, 120 }) lessonMinutes.Items.Add(Item(v + "분", v)); Select(lessonMinutes, data.LessonMinutes); SettingsWindow.StyleComboBox(lessonMinutes); row.Children.Add(lessonMinutes);
            row.Children.Add(Gap()); row.Children.Add(Label("쉬는 시간", 58)); foreach (var v in new[] { 0, 5, 10, 15, 20, 30 }) breakMinutes.Items.Add(Item(v + "분", v)); Select(breakMinutes, data.BreakMinutes); SettingsWindow.StyleComboBox(breakMinutes); row.Children.Add(breakMinutes);
            var apply = OnharuPopupChrome.Button("적용", 58, OnharuPopupChrome.SupportSurfaceColor, "#334155"); apply.Height = 28; apply.Margin = new Thickness(10, 0, 0, 0); apply.FontWeight = FontWeights.SemiBold; apply.Click += delegate { Apply(); }; row.Children.Add(apply); panel.Children.Add(row);
            var fontRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 30 };
            fontRow.Children.Add(Label("글자 크기", 84));
            foreach (var option in new[] { Item("작게", 115), Item("보통", 130), Item("크게", 150) })
            {
                var size = (int)option.Tag / 10.0;
                fontOptions.Children.Add(new RadioButton { Content = option.Content, Tag = size, GroupName = "TimetableFontSize",
                    IsChecked = Math.Abs(data.FontSize - size) < .2, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            fontRow.Children.Add(fontOptions); panel.Children.Add(fontRow);
            settingsPanel.Children.Add(new Border { Background = Brush(OnharuPopupChrome.SupportSurfaceColor), BorderBrush = Brush("#D5D8DE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 9, 12, 8), Child = panel });
        }

        void Apply()
        {
            Capture(); var days = dayBoxes.Where(x => x.IsChecked == true).Select(x => (int)x.Tag).ToList(); if (days.Count == 0) { days.Add(0); dayBoxes[0].IsChecked = true; } data.VisibleDays = days;
            data.PeriodCount = (int)((ComboBoxItem)periodCount.SelectedItem).Tag; TimeSpan start;
            if (!TimeSpan.TryParse(startTime.Text.Trim(), out start) || start.TotalHours >= 24) { start = new TimeSpan(9, 0, 0); startTime.Text = "09:00"; }
            data.StartHour = start.Hours; data.StartMinute = start.Minutes; data.LessonMinutes = SelectedTag(lessonMinutes); data.BreakMinutes = SelectedTag(breakMinutes);
            var selectedFont = fontOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true);
            if (selectedFont != null) data.FontSize = (double)selectedFont.Tag;
            data.Times.Clear(); for (var i = 0; i < data.PeriodCount; i++) data.Times.Add(TimetableStorage.DefaultTime(data, i)); Rebuild();
        }

        void Rebuild()
        {
            timeEditors.Clear(); slotEditors.Clear(); table.Children.Clear(); table.RowDefinitions.Clear(); table.ColumnDefinitions.Clear();
            var days = data.VisibleDays.Count == 0 ? new List<int> { 0, 1, 2, 3, 4, 5 } : data.VisibleDays; table.Background = Brush("#FAFAFF"); table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            foreach (var day in days) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 105 }); table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            for (var p = 0; p < data.PeriodCount; p++) table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            Cell(Center("시간", "#475569"), 0, 0, "#F1F5F9"); var names = new[] { "월요일", "화요일", "수요일", "목요일", "금요일", "토요일", "일요일" }; var colors = new[] { "#ECFEFF", "#EEF2FF", "#F0FDF4", "#FFF7ED", "#FDF2F8", "#EFF6FF", "#FEF2F2" };
            for (var col = 0; col < days.Count; col++) Cell(Center(names[days[col]], days[col] == 6 ? "#DC2626" : days[col] == 5 ? "#2563EB" : "#0F766E"), col + 1, 0, colors[days[col]]);
            while (data.Times.Count < data.PeriodCount) data.Times.Add(TimetableStorage.DefaultTime(data, data.Times.Count));
            for (var p = 0; p < data.PeriodCount; p++) { var time = Editor(data.Times[p], TextAlignment.Center, Math.Max(11.5, data.FontSize - 1)); timeEditors.Add(time); var timeCell = new Grid(); timeCell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); timeCell.RowDefinitions.Add(new RowDefinition()); var period = new TextBlock { Text = (p + 1) + "교시", FontSize = Math.Max(10.5, data.FontSize - 2), FontWeight = FontWeights.SemiBold, Foreground = Brush("#6366F1"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, -1) }; timeCell.Children.Add(period); Grid.SetRow(time, 1); timeCell.Children.Add(time); Cell(timeCell, 0, p + 1, p % 2 == 0 ? "#F8FAFC" : "#F1F5F9"); for (var col = 0; col < days.Count; col++) { var day = days[col]; var slot = data.Slots.FirstOrDefault(x => x.Day == day && x.Period == p); var editor = Editor(slot == null ? "" : slot.Text, TextAlignment.Center, data.FontSize); editor.AcceptsReturn = true; editor.TextWrapping = TextWrapping.Wrap; slotEditors[day + ":" + p] = editor; Cell(editor, col + 1, p + 1, p % 2 == 0 ? "#FFFFFF" : "#FCFCFF"); } }
        }

        void Capture() { if (timeEditors.Count > 0) data.Times = timeEditors.Select(x => x.Text.Trim()).ToList(); foreach (var pair in slotEditors) { var parts = pair.Key.Split(':'); var day = int.Parse(parts[0]); var period = int.Parse(parts[1]); data.Slots.RemoveAll(x => x.Day == day && x.Period == period); var text = pair.Value.Text.Trim(); if (text.Length > 0) data.Slots.Add(new TimetableSlot { Day = day, Period = period, Text = text }); } }
        void FreezeTableHeaders(ScrollViewer scroll) { foreach (UIElement child in table.Children) { var row = Grid.GetRow(child); var column = Grid.GetColumn(child); if (row == 0 || column == 0) { child.RenderTransform = new TranslateTransform(column == 0 ? scroll.HorizontalOffset : 0, row == 0 ? scroll.VerticalOffset : 0); Panel.SetZIndex(child, row == 0 && column == 0 ? 3 : 2); } } }
        void Cell(UIElement child, int column, int row, string background) { var border = new Border { Background = Brush(background), BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(0, 0, column == table.ColumnDefinitions.Count - 1 ? 0 : 1, row == table.RowDefinitions.Count - 1 ? 0 : 1), Child = child }; Grid.SetColumn(border, column); Grid.SetRow(border, row); table.Children.Add(border); }
        static TextBox Editor(string text, TextAlignment alignment, double fontSize) { return new TextBox { Text = text ?? "", BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(7, 4, 7, 4), TextAlignment = alignment, VerticalContentAlignment = VerticalAlignment.Center, Foreground = Brush("#334155"), FontSize = fontSize, Cursor = Cursors.IBeam, SelectionBrush = Brush("#C7D2FE") }; }
        static TextBox Box(double width) { var box = new TextBox { Width = width, Height = 28, Padding = new Thickness(7, 4, 7, 3), Background = Brushes.White, BorderBrush = Brush("#D5D8DE"), BorderThickness = new Thickness(1), VerticalContentAlignment = VerticalAlignment.Center, SelectionBrush = Brush("#C7D2FE") }; UiRound.StyleTextBox(box, 8); return box; }
        static TextBlock Label(string text, double width) { return new TextBlock { Text = text, Width = width, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center }; }
        static TextBlock Center(string text, string color) { return new TextBlock { Text = text, FontWeight = FontWeights.Bold, Foreground = Brush(color), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; }
        static Border Gap() { return new Border { Width = 10 }; }
        static ComboBoxItem Item(string text, int tag) { return new ComboBoxItem { Content = text, Tag = tag }; }
        static int SelectedTag(ComboBox combo) { return (int)((ComboBoxItem)combo.SelectedItem).Tag; }
        static void Select(ComboBox combo, int value) { foreach (ComboBoxItem item in combo.Items) if ((int)item.Tag == value) { combo.SelectedItem = item; return; } combo.SelectedIndex = 0; }
        static bool HasInteractiveParent(DependencyObject source) { while (source != null) { if (source is Button || source is ComboBox || source is TextBox || source is CheckBox || source is RadioButton) return true; source = VisualTreeHelper.GetParent(source); } return false; }
        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
