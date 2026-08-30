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
        readonly StackPanel customRangeRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 34, Margin = new Thickness(0, 7, 70, 0), Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Right };
        readonly DatePicker customFrom = new DatePicker { Width = 142, Height = 28, SelectedDate = DateTime.Today.AddMonths(-1) };
        readonly DatePicker customTo = new DatePicker { Width = 142, Height = 28, SelectedDate = DateTime.Today.AddMonths(1) };
        FrameworkElement todayAnchor;
        int renderRequestVersion;
        readonly List<PlannerItem> source; public PlannerItem SelectedItem;
        public SearchWindow(List<PlannerItem> items)
        {
            source = items; Title = "일정 검색"; Width = 520; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10), LastChildFill = true };
            OnharuPopupChrome.StyleHeader(header);
            var close = OnharuPopupChrome.ToolCloseButton(this); close.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var titleGroup = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(11, 0, 0, 0) };
            var headerTitle = new TextBlock { Text = "⌕  일정 검색", FontSize = 21, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            headerTitle.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; titleGroup.Children.Add(headerTitle);
            var todayButton = new Button { Content = "◎  오늘", Width = 68, Height = 28, Margin = new Thickness(12, 0, 0, 0), Background = Brush(OnharuPopupChrome.SelectionSurfaceColor), Foreground = Brush(OnharuPopupChrome.SelectionTextColor), BorderBrush = Brush(OnharuPopupChrome.SelectionBorderColor), BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
            UiRound.Apply(todayButton, 9); todayButton.Click += delegate { ScrollToToday(); }; titleGroup.Children.Add(todayButton); header.Children.Add(titleGroup); panel.Children.Add(header);
            foreach (var option in new[] { Tuple.Create("오늘 기준 ±1년", "around"), Tuple.Create("과거 1년", "past"), Tuple.Create("앞으로 1년", "future"), Tuple.Create("사용자 지정", "custom"), Tuple.Create("전체 일정", "all") })
                range.Items.Add(new ComboBoxItem { Content = option.Item1, Tag = option.Item2 });
            SettingsWindow.StyleComboBox(range);
            range.SelectedIndex = 0; range.SelectionChanged += delegate
            {
                var custom = range.SelectedItem != null && ((ComboBoxItem)range.SelectedItem).Tag.ToString() == "custom";
                customRangeRow.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
                if (resultScroller != null) resultScroller.Height = custom ? 366 : 400;
                if (IsLoaded) ScheduleRender();
            };
            var searchRow = new Grid { Margin = new Thickness(0, 0, 20, 0) }; searchRow.ColumnDefinitions.Add(new ColumnDefinition());
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(161) }); searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            query.BorderThickness = new Thickness(0); query.Background = Brushes.Transparent; query.Padding = new Thickness(11, 6, 10, 6);
            searchRow.Children.Add(new Border { Child = query, Height = 40, Background = Brush("#FFFFFF"), BorderBrush = Brush("#D5D8DE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11) });
            range.Margin = new Thickness(7, 0, 0, 0); range.Height = 40; range.Width = 154; Grid.SetColumn(range, 1); searchRow.Children.Add(range);
            var search = OnharuPopupChrome.PrimaryButton("⌕  검색", double.NaN); search.Height = 40; search.Margin = new Thickness(7, 0, 0, 0);
            UiRound.Apply(search, 11); search.Click += delegate { ScheduleRender(); }; Grid.SetColumn(search, 2); searchRow.Children.Add(search); panel.Children.Add(searchRow);
            customRangeRow.Children.Add(customFrom);
            customRangeRow.Children.Add(new TextBlock { Text = "~", Margin = new Thickness(7, 0, 7, 0), Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            customRangeRow.Children.Add(customTo);
            StyleDatePicker(customFrom); StyleDatePicker(customTo);
            customFrom.SelectedDateChanged += delegate { if (IsLoaded && customRangeRow.Visibility == Visibility.Visible) ScheduleRender(); };
            customTo.SelectedDateChanged += delegate { if (IsLoaded && customRangeRow.Visibility == Visibility.Visible) ScheduleRender(); };
            panel.Children.Add(customRangeRow);
            resultScroller = new ScrollViewer { Content = results, Height = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 7, 0, 0) };
            resultScroller.Resources["OnharuScrollThumb"] = Brush("#B7ACE8");
            resultScroller.Resources["OnharuScrollTrack"] = Brush("#F1F5F9");
            panel.Children.Add(resultScroller);
            query.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { ScheduleRender(); e.Handled = true; } };
            Content = OnharuPopupChrome.Shell(panel);
            Loaded += delegate
            {
                query.Focus();
                results.Children.Add(new TextBlock { Text = "일정을 불러오는 중…", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(12, 35, 12, 0) });
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
            var mode = range.SelectedItem == null ? "around" : ((ComboBoxItem)range.SelectedItem).Tag.ToString();
            var bounds = RangeBounds(mode, DateTime.Today, customFrom.SelectedDate ?? DateTime.Today, customTo.SelectedDate ?? DateTime.Today);
            var from = bounds[0]; var to = bounds[1];
            var includeToday = DateTime.Today >= from && DateTime.Today < to;
            var matches = source.Where(x => x.Start >= from && x.Start < to &&
                (text.Length == 0 || (x.Title ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0 || (x.Notes ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0))
                .OrderBy(x => Math.Abs((x.Start.Date - DateTime.Today).TotalDays)).Take(500).OrderBy(x => x.Start).ToList();
            if (matches.Count == 0)
            { results.Children.Add(new TextBlock { Text = "조건에 맞는 일정이 없습니다.", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 35, 0, 0) }); return; }
            var todayInserted = false;
            foreach (var item in matches)
            {
                if (includeToday && !todayInserted && item.Start.Date >= DateTime.Today) { AddTodayMarker(); todayInserted = true; }
                var status = item.Start.Date == DateTime.Today ? "오늘" : item.Start >= DateTime.Today ? "예정" : "지난";
                var statusColor = status == "오늘" ? OnharuPopupChrome.SelectionTextColor : "#64748B";
                var row = new Grid { Margin = new Thickness(12, 5, 12, 5) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.Children.Add(new TextBlock { Text = status, Foreground = Brush(statusColor), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
                var info = new StackPanel(); info.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, Foreground = Brush("#1E293B"), TextTrimming = TextTrimming.CharacterEllipsis });
                info.Children.Add(new TextBlock { Text = item.Start.ToString(item.AllDay ? "yyyy.MM.dd ddd" : "yyyy.MM.dd ddd  HH:mm", new CultureInfo("ko-KR")), FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(0, 2, 0, 0) });
                Grid.SetColumn(info, 1); row.Children.Add(info);
                var button = new Button { Content = row, Tag = item, Height = 54, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brush(OnharuPopupChrome.ContentSurfaceColor), BorderBrush = Brush("#D5D8DE"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 3, 0, 3), Cursor = Cursors.Hand };
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
            todayAnchor = new Border { Height = 32, Background = Brush(OnharuPopupChrome.SelectionSurfaceColor), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 7, 0, 7),
                Child = new TextBlock { Text = "오늘 · " + DateTime.Today.ToString("yyyy.MM.dd dddd", new CultureInfo("ko-KR")), Foreground = Brush(OnharuPopupChrome.SelectionTextColor), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            results.Children.Add(todayAnchor);
        }

        static void StyleDatePicker(DatePicker picker)
        {
            picker.Background = Brushes.White; picker.BorderBrush = Brush("#D5D8DE"); picker.BorderThickness = new Thickness(1);
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
            var popupBorder = new FrameworkElementFactory(typeof(Border)); popupBorder.SetValue(Border.BackgroundProperty, Brush("#FFF8F2"));
            popupBorder.SetValue(Border.BorderBrushProperty, Brush("#D5D8DE")); popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); popupBorder.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 0));
            var calendar = new FrameworkElementFactory(typeof(System.Windows.Controls.Calendar), "PART_Calendar");
            calendar.SetValue(System.Windows.Controls.Control.StyleProperty, OnharuCalendarStyle.Create());
            popupBorder.AppendChild(calendar); popup.AppendChild(popupBorder); grid.AppendChild(popup);
            picker.Template = new ControlTemplate(typeof(DatePicker)) { VisualTree = grid };
        }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
