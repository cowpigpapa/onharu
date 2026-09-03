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
            if (detailIncompleteMode)
            {
                var cutoff = IncompleteTodoCutoff();
                var periodStart = StartOfWeek(DateTime.Today).AddDays(detailMode == "next_week" ? 7 : 0);
                selectedTitle.FontSize = 15;
                selectedTitle.Text = detailMode == "selected" ? DetailIncompleteRange(cutoff, selectedDate)
                    : DetailIncompleteRange(periodStart, periodStart.AddDays(6));
                if (!AddIncompleteTodoCard())
                    detail.Children.Add(new TextBlock { Text = "미완료 일정이 없습니다.", Foreground = T("Disabled"), Margin = new Thickness(0, 8, 0, 0) });
                return;
            }
            if (detailMode == "selected")
            {
                selectedTitle.FontSize = 15;
                selectedTitle.Text = DetailDateTitle(selectedDate);
                var hasDayItems = AddDetailDay(selectedDate, false);
                if (!hasDayItems)
                    detail.Children.Add(new TextBlock { Text = "일정이 없습니다.", Foreground = T("Disabled"), Margin = new Thickness(0, 8, 0, 0) });
                var specialStart = detail.Children.Count;
                AddDdayCards(); AddAnniversaryCards();
                if (detail.Children.Count > specialStart)
                    detail.Children.Insert(specialStart, new Border { Height = 1, Background = T("Grid"),
                        Margin = new Thickness(3), IsHitTestVisible = false });
                ApplyDetailCardOrder();
                return;
            }
            var start = StartOfWeek(DateTime.Today).AddDays(detailMode == "next_week" ? 7 : 0);
            var end = start.AddDays(6);
            selectedTitle.FontSize = 15;
            selectedTitle.Text = DetailShortRange(start, end);
            var added = false;
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (!VisibleItems(date).Any()) continue;
                AddDetailDay(date, true); added = true;
            }
            if (!added) detail.Children.Add(new TextBlock { Text = "일정이 없습니다.", Foreground = T("Disabled"), Margin = new Thickness(0, 8, 0, 0) });
            var specialWeekStart = detail.Children.Count;
            AddDdayCards(); AddAnniversaryCards();
            if (detail.Children.Count > specialWeekStart)
                detail.Children.Insert(specialWeekStart, new Border { Height = 1, Background = T("Grid"),
                    Margin = new Thickness(3), IsHitTestVisible = false });
            // Weekly cards must remain attached to their date headers.
        }

        bool AddDetailDay(DateTime date, bool showDateHeader)
        {
            var dayItems = VisibleItems(date).Where(x => string.IsNullOrWhiteSpace(x.AnniversaryType)).ToList();
            if (showDateHeader)
                detail.Children.Add(new TextBlock { Text = date.ToString("M월 d일 ddd", new CultureInfo("ko-KR")),
                    Foreground = date.Date == DateTime.Today ? T("Accent") : T("Heading"), FontWeight = FontWeights.Bold,
                    FontSize = Ui(12), Margin = new Thickness(1, 8, 0, 3) });
            if (dayItems.Count == 0)
                return false;
            foreach (var categoryItems in DetailGroups(dayItems))
            {
                var timeMode = settings.DetailOrderMode == "time";
                var importantCard = !timeMode && settings.ImportantFirst && categoryItems.All(x => x.Important);
                var groupColor = ItemColor(categoryItems[0]);
                var cardForeground = importantCard ? EventTextBrush(categoryItems[0]) : timeMode ? (settings.ThemeId == "dark" ? Brushes.White : Brush("#334155"))
                    : new SolidColorBrush(CategoryColorSystem.DetailForeground(settings.ThemeId, groupColor));
                var cardSecondary = settings.ThemeId == "dark" ? Brushes.White : Brush("#64748B");
                var group = new StackPanel();
                var groupName = DetailGroupName(categoryItems);
                var groupKey = importantCard ? "★ " + groupName : groupName;
                var groupCollapsed = collapsedDetailGroups.Contains(groupKey);
                var groupHeader = new TextBlock { Text = "●  " + groupName, Foreground = cardForeground,
                    FontWeight = FontWeights.Bold, FontSize = Ui(12), VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    Tag = new DetailGroupHitTarget { GroupKey = groupKey } };
                var groupHeaderSurface = new DockPanel { Background = Brushes.Transparent, Cursor = Cursors.Arrow,
                    Margin = new Thickness(0, 0, 0, groupCollapsed ? 0 : 5), LastChildFill = false };
                groupHeaderSurface.Children.Add(groupHeader); group.Children.Add(groupHeaderSurface);
                foreach (var item in categoryItems)
                {
                    var row = new DockPanel { Margin = new Thickness(0, 2, 0, 5), Background = Brushes.Transparent, Cursor = Cursors.Arrow };
                    var itemForeground = item.Important ? EventTextBrush(item) : timeMode ? EventTextBrush(item) : cardForeground;
                    if (settings.CompletedDisplayMode == "fade" && item.IsTodo && item.Completed) row.Opacity = .66;
                    if (item.Category == "야구" || (item.AllDay && !string.IsNullOrWhiteSpace(item.GoogleCalendarId) && !item.IsTodo))
                    {
                        var sourceMark = new CheckBox { IsChecked = false, Focusable = false, Tag = "Unavailable", Cursor = Cursors.Hand,
                            ToolTip = "완료 체크를 지원하지 않는 일정입니다.",
                            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0) };
                        StyleVividCheckBox(sourceMark, ItemColor(item));
                        sourceMark.Click += delegate { sourceMark.IsChecked = false; };
                        var markSurface = new Border { Child = sourceMark, Padding = new Thickness(0, 0, 8, 0),
                            Background = Brushes.Transparent, Cursor = Cursors.Hand, Tag = "UnavailableTextSurface" };
                        DockPanel.SetDock(markSurface, Dock.Left); row.Children.Add(markSurface);
                    }
                    else if (item.IsTodo)
                    {
                        var check = new CheckBox { IsChecked = item.Completed,
                            ToolTip = GoogleTasks.IsTask(item) ? "완료 상태를 Google Tasks와 동기화합니다." :
                                !string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "완료 상태는 이 PC에 저장됩니다." : null,
                            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 8, 0) };
                        StyleVividCheckBox(check, ItemColor(item));
                        if (settings.ThemeId == "dark") check.BorderBrush = Brushes.White;
                        check.Click += async delegate { await SetTodoCompleted(item, check.IsChecked == true); };
                        DockPanel.SetDock(check, Dock.Left); row.Children.Add(check);
                    }
                    var text = new StackPanel();
                    text.Tag = new ItemHitTarget { Item = item, SegmentStart = date, SegmentEnd = date, Element = text, DetailCard = true };
                    var titleText = new TextBlock {
                        FontWeight = item.Important ? FontWeights.Bold : FontWeights.SemiBold,
                            Foreground = itemForeground,
                        TextDecorations = item.Completed ? TextDecorations.Strikethrough : null };
                    titleText.Inlines.Add(new System.Windows.Documents.Run((item.AllDay ? "" : TimeText(item.Start) + " ") +
                        (item.Important ? "★ " : "") + DdayText(item) + item.Title));
                    if (item.AllDay) titleText.Inlines.Add(new System.Windows.Documents.Run(" · 하루 종일") { Foreground = cardSecondary, FontSize = Ui(10) });
                    text.Children.Add(titleText);
                    if (!item.AllDay && IsMultiDay(item))
                        text.Children.Add(new TextBlock { Text = DetailTimeText(item, date),
                            FontSize = Ui(11), Foreground = itemForeground });
                    if (!string.IsNullOrWhiteSpace(item.Notes))
                        text.Children.Add(new TextBlock { Text = item.Notes, FontSize = Ui(11), Foreground = item.Important ? itemForeground : cardSecondary,
                            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                    text.Cursor = Cursors.Hand; text.HorizontalAlignment = HorizontalAlignment.Left;
                    EnableItemDrag(row, item);
                    text.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                    {
                        if (e.ClickCount != 2) return;
                        OpenEdit(item); e.Handled = true;
                    };
                    row.Children.Add(text);
                    if (itemNoticeId == item.Id) row.Margin = new Thickness(0, 3, 0, 2);
                    group.Children.Add(row);
                    if (groupCollapsed) row.Visibility = Visibility.Collapsed;
                    if (itemNoticeId == item.Id)
                    {
                        var notice = new TextBlock { Text = itemNoticeText, Foreground = Brush("#DC2626"), FontSize = Ui(11),
                            FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(item.IsTodo ? 24 : 0, 0, 0, 8) };
                        if (groupCollapsed) notice.Visibility = Visibility.Collapsed;
                        group.Children.Add(notice);
                    }
                }
                var cardBackground = importantCard ? EventBackgroundBrush(categoryItems[0]) : timeMode ? T("Card") : new SolidColorBrush(CategoryColorSystem.DetailBackground(settings.ThemeId, groupColor));
                var liftSurface = new Border { Background = cardBackground, CornerRadius = new CornerRadius(11),
                    Padding = new Thickness(10, 8, 10, groupCollapsed ? 9 : 7), Child = group };
                var detailCard = new Border { Background = Brushes.Transparent,
                    BorderBrush = importantCard ? EventTextBrush(categoryItems[0]) : timeMode ? (settings.ThemeId == "dark" ? T("Grid") : Brush("#CBD5E1"))
                        : new SolidColorBrush(CategoryColorSystem.DetailBorder(settings.ThemeId, groupColor)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 4, 0, 7),
                    Tag = groupKey, Child = liftSurface };
                EnableDetailCardOrder(groupHeaderSurface, detailCard, groupKey);
                detail.Children.Add(detailCard);
            }
            return true;
        }

        bool AddIncompleteTodoCard()
        {
            var cutoff = IncompleteTodoCutoff();
            var periodStart = StartOfWeek(DateTime.Today).AddDays(detailMode == "next_week" ? 7 : 0);
            var taskItems = items.Where(x => x.IsTodo && !x.Completed && x.Start.Date >= cutoff
                    && (detailMode == "selected" ? x.Start.Date <= selectedDate.Date
                        : x.Start.Date >= periodStart && x.Start.Date <= periodStart.AddDays(6))
                    && IsItemVisible(x) && string.IsNullOrWhiteSpace(x.AnniversaryType))
                .OrderBy(x => x.Start).ThenBy(x => x.Title).ToList();
            if (taskItems.Count == 0) return false;
            var foreground = T("Text");
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "●  미완료 To-Do", Foreground = foreground, FontWeight = FontWeights.Bold,
                FontSize = Ui(12), Margin = new Thickness(0, 0, 0, 6) });
            AddGoogleTaskSection(panel, "기한 지남", taskItems.Where(x => x.Start.Date < DateTime.Today), foreground, true);
            AddGoogleTaskSection(panel, "오늘", taskItems.Where(x => x.Start.Date == DateTime.Today), foreground, true);
            AddGoogleTaskSection(panel, "다가오는 할 일", taskItems.Where(x => x.Start.Date > DateTime.Today), foreground, true);
            var surface = new Border { Background = T("Card"),
                CornerRadius = new CornerRadius(11), Padding = new Thickness(10, 8, 10, 7), Child = panel };
            var card = new Border { Background = Brushes.Transparent, BorderBrush = settings.ThemeId == "dark" ? T("Grid") : Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 4, 0, 7), Child = surface,
                Tag = "미완료 To-Do" };
            detail.Children.Add(card); return true;
        }

        DateTime IncompleteTodoCutoff() { return DateTime.Today.AddMonths(-Math.Max(1, settings.IncompleteTodoLookbackMonths)).Date; }

        string DetailDateTitle(DateTime date)
        {
            return DetailDateValue(date) + date.ToString(" (ddd)", new CultureInfo("ko-KR"));
        }

        string DetailDateValue(DateTime date)
        {
            return date.ToString(settings.DetailDateFormat == "MM/dd/yy" ? "MM/dd/yy" : "yy/MM/dd", CultureInfo.InvariantCulture);
        }

        // 세부 제목의 날짜 표기는 선택 날짜·이번 주·다음 주와 카테고리순·시간순·미완료가
        // 모두 같은 양식을 쓴다. 한 화면에서 탭만 바꿨는데 표기가 달라지면 같은 값을 비교하기 어렵다.
        // 형식 자체는 사용자 설정(`yy/MM/dd` 또는 `MM/dd/yy`)을 따른다.
        string DetailShortRange(DateTime start, DateTime end)
        {
            return DetailDateValue(start) + " ~ " + DetailDateValue(end);
        }

        string DetailIncompleteRange(DateTime start, DateTime end)
        {
            return DetailShortRange(start, end);
        }

        void AddGoogleTaskSection(Panel panel, string title, IEnumerable<PlannerItem> source, Brush foreground, bool includeDate)
        {
            var rows = source.ToList(); if (rows.Count == 0) return;
            panel.Children.Add(new TextBlock { Text = title + " (" + rows.Count + ")", Foreground = T("Muted"), FontSize = Ui(10.5),
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 1) });
            AddGoogleTaskRows(panel, rows, foreground);
        }

        void AddGoogleTaskRows(Panel panel, IEnumerable<PlannerItem> rows, Brush foreground)
        {
            foreach (var item in rows)
            {
                var row = new DockPanel { Margin = new Thickness(0, 2, 0, 3) };
                var check = new CheckBox { IsChecked = item.Completed, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) };
                StyleVividCheckBox(check, ItemColor(item));
                check.Click += async delegate { await SetTodoCompleted(item, check.IsChecked == true); };
                DockPanel.SetDock(check, Dock.Left); row.Children.Add(check);
                var label = new TextBlock { Text = item.Start.ToString("M월 d일") + "  " + item.Title, Foreground = EventTextBrush(item),
                    TextDecorations = item.Completed ? TextDecorations.Strikethrough : null, TextTrimming = TextTrimming.CharacterEllipsis,
                    Cursor = Cursors.Hand, ToolTip = item.Title };
                // 고정 상태는 태그가 붙은 요소만 적중 지도에 담긴다. 태그가 없으면 이동에서는 열리고
                // 고정에서는 아무 일이 없다. 세부 카드의 일정 글자와 같은 표식을 써서 두 상태를 맞춘다.
                label.Tag = new ItemHitTarget { Item = item, SegmentStart = item.Start.Date, SegmentEnd = item.Start.Date,
                    Element = label, DetailCard = true };
                label.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) OpenEdit(item); };
                row.Children.Add(label); panel.Children.Add(row);
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
                    .OrderBy(x => x.Key.Item1).ThenBy(x => DetailGroupOrder(x.Key.Item2)).ThenBy(x => GroupOrder(x.First())).Select(x => x.ToList()).ToList();

            return new[]
            {
                orderedItems.Where(x => x.AllDay).ToList(),
                orderedItems.Where(x => !x.AllDay).OrderBy(x => x.Start).ToList()
            }.Where(x => x.Count > 0).OrderBy(x => DetailTimeGroupOrder(DetailGroupName(x))).ToList();
        }

        string DetailGroupName(List<PlannerItem> group)
        {
            return settings.DetailOrderMode == "time" ? (group.All(x => x.AllDay) ? "하루 종일" : "시간 일정") : DisplayGroup(group[0]);
        }

        int DetailGroupOrder(string name)
        {
            var index = settings.DetailCategoryOrder == null ? -1 : settings.DetailCategoryOrder.IndexOf(name);
            return index < 0 ? int.MaxValue : index;
        }

        int DetailTimeGroupOrder(string name)
        {
            var index = settings.DetailTimeOrder == null ? -1 : settings.DetailTimeOrder.IndexOf(name);
            if (index >= 0) return index;
            return name == "하루 종일" ? 0 : 1;
        }

        void UpdateDetailTabs()
        {
            if (detailPeriodSwitch != null)
            {
                detailPeriodSwitch.SetLabel(0, selectedDate.Date == DateTime.Today ? "오늘" : "선택 날짜");
                ApplyDetailSwitchPalette(detailPeriodSwitch);
                detailPeriodSwitch.SetSelected(detailMode == "this_week" ? 1 : detailMode == "next_week" ? 2 : 0, false);
            }
            StyleDetailHeaderActionButtons();
            if (detailScroll != null)
            {
                detailScroll.Resources["OnharuScrollThumb"] = Brush(OnharuStateColors.DetailScrollThumb(settings.ThemeId, detailMode));
            }
            if (dateColorButton != null)
            {
                var rangeTitle = detailMode != "selected" || detailIncompleteMode;
                // Collapsed로 감추면 머리글의 높이가 줄어 날짜 제목과 오른쪽 도구 아이콘이 함께
                // 2px 위로 움직인다. 시간순·카테고리순·미완료를 오갈 때마다 글자가 흔들려 보인다
                // (2026-09-03 사용자 보고). 자리는 그대로 두고 그림만 감춘다.
                dateColorButton.Visibility = rangeTitle ? Visibility.Hidden : Visibility.Visible;
                dateColorButton.IsHitTestVisible = !rangeTitle;
                if (rangeTitle) return;
                string selectedColor = null;
                var colored = settings.DateBackgroundColors != null && settings.DateBackgroundColors.TryGetValue(DateKey(selectedDate), out selectedColor);
                dateColorButton.Background = Brushes.Transparent;
                var starOutline = "#1F2937";
                var starFill = "#FFFFFF";
                if (colored)
                {
                    try
                    {
                        var baseColor = (Color)ColorConverter.ConvertFromString(selectedColor);
                        starFill = CategoryColorSystem.ToHex(CategoryColorSystem.StrongAccent(baseColor));
                        starOutline = starFill;
                    }
                    catch { starOutline = selectedColor; starFill = selectedColor; }
                }
                dateColorButton.Foreground = Brush(starOutline);
                dateColorButton.BorderBrush = Brushes.Transparent;
                dateColorButton.BorderThickness = new Thickness(0);
                dateColorButton.Content = ImportantDayStar(Brush(starOutline), Brush(starFill), colored ? 2.05 : 1.15);
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
