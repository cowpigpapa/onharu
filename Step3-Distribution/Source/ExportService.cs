using System;
using System.Collections.Generic;
using System.Globalization;
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
            var lines = new List<string> { "날짜,제목,카테고리,하루종일,시작,종료,할일,완료,중요,메모,출처" };
            foreach (var item in items.OrderBy(x => x.Start).ThenBy(x => x.Title))
                lines.Add(string.Join(",", new[] {
                    Q(item.Start.ToString("yyyy-MM-dd")), Q(item.Title), Q(ExportCategory(item)), Q(item.AllDay ? "예" : "아니오"),
                    Q(item.AllDay ? "" : item.Start.ToString("yyyy-MM-dd HH:mm")), Q(item.AllDay ? "" : item.End.ToString("yyyy-MM-dd HH:mm")),
                    Q(item.IsTodo ? "예" : "아니오"), Q(item.Completed ? "예" : "아니오"), Q(item.Important ? "예" : "아니오"),
                    Q(item.Notes), Q(string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "온하루" : "Google Calendar") }));
            File.WriteAllLines(path, lines, new UTF8Encoding(true));
        }

        public static void Ics(string path, List<PlannerItem> items)
        {
            var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//JUAN.HJLEE//ONHARU 1.0//KO", "CALSCALE:GREGORIAN" };
            foreach (var item in items.OrderBy(x => x.Start))
            {
                lines.Add("BEGIN:VEVENT");
                lines.Add("UID:" + Escape(string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id) + "@onharu");
                lines.Add("DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
                if (item.AllDay)
                {
                    lines.Add("DTSTART;VALUE=DATE:" + item.Start.ToString("yyyyMMdd"));
                    lines.Add("DTEND;VALUE=DATE:" + (item.End.Date <= item.Start.Date ? item.Start.Date.AddDays(1) : item.End.Date).ToString("yyyyMMdd"));
                }
                else
                {
                    lines.Add("DTSTART:" + item.Start.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
                    lines.Add("DTEND:" + item.End.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
                }
                lines.Add("SUMMARY:" + Escape(item.Title));
                if (!string.IsNullOrWhiteSpace(item.Notes)) lines.Add("DESCRIPTION:" + Escape(item.Notes));
                if (!string.IsNullOrWhiteSpace(ExportCategory(item))) lines.Add("CATEGORIES:" + Escape(ExportCategory(item)));
                if (item.Important) lines.Add("PRIORITY:1");
                if (item.Completed) lines.Add("X-ONHARU-COMPLETED:TRUE");
                if (!string.IsNullOrWhiteSpace(item.RecurrenceFrequency)) lines.Add(RecurrenceService.GoogleRecurrenceRule(item));
                lines.Add("END:VEVENT");
            }
            lines.Add("END:VCALENDAR");
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
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
            return clone;
        }
        static string ExportCategory(PlannerItem item) { return string.IsNullOrWhiteSpace(item.GoogleCalendarName) ? item.Category : item.GoogleCalendarName; }
        static string Escape(string value) { return (value ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n"); }
    }
}
