using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void RenderDetail()
        {
            detail.Children.Clear(); UpdateDetailTabs();
            if (detailMode == "selected")
            {
                selectedTitle.Text = selectedDate.ToString("M월 d일 dddd", new CultureInfo("ko-KR"));
                AddDetailDay(selectedDate, false);
                var add = Button("+ 이 날짜에 추가", AddItem, 150); add.Margin = new Thickness(0, 14, 0, 0); detail.Children.Add(add);
                AddDdayCards(); AddAnniversaryCards();
                return;
            }
            var start = StartOfWeek(DateTime.Today).AddDays(detailMode == "next_week" ? 7 : 0);
            var end = start.AddDays(6);
            selectedTitle.Text = (detailMode == "next_week" ? "다음 주" : "이번 주") + " · " + start.ToString("M/d") + "–" + end.ToString("M/d");
            var added = false;
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (!VisibleItems(date).Any()) continue;
                AddDetailDay(date, true); added = true;
            }
            if (!added) detail.Children.Add(new TextBlock { Text = "일정이 없습니다.", Foreground = Brush("#94A3B8"), Margin = new Thickness(0, 8, 0, 0) });
            AddDdayCards(); AddAnniversaryCards();
        }

        void AddDetailDay(DateTime date, bool showDateHeader)
        {
            var dayItems = VisibleItems(date).Where(x => string.IsNullOrWhiteSpace(x.AnniversaryType)).ToList();
            if (showDateHeader)
                detail.Children.Add(new TextBlock { Text = date.ToString("M월 d일 ddd", new CultureInfo("ko-KR")),
                    Foreground = date.Date == DateTime.Today ? Brush("#2563EB") : Brush("#475569"), FontWeight = FontWeights.Bold,
                    FontSize = Ui(12), Margin = new Thickness(1, 8, 0, 3) });
            if (dayItems.Count == 0)
            {
                detail.Children.Add(new TextBlock { Text = "일정이 없습니다.", Foreground = Brush("#94A3B8"), Margin = new Thickness(0, 8, 0, 0) });
                return;
            }
            foreach (var categoryItems in DetailGroups(dayItems))
            {
                var groupColor = ItemColor(categoryItems[0]);
                var group = new StackPanel();
                group.Children.Add(new TextBlock { Text = "●  " + DisplayGroup(categoryItems[0]), Foreground = Brush(groupColor),
                    FontWeight = FontWeights.Bold, FontSize = Ui(12), Margin = new Thickness(0, 0, 0, 7) });
                foreach (var item in categoryItems)
                {
                    var row = new DockPanel { Margin = new Thickness(0, 2, 0, 5) };
                    if (settings.CompletedDisplayMode == "fade" && item.IsTodo && item.Completed) row.Opacity = .48;
                    if (item.IsTodo)
                    {
                        var check = new CheckBox { IsChecked = item.Completed,
                            ToolTip = item.GoogleTaskEvent ? "완료 상태는 온하루에 저장됩니다." : null,
                            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 8, 0) };
                        check.Click += async delegate
                        {
                            item.Completed = check.IsChecked == true; Store.Save(items); RenderAll();
                            if (item.Category == "개인일정" && !item.GoogleTaskEvent && !item.GoogleReadOnly && GoogleCalendar.IsConnected) await SaveGoogleItem(item);
                        };
                        DockPanel.SetDock(check, Dock.Left); row.Children.Add(check);
                    }
                    var text = new StackPanel();
                    text.Tag = new ItemHitTarget { Item = item, SegmentStart = date, SegmentEnd = date, Element = text, DetailCard = true };
                    var titleText = new TextBlock {
                        FontWeight = item.Important ? FontWeights.Bold : FontWeights.SemiBold,
                        Foreground = item.Important ? Brush("#F20D7A") : item.Category == "국경일" ? Brush("#EF4444") : Brush("#1E293B"),
                        TextDecorations = item.Completed ? TextDecorations.Strikethrough : null };
                    titleText.Inlines.Add(new System.Windows.Documents.Run((item.Important ? "★ " : "") + DdayText(item) + (item.AllDay ? "" : TimeText(item.Start) + " ") + item.Title));
                    if (item.AllDay) titleText.Inlines.Add(new System.Windows.Documents.Run(" · 하루 종일") { Foreground = Brush("#94A3B8"), FontSize = Ui(10) });
                    text.Children.Add(titleText);
                    if (!item.AllDay && IsMultiDay(item))
                        text.Children.Add(new TextBlock { Text = DetailTimeText(item, date),
                            FontSize = Ui(11), Foreground = Brush(ItemColor(item)) });
                    if (!string.IsNullOrWhiteSpace(item.Notes))
                        text.Children.Add(new TextBlock { Text = item.Notes, FontSize = Ui(11), Foreground = Brush("#64748B"),
                            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                    text.Cursor = Cursors.Hand; text.ToolTip = "더블클릭하여 수정";
                    text.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                    {
                        if (e.ClickCount == 2) { OpenEdit(item); e.Handled = true; }
                    };
                    row.Children.Add(text);
                    if (itemNoticeId == item.Id) row.Margin = new Thickness(0, 3, 0, 2);
                    group.Children.Add(row);
                    if (itemNoticeId == item.Id)
                        group.Children.Add(new TextBlock { Text = itemNoticeText, Foreground = Brush("#DC2626"), FontSize = Ui(11),
                            FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(item.IsTodo ? 24 : 0, 0, 0, 8) });
                }
                detail.Children.Add(new Border { Background = PastelBrush(groupColor, .86),
                    BorderBrush = PastelBrush(groupColor, .62), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(11), Padding = new Thickness(10, 8, 10, 7), Margin = new Thickness(0, 3, 0, 5), Child = group });
            }
        }

        IEnumerable<List<PlannerItem>> DetailGroups(List<PlannerItem> orderedItems)
        {
            if (settings.CalendarOrderMode != "time")
                return orderedItems.GroupBy(DisplayGroup).OrderBy(x => GroupOrder(x.First())).Select(x => x.ToList()).ToList();

            var groups = new List<List<PlannerItem>>();
            foreach (var item in orderedItems)
            {
                if (groups.Count == 0 || DisplayGroup(groups[groups.Count - 1][0]) != DisplayGroup(item))
                    groups.Add(new List<PlannerItem>());
                groups[groups.Count - 1].Add(item);
            }
            return groups;
        }

        Button DetailTab(string text, string mode)
        {
            var button = Button(text, null, 80); button.Height = 31; button.Margin = new Thickness(2, 0, 2, 0); button.FontSize = Ui(11);
            button.Click += delegate { detailMode = mode; RenderDetail(); };
            return button;
        }

        void UpdateDetailTabs()
        {
            foreach (var entry in new[] { Tuple.Create(selectedDayButton, "selected"), Tuple.Create(thisWeekButton, "this_week"), Tuple.Create(nextWeekButton, "next_week") })
            {
                if (entry.Item1 == null) continue;
                var selected = detailMode == entry.Item2;
                entry.Item1.Background = Brush(selected ? "#4F46E5" : "#EEF2FF");
                entry.Item1.Foreground = Brush(selected ? "#FFFFFF" : "#4338CA");
            }
            if (dateColorButton != null)
            {
                dateColorButton.Visibility = detailMode == "selected" ? Visibility.Visible : Visibility.Collapsed;
                string selectedColor = null;
                var colored = settings.DateBackgroundColors != null && settings.DateBackgroundColors.TryGetValue(DateKey(selectedDate), out selectedColor);
                dateColorButton.Background = colored ? Brush(selectedColor) : Brushes.White;
                dateColorButton.Foreground = colored ? Brush("#F20D7A") : Brush("#64748B");
                dateColorButton.BorderBrush = colored ? Brush("#FBCFE8") : Brush("#CBD5E1");
                dateColorButton.ToolTip = colored ? "중요한 날 색상 변경" : "중요한 날 배경색 선택";
            }
        }

        DateTime StartOfWeek(DateTime date)
        {
            var firstDay = settings.WeekNumberRule == "iso" ? DayOfWeek.Monday : DayOfWeek.Sunday;
            return date.Date.AddDays(-((7 + (int)date.DayOfWeek - (int)firstDay) % 7));
        }
    }
}
