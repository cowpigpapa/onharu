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
        static readonly string[] WeekdayBackgrounds = { "#EAF4F3", "#EEF0FA", "#EEF6EC", "#FAF0E8", "#F8EAF0", "#EAF1F8", "#F8E9EC" };
        static readonly string[] WeekdayForegrounds = { "#356763", "#505584", "#536C28", "#8F4B28", "#884B60", "#3F5F85", "#9F3F46" };
        readonly TimetableData data;
        readonly List<TextBox> timeEditors = new List<TextBox>();
        readonly Dictionary<string, TextBox> slotEditors = new Dictionary<string, TextBox>();
        readonly Grid table = new Grid();
        DockPanel headerPanel;
        // 표 치수는 한 곳에서만 정의하고 창 크기 계산과 표 생성이 같은 값을 쓴다.
        const double TimeColumnWidth = 92, PreferredDayWidth = 132, MinDayWidth = 105;
        const double HeaderRowHeight = 38, PeriodRowHeight = 48;

        public TimetableWindow()
        {
            data = TimetableStorage.Load();
            // Shell에 사방 12px 그림자 여백을 둔다. 없으면 DropShadow가 창 경계에서 잘려
            // 네 모서리에 검은 자국으로 남는다. 그 여백은 아래 크기 계산에 포함돼 있다.
            // 창 크기는 표 내용에 맞춰 아래 ApplyPreferredSize가 정한다. 여기 값은 최소 한계다.
            Title = "온하루 시간표"; MinWidth = 520; MinHeight = 360;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            // 크기 조절은 메인 창·검색창과 같은 네이티브 방식을 쓴다. 아래 EnableResize가 테두리를 잡는다.
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Malgun Gothic");
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            // 왼쪽 여백 17은 의도한 값이다. FeatureHeading의 글리프가 24px 상자에 가운데 정렬돼 있어
            // `▦`의 획이 상자보다 약 3px 안쪽에서 시작한다. 상자가 아니라 획을 20px 기준선에 세운다.
            var header = new DockPanel { Margin = new Thickness(17, 9, 14, 9), Background = Brushes.Transparent };
            headerPanel = header;
            OnharuPopupChrome.StyleHeader(header);
            var close = OnharuPopupChrome.ToolCloseButton(this); close.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            // 창의 대표 실행 버튼 하나에만 브랜드 그라데이션을 쓴다. 검색창의 오늘 버튼과 같은 규격이다.
            var save = OnharuPopupChrome.Button("✓  시간표 저장", 118, "#4F46E5", "#FFFFFF");
            save.Background = OnharuPopupChrome.BrandGradientBrush(); save.Foreground = Brushes.White; save.BorderBrush = Brushes.Transparent;
            save.Height = 30; save.Margin = new Thickness(0, 0, 8, 0); save.FontWeight = FontWeights.Bold; save.VerticalAlignment = VerticalAlignment.Center;
            UiRound.Apply(save, 8);
            DockPanel.SetDock(save, Dock.Right); header.Children.Add(save);
            // 톱니는 글꼴 문자가 아니라 OnharuIcons 도형이다. 메인 헤더·설정창과 같은 그림을 쓴다.
            var settingsButton = OnharuPopupChrome.Button("", 36, "#FFFFFF", "#334155");
            settingsButton.Content = OnharuIcons.Draw("settings", Brush("#334155"), 21);
            settingsButton.Padding = new Thickness(0);
            settingsButton.Height = 30; settingsButton.Margin = new Thickness(0, 0, 8, 0);
            settingsButton.BorderBrush = Brush("#CBD5E1"); settingsButton.VerticalAlignment = VerticalAlignment.Center;
            settingsButton.ToolTip = "시간표 설정"; UiRound.Apply(settingsButton, 8);
            DockPanel.SetDock(settingsButton, Dock.Right); header.Children.Add(settingsButton);
            header.Children.Add(OnharuPopupChrome.FeatureHeading("▦", "나의 시간표"));
            OnharuPopupChrome.EnableDrag(this, header);
            root.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.GetPosition(root).Y > 48 || HasInteractiveParent(e.OriginalSource as DependencyObject)) return;
                if (Mouse.LeftButton == MouseButtonState.Pressed) { DragMove(); e.Handled = true; }
            };
            root.Children.Add(header);
            // 설정은 자식 차단 팝업으로 분리했다. 표가 밀리지 않고 세로 공간을 모두 쓴다.
            settingsButton.Click += delegate
            {
                Capture();
                var settings = new TimetableSettingsWindow(data) { Owner = this };
                if (settings.ShowDialog() != true) return;
                Rebuild(); ApplyPreferredSize();
            };
            save.Click += delegate
            {
                Capture(); TimetableStorage.Save(data);
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
            // 표를 좌우·아래로 12px 들여 팝업 면이 테두리처럼 남게 한다.
            // 이 띠가 있어야 테두리를 잡을 때 표 안 입력칸 클릭과 겹치지 않는다.
            var tableShell = new Border { Margin = new Thickness(12, 0, 12, 12), Background = Brush(OnharuPopupChrome.ContentSurfaceColor), BorderBrush = Brush("#D6DCE8"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(0), Child = scroll };
            tableShell.SizeChanged += delegate
            {
                scroll.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, scroll.ActualWidth), Math.Max(0, scroll.ActualHeight)), 11, 11);
            };
            Grid.SetRow(tableShell, 1); root.Children.Add(tableShell); Rebuild(); ApplyPreferredSize();
            var shell = OnharuPopupChrome.Shell(root);
            shell.Margin = new Thickness(12);
            OnharuPopupChrome.EnableResize(this, shell);
            Content = shell;
        }

        // 창 크기를 표 내용에 맞춘다. 교시 수와 표시 요일이 바뀌면 남는 여백도 스크롤도 없게 다시 맞춘다.
        // 제목줄 높이는 추정하지 않고 실제로 잰다. 글꼴·DPI·버튼 높이가 바뀌면 값이 달라지기 때문이다.
        // 나머지는 고정 요소다. 그림자 여백 12×2, 셸 테두리 2×2(UiRound.EmphasizePopup이 2로 덮어쓴다),
        // 표 카드 여백 좌우 12+12·아래 12, 표 카드 테두리 1×2.
        const double ShellMargin = 12, ShellBorder = 2, TableCardMargin = 12, TableCardBorder = 1, SizeSlack = 2;

        void ApplyPreferredSize()
        {
            var dayCount = data.VisibleDays.Count == 0 ? 6 : data.VisibleDays.Count;
            var tableWidth = TimeColumnWidth + dayCount * PreferredDayWidth;
            var tableHeight = HeaderRowHeight + data.PeriodCount * PeriodRowHeight;
            var chrome = 2 * (ShellMargin + ShellBorder + TableCardMargin + TableCardBorder);
            headerPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var headerHeight = headerPanel.DesiredSize.Height;
            var area = SystemParameters.WorkArea;
            var width = tableWidth + chrome + SizeSlack;
            var height = tableHeight + 2 * (ShellMargin + ShellBorder) + headerHeight
                + TableCardMargin + 2 * TableCardBorder + SizeSlack;
            Width = Math.Min(width, Math.Max(MinWidth, area.Width - 48));
            Height = Math.Min(height, Math.Max(MinHeight, area.Height - 48));
        }

        void Rebuild()
        {
            timeEditors.Clear(); slotEditors.Clear(); table.Children.Clear(); table.RowDefinitions.Clear(); table.ColumnDefinitions.Clear();
            var days = data.VisibleDays.Count == 0 ? new List<int> { 0, 1, 2, 3, 4, 5 } : data.VisibleDays; table.Background = Brush(OnharuPopupChrome.ContentSurfaceColor); table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeColumnWidth) });
            foreach (var day in days) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = MinDayWidth }); table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderRowHeight) });
            for (var p = 0; p < data.PeriodCount; p++) table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(PeriodRowHeight) });
            Cell(Center("시간", "#475569"), 0, 0, "#F1F5F9"); var names = new[] { "월요일", "화요일", "수요일", "목요일", "금요일", "토요일", "일요일" };
            for (var col = 0; col < days.Count; col++) Cell(Center(names[days[col]], WeekdayForegrounds[days[col]]), col + 1, 0, WeekdayBackgrounds[days[col]]);
            while (data.Times.Count < data.PeriodCount) data.Times.Add(TimetableStorage.DefaultTime(data, data.Times.Count));
            for (var p = 0; p < data.PeriodCount; p++) { var time = Editor(data.Times[p], TextAlignment.Center, Math.Max(11.5, data.FontSize - 1)); timeEditors.Add(time); var timeCell = new Grid(); timeCell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); timeCell.RowDefinitions.Add(new RowDefinition()); var period = new TextBlock { Text = (p + 1) + "교시", FontSize = Math.Max(10.5, data.FontSize - 2), FontWeight = FontWeights.SemiBold, Foreground = Brush("#356763"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, -1) }; timeCell.Children.Add(period); Grid.SetRow(time, 1); timeCell.Children.Add(time); Cell(timeCell, 0, p + 1, p % 2 == 0 ? "#F8FAFC" : "#F1F5F9"); for (var col = 0; col < days.Count; col++) { var day = days[col]; var slot = data.Slots.FirstOrDefault(x => x.Day == day && x.Period == p); var editor = Editor(slot == null ? "" : slot.Text, TextAlignment.Center, data.FontSize); editor.AcceptsReturn = true; editor.TextWrapping = TextWrapping.Wrap; slotEditors[day + ":" + p] = editor; Cell(editor, col + 1, p + 1, p % 2 == 0 ? "#FFFFFF" : "#FAFAFC"); } }
        }

        void Capture() { if (timeEditors.Count > 0) data.Times = timeEditors.Select(x => x.Text.Trim()).ToList(); foreach (var pair in slotEditors) { var parts = pair.Key.Split(':'); var day = int.Parse(parts[0]); var period = int.Parse(parts[1]); data.Slots.RemoveAll(x => x.Day == day && x.Period == period); var text = pair.Value.Text.Trim(); if (text.Length > 0) data.Slots.Add(new TimetableSlot { Day = day, Period = period, Text = text }); } }
        void FreezeTableHeaders(ScrollViewer scroll) { foreach (UIElement child in table.Children) { var row = Grid.GetRow(child); var column = Grid.GetColumn(child); if (row == 0 || column == 0) { child.RenderTransform = new TranslateTransform(column == 0 ? scroll.HorizontalOffset : 0, row == 0 ? scroll.VerticalOffset : 0); Panel.SetZIndex(child, row == 0 && column == 0 ? 3 : 2); } } }
        void Cell(UIElement child, int column, int row, string background) { var border = new Border { Background = Brush(background), BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(0, 0, column == table.ColumnDefinitions.Count - 1 ? 0 : 1, row == table.RowDefinitions.Count - 1 ? 0 : 1), Child = child }; Grid.SetColumn(border, column); Grid.SetRow(border, row); table.Children.Add(border); }
        static TextBox Editor(string text, TextAlignment alignment, double fontSize) { var editor = new TextBox { Text = text ?? "", BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(7, 4, 7, 4), TextAlignment = alignment, VerticalContentAlignment = VerticalAlignment.Center, Foreground = Brush("#334155"), FontSize = fontSize, Cursor = Cursors.IBeam, SelectionBrush = Brush("#C7D2FE") }; UiRound.SelectAllOnFocus(editor, true); return editor; }
        static TextBlock Center(string text, string color) { return new TextBlock { Text = text, FontWeight = FontWeights.Bold, Foreground = Brush(color), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; }
        static bool HasInteractiveParent(DependencyObject source) { while (source != null) { if (source is Button || source is ComboBox || source is TextBox || source is CheckBox || source is RadioButton) return true; source = VisualTreeHelper.GetParent(source); } return false; }
        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    // 시간표 설정. POPUP_POLICY 3장 자식 차단 팝업이다. 부모 창의 Owner를 지정하고 ShowDialog()를 쓴다.
    // 설정을 본 창에서 분리해 표가 밀리지 않고 세로 공간을 모두 쓰게 한다.
    public sealed class TimetableSettingsWindow : Window
    {
        static readonly string[] WeekdayForegrounds = { "#356763", "#505584", "#536C28", "#8F4B28", "#884B60", "#3F5F85", "#9F3F46" };
        readonly TimetableData data;
        readonly List<CheckBox> dayBoxes = new List<CheckBox>();
        readonly ComboBox periodCount = new ComboBox { Width = 82, Height = 30, Background = Brushes.White, BorderBrush = Brush("#CBD5E1") };
        readonly TextBox startTime = Box(82);
        readonly ComboBox lessonMinutes = new ComboBox { Width = 82, Height = 30, Background = Brushes.White, BorderBrush = Brush("#CBD5E1") };
        readonly ComboBox breakMinutes = new ComboBox { Width = 82, Height = 30, Background = Brushes.White, BorderBrush = Brush("#CBD5E1") };
        readonly StackPanel fontOptions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        public TimetableSettingsWindow(TimetableData source)
        {
            data = source;
            // 본체 470×298 + 그림자 여백 12px. 폭은 표시 요일 7칸이 한 줄에 들어가는 값이다.
            Title = "시간표 설정"; Width = 494; Height = 322;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Malgun Gothic");

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new DockPanel { Margin = new Thickness(17, 9, 14, 9), Background = Brushes.Transparent };
            OnharuPopupChrome.StyleHeader(header);
            var close = OnharuPopupChrome.ToolCloseButton(this);
            close.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(OnharuPopupChrome.FeatureHeading("▦", "시간표 설정"));
            OnharuPopupChrome.EnableDrag(this, header);
            Grid.SetRow(header, 0); root.Children.Add(header);

            var form = new StackPanel();
            var days = new StackPanel { Orientation = Orientation.Horizontal, Height = 36 };
            days.Children.Add(Label("표시 요일"));
            var names = new[] { "월", "화", "수", "목", "금", "토", "일" };
            for (var day = 0; day < 7; day++)
            {
                // 여기서 색은 장식이 아니라 주말 구분이다. 메인 달력과 같은 규칙으로 토요일은 파랑,
                // 일요일은 빨강만 쓰고 월~금은 공통 글자색으로 둔다. 표 머리글의 요일별 색조는 그대로다.
                var weekend = day == 5 ? "#2563EB" : day == 6 ? "#DC2626" : "#334155";
                var box = new CheckBox { Content = names[day], Tag = day, IsChecked = data.VisibleDays.Contains(day),
                    Margin = new Thickness(0, 0, day == 6 ? 0 : 13, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brush(weekend) };
                dayBoxes.Add(box); days.Children.Add(box);
            }
            form.Children.Add(days);

            var timeRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 36 };
            timeRow.Children.Add(Label("교시"));
            for (var i = 1; i <= 12; i++) periodCount.Items.Add(Item(i + "개", i));
            periodCount.SelectedIndex = data.PeriodCount - 1; SettingsWindow.StyleComboBox(periodCount); timeRow.Children.Add(periodCount);
            timeRow.Children.Add(Label("시작", 48, 16));
            startTime.Text = data.StartHour.ToString("00") + ":" + data.StartMinute.ToString("00");
            OnharuTimeInput.Attach(startTime, new TimeSpan(9, 0, 0));
            timeRow.Children.Add(startTime);
            form.Children.Add(timeRow);

            var lengthRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 36 };
            lengthRow.Children.Add(Label("수업"));
            foreach (var v in new[] { 30, 40, 45, 50, 60, 75, 90, 120 }) lessonMinutes.Items.Add(Item(v + "분", v));
            Select(lessonMinutes, data.LessonMinutes); SettingsWindow.StyleComboBox(lessonMinutes); lengthRow.Children.Add(lessonMinutes);
            lengthRow.Children.Add(Label("쉬는 시간", 66, 16));
            foreach (var v in new[] { 0, 5, 10, 15, 20, 30 }) breakMinutes.Items.Add(Item(v + "분", v));
            Select(breakMinutes, data.BreakMinutes); SettingsWindow.StyleComboBox(breakMinutes); lengthRow.Children.Add(breakMinutes);
            form.Children.Add(lengthRow);

            var fontRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 36 };
            fontRow.Children.Add(Label("글자 크기"));
            foreach (var option in new[] { Item("작게", 115), Item("보통", 130), Item("크게", 150) })
            {
                var size = (int)option.Tag / 10.0;
                fontOptions.Children.Add(new RadioButton { Content = option.Content, Tag = size, GroupName = "TimetableFontSize",
                    IsChecked = Math.Abs(data.FontSize - size) < .2, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            fontRow.Children.Add(fontOptions); form.Children.Add(fontRow);

            var card = new Border { Margin = new Thickness(12, 0, 12, 12), Background = Brush(OnharuPopupChrome.ContentSurfaceColor),
                BorderBrush = Brush("#D6DCE8"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
                Padding = new Thickness(7, 8, 12, 8), Child = form };
            Grid.SetRow(card, 1); root.Children.Add(card);

            var footer = new DockPanel { Margin = new Thickness(20, 0, 20, 16), LastChildFill = false };
            // 창의 대표 실행 버튼 하나에만 브랜드 그라데이션을 쓴다.
            var apply = OnharuPopupChrome.Button("적용", 82, "#4F46E5", "#FFFFFF");
            apply.Background = OnharuPopupChrome.BrandGradientBrush(); apply.Foreground = Brushes.White; apply.BorderBrush = Brushes.Transparent;
            apply.Height = 32; apply.FontWeight = FontWeights.Bold; UiRound.Apply(apply, 8);
            apply.Click += delegate { Apply(); DialogResult = true; };
            DockPanel.SetDock(apply, Dock.Right); footer.Children.Add(apply);
            var cancel = OnharuPopupChrome.Button("취소", 66, "#FFFFFF", "#334155");
            cancel.Height = 32; cancel.BorderBrush = Brush("#CBD5E1"); cancel.Margin = new Thickness(0, 0, 8, 0); UiRound.Apply(cancel, 8);
            cancel.Click += delegate { Close(); };
            DockPanel.SetDock(cancel, Dock.Right); footer.Children.Add(cancel);
            Grid.SetRow(footer, 2); root.Children.Add(footer);

            var shell = OnharuPopupChrome.Shell(root);
            shell.Margin = new Thickness(12);
            Content = shell;
        }

        void Apply()
        {
            var days = dayBoxes.Where(x => x.IsChecked == true).Select(x => (int)x.Tag).ToList();
            if (days.Count == 0) { days.Add(0); dayBoxes[0].IsChecked = true; }
            data.VisibleDays = days;
            data.PeriodCount = (int)((ComboBoxItem)periodCount.SelectedItem).Tag;
            var start = OnharuTimeInput.Normalize(startTime, new TimeSpan(9, 0, 0));
            data.StartHour = start.Hours; data.StartMinute = start.Minutes;
            data.LessonMinutes = SelectedTag(lessonMinutes); data.BreakMinutes = SelectedTag(breakMinutes);
            var selectedFont = fontOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true);
            if (selectedFont != null) data.FontSize = (double)selectedFont.Tag;
            data.Times.Clear();
            for (var i = 0; i < data.PeriodCount; i++) data.Times.Add(TimetableStorage.DefaultTime(data, i));
        }

        static TextBlock Label(string text) { return Label(text, 76, 0); }
        static TextBlock Label(string text, double width, double left)
        {
            return new TextBlock { Text = text, Width = width, FontSize = 12.5, FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#475569"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(left, 0, 0, 0) };
        }
        static TextBox Box(double width)
        {
            // 짧은 값만 받는 칸이라 가운데로 세운다. 알람의 시각 칸과 같은 규칙이다.
            var box = new TextBox { Width = width, Height = 30, Padding = new Thickness(9, 4, 9, 3), Background = Brushes.White,
                BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center, SelectionBrush = Brush("#C7D2FE") };
            UiRound.StyleTextBox(box, 8); return box;
        }
        static ComboBoxItem Item(string text, int tag) { return new ComboBoxItem { Content = text, Tag = tag }; }
        static int SelectedTag(ComboBox combo) { return (int)((ComboBoxItem)combo.SelectedItem).Tag; }
        static void Select(ComboBox combo, int value) { foreach (ComboBoxItem item in combo.Items) if ((int)item.Tag == value) { combo.SelectedItem = item; return; } combo.SelectedIndex = 0; }
        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
