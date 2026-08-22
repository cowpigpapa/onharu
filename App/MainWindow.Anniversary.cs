using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void AddDdayCards()
        {
            if (!settings.DdayPanelVisible) return;
            // D-Day is an independent summary view. Calendar filters may hide
            // event bars without hiding explicitly pinned D-Days.
            var ddayItems = items.Where(x => x.ShowDday && string.IsNullOrWhiteSpace(x.AnniversaryType)
                    && (x.Start.Date - DateTime.Today).Days >= -7)
                .GroupBy(x => !string.IsNullOrWhiteSpace(x.GoogleRecurringEventId) ? "g:" + x.GoogleRecurringEventId :
                    !string.IsNullOrWhiteSpace(x.SeriesId) ? "s:" + x.SeriesId : "i:" + x.Id)
                .Select(x => x.OrderBy(y => Math.Abs((y.Start.Date - DateTime.Today).Days)).First())
                .OrderBy(x => Math.Abs((x.Start.Date - DateTime.Today).Days)).ThenBy(x => x.Start).ThenBy(x => x.Title).ToList();
            if (ddayItems.Count == 0) return;

            var stack = new StackPanel();
            var heading = new DockPanel { Height = 24, Margin = new Thickness(1, 0, 3, ddaySectionCollapsed ? 0 : 7), LastChildFill = true };
            heading.Children.Add(SectionTitleButton("◈  D-Day (" + ddayItems.Count + "개)", "#0369A1",
                ddaySectionCollapsed ? "D-Day 펼치기" : "D-Day 접기", delegate { ddaySectionCollapsed = !ddaySectionCollapsed; RenderDetail(); }));
            stack.Children.Add(heading);

            if (!ddaySectionCollapsed)
            {
                var visibleCount = ddayCardsExpanded ? ddayItems.Count : Math.Min(5, ddayItems.Count);
                foreach (var item in ddayItems.Take(visibleCount))
                {
                    var days = (item.Start.Date - DateTime.Today).Days;
                    var isToday = days == 0;
                    var label = isToday ? "D-Day" : days > 0 ? "D-" + days.ToString("N0") : "D+" + (-days).ToString("N0");
                    var content = new TextBlock { Tag = item, Cursor = Cursors.Hand, Text = "◈ " + item.Title + " · " + label,
                        FontSize = Ui(11), Foreground = Brush(isToday ? "#075985" : "#0369A1"), FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis,
                        ToolTip = item.Title + " · " + item.Start.ToString("yyyy.MM.dd") + " · " + label };
                    content.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                    { if (e.ClickCount == 2) { OpenEdit(item); e.Handled = true; } else e.Handled = true; };
                    stack.Children.Add(new Border { Background = isToday ? Brush("#E0F2FE") : Brushes.Transparent,
                        BorderBrush = Brush(isToday ? "#38BDF8" : "#BAE6FD"), BorderThickness = isToday ? new Thickness(1) : new Thickness(0, 0, 0, 1),
                        CornerRadius = isToday ? new CornerRadius(8) : new CornerRadius(0), Margin = isToday ? new Thickness(0, 1, 0, 2) : new Thickness(0),
                        Padding = isToday ? new Thickness(7, 5, 7, 5) : new Thickness(2, 5, 2, 5), Child = content });
                }
                if (ddayItems.Count > 5)
                {
                    var toggle = new Button { Content = ddayCardsExpanded ? "접기" : "+ " + (ddayItems.Count - 5) + "개 더보기",
                        Height = 29, Background = Brushes.Transparent, Foreground = Brush("#0284C7"), BorderThickness = new Thickness(0),
                        FontSize = Ui(11), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
                    toggle.Click += delegate { ddayCardsExpanded = !ddayCardsExpanded; RenderDetail(); }; stack.Children.Add(toggle);
                }
            }
            detail.Children.Add(new Border { Background = Brush("#F0F9FF"), BorderBrush = Brush("#BAE6FD"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 8, 11, 8), Margin = new Thickness(0, 8, 0, 0), Child = stack });
        }

        Button SmallSectionButton(string text, string background, string border, string foreground)
        {
            var label = new TextBlock { Text = text, Foreground = Brush(foreground), FontSize = Ui(10), FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var face = new Border { Width = 43, Height = 22, Background = Brush(background), BorderBrush = Brush(border), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Child = label, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var button = Button("", null, 45); button.Content = face; button.Height = 24; button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0); button.Padding = new Thickness(0); button.Cursor = Cursors.Hand;
            button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Template = ContentOnlyButtonTemplate(); return button;
        }

        Button SectionTitleButton(string text, string foreground, string tooltip, RoutedEventHandler click)
        {
            var title = new TextBlock { Text = text, FontSize = Ui(13), FontWeight = FontWeights.Bold,
                Foreground = Brush(foreground), VerticalAlignment = VerticalAlignment.Center };
            var button = new Button { Content = title, Height = 24, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(0) };
            button.HorizontalContentAlignment = HorizontalAlignment.Left; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Cursor = Cursors.Hand; button.ToolTip = tooltip;
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(System.Windows.Controls.Button.ContentProperty));
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = presenter };
            if (click != null) button.Click += click;
            return button;
        }

        void AddAnniversaryCards()
        {
            if (!settings.AnniversaryVisible) return;
            var anniversaries = items.Where(x => !string.IsNullOrWhiteSpace(x.AnniversaryType) && IsItemVisible(x) && x.AnniversaryDate.Year >= 1900)
                .GroupBy(x => !string.IsNullOrWhiteSpace(x.GoogleRecurringEventId) ? "g:" + x.GoogleRecurringEventId :
                    !string.IsNullOrWhiteSpace(x.SeriesId) ? "s:" + x.SeriesId : "t:" + x.Title + "|" + x.AnniversaryDate.ToString("MMdd"))
                .Select(x => x.OrderBy(y => y.AnniversaryDate).First())
                .OrderBy(x => AnniversaryRemainingDays(x.AnniversaryDate, DateTime.Today)).ThenBy(x => x.Title).ToList();
            var stack = new StackPanel();
            var heading = new DockPanel { Height = 24, Margin = new Thickness(1, 0, 3, anniversarySectionCollapsed ? 0 : 7), LastChildFill = true };
            var makeLabel = new TextBlock { Text = "만들기", Foreground = Brush("#0F766E"), FontSize = Ui(10), FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var makeFace = new Border { Width = 43, Height = 22, Background = Brush("#DFF7F1"), BorderBrush = Brush("#B7E8DC"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = makeLabel,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var make = Button("", delegate { OpenAnniversary(null); }, 45); make.Content = makeFace; make.Height = 24;
            make.Background = Brushes.Transparent; make.BorderThickness = new Thickness(0); make.Padding = new Thickness(0);
            make.HorizontalContentAlignment = HorizontalAlignment.Center; make.VerticalContentAlignment = VerticalAlignment.Center;
            make.Cursor = Cursors.Hand; make.ToolTip = "기념일 만들기";
            make.Template = ContentOnlyButtonTemplate();
            DockPanel.SetDock(make, Dock.Right); heading.Children.Add(make);
            heading.Children.Add(SectionTitleButton("✦  기념일 (" + anniversaries.Count + "개)", "#6D28D9",
                anniversarySectionCollapsed ? "기념일 펼치기" : "기념일 접기", delegate { anniversarySectionCollapsed = !anniversarySectionCollapsed; RenderDetail(); }));
            stack.Children.Add(heading);
            if (anniversarySectionCollapsed)
            {
                detail.Children.Add(new Border { Background = Brush("#FAF5FF"), BorderBrush = Brush("#DDD6FE"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 8, 11, 8), Margin = new Thickness(0, 8, 0, 0), Child = stack });
                return;
            }
            var visibleCount = AnniversaryVisibleCount(anniversaries.Count, anniversaryCardsExpanded);
            foreach (var item in anniversaries.Take(visibleCount))
            {
                var isToday = AnniversaryRemainingDays(item.AnniversaryDate, DateTime.Today) == 0;
                var content = new TextBlock { Tag = item, Cursor = Cursors.Hand, Text = "✦ " + item.Title + " · " + AnniversarySummary(item, DateTime.Today),
                    FontSize = Ui(11), Foreground = Brush(isToday ? "#BE185D" : "#6D28D9"), FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = item.Title + " · " + AnniversarySummary(item, DateTime.Today) };
                content.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    if (e.ClickCount == 2 && !string.IsNullOrWhiteSpace(item.AnniversaryType)) { OpenAnniversary(item); e.Handled = true; }
                    else if (e.ClickCount == 2) { OpenEdit(item); e.Handled = true; }
                    else e.Handled = true;
                };
                stack.Children.Add(new Border { Background = isToday ? Brush("#FDF2F8") : Brushes.Transparent,
                    BorderBrush = Brush(isToday ? "#F472B6" : "#E9D5FF"), BorderThickness = isToday ? new Thickness(1) : new Thickness(0, 0, 0, 1),
                    CornerRadius = isToday ? new CornerRadius(8) : new CornerRadius(0), Margin = isToday ? new Thickness(0, 1, 0, 2) : new Thickness(0),
                    Padding = isToday ? new Thickness(7, 5, 7, 5) : new Thickness(2, 5, 2, 5), Child = content });
            }
            if (anniversaries.Count > 5)
            {
                var remainingCount = anniversaries.Count - 5;
                var toggle = new Button { Content = anniversaryCardsExpanded ? "접기" : "+ " + remainingCount + "개 더보기",
                    Height = 29, Background = Brushes.Transparent, Foreground = Brush("#7C3AED"), BorderThickness = new Thickness(0),
                    FontSize = Ui(11), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
                toggle.Click += delegate { anniversaryCardsExpanded = !anniversaryCardsExpanded; RenderDetail(); };
                stack.Children.Add(toggle);
            }
            detail.Children.Add(new Border { Background = Brush("#FAF5FF"), BorderBrush = Brush("#DDD6FE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 8, 11, 8), Margin = new Thickness(0, 8, 0, 0), Child = stack });
        }

        static ControlTemplate ContentOnlyButtonTemplate()
        {
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(System.Windows.Controls.Button.ContentProperty));
            return new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = content };
        }

        void OpenAnniversary(PlannerItem existing)
        {
            if (existing == null && AnniversaryCount() >= 10) { ShowNotice("기념일은 최대 10개까지 등록할 수 있습니다. 기존 기념일을 더블클릭해 삭제한 뒤 다시 등록해 주세요.", true); return; }
            var window = new AnniversaryWindow(existing); PlaceCalendarDialog(window);
            if (window.ShowDialog() != true) { if (positionLocked && IsVisible) PublishAndHide(); return; }
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(existing.SeriesId)) items.RemoveAll(x => x.SeriesId == existing.SeriesId);
                else if (!string.IsNullOrWhiteSpace(existing.GoogleRecurringEventId)) items.RemoveAll(x => x.GoogleRecurringEventId == existing.GoogleRecurringEventId);
                else items.RemoveAll(x => x.Id == existing.Id);
            }
            if (window.DeleteRequested) { Store.Save(items); RenderAll(); if (positionLocked && IsVisible) PublishAndHide(); return; }
            var start = window.BaseDate.Date;
            if (window.ConvertToScheduleRequested)
            {
                items.Add(new PlannerItem { Id = Guid.NewGuid().ToString(), Title = window.AnniversaryTitle, Start = start, End = start.AddDays(1),
                    AllDay = true, IsTodo = true, Category = "개인일정", CreatedInOnharu = true, Important = false,
                    ShowDday = window.ShowDday, ReminderMinutes = -1, ReminderConfigured = true });
                Store.Save(items); RenderAll();
                if (positionLocked && IsVisible) PublishAndHide(); return;
            }
            var master = new PlannerItem { Id = Guid.NewGuid().ToString(), Title = window.AnniversaryTitle, Start = start, End = start.AddDays(1),
                AllDay = true, IsTodo = false, Category = "기념일", CreatedInOnharu = true, Important = false,
                ShowDday = window.ShowDday, AnniversaryDate = start, AnniversaryType = window.AnniversaryType,
                RecurrenceFrequency = "yearly", RecurrenceMode = "date", RecurrenceUntil = start, ReminderMinutes = -1, ReminderConfigured = true };
            // Store one anniversary basis record. Visible yearly occurrences are
            // projected at render time instead of materializing 100 years of data.
            items.Add(master); Store.Save(items);
            // Registering or editing a summary card must not navigate the main calendar.
            RenderAll();
            if (positionLocked && IsVisible) PublishAndHide();
        }

        int AnniversaryCount()
        {
            return items.Where(x => !string.IsNullOrWhiteSpace(x.AnniversaryType))
                .Select(x => !string.IsNullOrWhiteSpace(x.GoogleRecurringEventId) ? "g:" + x.GoogleRecurringEventId : !string.IsNullOrWhiteSpace(x.SeriesId) ? "s:" + x.SeriesId : "i:" + x.Id).Distinct().Count();
        }

        internal static string InferAnniversaryType(string title)
        {
            title = title ?? "";
            return title.Contains("생일") ? "birthday" : title.Contains("결혼") ? "wedding" : "other";
        }

        internal static int AnniversaryVisibleCount(int total, bool expanded)
        {
            return expanded ? Math.Max(0, total) : Math.Max(0, Math.Min(5, total));
        }

        internal static int AnniversaryElapsedDays(DateTime anniversary, DateTime today)
        {
            return Math.Max(0, (today.Date - anniversary.Date).Days);
        }

        internal static DateTime NextAnniversaryDate(DateTime anniversary, DateTime today)
        {
            var day = Math.Min(anniversary.Day, DateTime.DaysInMonth(today.Year, anniversary.Month));
            var next = new DateTime(today.Year, anniversary.Month, day);
            if (next < today.Date)
            {
                var year = today.Year + 1; day = Math.Min(anniversary.Day, DateTime.DaysInMonth(year, anniversary.Month));
                next = new DateTime(year, anniversary.Month, day);
            }
            return next;
        }

        internal static int AnniversaryRemainingDays(DateTime anniversary, DateTime today)
        {
            return (NextAnniversaryDate(anniversary, today) - today.Date).Days;
        }

        static string AnniversarySummary(PlannerItem item, DateTime today)
        {
            var elapsed = AnniversaryElapsedDays(item.AnniversaryDate, today);
            var remaining = AnniversaryRemainingDays(item.AnniversaryDate, today);
            return "+" + elapsed.ToString("N0") + "일" + (!item.ShowDday ? "" : " · " + (remaining == 0 ? "D-Day" : "D-" + remaining.ToString("N0")));
        }
    }
}
