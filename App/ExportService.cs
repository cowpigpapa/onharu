using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace FamilyPlanner
{
    static class ExportService
    {
        public static void Json(string path, List<PlannerItem> items)
        {
            var exported = items.Select(CloneForExport).ToList();
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                new DataContractJsonSerializer(typeof(List<PlannerItem>)).WriteObject(stream, exported);
        }

        public static void Csv(string path, List<PlannerItem> items)
        {
            var lines = new List<string> { "날짜,제목,카테고리,하루종일,시작,종료,할일,완료,중요,D-Day 표시,기념일 기준일,반복,알림(분 전),메모,출처,ONHARU ID,기념일 종류,반복 방식,반복 요일" };
            foreach (var item in items.OrderBy(x => x.Start).ThenBy(x => x.Title))
                lines.Add(string.Join(",", new[] {
                    Q(item.Start.ToString("yyyy-MM-dd")), Q(item.Title), Q(ExportCategory(item)), Q(item.AllDay ? "예" : "아니오"),
                    Q(item.AllDay ? "" : item.Start.ToString("yyyy-MM-dd HH:mm")), Q(item.AllDay ? "" : item.End.ToString("yyyy-MM-dd HH:mm")),
                    Q(item.IsTodo ? "예" : "아니오"), Q(item.Completed ? "예" : "아니오"), Q(item.Important ? "예" : "아니오"),
                    Q(item.ShowDday ? "예" : "아니오"), Q(item.AnniversaryDate.Year >= 1900 ? item.AnniversaryDate.ToString("yyyy-MM-dd") : ""),
                    Q(Recurrence(item)), Q(item.ReminderConfigured && item.ReminderMinutes >= 0 ? item.ReminderMinutes.ToString() : ""),
                    Q(item.Notes), Q(Source(item)), Q(item.Id), Q(item.AnniversaryType), Q(item.RecurrenceMode), Q(item.RecurrenceDays) }));
            File.WriteAllLines(path, lines, new UTF8Encoding(true));
        }

        static string Q(string value)
        {
            var clean = (value ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
            var formula = clean.TrimStart();
            if (formula.Length > 0 && "=+-@".IndexOf(formula[0]) >= 0) clean = "'" + clean;
            return "\"" + clean + "\"";
        }
        static PlannerItem CloneForExport(PlannerItem item)
        {
            var clone = new PlannerItem();
            foreach (var field in typeof(PlannerItem).GetFields()) field.SetValue(clone, field.GetValue(item));
            clone.Category = ExportCategory(item);
            clone.ExportSource = Source(item);
            return clone;
        }
        static string Source(PlannerItem item) { return Store.IsGoogleItem(item) ? "Google · " + (string.IsNullOrWhiteSpace(item.GoogleCalendarName) ? "캘린더" : item.GoogleCalendarName) : "온하루 · 로컬"; }
        static string Recurrence(PlannerItem item)
        {
            if (string.IsNullOrWhiteSpace(item.RecurrenceFrequency)) return "";
            var name = item.RecurrenceFrequency == "daily" ? "매일" : item.RecurrenceFrequency == "weekly" ? "매주" : item.RecurrenceFrequency == "monthly" ? "매월" : "매년";
            return item.RecurrenceCount > 0 ? name + " · " + item.RecurrenceCount + "회" : item.RecurrenceUntil > item.Start.Date ? name + " · " + item.RecurrenceUntil.ToString("yyyy-MM-dd") + "까지" : name;
        }
        static string ExportCategory(PlannerItem item) { return string.IsNullOrWhiteSpace(item.GoogleCalendarName) ? item.Category : item.GoogleCalendarName; }
    }
}
