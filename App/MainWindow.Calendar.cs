using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
                first = shownMonth.Date.AddDays(-anchorOffset - (Math.Max(1, Math.Min(rowCount, settings.TodayRow)) - 1) * 7);
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
            var availableCalendarHeight = calendar.ActualHeight > 100 ? calendar.ActualHeight : Math.Max(300, ActualHeight - 142);
            var dayCellHeight = Math.Max(55, (availableCalendarHeight - 34) / rowCount);
            visibleEventLanes = Math.Max(1, Math.Min(12, (int)Math.Floor((dayCellHeight - Ui(29)) / Ui(20))));
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
                    VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#94A3B8"), FontSize = Ui(10), FontWeight = FontWeights.Bold };
                calendar.Children.Add(weekHeader);
            }
            for (var c = 0; c < 7; c++)
            {
                var day = new TextBlock { Text = weekdays[c], HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold, FontSize = Ui(13),
                    Foreground = IsRestDay(first.AddDays(c)) ? Brush("#DC6B73") : Brush("#0F766E") };
                Grid.SetColumn(day, c + weekOffset); calendar.Children.Add(day);
            }
            if (settings.ShowWeekNumbers)
                for (var r = 0; r < rowCount; r++)
                {
                    var week = new TextBlock { Text = "W" + GetWeekNumber(first.AddDays(r * 7)).ToString("00"),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brush("#64748B"), FontSize = Ui(10), FontWeight = FontWeights.SemiBold };
                    Grid.SetRow(week, r + 1); calendar.Children.Add(week);
                }
            for (var i = 0; i < rowCount * 7; i++) AddDayCell(first.AddDays(i), i / 7 + 1, i % 7 + weekOffset);
            for (var r = 0; r < rowCount; r++) AddWeekEventBars(first.AddDays(r * 7), r + 1, weekOffset);
            RenderDetail();
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
                Foreground = ActiveCalendarRangeMode != "weeks" && date.Month != shownMonth.Month ? Brush("#CBD5E1") : isHoliday ? Brush("#EF4444") : IsRestDay(date) ? Brush("#DC6B73") : Brush("#0F172A"),
                Margin = new Thickness(5, 3, 2, 4), Tag = diaryTarget, ToolTip = settings.UseDiary ? "더블클릭하여 일기 쓰기" : null };
            if (settings.UseDiary) number.MouseLeftButtonDown += openDiary;
            dateHeader.Children.Add(number);
            if (settings.ShowLunar)
            {
                var lunar = new TextBlock { Text = Lunar(date), Foreground = Brush("#8B5CF6"), FontSize = Ui(11), Tag = diaryTarget,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 1, 1, 2), ToolTip = settings.UseDiary ? "더블클릭하여 일기 쓰기" : null };
                if (settings.UseDiary) lunar.MouseLeftButtonDown += openDiary; dateHeader.Children.Add(lunar);
            }
            var solarTerm = settings.ShowSolarTerms ? SolarTerm(date) : null;
            if (!string.IsNullOrWhiteSpace(solarTerm))
                dateHeader.Children.Add(new TextBlock { Text = solarTerm, Foreground = Brush("#0F766E"), FontSize = Ui(11),
                    FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 1, 1, 2) });
            if (settings.UseDiary && diaryDates.Contains(date.Date))
            {
                var diaryDot = new TextBlock { Text = "•", Foreground = Brush("#7C3AED"), FontSize = Ui(14), FontWeight = FontWeights.Bold, Tag = diaryTarget,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 2), ToolTip = "작성한 일기 · 더블클릭하여 열기", Cursor = Cursors.Hand };
                diaryDot.MouseLeftButtonDown += openDiary; dateHeader.Children.Add(diaryDot);
            }
            var holidays = string.Join(", ", dateItems.Where(x => x.Category == "국경일").Select(x => x.Title).ToArray());
            if (date == DateTime.Today)
                dateHeader.Children.Add(new TextBlock { Text = "오늘", Foreground = Brush("#2563EB"), FontSize = Ui(10),
                    FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 1, 0, 2) });
            if (!string.IsNullOrWhiteSpace(holidays))
                dateHeader.Children.Add(new TextBlock { Text = (date == DateTime.Today ? ". " : "") + holidays, Foreground = Brush("#EF4444"), FontSize = Ui(11),
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
            return (settings.SelectedDateStyle == "fill" || settings.SelectedDateStyle == "both") && date.Date == selectedDate.Date ? Brush(settings.SelectedDateFillColor)
                : date.Date == DateTime.Today && (settings.TodayStyle == "fill" || settings.TodayStyle == "both") && settings.TodayColor != "none" ? Brush(settings.TodayColor)
                : custom ? Brush(customBackground) : Brush("#99FFFFFF");
        }

        void StyleDayCell(Border cell, DateTime date)
        {
            var selectedBorder = (settings.SelectedDateStyle == "border" || settings.SelectedDateStyle == "both") && date.Date == selectedDate.Date;
            var todayBorder = !selectedBorder && (settings.TodayStyle == "border" || settings.TodayStyle == "both") && date.Date == DateTime.Today;
            cell.Background = DayBackground(date);
            cell.BorderBrush = Brush(selectedBorder ? settings.SelectedDateBorderColor : todayBorder ? settings.TodayBorderColor : "#99CBD5E1");
            cell.BorderThickness = new Thickness(selectedBorder || todayBorder ? 2 : .5);
            cell.Margin = selectedBorder || todayBorder ? new Thickness(-1.5) : new Thickness(0);
            Panel.SetZIndex(cell, selectedBorder || todayBorder ? 2 : 0);
        }

        void AddWeekEventBars(DateTime weekStart, int row, int weekOffset)
        {
            var weekEnd = weekStart.AddDays(6);
            var weekItems = ProjectItems(weekStart, weekEnd).Where(x => x.Category != "국경일" && IsItemVisible(x) && ShowCompleted(x) &&
                x.Start.Date <= weekEnd && (x.End > x.Start ? x.End.AddTicks(-1).Date : x.Start.Date) >= weekStart);
            if (settings.CalendarOrderMode == "time")
                weekItems = settings.MultiDayFirst
                    ? weekItems.OrderBy(CompletedRank).ThenByDescending(IsMultiDay).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title)
                    : weekItems.OrderBy(CompletedRank).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title);
            else
                weekItems = settings.MultiDayFirst
                    ? weekItems.OrderBy(CompletedRank).ThenByDescending(IsMultiDay).ThenBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start)
                    : weekItems.OrderBy(CompletedRank).ThenBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start);
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
            foreach (var segment in segments.Where(x => x.Item4 < eventLaneLimit))
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
                    FontSize = Ui(11), Foreground = item.Important ? Brush("#F20D7A") : settings.PastelEventStyle ? Brush("#1F2937") : Brushes.White,
                    FontWeight = item.Important ? FontWeights.Bold : FontWeights.Normal, Padding = new Thickness(5, 1, 4, 1),
                    TextTrimming = TextTrimming.CharacterEllipsis, TextDecorations = item.Completed ? TextDecorations.Strikethrough : null };
                var bar = new Border { Child = text, Height = Ui(19), CornerRadius = new CornerRadius(4),
                    Background = item.Important ? Brush("#FFF1F7") : settings.PastelEventStyle ? PastelBrush(ItemColor(item), .72) : Brush(ItemColor(item)),
                    Margin = new Thickness(2, Ui(29 + lane * 20), 2, 0), VerticalAlignment = VerticalAlignment.Top,
                    Cursor = Cursors.Hand, ToolTip = "클릭하여 날짜 선택 · 더블클릭하여 수정" };
                bar.Tag = new ItemHitTarget { Item = item, SegmentStart = segmentStart, SegmentEnd = segmentEnd, Element = bar };
                if (settings.CompletedDisplayMode == "fade" && item.IsTodo && item.Completed) bar.Opacity = .48;
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
        }
    }
}
