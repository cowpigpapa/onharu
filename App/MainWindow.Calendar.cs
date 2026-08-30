using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void RenderAll()
        {
            UpdatePeriodNavigationButtons();
            if (!settings.UseDiary)
            {
                diaryDates.Clear(); diaryDatesLoaded = false;
            }
            else if (!diaryDatesLoaded)
            {
                diaryDates.Clear(); foreach (var entry in DiaryStore.Load()) diaryDates.Add(entry.Date.Date); diaryDatesLoaded = true;
            }
            var firstDayOfWeek = ConfiguredFirstDay();
            var monthStart = new DateTime(shownMonth.Year, shownMonth.Month, 1);
            var monthOffset = (7 + (int)monthStart.DayOfWeek - (int)firstDayOfWeek) % 7;
            var rangeMode = ActiveCalendarRangeMode;
            var rowCount = rangeMode == "month5" ? 5 : 6;
            DateTime first;
            if (rangeMode == "weeks")
            {
                rowCount = Math.Max(1, Math.Min(6, settings.VisibleWeekCount));
                var anchorOffset = (7 + (int)shownMonth.DayOfWeek - (int)firstDayOfWeek) % 7;
                var todayRow = rowCount <= 2 ? 1 : 2;
                first = shownMonth.Date.AddDays(-anchorOffset - (todayRow - 1) * 7);
                var last = first.AddDays(rowCount * 7 - 1);
                monthTitle.Content = first.Year == last.Year
                    ? first.ToString("yyyy년 M월 d일") + " – " + last.ToString("M월 d일")
                    : first.ToString("yyyy년 M월 d일") + " – " + last.ToString("yyyy년 M월 d일");
            }
            else
            {
                first = monthStart.AddDays(-monthOffset);
                if (rangeMode == "monthAuto")
                    rowCount = Math.Max(4, Math.Min(6, (int)Math.Ceiling((monthOffset + DateTime.DaysInMonth(monthStart.Year, monthStart.Month)) / 7.0)));
                monthTitle.Content = monthStart.ToString("yyyy년 M월");
            }
            UpdateCompactHeaderTypography();
            ApplyCalendarMinimumHeight(rowCount);
            var availableCalendarHeight = calendar.ActualHeight > 100 ? calendar.ActualHeight : Math.Max(calendar.MinHeight, ActualHeight - 142);
            var dayCellHeight = Math.Max(55, (availableCalendarHeight - 34) / rowCount);
            // Keep at least three lanes, then use every complete 20px event lane
            // made available when the calendar grows. Overflow occupies the last lane.
            visibleEventLanes = Math.Max(3, (int)Math.Floor((dayCellHeight - Ui(48)) / Ui(20)) + 1);
            lastRenderedCalendarHeight = calendar.ActualHeight;
            calendar.Children.Clear(); calendar.RowDefinitions.Clear(); calendar.ColumnDefinitions.Clear();
            dayCells.Clear();
            var weekOffset = settings.ShowWeekNumbers ? 1 : 0;
            if (settings.ShowWeekNumbers) calendar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            for (var c = 0; c < 7; c++) calendar.ColumnDefinitions.Add(new ColumnDefinition());
            calendar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            for (var r = 0; r < rowCount; r++) calendar.RowDefinitions.Add(new RowDefinition());
            var dayNames = new[] { "일", "월", "화", "수", "목", "금", "토" };
            var weekdays = Enumerable.Range(0, 7).Select(x => dayNames[((int)firstDayOfWeek + x) % 7]).ToArray();
            if (settings.ShowWeekNumbers)
            {
                var weekHeader = new TextBlock { Text = "주", HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, Foreground = T("Disabled"), FontSize = Ui(10), FontWeight = FontWeights.Bold };
                calendar.Children.Add(weekHeader);
            }
            for (var c = 0; c < 7; c++)
            {
                var day = new TextBlock { Text = weekdays[c], HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold, FontSize = Ui(13),
                    Foreground = IsRestDay(first.AddDays(c)) ? Brush("#DC6B73") : T("Weekday") };
                Grid.SetColumn(day, c + weekOffset); calendar.Children.Add(day);
            }
            if (settings.ShowWeekNumbers)
                for (var r = 0; r < rowCount; r++)
                {
                    var week = new TextBlock { Text = "W" + GetWeekNumber(first.AddDays(r * 7)).ToString("00"),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                        Foreground = T("Muted"), FontSize = Ui(10), FontWeight = FontWeights.SemiBold };
                    Grid.SetRow(week, r + 1); calendar.Children.Add(week);
                }
            for (var i = 0; i < rowCount * 7; i++) AddDayCell(first.AddDays(i), i / 7 + 1, i % 7 + weekOffset);
            for (var r = 0; r < rowCount; r++) AddWeekEventBars(first.AddDays(r * 7), r + 1, weekOffset);
            RenderDetail();
            TemporarySegmentPaletteTool.ApplyOverrides(this);
            if (IsLoaded && positionLocked) SchedulePublish();
        }

        bool IsRestDay(DateTime date)
        {
            return settings.RestDays != null && settings.RestDays.Contains((int)date.DayOfWeek);
        }

        DayOfWeek ConfiguredFirstDay()
        {
            return settings.WeekStartDay == "sunday" ? DayOfWeek.Sunday : settings.WeekStartDay == "tuesday" ? DayOfWeek.Tuesday :
                settings.WeekStartDay == "wednesday" ? DayOfWeek.Wednesday : settings.WeekStartDay == "thursday" ? DayOfWeek.Thursday :
                settings.WeekStartDay == "friday" ? DayOfWeek.Friday : settings.WeekStartDay == "saturday" ? DayOfWeek.Saturday : DayOfWeek.Monday;
        }

        void AddDayCell(DateTime date, int row, int col)
        {
            var stack = new StackPanel();
            var dateItems = VisibleItems(date).ToList();
            var isHoliday = dateItems.Any(x => x.Category == "국경일");
            var dateHeader = new StackPanel { Orientation = Orientation.Horizontal };
            var diaryTarget = settings.UseDiary ? new DiaryDateHitTarget(date) : null;
            MouseButtonEventHandler openDiary = delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount != 2) return; selectedDate = date; detailMode = "selected"; e.Handled = true; OpenDiaryEditor(date);
            };
            var number = new TextBlock { Text = date.Day.ToString(), FontSize = Ui(13), FontWeight = date == DateTime.Today ? FontWeights.Bold : FontWeights.Normal,
                Foreground = ActiveCalendarRangeMode != "weeks" && date.Month != shownMonth.Month ? T("Disabled") : isHoliday ? Brush("#DC6B73") : IsRestDay(date) ? Brush("#DC6B73") : T("Text"),
                Margin = new Thickness(5, 3, 2, 4), Tag = diaryTarget, ToolTip = settings.UseDiary ? "더블클릭하여 일기 쓰기" : null };
            var todayIcon = date.Date == DateTime.Today && (settings.TodayStyle == "icon" || settings.TodayStyle == "fill_icon");
            if (todayIcon)
            {
                number.Margin = new Thickness(0); number.Foreground = Brushes.White;
                var todayCircle = new Border { Width = Ui(23), Height = Ui(23), CornerRadius = new CornerRadius(Ui(12)),
                    Background = Brush(settings.TodayBorderColor), Margin = new Thickness(3, 1, 2, 1), Child = number, Tag = diaryTarget,
                    ToolTip = settings.UseDiary ? "더블클릭하여 일기 쓰기" : null };
                number.HorizontalAlignment = HorizontalAlignment.Center; number.VerticalAlignment = VerticalAlignment.Center;
                if (settings.UseDiary) todayCircle.MouseLeftButtonDown += openDiary; dateHeader.Children.Add(todayCircle);
            }
            else
            {
                if (settings.UseDiary) number.MouseLeftButtonDown += openDiary; dateHeader.Children.Add(number);
            }
            if (settings.ShowLunar)
            {
                var lunar = new TextBlock { Text = Lunar(date), Foreground = T("Muted"), FontSize = Ui(11),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 1, 1, 2) };
                dateHeader.Children.Add(lunar);
            }
            var solarTerm = settings.ShowSolarTerms ? SolarTerm(date) : null;
            if (!string.IsNullOrWhiteSpace(solarTerm))
                dateHeader.Children.Add(new TextBlock { Text = solarTerm, Foreground = T("Weekday"), FontSize = Ui(11),
                    FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 1, 1, 2) });
            if (settings.UseDiary && diaryDates.Contains(date.Date))
            {
                var diaryDot = new TextBlock { Text = "•", Foreground = Brush("#7C3AED"), FontSize = Ui(14), FontWeight = FontWeights.Bold, Tag = diaryTarget,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 2), ToolTip = "작성한 일기 · 더블클릭하여 열기", Cursor = Cursors.Hand };
                diaryDot.MouseLeftButtonDown += openDiary; dateHeader.Children.Add(diaryDot);
            }
            var holidays = string.Join(", ", dateItems.Where(x => x.Category == "국경일").Select(x => x.Title).ToArray());
            if (date == DateTime.Today)
                dateHeader.Children.Add(new TextBlock { Text = "오늘", Foreground = T("Accent"), FontSize = Ui(11),
                    FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 1, 0, 2) });
            if (!string.IsNullOrWhiteSpace(holidays))
                dateHeader.Children.Add(new TextBlock { Text = (date == DateTime.Today ? ". " : "") + holidays, Foreground = Brush("#DC6B73"), FontSize = Ui(11),
                    FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(3, 1, 2, 2), TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(dateHeader);
            var border = new Border { Child = stack, Tag = date, Cursor = Cursors.Hand, ToolTip = "더블클릭하여 새 일정 등록" };
            StyleDayCell(border, date);
            border.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                selectedDate = date; detailMode = "selected";
                if (e.ClickCount == 2) AddItem(sender, e); else RenderAll();
            };
            Grid.SetRow(border, row); Grid.SetColumn(border, col); calendar.Children.Add(border); dayCells[date.Date] = border;
        }

        Brush DayBackground(DateTime date)
        {
            string customBackground = null;
            var custom = settings.DateBackgroundColors != null && settings.DateBackgroundColors.TryGetValue(DateKey(date), out customBackground);
            return (settings.SelectedDateStyle == "fill" || settings.SelectedDateStyle == "both") && date.Date == selectedDate.Date
                    ? new SolidColorBrush(CategoryColorSystem.SelectionBackground(settings.ThemeId, settings.SelectedDateFillColor))
                : date.Date == DateTime.Today && (settings.TodayStyle == "fill" || settings.TodayStyle == "fill_icon") && settings.TodayColor != "none" ? Brush(settings.TodayColor)
                : custom ? Brush(customBackground) : Brush(OnharuStateColors.CalendarCell(settings.ThemeId));
        }

        void StyleDayCell(Border cell, DateTime date)
        {
            var selectedBorder = (settings.SelectedDateStyle == "border" || settings.SelectedDateStyle == "both") && date.Date == selectedDate.Date;
            cell.Background = DayBackground(date);
            cell.BorderBrush = selectedBorder ? Brush(settings.SelectedDateBorderColor) : T("Grid");
            cell.BorderThickness = new Thickness(selectedBorder ? 2 : .5);
            cell.Margin = selectedBorder ? new Thickness(-1.5) : new Thickness(0);
            Panel.SetZIndex(cell, selectedBorder ? 2 : 0);
        }

        void AddWeekEventBars(DateTime weekStart, int row, int weekOffset)
        {
            var weekEnd = weekStart.AddDays(6);
            var weekItems = ProjectItems(weekStart, weekEnd).Where(x => x.Category != "국경일" && IsItemVisible(x) && ShowCompleted(x) &&
                x.Start.Date <= weekEnd && (x.End > x.Start ? x.End.AddTicks(-1).Date : x.Start.Date) >= weekStart);
            if (settings.CalendarOrderMode == "time")
                weekItems = settings.MultiDayFirst
                    ? weekItems.OrderBy(CompletedRank).ThenBy(ImportantRank).ThenBy(x => x.AllDay ? 0 : 1).ThenByDescending(IsMultiDay).ThenBy(x => x.Start).ThenBy(x => x.Title)
                    : weekItems.OrderBy(CompletedRank).ThenBy(ImportantRank).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title);
            else
                weekItems = settings.MultiDayFirst
                    ? weekItems.OrderBy(CompletedRank).ThenBy(ImportantRank).ThenBy(x => x.AllDay ? 0 : 1).ThenByDescending(IsMultiDay).ThenBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.Start)
                    : weekItems.OrderBy(CompletedRank).ThenBy(ImportantRank).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.Start);
            // The configured order is not necessarily chronological.  Tracking only
            // the last end date therefore created artificial empty lanes whenever a
            // later date was encountered before an earlier one.  Keep the occupied
            // intervals for each lane and place an item in the first lane that does
            // not actually overlap it.
            var laneOccupancy = new List<List<Tuple<DateTime, DateTime>>>();
            var segments = new List<Tuple<PlannerItem, DateTime, DateTime, int>>();
            foreach (var item in weekItems)
            {
                var itemEnd = item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date;
                var segmentStart = item.Start.Date < weekStart ? weekStart : item.Start.Date;
                var segmentEnd = itemEnd > weekEnd ? weekEnd : itemEnd;
                var lane = laneOccupancy.FindIndex(occupied =>
                    occupied.All(range => segmentEnd < range.Item1 || segmentStart > range.Item2));
                if (lane < 0)
                {
                    lane = laneOccupancy.Count;
                    laneOccupancy.Add(new List<Tuple<DateTime, DateTime>>());
                }
                laneOccupancy[lane].Add(Tuple.Create(segmentStart, segmentEnd));
                segments.Add(Tuple.Create(item, segmentStart, segmentEnd, lane));
            }
            var eventLaneLimit = visibleEventLanes;
            var hasOverflow = segments.Any(x => x.Item4 >= eventLaneLimit);
            var visibleLaneLimit = hasOverflow ? Math.Max(1, eventLaneLimit - 1) : eventLaneLimit;
            foreach (var segment in segments.Where(x => x.Item4 < visibleLaneLimit))
            {
                var item = segment.Item1; var segmentStart = segment.Item2; var segmentEnd = segment.Item3; var lane = segment.Item4;
                var itemEnd = item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date;
                var spansDays = item.Start.Date != itemEnd;
                var prefix = item.IsTodo ? (item.Completed ? "✓ " : "□ ") : "";
                if (!item.AllDay && !spansDays) prefix += TimeText(item.Start) + " ";
                var calendarTitle = !string.IsNullOrWhiteSpace(item.AnniversaryType)
                    ? item.Title + AnniversaryOccurrenceText(item)
                    : DdayText(item) + item.Title;
                var text = new TextBlock { Text = prefix + (item.Important ? "★ " : "") + calendarTitle,
                    FontSize = Ui(11), Foreground = EventTextBrush(item),
                    FontWeight = item.Important ? FontWeights.Bold : FontWeights.Normal, Padding = new Thickness(5, 1, 4, 1),
                    TextTrimming = TextTrimming.CharacterEllipsis, TextDecorations = item.Completed ? TextDecorations.Strikethrough : null,
                    HorizontalAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand };
                var continuesBefore = item.Start.Date < segmentStart;
                var continuesAfter = itemEnd > segmentEnd;
                var bar = new Border { Child = text, Height = Ui(19),
                    CornerRadius = new CornerRadius(continuesBefore ? 0 : 4, continuesAfter ? 0 : 4, continuesAfter ? 0 : 4, continuesBefore ? 0 : 4),
                    Background = EventBackgroundBrush(item),
                    Margin = new Thickness(2, Ui(29 + lane * 20), 2, 0), VerticalAlignment = VerticalAlignment.Top,
                    Cursor = Cursors.Arrow };
                EnableItemDrag(bar, item);
                bar.Tag = new ItemHitTarget { Item = item, SegmentStart = segmentStart, SegmentEnd = segmentEnd, Element = bar };
                if (settings.CompletedDisplayMode == "fade" && item.IsTodo && item.Completed) bar.Opacity = .66;
                bar.MouseLeftButtonDown += async delegate(object sender, MouseButtonEventArgs e)
                {
                    var localPoint = e.GetPosition(bar);
                    if (e.ClickCount == 1 && item.IsTodo && localPoint.X <= Ui(23))
                    {
                        await SetTodoCompleted(item, !item.Completed); e.Handled = true; return;
                    }
                    var days = (segmentEnd - segmentStart).Days + 1;
                    var clickedDay = bar.ActualWidth <= 0 ? 0 : Math.Min(days - 1, Math.Max(0, (int)(localPoint.X / bar.ActualWidth * days)));
                    var clickedDate = segmentStart.AddDays(clickedDay);
                    if (e.ClickCount == 2) { selectedDate = clickedDate; detailMode = "selected"; OpenEdit(item); }
                    else SelectDateFast(clickedDate);
                    e.Handled = true;
                };
                Grid.SetRow(bar, row); Grid.SetColumn(bar, (segmentStart - weekStart).Days + weekOffset);
                Grid.SetColumnSpan(bar, (segmentEnd - segmentStart).Days + 1); Panel.SetZIndex(bar, 5); calendar.Children.Add(bar);
            }
            if (hasOverflow)
                for (var day = weekStart; day <= weekEnd; day = day.AddDays(1))
                {
                    var hidden = segments.Count(x => x.Item4 >= visibleLaneLimit && day >= x.Item2 && day <= x.Item3);
                    if (hidden == 0) continue;
                    var targetDate = day;
                    var more = Button("+ " + hidden + "개 더보기", null, double.NaN); more.FontSize = Ui(10); more.FontWeight = FontWeights.Bold;
                    var moreColors = OnharuStateColors.MoreButton(settings.ThemeId);
                    more.Foreground = new SolidColorBrush(moreColors.Foreground); more.Background = new SolidColorBrush(moreColors.Background);
                    more.BorderBrush = new SolidColorBrush(moreColors.Border); more.BorderThickness = new Thickness(1); more.Cursor = Cursors.Hand;
                    more.Padding = new Thickness(6, 0, 6, 0); more.HorizontalContentAlignment = HorizontalAlignment.Left;
                    more.Height = Ui(16);
                    more.Click += delegate
                    {
                        selectedDate = targetDate; detailMode = "selected"; RenderDetail();
                        if (!settings.SidebarVisible || settings.ShowOverflowPopupWithSidebar)
                            ShowDayOverflowPopup(more, targetDate);
                    };
                    more.Margin = new Thickness(5, Ui(31 + visibleLaneLimit * 20), 3, 0);
                    more.VerticalAlignment = VerticalAlignment.Top;
                    more.ToolTip = targetDate.ToString("M월 d일") + " 일정 모두 보기";
                    Grid.SetRow(more, row); Grid.SetColumn(more, (day - weekStart).Days + weekOffset);
                    Panel.SetZIndex(more, 7); calendar.Children.Add(more);
                }
        }

        void ApplyCalendarMinimumHeight(int rowCount)
        {
            var minimumCalendarHeight = Ui(34 + rowCount * 87);
            calendar.MinHeight = minimumCalendarHeight;
            MinHeight = Math.Max(560, minimumCalendarHeight + Ui(142));
        }

        void CalendarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!IsLoaded || calendarResizeRenderPending || Math.Abs(e.NewSize.Height - lastRenderedCalendarHeight) < 1.5) return;
            calendarResizeRenderPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
            {
                calendarResizeRenderPending = false;
                if (Math.Abs(calendar.ActualHeight - lastRenderedCalendarHeight) >= 1.5) RenderAll();
            }));
        }

        void ShowDayOverflowPopup(FrameworkElement target, DateTime date)
        {
            CloseTransientPopup();
            var window = new Window { Title = date.ToString("M월 d일 일정"), Width = Ui(320), SizeToContent = SizeToContent.Height,
                MaxHeight = Ui(440), WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = positionLocked, Opacity = 1.0 };
            PlannerItem selectedItem = null;
            var dayItems = VisibleItems(date).Where(x => x.Category != "국경일").ToList();
            var list = new StackPanel();
            foreach (var item in dayItems)
            {
                var localItem = item;
                var prefix = localItem.AllDay ? "" : TimeText(localItem.Start) + " ";
                var title = new TextBlock { Text = prefix + (localItem.Important ? "★ " : "") + localItem.Title,
                    FontSize = Ui(11.5), FontWeight = localItem.Important ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = EventTextBrush(localItem), TextTrimming = TextTrimming.CharacterEllipsis,
                    TextDecorations = localItem.Completed ? TextDecorations.Strikethrough : null,
                    VerticalAlignment = VerticalAlignment.Center };
                var usesBar = localItem.AllDay || IsMultiDay(localItem);
                var rowContent = new Grid();
                rowContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowContent.ColumnDefinitions.Add(new ColumnDefinition());
                rowContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                CheckBox completionCheck = null;
                if (localItem.IsTodo)
                {
                    var check = new CheckBox { IsChecked = localItem.Completed, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 7, 0) };
                    completionCheck = check;
                    StyleThemeCheckBox(check, ItemColor(localItem));
                    check.Click += async delegate { await SetTodoCompleted(localItem, check.IsChecked == true); };
                    rowContent.Children.Add(check);
                }
                else if (!usesBar)
                    rowContent.Children.Add(new Border { Width = Ui(8), Height = Ui(8), CornerRadius = new CornerRadius(Ui(4)),
                        Background = EventTextBrush(localItem), Margin = new Thickness(1, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
                Grid.SetColumn(title, 2); rowContent.Children.Add(title);
                var rightPadding = localItem.GoogleReadOnly ? 58.0 : 9.0;
                var row = new Border { Height = Ui(28), Margin = new Thickness(0, 0, 0, 3), Padding = new Thickness(usesBar ? 9 : 5, 2, rightPadding, 2),
                    CornerRadius = new CornerRadius(usesBar ? 6 : 0), Background = usesBar ? EventBackgroundBrush(localItem) : Brushes.Transparent,
                    Cursor = Cursors.Hand, Child = rowContent };
                FrameworkElement clickableRow = row;
                if (usesBar && IsMultiDay(localItem))
                {
                    var itemEnd = localItem.End > localItem.Start ? localItem.End.AddTicks(-1).Date : localItem.Start.Date;
                    var continuesBefore = localItem.Start.Date < date.Date;
                    var continuesAfter = itemEnd > date.Date;
                    var arrowWidth = Ui(11);
                    var strip = new Grid { Height = Ui(28), Margin = new Thickness(0, 0, 0, 3), Cursor = Cursors.Arrow,
                        SnapsToDevicePixels = true };
                    row.Margin = new Thickness(continuesBefore ? arrowWidth : 0, 0, continuesAfter ? arrowWidth : 0, 0);
                    if (continuesAfter) row.Padding = new Thickness(row.Padding.Left, row.Padding.Top, Math.Max(5, rightPadding - arrowWidth), row.Padding.Bottom);
                    row.CornerRadius = new CornerRadius(continuesBefore ? 0 : 6, continuesAfter ? 0 : 6,
                        continuesAfter ? 0 : 6, continuesBefore ? 0 : 6);
                    strip.Children.Add(row);
                    if (continuesBefore)
                        strip.Children.Add(new Polygon { Points = new PointCollection { new Point(arrowWidth, 0), new Point(0, Ui(14)), new Point(arrowWidth, Ui(28)) },
                            Fill = EventBackgroundBrush(localItem), HorizontalAlignment = HorizontalAlignment.Left });
                    if (continuesAfter)
                        strip.Children.Add(new Polygon { Points = new PointCollection { new Point(0, 0), new Point(arrowWidth, Ui(14)), new Point(0, Ui(28)) },
                            Fill = EventBackgroundBrush(localItem), HorizontalAlignment = HorizontalAlignment.Right });
                    clickableRow = strip;
                }
                clickableRow.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    if (e.ClickCount != 2 || completionCheck != null && IsWithin(e.OriginalSource as DependencyObject, completionCheck)) return;
                    if (localItem.GoogleReadOnly) { e.Handled = true; return; }
                    selectedItem = localItem; window.DialogResult = true; e.Handled = true;
                };
                var rowShell = new Grid(); rowShell.Children.Add(clickableRow);
                if (localItem.GoogleReadOnly)
                {
                    var readOnly = new TextBlock { Text = "수정 불가", Foreground = T("Disabled"), FontSize = Ui(9.5),
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, Ui(12), Ui(3)), IsHitTestVisible = false };
                    Panel.SetZIndex(readOnly, 2); rowShell.Children.Add(readOnly);
                }
                list.Children.Add(rowShell);
            }
            var header = new Grid { Height = Ui(42), Margin = new Thickness(2, -2, 2, 6) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = date.ToString("M월 d일 (ddd)", new CultureInfo("ko-KR")), FontSize = Ui(16),
                FontWeight = FontWeights.Bold, Foreground = T("Text"), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center });
            var close = OnharuPopupChrome.ToolCloseButton(window);
            close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 3, 0, 0);
            Grid.SetColumn(close, 1); header.Children.Add(close);
            var content = new StackPanel { Margin = new Thickness(12, 8, 12, 12) }; content.Children.Add(header);
            content.Children.Add(new ScrollViewer { Content = list, MaxHeight = Ui(310), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            window.Content = OnharuPopupChrome.Shell(content);
            PlaceDayOverflowDialog(window, target, date);
            ShowBlockingDialog(window);
            if (positionLocked && IsVisible) PublishAndHide();
            if (selectedItem == null) return;
            OpenEdit(selectedItem);
        }

        void PlaceDayOverflowDialog(Window window, FrameworkElement target, DateTime date)
        {
            PlaceCalendarDialog(window);
            Border dayCell;
            FrameworkElement cell = dayCells.TryGetValue(date.Date, out dayCell) ? dayCell : target;
            var source = PresentationSource.FromVisual(this);
            var fromDevice = source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            var calendarTopLeft = fromDevice.Transform(calendar.PointToScreen(new Point()));
            var calendarBottomRight = fromDevice.Transform(calendar.PointToScreen(new Point(calendar.ActualWidth, calendar.ActualHeight)));
            var cellTopLeft = fromDevice.Transform(cell.PointToScreen(new Point()));
            var cellBottomRight = fromDevice.Transform(cell.PointToScreen(new Point(cell.ActualWidth, cell.ActualHeight)));
            var content = window.Content as FrameworkElement;
            if (content != null) content.Measure(new Size(window.Width, double.PositiveInfinity));
            var width = window.Width;
            var height = Math.Min(window.MaxHeight, content == null ? Ui(420) : content.DesiredSize.Height);
            var gap = Ui(6);
            var opensLeft = (cellTopLeft.X + cellBottomRight.X) / 2 > (calendarTopLeft.X + calendarBottomRight.X) / 2;
            var opensUp = (cellTopLeft.Y + cellBottomRight.Y) / 2 > (calendarTopLeft.Y + calendarBottomRight.Y) / 2;
            var left = opensLeft ? cellTopLeft.X - width - gap : cellBottomRight.X + gap;
            var top = opensUp ? cellBottomRight.Y - height : cellTopLeft.Y;
            window.Left = Math.Max(calendarTopLeft.X + gap, Math.Min(left, calendarBottomRight.X - width - gap));
            window.Top = Math.Max(calendarTopLeft.Y + gap, Math.Min(top, calendarBottomRight.Y - height - gap));
        }
    }
}
