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
    public class SearchWindow : Window
    {
        readonly StackPanel results = new StackPanel(); readonly TextBox query = new TextBox { Height = 30, FontSize = 12.5 };
        readonly ScrollViewer resultScroller;
        readonly OnharuSegmentedSwitch range;
        readonly string[] rangeModes;
        readonly StackPanel customRangeRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 30, Margin = new Thickness(20, 0, 20, 8), Visibility = Visibility.Collapsed };
        readonly DatePicker customFrom = new DatePicker { Width = 142, Height = 30, SelectedDate = DateTime.Today.AddMonths(-1) };
        readonly DatePicker customTo = new DatePicker { Width = 142, Height = 30, SelectedDate = DateTime.Today.AddMonths(1) };
        // 건수는 범위 슬라이딩 버튼의 아래선에 맞춰 세운다. 가운데 정렬하면 버튼보다 떠 보인다.
        // 오른쪽 여백 20px은 결과 목록 글자의 끝선과 같은 기준선이다.
        readonly TextBlock resultCount = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(9, 0, 0, 3), TextTrimming = TextTrimming.CharacterEllipsis };
        // 슬라이딩 버튼 애니메이션은 130ms다. 그 사이에 목록을 다시 그리면 UI 스레드가 막혀
        // 손잡이가 끊겨 보이므로, 애니메이션이 끝난 뒤로 렌더를 미룬다. 연속 클릭도 함께 묶인다.
        readonly DispatcherTimer slideSettle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(170) };
        FrameworkElement todayAnchor;
        int renderRequestVersion;
        readonly List<PlannerItem> source; public PlannerItem SelectedItem;
        public SearchWindow(List<PlannerItem> items)
        {
            // 창 크기는 팝업 본체 520×540에 그림자 여백 12px을 사방으로 더한 값이다.
            // 여백이 없으면 Shell의 DropShadow가 창 경계에서 잘려 네 모서리에 검은 자국으로 남는다.
            source = items; Title = "일정 검색"; Width = 544; Height = 564; MinWidth = 464; MinHeight = 404;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            // 크기 조절은 메인 창과 같은 네이티브 방식을 쓴다. 아래 EnableResize가 테두리를 잡는다.
            ResizeMode = ResizeMode.NoResize;
            var panel = new Grid();
            foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto })
                panel.RowDefinitions.Add(new RowDefinition { Height = height });
            panel.RowDefinitions.Add(new RowDefinition());

            // 제목·검색 입력·오늘·닫기를 한 행에 모아 결과 목록에 세로 공간을 넘긴다.
            // 왼쪽 여백이 20이 아니라 14인 것은 의도한 값이다. FeatureHeading의 글리프는 24px 상자에
            // 가운데 정렬돼 획이 상자보다 약 6px 안쪽에서 시작한다. 상자가 아니라 획을 20px 기준선에
            // 세워야 제목·범위 버튼·결과 글자가 한 줄로 읽힌다.
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(14, 9, 14, 9) };
            OnharuPopupChrome.StyleHeader(header);
            var close = OnharuPopupChrome.ToolCloseButton(this); close.Margin = new Thickness(7, 0, 0, 0);
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var todayButton = OnharuPopupChrome.Button("◎  오늘", 68, OnharuPopupChrome.TodaySurfaceColor, OnharuPopupChrome.TodayTextColor);
            todayButton.Height = 30; todayButton.Margin = new Thickness(9, 0, 0, 0); todayButton.FontWeight = FontWeights.SemiBold;
            todayButton.Background = OnharuPopupChrome.BrandGradientBrush(); todayButton.Foreground = Brushes.White; todayButton.BorderBrush = Brushes.Transparent;
            todayButton.ToolTip = "결과에서 오늘 위치로 이동";
            UiRound.Apply(todayButton, 8); todayButton.Click += delegate { ScrollToToday(); };
            DockPanel.SetDock(todayButton, Dock.Right); header.Children.Add(todayButton);
            var titleGroup = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var headerTitle = OnharuPopupChrome.FeatureHeading("⌕", "검색");
            headerTitle.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; titleGroup.Children.Add(headerTitle);
            DockPanel.SetDock(titleGroup, Dock.Left); header.Children.Add(titleGroup);
            query.BorderThickness = new Thickness(0); query.Background = Brushes.Transparent; query.Padding = new Thickness(10, 0, 10, 0);
            query.VerticalContentAlignment = VerticalAlignment.Center; UiRound.SelectAllOnFocus(query);
            var queryHint = new TextBlock { Text = "일정 제목·메모 검색", FontSize = 12.5, Foreground = Brush("#94A3B8"), IsHitTestVisible = false,
                Margin = new Thickness(11, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            query.TextChanged += delegate { queryHint.Visibility = query.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            var queryShell = new Grid(); queryShell.Children.Add(query); queryShell.Children.Add(queryHint);
            header.Children.Add(new Border { Child = queryShell, Height = 30, Margin = new Thickness(11, 0, 0, 0),
                Background = Brush(OnharuPopupChrome.ContentSurfaceColor), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) });
            Grid.SetRow(header, 0); panel.Children.Add(header);

            // 범위는 드롭다운 대신 공통 슬라이딩 버튼으로 항상 펼쳐 둔다.
            var options = new[] { Tuple.Create("±1년", "around"), Tuple.Create("과거", "past"), Tuple.Create("앞으로", "future"), Tuple.Create("지정", "custom"), Tuple.Create("전체", "all") };
            rangeModes = options.Select(x => x.Item2).ToArray();
            var labels = options.Select(x => x.Item1).ToArray();
            slideSettle.Tick += delegate { slideSettle.Stop(); ScheduleRender(); };
            range = new OnharuSegmentedSwitch(labels, new[] { 70.0, 70.0, 70.0, 70.0, 70.0 }, 0, delegate(int index)
            {
                customRangeRow.Visibility = rangeModes[index] == "custom" ? Visibility.Visible : Visibility.Collapsed;
                if (!IsLoaded) return;
                slideSettle.Stop(); slideSettle.Start();
            });
            range.SetPalette(Brush("#EDE9FE"), Brush("#6D28D9"), Brush("#F8FAFC"), Brush("#64748B"), Brush("#C4B5FD"));
            var rangeRow = new DockPanel { Margin = new Thickness(20, 9, 20, 8), LastChildFill = false };
            DockPanel.SetDock(range, Dock.Left); rangeRow.Children.Add(range);
            DockPanel.SetDock(resultCount, Dock.Right); rangeRow.Children.Add(resultCount);
            Grid.SetRow(rangeRow, 1); panel.Children.Add(rangeRow);

            customRangeRow.Children.Add(customFrom);
            customRangeRow.Children.Add(new TextBlock { Text = "~", Margin = new Thickness(7, 0, 7, 0), Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            customRangeRow.Children.Add(customTo);
            StyleDatePicker(customFrom); StyleDatePicker(customTo);
            customFrom.SelectedDateChanged += delegate { if (IsLoaded && customRangeRow.Visibility == Visibility.Visible) ScheduleRender(); };
            customTo.SelectedDateChanged += delegate { if (IsLoaded && customRangeRow.Visibility == Visibility.Visible) ScheduleRender(); };
            Grid.SetRow(customRangeRow, 2); panel.Children.Add(customRangeRow);

            resultScroller = new ScrollViewer { Content = results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brush(OnharuPopupChrome.ContentSurfaceColor) };
            resultScroller.Resources["OnharuScrollThumb"] = Brush("#B7ACE8");
            resultScroller.Resources["OnharuScrollTrack"] = Brush("#F1F5F9");
            // 왼쪽 기준선을 20px 하나로 통일한다. 제목·범위 버튼은 여백 20, 목록 카드는 모서리 12 +
            // 테두리 1 + 행 여백 7 = 20에서 글자가 시작한다. 카드 바깥 12px 띠는 테두리를 잡는 자리다.
            var listShell = new Border { Margin = new Thickness(12, 0, 12, 12), CornerRadius = new CornerRadius(10),
                Background = Brush(OnharuPopupChrome.ContentSurfaceColor), BorderBrush = Brush("#E2E8F0"),
                BorderThickness = new Thickness(1), Child = resultScroller };
            Action clipList = delegate
            {
                if (listShell.ActualWidth > 0 && listShell.ActualHeight > 0)
                    listShell.Clip = new RectangleGeometry(new Rect(0, 0, listShell.ActualWidth, listShell.ActualHeight), 10, 10);
            };
            listShell.Loaded += delegate { clipList(); }; listShell.SizeChanged += delegate { clipList(); };
            Grid.SetRow(listShell, 3); panel.Children.Add(listShell);
            query.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { ScheduleRender(); e.Handled = true; } };
            var shell = OnharuPopupChrome.Shell(panel);
            shell.Margin = new Thickness(12);
            OnharuPopupChrome.EnableResize(this, shell);
            Content = shell;
            Loaded += delegate
            {
                query.Focus();
                results.Children.Add(new TextBlock { Text = "일정을 불러오는 중…", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(7, 35, 7, 0) });
                resultScroller.ApplyTemplate();
                UiRound.SoftenScrollBars(resultScroller);
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ScheduleRender));
            };
        }

        void ScheduleRender()
        {
            var request = ++renderRequestVersion;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
            {
                if (request == renderRequestVersion) Render();
            }));
        }

        void Render()
        {
            results.Children.Clear(); todayAnchor = null; var text = query.Text.Trim();
            var mode = rangeModes[range.SelectedIndex];
            var bounds = RangeBounds(mode, DateTime.Today, customFrom.SelectedDate ?? DateTime.Today, customTo.SelectedDate ?? DateTime.Today);
            var from = bounds[0]; var to = bounds[1];
            var includeToday = DateTime.Today >= from && DateTime.Today < to;
            var found = source.Where(x => x.Start >= from && x.Start < to &&
                (text.Length == 0 || (x.Title ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0 || (x.Notes ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToList();
            var matches = found
                .OrderBy(x => Math.Abs((x.Start.Date - DateTime.Today).TotalDays)).Take(500).OrderBy(x => x.Start).ToList();
            // 범위 슬라이딩 버튼 옆의 좁은 자리라 문구를 짧게 유지한다.
            // 잘린 경우에도 표시 건수와 전체 건수를 함께 보여 조용한 절단이 되지 않게 한다.
            var truncated = matches.Count < found.Count;
            resultCount.Text = found.Count == 0 ? "" : truncated ? matches.Count + "(최대검색)/" + found.Count + "건" : found.Count + "건";
            resultCount.ToolTip = truncated ? "전체 " + found.Count + "건 중 오늘과 가까운 " + matches.Count + "건만 표시합니다." : null;
            resultCount.Visibility = resultCount.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (matches.Count == 0)
            { results.Children.Add(new TextBlock { Text = "조건에 맞는 일정이 없습니다.", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(7, 35, 7, 0) }); return; }
            var todayInserted = false;
            foreach (var item in matches)
            {
                if (includeToday && !todayInserted && item.Start.Date >= DateTime.Today) { AddTodayMarker(); todayInserted = true; }
                var isToday = item.Start.Date == DateTime.Today;
                // 날짜를 왼쪽 고정 열로 정렬해 위아래로 훑을 때 눈이 한 줄에 머문다.
                var row = new Grid { Margin = new Thickness(7, 0, 7, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock { Text = item.Start.ToString("MM.dd ddd", new CultureInfo("ko-KR")), FontSize = 11.5,
                    Foreground = Brush(isToday ? "#334155" : "#64748B"), FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center });
                var dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left, Background = Brush(isToday ? "#0EA5E9" : "#CBD5E1") };
                Grid.SetColumn(dot, 1); row.Children.Add(dot);
                var title = new TextBlock { Text = item.Title, FontSize = 13, Foreground = Brush("#1E293B"),
                    FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(title, 2); row.Children.Add(title);
                var time = new TextBlock { Text = item.AllDay ? "하루" : item.Start.ToString("HH:mm"), FontSize = 11.5,
                    Foreground = Brush("#94A3B8"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                Grid.SetColumn(time, 3); row.Children.Add(time);
                var button = new Button { Content = row, Tag = item, Height = 42, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = Brush(isToday ? "#F0F9FF" : OnharuPopupChrome.ContentSurfaceColor), BorderBrush = Brush("#EEF1F6"),
                    BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0), Cursor = Cursors.Hand };
                button.Click += delegate { SelectedItem = (PlannerItem)button.Tag; DialogResult = true; }; results.Children.Add(button);
            }
            if (includeToday && !todayInserted) AddTodayMarker();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ScrollToToday));
        }

        public static DateTime[] RangeBounds(string mode, DateTime today, DateTime customStart, DateTime customEnd)
        {
            today = today.Date;
            if (mode == "all") return new[] { DateTime.MinValue, DateTime.MaxValue };
            if (mode == "past") return new[] { today.AddYears(-1), today.AddDays(1) };
            if (mode == "future") return new[] { today, today.AddYears(1).AddDays(1) };
            if (mode == "custom")
            {
                var first = customStart.Date <= customEnd.Date ? customStart.Date : customEnd.Date;
                var last = customStart.Date <= customEnd.Date ? customEnd.Date : customStart.Date;
                return new[] { first, last == DateTime.MaxValue.Date ? DateTime.MaxValue : last.AddDays(1) };
            }
            return new[] { today.AddYears(-1), today.AddYears(1).AddDays(1) };
        }
        void ScrollToToday()
        {
            if (todayAnchor == null) return;
            resultScroller.UpdateLayout();
            var y = todayAnchor.TranslatePoint(new Point(0, 0), results).Y;
            resultScroller.ScrollToVerticalOffset(Math.Max(0, y - resultScroller.ViewportHeight / 2));
        }
        void AddTodayMarker()
        {
            // 목록 폭을 가로지르는 얇은 띠로 두어 42px 행 리듬을 끊지 않는다.
            todayAnchor = new Border { Height = 26, Background = Brush(OnharuPopupChrome.TodaySurfaceColor),
                BorderBrush = Brush(OnharuPopupChrome.TodayBorderColor), BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new TextBlock { Text = "오늘 · " + DateTime.Today.ToString("yyyy.MM.dd dddd", new CultureInfo("ko-KR")),
                    FontSize = 11, Foreground = Brush(OnharuPopupChrome.TodayTextColor), FontWeight = FontWeights.Bold,
                    Margin = new Thickness(7, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center } };
            results.Children.Add(todayAnchor);
        }

        static void StyleDatePicker(DatePicker picker)
        {
            picker.Background = Brushes.White; picker.BorderBrush = Brush("#CBD5E1"); picker.BorderThickness = new Thickness(1);
            // DatePicker는 템플릿의 PART_Calendar를 쓰지 않고 자기 Calendar를 만들어 팝업에 넣는다.
            // 온하루 달력을 적용하려면 반드시 CalendarStyle로 넘겨야 한다.
            picker.CalendarStyle = OnharuCalendarStyle.Create();
            var grid = new FrameworkElementFactory(typeof(Grid));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(DatePicker.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(DatePicker.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(DatePicker.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); grid.AppendChild(border);
            var text = new FrameworkElementFactory(typeof(DatePickerTextBox), "PART_TextBox");
            text.SetValue(Control.BackgroundProperty, Brushes.Transparent); text.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            text.SetValue(Control.PaddingProperty, new Thickness(9, 0, 28, 0)); text.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center); grid.AppendChild(text);
            var button = new FrameworkElementFactory(typeof(Button), "PART_Button");
            button.SetValue(Button.ContentProperty, "▾"); button.SetValue(Button.WidthProperty, 28.0); button.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            button.SetValue(Button.BackgroundProperty, Brushes.Transparent); button.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            button.SetValue(Button.ForegroundProperty, Brush("#64748B")); button.SetValue(Button.CursorProperty, Cursors.Hand); grid.AppendChild(button);
            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom); popup.SetValue(Popup.AllowsTransparencyProperty, true); popup.SetValue(Popup.StaysOpenProperty, false);
            var popupBorder = new FrameworkElementFactory(typeof(Border)); popupBorder.SetValue(Border.BackgroundProperty, Brush(OnharuPopupChrome.ContentSurfaceColor));
            popupBorder.SetValue(Border.BorderBrushProperty, Brush("#CBD5E1")); popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); popupBorder.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 0));
            popup.AppendChild(popupBorder); grid.AppendChild(popup);
            picker.Template = new ControlTemplate(typeof(DatePicker)) { VisualTree = grid };
        }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
