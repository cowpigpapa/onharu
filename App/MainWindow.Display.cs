using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        int CompletedRank(PlannerItem item) { return settings.CompletedLast && item.IsTodo && item.Completed ? 1 : 0; }
        bool ShowCompleted(PlannerItem item) { return settings.CompletedDisplayMode != "hide" || !item.IsTodo || !item.Completed; }
        static string DdayText(PlannerItem item)
        {
            if (!item.ShowDday) return "";
            var days = (item.Start.Date - DateTime.Today).Days;
            return days == 0 ? "D-Day · " : days > 0 ? "D-" + days + " · " : "D+" + (-days) + " · ";
        }

        static string AnniversaryOccurrenceText(PlannerItem item)
        {
            if (!item.ShowDday) return "";
            return " D+" + Math.Max(0, (item.Start.Date - item.AnniversaryDate.Date).Days).ToString("N0");
        }

        internal static bool OccursOnDate(PlannerItem item, DateTime date)
        {
            var last = item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date;
            return date.Date >= item.Start.Date && date.Date <= last;
        }

        IEnumerable<PlannerItem> ProjectItems(DateTime from, DateTime to)
        {
            foreach (var item in items.Where(x => string.IsNullOrWhiteSpace(x.AnniversaryType))) yield return item;
            foreach (var master in items.Where(x => !string.IsNullOrWhiteSpace(x.AnniversaryType)))
            {
                var basis = master.AnniversaryDate.Year >= 1900 ? master.AnniversaryDate.Date : master.Start.Date;
                for (var year = Math.Max(basis.Year, from.Year); year <= to.Year; year++)
                {
                    var occurrence = new DateTime(year, basis.Month, Math.Min(basis.Day, DateTime.DaysInMonth(year, basis.Month)));
                    if (occurrence < from.Date || occurrence > to.Date) continue;
                    var clone = new PlannerItem();
                    foreach (var field in typeof(PlannerItem).GetFields()) field.SetValue(clone, field.GetValue(master));
                    clone.Start = occurrence; clone.End = occurrence.AddDays(1);
                    yield return clone;
                }
            }
        }

        static bool IsMultiDay(PlannerItem item)
        {
            return item.Start.Date != (item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date);
        }

        int ImportantRank(PlannerItem item) { return settings.ImportantFirst && item.Important ? 0 : 1; }

        string TimeText(DateTime value)
        {
            return settings.Use24HourTime ? value.ToString("HH:mm") : value.ToString("tt h:mm", new CultureInfo("ko-KR"));
        }

        string DetailTimeText(PlannerItem item, DateTime date)
        {
            if (item.AllDay) return "하루 종일";
            if (!IsMultiDay(item)) return TimeText(item.Start);
            return item.Start.ToString("M/d ") + TimeText(item.Start) + " – " + item.End.ToString("M/d ") + TimeText(item.End);
        }

        bool IsItemVisible(PlannerItem item)
        {
            if (GoogleTasks.IsTask(item) && !settings.ShowGoogleTasks) return false;
            if (!string.IsNullOrWhiteSpace(item.GoogleCalendarId))
            {
                var source = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Id == item.GoogleCalendarId);
                return source != null && source.Visible;
            }
            return !filters.ContainsKey(item.Category) || filters[item.Category].IsChecked == true;
        }

        string ItemColor(PlannerItem item)
        {
            if (item.Category == "국경일") return Colors["국경일"];
            return !string.IsNullOrWhiteSpace(item.GoogleCalendarColor) ? item.GoogleCalendarColor : Colors.ContainsKey(item.Category) ? Colors[item.Category] : Colors["개인일정"];
        }

        string DisplayGroup(PlannerItem item)
        {
            if (string.IsNullOrWhiteSpace(item.GoogleCalendarId)) return item.Category;
            var source = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Id == item.GoogleCalendarId);
            var name = !string.IsNullOrWhiteSpace(item.GoogleCalendarName) ? item.GoogleCalendarName : source == null ? "Google" : source.Name;
            return source != null && source.Primary ? "내 캘린더 · " + name : name;
        }

        int GroupOrder(PlannerItem item)
        {
            return CategoryOrderPolicy.Rank(settings.CategoryOrder, CategoryOrderPolicy.ItemKey(item));
        }

        int GetWeekNumber(DateTime date)
        {
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date,
                settings.WeekNumberRule == "iso" ? CalendarWeekRule.FirstFourDayWeek : CalendarWeekRule.FirstDay,
                settings.WeekNumberRule == "iso" ? DayOfWeek.Monday : DayOfWeek.Sunday);
        }

        static string Lunar(DateTime date)
        {
            try { var c = new KoreanLunisolarCalendar(); return "음 " + c.GetMonth(date) + "/" + c.GetDayOfMonth(date); }
            catch { return ""; }
        }

        void BuildGoogleFilters()
        {
            if (googleFilterPanel == null) return;
            UpdateAccountStatus();
            foreach (var key in filters.Keys.Where(x => x.StartsWith("google:")).ToList()) filters.Remove(key);
            googleFilterPanel.Children.Clear();
            if (settings.GoogleCalendars == null || !settings.GoogleCalendars.Any(x => settings.ShowGoogleTasks || !GoogleTasks.IsSource(x.Id)))
            {
                googleFilterPanel.Children.Add(new TextBlock { Text = "G 연결 후 목록이 표시됩니다.", Foreground = T("Disabled"), FontSize = Ui(11) });
                UpdateGroupFilterChecks();
                return;
            }
            var orderedSources = CategoryOrderPolicy.GoogleSources(
                settings.GoogleCalendars.Where(x => settings.ShowGoogleTasks || !GoogleTasks.IsSource(x.Id)), settings.CategoryOrder).ToList();
            var boxes = new List<CheckBox>();
            foreach (var source in orderedSources)
            {
                var key = "google:" + source.Id;
                var color = string.IsNullOrWhiteSpace(source.Color) ? Colors["개인일정"] : source.Color;
                var label = (source.Primary ? "내 캘린더 · " : "") + source.Name;
                var box = new CheckBox { Content = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis,
                        ToolTip = label, VerticalAlignment = VerticalAlignment.Center },
                    IsChecked = source.Visible, Foreground = Brush(color), Background = Brush(color), Tag = color,
                    Margin = new Thickness(0, 0, 4, 6), HorizontalAlignment = HorizontalAlignment.Stretch };
                StyleVividCheckBox(box, color);
                box.Click += delegate { source.Visible = box.IsChecked == true; Store.SaveSettings(settings); UpdateGroupFilterChecks(); RenderAll(); };
                filters[key] = box; boxes.Add(box);
            }
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(17) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var left = new StackPanel(); var right = new StackPanel();
            var split = (boxes.Count + 1) / 2;
            for (var index = 0; index < boxes.Count; index++)
                (index < split ? left : right).Children.Add(boxes[index]);
            var divider = new Border { Width = 1, Background = T("Grid"), Margin = new Thickness(8, 1, 8, 2) };
            grid.Children.Add(left); Grid.SetColumn(divider, 1); grid.Children.Add(divider); Grid.SetColumn(right, 2); grid.Children.Add(right);
            googleFilterPanel.Children.Add(grid);
            UpdateGroupFilterChecks();
        }

        void ApplySidebarCategoryOrder()
        {
            ReorderFilterPanel(localFilterRow, new[] {
                Tuple.Create("업무일정", "local:business"), Tuple.Create("개인일정", "local:personal"), Tuple.Create("야구", "local:baseball") });
            ReorderFilterPanel(specialFilterRow, new[] {
                Tuple.Create("D-Day", "special:dday"), Tuple.Create("기념일", "special:anniversary") });
        }

        void ReorderFilterPanel(Panel panel, IEnumerable<Tuple<string, string>> entries)
        {
            if (panel == null) return;
            var boxes = entries.Where(x => filters.ContainsKey(x.Item1))
                .OrderBy(x => CategoryOrderPolicy.Rank(settings.CategoryOrder, x.Item2))
                .Select(x => filters[x.Item1]).ToList();
            foreach (var box in boxes) panel.Children.Remove(box);
            foreach (var box in boxes) panel.Children.Add(box);
            if (panel == specialFilterRow) NormalizeSpecialFilterSpacing();
        }

        void NormalizeSpecialFilterSpacing()
        {
            if (specialFilterRow == null) return;
            var boxes = specialFilterRow.Children.OfType<CheckBox>().ToList();
            for (var i = 0; i < boxes.Count; i++) boxes[i].Margin = new Thickness(0, 0, i == boxes.Count - 1 ? 0 : 7, 4);
        }

        CheckBox HeaderAllFilter(string toolTip, Action<bool> change)
        {
            var box = new CheckBox { IsThreeState = true, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand, ToolTip = toolTip };
            StyleVividCheckBox(box, SupportAccentColor());
            box.Click += delegate { change(box.IsChecked == true); };
            return box;
        }

        void SetLocalFilters(bool visible)
        {
            SetFilterValues(VisibleFilterKeys(new[] { "업무일정", "개인일정", "야구" }), visible);
        }

        void SetSpecialFilters(bool visible)
        {
            SetFilterValues(VisibleFilterKeys(new[] { "D-Day", "기념일" }), visible);
        }

        IEnumerable<string> VisibleFilterKeys(IEnumerable<string> keys)
        {
            return keys.Where(key => filters.ContainsKey(key) && filters[key].Visibility == Visibility.Visible);
        }

        void SetFilterValues(IEnumerable<string> keys, bool visible)
        {
            foreach (var key in keys) if (filters.ContainsKey(key)) filters[key].IsChecked = visible;
            SaveWindowSettings(); UpdateGroupFilterChecks(); RenderAll();
        }

        void SetGoogleFilters(bool visible)
        {
            foreach (var source in settings.GoogleCalendars.Where(x => settings.ShowGoogleTasks || !GoogleTasks.IsSource(x.Id)))
            {
                source.Visible = visible;
                CheckBox box; if (filters.TryGetValue("google:" + source.Id, out box)) box.IsChecked = visible;
            }
            Store.SaveSettings(settings); UpdateGroupFilterChecks(); RenderAll();
        }

        void UpdateGroupFilterChecks()
        {
            SetHeaderAllState(localAllFilter, VisibleFilterKeys(new[] { "업무일정", "개인일정", "야구" }).Select(x => filters[x]));
            SetHeaderAllState(specialAllFilter, VisibleFilterKeys(new[] { "D-Day", "기념일" }).Select(x => filters[x]));
            SetHeaderAllState(googleAllFilter, filters.Where(x => x.Key.StartsWith("google:")).Select(x => x.Value));
        }

        static void SetHeaderAllState(CheckBox header, IEnumerable<CheckBox> entries)
        {
            if (header == null) return;
            var boxes = entries.ToList(); header.Visibility = boxes.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            header.IsChecked = boxes.Count == 0 || boxes.All(x => x.IsChecked == true) ? true : boxes.All(x => x.IsChecked != true) ? false : (bool?)null;
        }

        static bool IsHolidayCalendar(GoogleCalendarSetting source)
        {
            return (source.Name ?? "").Contains("휴일") || (source.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        int CategoryRank(string key)
        {
            return CategoryOrderPolicy.Rank(settings.CategoryOrder, key);
        }
    }
}
