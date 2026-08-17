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

        static bool IsMultiDay(PlannerItem item)
        {
            return item.Start.Date != (item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date);
        }

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
            if (!string.IsNullOrWhiteSpace(item.GoogleCalendarId))
            {
                if (settings.GoogleCalendars == null || !settings.GoogleCalendars.Any(x => x.Id == item.GoogleCalendarId)) return false;
                var key = "google:" + item.GoogleCalendarId;
                return !filters.ContainsKey(key) || filters[key].IsChecked == true;
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
            var key = item.Category == "업무일정" ? "local:business" : string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "local:personal" : "google:" + item.GoogleCalendarId;
            var index = settings.CategoryOrder == null ? -1 : settings.CategoryOrder.IndexOf(key);
            return index < 0 ? 999 : index;
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
            if (settings.GoogleCalendars == null || settings.GoogleCalendars.Count == 0)
            {
                googleFilterPanel.Children.Add(new TextBlock { Text = "G 연결 후 목록이 표시됩니다.", Foreground = Brush("#94A3B8"), FontSize = Ui(11) });
                return;
            }
            foreach (var source in settings.GoogleCalendars.OrderBy(x => CategoryRank("google:" + x.Id)).ThenBy(x => x.Name))
            {
                var key = "google:" + source.Id;
                var color = string.IsNullOrWhiteSpace(source.Color) ? Colors["개인일정"] : source.Color;
                var box = new CheckBox { Content = (source.Primary ? "내 캘린더 · " : "") + source.Name,
                    IsChecked = source.Visible, Foreground = Brush(color), Margin = new Thickness(0, 0, 0, 6) };
                box.Click += delegate { source.Visible = box.IsChecked == true; Store.SaveSettings(settings); RenderAll(); };
                filters[key] = box; googleFilterPanel.Children.Add(box);
            }
        }

        static bool IsHolidayCalendar(GoogleCalendarSetting source)
        {
            return (source.Name ?? "").Contains("휴일") || (source.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        int CategoryRank(string key)
        {
            var index = settings.CategoryOrder == null ? -1 : settings.CategoryOrder.IndexOf(key);
            return index < 0 ? 999 : index;
        }
    }
}

