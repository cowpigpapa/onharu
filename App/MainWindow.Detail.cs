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
                selectedTitle.Text = selectedDate.ToString("M월 d일 (ddd)", new CultureInfo("ko-KR"));
                AddDetailDay(selectedDate, false);
                var add = Button("+ 이 날짜에 추가", AddItem, 150); add.Height = 27; add.Margin = new Thickness(0, 3, 0, 0); detail.Children.Add(add);
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
            if (!added) detail.Children.Add(new TextBlock { Text = "일정이 없습니다.", Foreground = T("Disabled"), Margin = new Thickness(0, 8, 0, 0) });
            AddDdayCards(); AddAnniversaryCards();
        }

        void AddDetailDay(DateTime date, bool showDateHeader)
        {
            var dayItems = VisibleItems(date).Where(x => string.IsNullOrWhiteSpace(x.AnniversaryType)).ToList();
            if (showDateHeader)
                detail.Children.Add(new TextBlock { Text = date.ToString("M월 d일 ddd", new CultureInfo("ko-KR")),
                    Foreground = date.Date == DateTime.Today ? T("Accent") : T("Heading"), FontWeight = FontWeights.Bold,
                    FontSize = Ui(12), Margin = new Thickness(1, 8, 0, 3) });
            if (dayItems.Count == 0)
            {
                detail.Children.Add(new TextBlock { Text = "일정이 없습니다.", Foreground = T("Disabled"), Margin = new Thickness(0, 8, 0, 0) });
                return;
            }
            foreach (var categoryItems in DetailGroups(dayItems))
            {
                var groupColor = ItemColor(categoryItems[0]);
                var cardForeground = categoryItems[0].Important ? EventTextBrush(categoryItems[0])
                    : new SolidColorBrush(CategoryColorSystem.DetailForeground(settings.ThemeId, groupColor));
                var cardSecondary = settings.ThemeId == "dark" ? Brushes.White : Brush("#64748B");
                var group = new StackPanel();
                group.Children.Add(new TextBlock { Text = "●  " + DisplayGroup(categoryItems[0]), Foreground = cardForeground,
                    FontWeight = FontWeights.Bold, FontSize = Ui(12), Margin = new Thickness(0, 0, 0, 7) });
                foreach (var item in categoryItems)
                {
                    var row = new DockPanel { Margin = new Thickness(0, 2, 0, 5) };
                    if (settings.CompletedDisplayMode == "fade" && item.IsTodo && item.Completed) row.Opacity = .48;
                    if (item.IsTodo)
                    {
                        var check = new CheckBox { IsChecked = item.Completed,
                            ToolTip = GoogleTasks.IsTask(item) ? "완료 상태를 Google Tasks와 동기화합니다." :
                                !string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "완료 상태는 이 PC에 저장됩니다." : null,
                            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 8, 0) };
                        StyleThemeCheckBox(check, ItemColor(item));
                        if (settings.ThemeId == "dark") check.BorderBrush = Brushes.White;
                        check.Click += async delegate { await SetTodoCompleted(item, check.IsChecked == true); };
                        DockPanel.SetDock(check, Dock.Left); row.Children.Add(check);
                    }
                    var text = new StackPanel();
                    text.Tag = new ItemHitTarget { Item = item, SegmentStart = date, SegmentEnd = date, Element = text, DetailCard = true };
                    var titleText = new TextBlock {
                        FontWeight = item.Important ? FontWeights.Bold : FontWeights.SemiBold,
                            Foreground = cardForeground,
                        TextDecorations = item.Completed ? TextDecorations.Strikethrough : null };
                    titleText.Inlines.Add(new System.Windows.Documents.Run((item.Important ? "★ " : "") + DdayText(item) + (item.AllDay ? "" : TimeText(item.Start) + " ") + item.Title));
                    if (item.AllDay) titleText.Inlines.Add(new System.Windows.Documents.Run(" · 하루 종일") { Foreground = cardSecondary, FontSize = Ui(10) });
                    text.Children.Add(titleText);
                    if (!item.AllDay && IsMultiDay(item))
                        text.Children.Add(new TextBlock { Text = DetailTimeText(item, date),
                            FontSize = Ui(11), Foreground = cardForeground });
                    if (!string.IsNullOrWhiteSpace(item.Notes))
                        text.Children.Add(new TextBlock { Text = item.Notes, FontSize = Ui(11), Foreground = cardSecondary,
                            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                    text.Cursor = Cursors.Hand; text.ToolTip = "더블클릭하여 수정";
                    text.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                    {
                        if (e.ClickCount != 2) return;
                        OpenEdit(item); e.Handled = true;
                    };
                    row.Children.Add(text);
                    if (itemNoticeId == item.Id) row.Margin = new Thickness(0, 3, 0, 2);
                    group.Children.Add(row);
                    if (itemNoticeId == item.Id)
                        group.Children.Add(new TextBlock { Text = itemNoticeText, Foreground = Brush("#DC2626"), FontSize = Ui(11),
                            FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(item.IsTodo ? 24 : 0, 0, 0, 8) });
                }
                detail.Children.Add(new Border { Background = categoryItems[0].Important ? EventBackgroundBrush(categoryItems[0]) : new SolidColorBrush(CategoryColorSystem.DetailBackground(settings.ThemeId, groupColor)),
                    BorderBrush = new SolidColorBrush(CategoryColorSystem.DetailBorder(settings.ThemeId, groupColor)), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(11), Padding = new Thickness(10, 8, 10, 7), Margin = new Thickness(0, 3, 0, 5), Child = group });
            }
        }

        async Task SetTodoCompleted(PlannerItem item, bool completed)
        {
            item.Completed = completed; Store.Save(items); RenderAll();
            if (!GoogleTasks.IsTask(item) || !GoogleCalendar.IsConnected) return;
            try
            {
                await GoogleTasks.SetCompletedAsync(item, completed); item.PendingGoogleSync = false; Store.Save(items);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Update Google Task completion", ex); item.PendingGoogleSync = true; Store.Save(items);
                ShowItemNotice(item, "로컬 저장됨 · Google Tasks 동기화 대기");
            }
        }

        IEnumerable<List<PlannerItem>> DetailGroups(List<PlannerItem> orderedItems)
        {
            if (settings.DetailOrderMode != "time")
                return orderedItems.GroupBy(x => Tuple.Create(ImportantRank(x), DisplayGroup(x)))
                    .OrderBy(x => x.Key.Item1).ThenBy(x => GroupOrder(x.First())).Select(x => x.ToList()).ToList();

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
            var button = Button(text, null, 88); button.Height = 27; button.Margin = new Thickness(0); button.FontSize = Ui(11);
            button.Click += delegate { detailMode = mode; RenderDetail(); };
            return button;
        }

        void UpdateDetailTabs()
        {
            foreach (var entry in new[]
            {
                Tuple.Create(selectedDayButton, detailMode == "selected"),
                Tuple.Create(thisWeekButton, detailMode == "this_week"),
                Tuple.Create(nextWeekButton, detailMode == "next_week")
            })
            {
                if (entry.Item1 == null) continue;
                var colors = OnharuStateColors.DetailTab(settings.ThemeId, entry.Item2, ActionAccentColor());
                entry.Item1.Background = new SolidColorBrush(colors.Background);
                entry.Item1.Foreground = new SolidColorBrush(colors.Foreground);
                entry.Item1.BorderBrush = new SolidColorBrush(colors.Border);
            }
            if (dateColorButton != null)
            {
                dateColorButton.Visibility = detailMode == "selected" ? Visibility.Visible : Visibility.Collapsed;
                string selectedColor = null;
                var colored = settings.DateBackgroundColors != null && settings.DateBackgroundColors.TryGetValue(DateKey(selectedDate), out selectedColor);
                var colors = OnharuStateColors.ImportantDay(colored ? selectedColor : null);
                dateColorButton.Background = Brushes.Transparent;
                dateColorButton.Foreground = new SolidColorBrush(colors.Foreground);
                dateColorButton.BorderBrush = Brushes.Transparent;
                dateColorButton.Content = HeaderGlyph("important_day", new SolidColorBrush(colors.Foreground));
                dateColorButton.ToolTip = colored ? "중요한 날 · 색상 변경" : "중요한 날로 표시";
            }
        }

        DateTime StartOfWeek(DateTime date)
        {
            var firstDay = ConfiguredFirstDay();
            return date.Date.AddDays(-((7 + (int)date.DayOfWeek - (int)firstDay) % 7));
        }
    }
}
