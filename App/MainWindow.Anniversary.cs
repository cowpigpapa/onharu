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
            if (!settings.DdayEnabled || !settings.DdayPanelVisible) return;
            // D-Day is an independent summary view. Calendar filters may hide
            // event bars without hiding explicitly pinned D-Days.
            var ddayItems = items.Where(x => x.ShowDday && string.IsNullOrWhiteSpace(x.AnniversaryType)
                    && (x.Start.Date - DateTime.Today).Days >= -7)
                .GroupBy(x => !string.IsNullOrWhiteSpace(x.GoogleRecurringEventId) ? "g:" + x.GoogleRecurringEventId :
                    !string.IsNullOrWhiteSpace(x.SeriesId) ? "s:" + x.SeriesId : "i:" + x.Id)
                .Select(x => x.OrderBy(y => Math.Abs((y.Start.Date - DateTime.Today).Days)).First())
                .OrderBy(x => Math.Abs((x.Start.Date - DateTime.Today).Days)).ThenBy(x => x.Start).ThenBy(x => x.Title).ToList();
            if (ddayItems.Count == 0) return;

            var ddayColor = Colors["D-Day"];
            var ddayForeground = new SolidColorBrush(CategoryColorSystem.DetailForeground(settings.ThemeId, ddayColor));
            var ddayBackground = new SolidColorBrush(CategoryColorSystem.DetailBackground(settings.ThemeId, ddayColor));
            var ddayBorder = new SolidColorBrush(CategoryColorSystem.DetailBorder(settings.ThemeId, ddayColor));

            var stack = new StackPanel();
            var heading = new DockPanel { Height = ddaySectionCollapsed ? 18 : 24, Margin = new Thickness(1, 0, 3, ddaySectionCollapsed ? 0 : 1), LastChildFill = true };
            var titleButton = SectionTitleButton("◈  D-Day (" + ddayItems.Count + "개)", ddayForeground,
                ddaySectionCollapsed ? "D-Day 펼치기" : "D-Day 접기", delegate { ddaySectionCollapsed = !ddaySectionCollapsed; RenderDetail(); }, 18);
            heading.Children.Add(titleButton);
            stack.Children.Add(heading);

            if (!ddaySectionCollapsed)
            {
                var visibleCount = ddayCardsExpanded ? ddayItems.Count : Math.Min(5, ddayItems.Count);
                var visibleItems = ddayItems.Take(visibleCount).ToList();
                for (var index = 0; index < visibleItems.Count; index++)
                {
                    var item = visibleItems[index];
                    var days = (item.Start.Date - DateTime.Today).Days;
                    var isToday = days == 0;
                    var label = isToday ? "D-Day" : days > 0 ? "D-" + days.ToString("N0") : "D+" + (-days).ToString("N0");
                    var content = new TextBlock { Tag = item, Cursor = Cursors.Hand, Text = "◈ " + item.Title + " · " + label,
                        FontSize = Ui(11), Foreground = ddayForeground, FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis,
                        ToolTip = item.Title + " · " + item.Start.ToString("yyyy.MM.dd") + " · " + label };
                    content.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                    { if (e.ClickCount == 2) { OpenEdit(item); e.Handled = true; } else e.Handled = true; };
                    stack.Children.Add(new Border { Background = isToday ? ddayBackground : Brushes.Transparent,
                        BorderBrush = ddayBorder, BorderThickness = isToday ? new Thickness(1) : new Thickness(0, 0, 0, index < visibleItems.Count - 1 || ddayItems.Count > 5 ? 1 : 0),
                        CornerRadius = isToday ? new CornerRadius(8) : new CornerRadius(0), Margin = isToday ? new Thickness(0, 1, 0, 2) : new Thickness(0),
                        Padding = isToday ? new Thickness(7, 5, 7, 5) : new Thickness(2, 5, 2, 5), Child = content });
                }
                if (ddayItems.Count > 5)
                {
                    var toggle = new Button { Content = ddayCardsExpanded ? "접기" : "+ " + (ddayItems.Count - 5) + "개 더보기",
                        Height = 29, Background = Brushes.Transparent, Foreground = ddayForeground, BorderThickness = new Thickness(0),
                        FontSize = Ui(11), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
                    toggle.Click += delegate { ddayCardsExpanded = !ddayCardsExpanded; RenderDetail(); }; stack.Children.Add(toggle);
                }
            }
            var ddayCard = SpecialDetailCard(ddayBackground, ddayBorder,
                new Thickness(10, 8, 10, ddaySectionCollapsed ? 8 : 7), "special:D-Day", stack);
            EnableDetailCardOrder(heading, ddayCard, "special:D-Day", false); detail.Children.Add(ddayCard);
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

        Button SectionTitleButton(string text, Brush foreground, string tooltip, RoutedEventHandler click, double height = 24)
        {
            var title = new TextBlock { Text = text, FontSize = Ui(12), FontWeight = FontWeights.Bold,
                Foreground = foreground, VerticalAlignment = VerticalAlignment.Center };
            var button = new Button { Content = title, Height = height, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left };
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
            if (!settings.AnniversaryEnabled || !settings.AnniversaryVisible) return;
            var anniversaries = items.Where(x => !string.IsNullOrWhiteSpace(x.AnniversaryType) && IsItemVisible(x) && x.AnniversaryDate.Year >= 1900)
                .GroupBy(x => !string.IsNullOrWhiteSpace(x.GoogleRecurringEventId) ? "g:" + x.GoogleRecurringEventId :
                    !string.IsNullOrWhiteSpace(x.SeriesId) ? "s:" + x.SeriesId : "t:" + x.Title + "|" + x.AnniversaryDate.ToString("MMdd"))
                .Select(x => x.OrderBy(y => y.AnniversaryDate).First())
                .OrderBy(x => AnniversaryRemainingDays(x.AnniversaryDate, DateTime.Today)).ThenBy(x => x.Title).ToList();
            var stack = new StackPanel();
            var anniversaryColor = Colors["기념일"];
            var anniversaryForeground = new SolidColorBrush(CategoryColorSystem.DetailForeground(settings.ThemeId, anniversaryColor));
            var anniversaryBackground = new SolidColorBrush(CategoryColorSystem.DetailBackground(settings.ThemeId, anniversaryColor));
            var anniversaryBorder = new SolidColorBrush(CategoryColorSystem.DetailBorder(settings.ThemeId, anniversaryColor));
            var heading = new DockPanel { Height = anniversarySectionCollapsed ? 18 : 26,
                Margin = new Thickness(1, 0, 3, anniversarySectionCollapsed ? 0 : 1), LastChildFill = true };
            var titleButton = SectionTitleButton("✦  기념일 (" + anniversaries.Count + "개)", anniversaryForeground,
                anniversarySectionCollapsed ? "기념일 펼치기" : "기념일 접기", delegate { anniversarySectionCollapsed = !anniversarySectionCollapsed; RenderDetail(); }, 18);
            const double makeSize = 18;
            var make = IconButton("", delegate { OpenAnniversary(null); }, makeSize); make.Width = makeSize; make.Height = makeSize;
            make.Content = HeaderGlyph("add", anniversaryForeground); make.ToolTip = "기념일 만들기";
            make.Padding = new Thickness(0); make.Background = Brushes.Transparent; make.Foreground = anniversaryForeground;
            // 머리글 높이가 접힘 여부에 따라 달라져 가운데 정렬이면 `+`가 위아래로 움직인다
            // (2026-09-03 사용자 보고). 제목 글자와 같은 윗선에 세워 두 상태에서 자리를 지킨다.
            make.BorderBrush = anniversaryBorder; make.BorderThickness = new Thickness(1); make.VerticalAlignment = VerticalAlignment.Top;
            System.Windows.Automation.AutomationProperties.SetName(make, "기념일 만들기");
            DockPanel.SetDock(make, Dock.Right); heading.Children.Add(make);
            heading.Children.Add(titleButton);
            stack.Children.Add(heading);
            if (anniversarySectionCollapsed)
            {
                var collapsedCard = SpecialDetailCard(anniversaryBackground, anniversaryBorder,
                    new Thickness(10, 8, 10, 8), "special:기념일", stack);
                EnableDetailCardOrder(heading, collapsedCard, "special:기념일", false); detail.Children.Add(collapsedCard);
                return;
            }
            var visibleCount = AnniversaryVisibleCount(anniversaries.Count, anniversaryCardsExpanded);
            var visibleItems = anniversaries.Take(visibleCount).ToList();
            for (var index = 0; index < visibleItems.Count; index++)
            {
                var item = visibleItems[index];
                var isToday = AnniversaryRemainingDays(item.AnniversaryDate, DateTime.Today) == 0;
                var content = new TextBlock { Tag = item, Cursor = Cursors.Hand, Text = "✦ " + item.Title + " · " + AnniversarySummary(item, DateTime.Today),
                    FontSize = Ui(11), Foreground = anniversaryForeground, FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = item.Title + " · " + AnniversarySummary(item, DateTime.Today) };
                content.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    if (e.ClickCount == 2 && !string.IsNullOrWhiteSpace(item.AnniversaryType)) { OpenAnniversary(item); e.Handled = true; }
                    else if (e.ClickCount == 2) { OpenEdit(item); e.Handled = true; }
                    else e.Handled = true;
                };
                stack.Children.Add(new Border { Background = isToday ? anniversaryBackground : Brushes.Transparent,
                    BorderBrush = anniversaryBorder, BorderThickness = isToday ? new Thickness(1) : new Thickness(0, 0, 0, index < visibleItems.Count - 1 || anniversaries.Count > 5 ? 1 : 0),
                    CornerRadius = isToday ? new CornerRadius(8) : new CornerRadius(0), Margin = isToday ? new Thickness(0, 1, 0, 2) : new Thickness(0),
                    Padding = isToday ? new Thickness(7, 5, 7, 5) : new Thickness(2, 5, 2, 5), Child = content });
            }
            if (anniversaries.Count > 5)
            {
                var remainingCount = anniversaries.Count - 5;
                var toggle = new Button { Content = anniversaryCardsExpanded ? "접기" : "+ " + remainingCount + "개 더보기",
                    Height = 29, Background = Brushes.Transparent, Foreground = anniversaryForeground, BorderThickness = new Thickness(0),
                    FontSize = Ui(11), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
                toggle.Click += delegate { anniversaryCardsExpanded = !anniversaryCardsExpanded; RenderDetail(); };
                stack.Children.Add(toggle);
            }
            var anniversaryCard = SpecialDetailCard(anniversaryBackground, anniversaryBorder,
                new Thickness(10, 8, 10, 7), "special:기념일", stack);
            EnableDetailCardOrder(heading, anniversaryCard, "special:기념일", false); detail.Children.Add(anniversaryCard);
        }

        static Border SpecialDetailCard(Brush background, Brush borderBrush, Thickness padding, string tag, UIElement content)
        {
            var liftSurface = new Border { Background = background, CornerRadius = new CornerRadius(11),
                Padding = padding, Child = content };
            return new Border { Background = Brushes.Transparent, BorderBrush = borderBrush, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 4, 0, 7), Tag = tag, Child = liftSurface };
        }

        static ControlTemplate ContentOnlyButtonTemplate()
        {
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(System.Windows.Controls.Button.ContentProperty));
            return new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = content };
        }

        void OpenAnniversary(PlannerItem existing)
        {
            if (existing == null && AnniversaryCount() >= 10) { ShowNotice("기념일은 최대 10개까지 등록할 수 있습니다. 기존 기념일을 더블클릭해 삭제한 뒤 다시 등록해 주세요.", true); return; }
            var window = new AnniversaryWindow(existing); PlaceCalendarDialog(window);
            if (ShowBlockingDialog(window) != true) { if (positionLocked && IsVisible) PublishAndHide(); return; }
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
