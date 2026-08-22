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
        readonly StackPanel results = new StackPanel(); readonly TextBox query = new TextBox { Height = 38, FontSize = 14 };
        readonly ScrollViewer resultScroller;
        readonly ComboBox range = new ComboBox { Width = 154, Height = 29, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), Cursor = Cursors.Hand };
        readonly StackPanel customRangeRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 34, Margin = new Thickness(0, 7, 82, 0), Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Right };
        readonly DatePicker customFrom = new DatePicker { Width = 130, Height = 28, SelectedDate = DateTime.Today.AddMonths(-1) };
        readonly DatePicker customTo = new DatePicker { Width = 130, Height = 28, SelectedDate = DateTime.Today.AddMonths(1) };
        FrameworkElement todayAnchor;
        int renderRequestVersion;
        readonly List<PlannerItem> source; public PlannerItem SelectedItem;
        public SearchWindow(List<PlannerItem> items)
        {
            source = items; Title = "일정 검색"; Width = 520; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 10) };
            var titleGroup = new StackPanel { Orientation = Orientation.Horizontal };
            var headerTitle = new TextBlock { Text = "⌕  일정 검색", FontSize = 21, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            headerTitle.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; titleGroup.Children.Add(headerTitle);
            var todayButton = new Button { Content = "◎  오늘", Width = 68, Height = 28, Margin = new Thickness(12, 0, 0, 0), Background = Brush("#FCE7F3"), Foreground = Brush("#DB2777"), BorderBrush = Brush("#FBCFE8"), BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
            UiRound.Apply(todayButton, 9); todayButton.Click += delegate { ScrollToToday(); }; titleGroup.Children.Add(todayButton); header.Children.Add(titleGroup); panel.Children.Add(header);
            foreach (var option in new[] { Tuple.Create("오늘 기준 ±1년", "around"), Tuple.Create("과거 1년", "past"), Tuple.Create("앞으로 1년", "future"), Tuple.Create("사용자 지정", "custom"), Tuple.Create("전체 일정", "all") })
                range.Items.Add(new ComboBoxItem { Content = option.Item1, Tag = option.Item2 });
            SettingsWindow.StyleComboBox(range);
            range.SelectedIndex = 0; range.SelectionChanged += delegate
            {
                var custom = range.SelectedItem != null && ((ComboBoxItem)range.SelectedItem).Tag.ToString() == "custom";
                customRangeRow.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
                if (resultScroller != null) resultScroller.Height = custom ? 336 : 370;
                if (IsLoaded) ScheduleRender();
            };
            var searchRow = new Grid(); searchRow.ColumnDefinitions.Add(new ColumnDefinition());
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(161) }); searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            query.BorderThickness = new Thickness(0); query.Background = Brushes.Transparent; query.Padding = new Thickness(11, 6, 10, 6);
            searchRow.Children.Add(new Border { Child = query, Height = 40, Background = Brush("#F8FAFF"), BorderBrush = Brush("#C7D2FE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11) });
            range.Margin = new Thickness(7, 0, 0, 0); range.Height = 40; range.Width = 154; Grid.SetColumn(range, 1); searchRow.Children.Add(range);
            var search = new Button { Content = "⌕  검색", Height = 40, Margin = new Thickness(7, 0, 0, 0), Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
            UiRound.Apply(search, 11); search.Click += delegate { ScheduleRender(); }; Grid.SetColumn(search, 2); searchRow.Children.Add(search); panel.Children.Add(searchRow);
            customRangeRow.Children.Add(customFrom);
            customRangeRow.Children.Add(new TextBlock { Text = "~", Margin = new Thickness(7, 0, 7, 0), Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            customRangeRow.Children.Add(customTo);
            StyleDatePicker(customFrom); StyleDatePicker(customTo);
            customFrom.SelectedDateChanged += delegate { if (IsLoaded && customRangeRow.Visibility == Visibility.Visible) ScheduleRender(); };
            customTo.SelectedDateChanged += delegate { if (IsLoaded && customRangeRow.Visibility == Visibility.Visible) ScheduleRender(); };
            panel.Children.Add(customRangeRow);
            resultScroller = new ScrollViewer { Content = results, Height = 370, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 7, 0, 0) };
            panel.Children.Add(resultScroller);
            query.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { ScheduleRender(); e.Handled = true; } };
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            UiRound.Apply(close, 10);
            close.Click += delegate { DialogResult = false; };
            var frame = new Grid(); frame.Children.Add(UiRound.EmphasizePopup(new Border { Background = Brush("#FFFAFCFF"), CornerRadius = new CornerRadius(18), Child = panel }));
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
            Loaded += delegate
            {
                query.Focus();
                results.Children.Add(new TextBlock { Text = "일정을 불러오는 중…", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(12, 35, 12, 0) });
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { UiRound.SoftenScrollBars(resultScroller); ScheduleRender(); }));
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
            var mode = range.SelectedItem == null ? "around" : ((ComboBoxItem)range.SelectedItem).Tag.ToString();
            var bounds = RangeBounds(mode, DateTime.Today, customFrom.SelectedDate ?? DateTime.Today, customTo.SelectedDate ?? DateTime.Today);
            var from = bounds[0]; var to = bounds[1];
            var includeToday = DateTime.Today >= from && DateTime.Today < to;
            var matches = source.Where(x => x.Start >= from && x.Start < to &&
                (text.Length == 0 || (x.Title ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0 || (x.Notes ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0)).OrderBy(x => x.Start).Take(500).ToList();
            if (matches.Count == 0)
            { results.Children.Add(new TextBlock { Text = "조건에 맞는 일정이 없습니다.", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 35, 0, 0) }); return; }
            var todayInserted = false;
            foreach (var item in matches)
            {
                if (includeToday && !todayInserted && item.Start.Date >= DateTime.Today) { AddTodayMarker(); todayInserted = true; }
                var status = item.Start.Date == DateTime.Today ? "오늘" : item.Start >= DateTime.Today ? "예정" : "지난";
                var statusColor = status == "오늘" ? "#DB2777" : status == "예정" ? "#4F46E5" : "#64748B";
                var row = new Grid { Margin = new Thickness(12, 5, 12, 5) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.Children.Add(new TextBlock { Text = status, Foreground = Brush(statusColor), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
                var info = new StackPanel(); info.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, Foreground = Brush("#1E293B"), TextTrimming = TextTrimming.CharacterEllipsis });
                info.Children.Add(new TextBlock { Text = item.Start.ToString(item.AllDay ? "yyyy.MM.dd ddd" : "yyyy.MM.dd ddd  HH:mm", new CultureInfo("ko-KR")), FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(0, 2, 0, 0) });
                Grid.SetColumn(info, 1); row.Children.Add(info);
                var button = new Button { Content = row, Tag = item, Height = 54, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brush("#F8FAFC"), BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 3, 0, 3), Cursor = Cursors.Hand };
                UiRound.Apply(button, 11);
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
            todayAnchor = new Border { Height = 32, Background = Brush("#FCE7F3"), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 7, 0, 7),
                Child = new TextBlock { Text = "오늘 · " + DateTime.Today.ToString("yyyy.MM.dd dddd", new CultureInfo("ko-KR")), Foreground = Brush("#DB2777"), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            results.Children.Add(todayAnchor);
        }

        static void StyleDatePicker(DatePicker picker)
        {
            picker.Background = Brushes.White; picker.BorderBrush = Brush("#C7D2FE"); picker.BorderThickness = new Thickness(1);
            var grid = new FrameworkElementFactory(typeof(Grid));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(DatePicker.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(DatePicker.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(DatePicker.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9)); grid.AppendChild(border);
            var text = new FrameworkElementFactory(typeof(DatePickerTextBox), "PART_TextBox");
            text.SetValue(Control.BackgroundProperty, Brushes.Transparent); text.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            text.SetValue(Control.PaddingProperty, new Thickness(9, 0, 28, 0)); text.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center); grid.AppendChild(text);
            var button = new FrameworkElementFactory(typeof(Button), "PART_Button");
            button.SetValue(Button.ContentProperty, "▾"); button.SetValue(Button.WidthProperty, 28.0); button.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            button.SetValue(Button.BackgroundProperty, Brushes.Transparent); button.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            button.SetValue(Button.ForegroundProperty, Brush("#64748B")); button.SetValue(Button.CursorProperty, Cursors.Hand); grid.AppendChild(button);
            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom); popup.SetValue(Popup.AllowsTransparencyProperty, true); popup.SetValue(Popup.StaysOpenProperty, false);
            var popupBorder = new FrameworkElementFactory(typeof(Border)); popupBorder.SetValue(Border.BackgroundProperty, Brushes.White);
            popupBorder.SetValue(Border.BorderBrushProperty, Brush("#C7D2FE")); popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); popupBorder.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 0));
            var calendar = new FrameworkElementFactory(typeof(System.Windows.Controls.Calendar), "PART_Calendar"); popupBorder.AppendChild(calendar); popup.AppendChild(popupBorder); grid.AppendChild(popup);
            calendar.SetValue(System.Windows.Controls.Control.StyleProperty, OnharuCalendarStyle.Create());
            picker.Template = new ControlTemplate(typeof(DatePicker)) { VisualTree = grid };
        }

        static Style SearchCalendarDayStyle()
        {
            var template = new ControlTemplate(typeof(CalendarDayButton));
            var border = new FrameworkElementFactory(typeof(Border)); border.Name = "DayBorder";
            border.SetValue(Border.BackgroundProperty, Brush("#FFFEFC")); border.SetValue(Border.BorderBrushProperty, Brush("#F3D4C7"));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(.6)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content); template.VisualTree = border;
            var today = new Trigger { Property = CalendarDayButton.IsTodayProperty, Value = true };
            today.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#DDF7F0"), "DayBorder")); today.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("#34B89A"), "DayBorder")); template.Triggers.Add(today);
            var selected = new Trigger { Property = CalendarDayButton.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#E56B6F"), "DayBorder")); selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); template.Triggers.Add(selected);
            var inactive = new Trigger { Property = CalendarDayButton.IsInactiveProperty, Value = true }; inactive.Setters.Add(new Setter(Control.OpacityProperty, .38)); template.Triggers.Add(inactive);
            var style = new Style(typeof(CalendarDayButton)); style.Setters.Add(new Setter(Control.TemplateProperty, template)); style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(1.5)));
            style.Setters.Add(new Setter(Control.MinWidthProperty, 29.0)); style.Setters.Add(new Setter(Control.MinHeightProperty, 27.0));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0)); style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#5B3540"))); return style;
        }

        static Style SearchCalendarButtonStyle()
        {
            var template = new ControlTemplate(typeof(CalendarButton));
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.BackgroundProperty, Brush("#FDE8E3"));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); border.SetValue(Border.MarginProperty, new Thickness(2));
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content); template.VisualTree = border;
            var style = new Style(typeof(CalendarButton)); style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#B4474D"))); style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 16.0)); return style;
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
