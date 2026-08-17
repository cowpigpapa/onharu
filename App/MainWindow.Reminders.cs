using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        async void Rollover()
        {
            var changed = items.Where(x => x.IsTodo && !string.IsNullOrWhiteSpace(x.RolloverMode) && !x.GoogleTaskEvent && !x.Completed && x.Start.Date < DateTime.Today).ToList();
            foreach (var item in changed)
            {
                var duration = item.End - item.Start;
                var nextDate = NextRolloverDate(item.Start.Date, item.RolloverMode);
                item.Start = nextDate.Add(item.AllDay ? TimeSpan.Zero : item.Start.TimeOfDay);
                item.End = item.Start.Add(item.AllDay ? TimeSpan.FromDays(1) : duration);
            }
            if (changed.Count == 0) return;
            Store.Save(items); RenderAll();
            if (GoogleCalendar.IsConnected)
                foreach (var item in changed.Where(x => x.Category == "개인일정" && !string.IsNullOrWhiteSpace(x.GoogleEventId)))
                    try { await GoogleCalendar.UpsertAsync(item); } catch (Exception ex) { ErrorLog.Write("Rollover Google event", ex); }
            Store.Save(items);
        }

        void SafeCheckReminders()
        {
            try { CheckReminders(); }
            catch (Exception ex) { ErrorLog.Write("Check reminders", ex); }
        }

        void CheckReminders()
        {
            var now = DateTime.Now; var due = new List<PlannerItem>(); var keys = new Dictionary<string, string>();
            foreach (var item in items.Where(x => x.ReminderConfigured && !x.Completed))
            {
                try
                {
                    item.ReminderMinutes = GoogleCalendar.NormalizeReminderMinutes(item.ReminderMinutes, item.AllDay);
                    if (item.ReminderMinutes < 0) continue;
                    var key = item.Id + "|" + item.Start.ToString("o") + "|" + item.ReminderMinutes;
                    if (item.ReminderDismissedKey == key) continue;
                    var target = item.SnoozeUntil > now.AddMinutes(-2) ? item.SnoozeUntil : (item.AllDay ? item.Start.Date.AddHours(9) : item.Start).AddMinutes(-item.ReminderMinutes);
                    if (now >= target && now < target.AddMinutes(2) && shownReminders.Add(key)) { due.Add(item); keys[item.Id] = key; }
                }
                catch (Exception ex) { ErrorLog.Write("Check reminder item", ex); }
            }
            if (due.Count > 0)
            {
                if (settings.ReminderSound && !IsQuietHour(now.Hour)) System.Media.SystemSounds.Asterisk.Play();
                new ReminderWindow(due, delegate(int? snooze)
            {
                foreach (var item in due)
                {
                    var key = keys[item.Id];
                    if (snooze.HasValue) { item.SnoozeUntil = DateTime.Now.AddMinutes(snooze.Value); shownReminders.Remove(key); }
                    else { item.ReminderDismissedKey = key; item.SnoozeUntil = DateTime.MinValue; }
                }
                Store.Save(items);
            }) { Owner = null }.Show();
            }
        }

        bool IsQuietHour(int hour)
        {
            return settings.QuietStartHour == settings.QuietEndHour ? false : settings.QuietStartHour < settings.QuietEndHour
                ? hour >= settings.QuietStartHour && hour < settings.QuietEndHour
                : hour >= settings.QuietStartHour || hour < settings.QuietEndHour;
        }

        static DateTime NextRolloverDate(DateTime date, string mode)
        {
            do
            {
                date = mode == "next_week" ? date.AddDays(7) : date.AddDays(1);
                if (mode == "next_weekday")
                    while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) date = date.AddDays(1);
            }
            while (date < DateTime.Today);
            return date;
        }
    }
}
