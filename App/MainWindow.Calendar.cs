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
            var mondayFirst = settings.WeekNumberRule == "iso";
            var monthStart = new DateTime(shownMonth.Year, shownMonth.Month, 1);
            var monthOffset = mondayFirst ? ((int)monthStart.DayOfWeek + 6) % 7 : (int)monthStart.DayOfWeek;
            var rowCount = settings.CalendarRangeMode == "month5" ? 5 : 6;
            DateTime first;
            if (settings.CalendarRangeMode == "weeks")
            {
                rowCount = Math.Max(1, Math.Min(6, settings.VisibleWeekCount));
                var anchorOffset = mondayFirst ? ((int)shownMonth.DayOfWeek + 6) % 7 : (int)shownMonth.DayOfWeek;
                first = shownMonth.Date.AddDays(-anchorOffset - (Math.Max(1, Math.Min(rowCount, settings.TodayRow)) - 1) * 7);
                var last = first.AddDays(rowCount * 7 - 1);
                monthTitle.Content = first.Year == last.Year
                    ? first.ToString("yyyy년 M월 d일") + " – " + last.ToString("M월 d일")
                    : first.ToString("yyyy년 M월 d일") + " – " + last.ToString("yyyy년 M월 d일");
            }
            else
            {
                first = monthStart.AddDays(-monthOffset);
                if (settings.CalendarRangeMode == "monthAuto")
                    rowCount = Math.Max(4, Math.Min(6, (int)Math.Ceiling((monthOffset + DateTime.DaysInMonth(monthStart.Year, monthStart.Month)) / 7.0)));
                monthTitle.Content = monthStart.ToString("yyyy년 M월");
            }
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
            var weekdays = mondayFirst ? new[] { "월", "화", "수", "목", "금", "토", "일" } : new[] { "일", "월", "화", "수", "목", "금", "토" };
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
                    Foreground = weekdays[c] == "일" ? Brush("#DC2626") : weekdays[c] == "토" ? Brush("#2563EB") : Brush("#0F766E") };
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

        void AddDayCell(DateTime date, int row, int col)
        {
            var stack = new StackPanel();
            var dateItems = VisibleItems(date).ToList();
            var isHoliday = dateItems.Any(x => x.Category == "국경일");
            var dateHeader = new StackPanel { Orientation = Orientation.Horizontal };
            var number = new TextBlock { Text = date.Day.ToString(), FontSize = Ui(13), FontWeight = date == DateTime.Today ? FontWeights.Bold : FontWeights.Normal,
                Foreground = settings.CalendarRangeMode != "weeks" && date.Month != shownMonth.Month ? Brush("#CBD5E1") : isHoliday || date.DayOfWeek == DayOfWeek.Sunday ? Brush("#EF4444") : date.DayOfWeek == DayOfWeek.Saturday ? Brush("#3B82F6") : Brush("#0F172A"),
                Margin = new Thickness(5, 3, 2, 4) };
            dateHeader.Children.Add(number);
            if (settings.ShowLunar)
                dateHeader.Children.Add(new TextBlock { Text = Lunar(date), Foreground = Brush("#8B5CF6"), FontSize = Ui(11),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 1, 1, 2) });
            var solarTerm = settings.ShowSolarTerms ? SolarTerm(date) : null;
            if (!string.IsNullOrWhiteSpace(solarTerm))
                dateHeader.Children.Add(new TextBlock { Text = solarTerm, Foreground = Brush("#0F766E"), FontSize = Ui(11),
                    FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 1, 1, 2) });
            var holidays = string.Join(", ", dateItems.Where(x => x.Category == "국경일").Select(x => x.Title).ToArray());
            if (date == DateTime.Today)
                dateHeader.Children.Add(new TextBlock { Text = "오늘", Foreground = Brush("#2563EB"), FontSize = Ui(10),
                    FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 1, 0, 2) });
            if (!string.IsNullOrWhiteSpace(holidays))
                dateHeader.Children.Add(new TextBlock { Text = (date == DateTime.Today ? ". " : "") + holidays, Foreground = Brush("#EF4444"), FontSize = Ui(11),
                    FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(3, 1, 2, 2), TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(dateHeader);
            var border = new Border { Child = stack, Tag = date, Cursor = Cursors.Hand };
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
            return settings.SelectedDateStyle != "border" && date.Date == selectedDate.Date ? Brush(settings.SelectedDateFillColor) : date.Date == DateTime.Today ? Brush("#CCFCE7F3")
                : custom ? Brush(customBackground) : Brush("#99FFFFFF");
        }

        void StyleDayCell(Border cell, DateTime date)
        {
            var selectedBorder = settings.SelectedDateStyle == "border" && date.Date == selectedDate.Date;
            cell.Background = DayBackground(date);
            cell.BorderBrush = Brush(selectedBorder ? settings.SelectedDateBorderColor : "#99CBD5E1");
            cell.BorderThickness = new Thickness(selectedBorder ? 2 : .5);
            cell.Margin = selectedBorder ? new Thickness(-1.5) : new Thickness(0);
            Panel.SetZIndex(cell, selectedBorder ? 2 : 0);
        }

        void AddWeekEventBars(DateTime weekStart, int row, int weekOffset)
        {
            var weekEnd = weekStart.AddDays(6);
            var weekItems = items.Where(x => x.Category != "국경일" && IsItemVisible(x) && ShowCompleted(x) &&
                x.Start.Date <= weekEnd && (x.End > x.Start ? x.End.AddTicks(-1).Date : x.Start.Date) >= weekStart);
            if (settings.CalendarOrderMode == "time")
                weekItems = settings.MultiDayFirst
                    ? weekItems.OrderBy(CompletedRank).ThenByDescending(IsMultiDay).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title)
                    : weekItems.OrderBy(CompletedRank).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title);
            else
                weekItems = settings.MultiDayFirst
                    ? weekItems.OrderBy(CompletedRank).ThenByDescending(IsMultiDay).ThenBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start)
                    : weekItems.OrderBy(CompletedRank).ThenBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start);
            var laneEnds = new List<DateTime>();
            var segments = new List<Tuple<PlannerItem, DateTime, DateTime, int>>();
            foreach (var item in weekItems)
            {
                var itemEnd = item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date;
                var segmentStart = item.Start.Date < weekStart ? weekStart : item.Start.Date;
                var segmentEnd = itemEnd > weekEnd ? weekEnd : itemEnd;
                var lane = laneEnds.FindIndex(x => x < segmentStart);
                if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(segmentEnd); } else laneEnds[lane] = segmentEnd;
                segments.Add(Tuple.Create(item, segmentStart, segmentEnd, lane));
            }
            var overflow = segments.Any(x => x.Item4 >= visibleEventLanes);
            var eventLaneLimit = overflow ? Math.Max(0, visibleEventLanes - 1) : visibleEventLanes;
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
                bar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    var days = (segmentEnd - segmentStart).Days + 1;
                    var clickedDay = bar.ActualWidth <= 0 ? 0 : Math.Min(days - 1, Math.Max(0, (int)(e.GetPosition(bar).X / bar.ActualWidth * days)));
                    var clickedDate = segmentStart.AddDays(clickedDay);
                    if (e.ClickCount == 2) { selectedDate = clickedDate; detailMode = "selected"; OpenEdit(item); }
                    else SelectDateFast(clickedDate);
                    e.Handled = true;
                };
                Grid.SetRow(bar, row); Grid.SetColumn(bar, (segmentStart - weekStart).Days + weekOffset);
                Grid.SetColumnSpan(bar, (segmentEnd - segmentStart).Days + 1); Panel.SetZIndex(bar, 5); calendar.Children.Add(bar);
            }
            if (overflow)
            {
                var hidden = segments.Count(x => x.Item4 >= eventLaneLimit);
                var more = new Border { Height = Ui(19), CornerRadius = new CornerRadius(4),
                    Background = Brush("#E2E8F0"), Margin = new Thickness(2, Ui(29 + eventLaneLimit * 20), 2, 0),
                    VerticalAlignment = VerticalAlignment.Top, ToolTip = hidden + "개 일정이 셀 높이를 넘어 숨겨졌습니다.",
                    Child = new TextBlock { Text = "⌄  +" + hidden + "개 일정", FontSize = Ui(10), Foreground = Brush("#475569"),
                        FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(4, 1, 4, 1) } };
                Grid.SetRow(more, row); Grid.SetColumn(more, weekOffset); Grid.SetColumnSpan(more, 7); Panel.SetZIndex(more, 6); calendar.Children.Add(more);
            }
        }
    }
}
