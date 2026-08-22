using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FamilyPlanner
{
    static class CalendarExchangeService
    {
        public static void Ics(string path, List<PlannerItem> items)
        {
            var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//ONHARU//Calendar 2.1//KO", "CALSCALE:GREGORIAN" };
            foreach (var item in items.OrderBy(x => x.Start).ThenBy(x => x.Title))
            {
                lines.Add("BEGIN:VEVENT");
                lines.Add("UID:" + Escape(string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id) + "@onharu.net");
                lines.Add("DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"));
                if (item.AllDay)
                {
                    lines.Add("DTSTART;VALUE=DATE:" + item.Start.ToString("yyyyMMdd"));
                    lines.Add("DTEND;VALUE=DATE:" + (item.End.Date > item.Start.Date ? item.End.Date : item.Start.Date.AddDays(1)).ToString("yyyyMMdd"));
                }
                else
                {
                    lines.Add("DTSTART:" + item.Start.ToString("yyyyMMdd'T'HHmmss"));
                    lines.Add("DTEND:" + (item.End > item.Start ? item.End : item.Start.AddMinutes(30)).ToString("yyyyMMdd'T'HHmmss"));
                }
                lines.Add("SUMMARY:" + Escape(item.Title));
                if (!string.IsNullOrWhiteSpace(item.Notes)) lines.Add("DESCRIPTION:" + Escape(item.Notes));
                lines.Add("CATEGORIES:" + Escape(item.Category));
                if (!string.IsNullOrWhiteSpace(item.RecurrenceFrequency)) lines.Add("RRULE:" + RRule(item));
                lines.Add("X-ONHARU-TODO:" + (item.IsTodo ? "TRUE" : "FALSE"));
                lines.Add("X-ONHARU-COMPLETED:" + (item.Completed ? "TRUE" : "FALSE"));
                lines.Add("X-ONHARU-IMPORTANT:" + (item.Important ? "TRUE" : "FALSE"));
                lines.Add("X-ONHARU-DDAY:" + (item.ShowDday ? "TRUE" : "FALSE"));
                if (item.AnniversaryDate.Year >= 1900) lines.Add("X-ONHARU-ANNIVERSARY-DATE:" + item.AnniversaryDate.ToString("yyyyMMdd"));
                if (!string.IsNullOrWhiteSpace(item.AnniversaryType)) lines.Add("X-ONHARU-ANNIVERSARY-TYPE:" + Escape(item.AnniversaryType));
                if (item.ReminderConfigured && item.ReminderMinutes >= 0) lines.Add("X-ONHARU-REMINDER-MINUTES:" + item.ReminderMinutes);
                lines.Add("END:VEVENT");
            }
            lines.Add("END:VCALENDAR");
            File.WriteAllLines(path, lines.SelectMany(Fold), new UTF8Encoding(true));
        }

        public static List<PlannerItem> ReadIcs(string path)
        {
            var raw = File.ReadAllLines(path, Encoding.UTF8); var lines = new List<string>();
            foreach (var line in raw)
                if ((line.StartsWith(" ") || line.StartsWith("\t")) && lines.Count > 0) lines[lines.Count - 1] += line.Substring(1); else lines.Add(line);
            var result = new List<PlannerItem>(); Dictionary<string, string> values = null;
            foreach (var line in lines)
            {
                if (line == "BEGIN:VEVENT") { values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); continue; }
                if (line == "END:VEVENT" && values != null) { result.Add(ParseEvent(values)); values = null; continue; }
                if (values == null) continue;
                var colon = line.IndexOf(':'); if (colon <= 0) continue;
                var key = line.Substring(0, colon); var semicolon = key.IndexOf(';'); if (semicolon >= 0) key = key.Substring(0, semicolon);
                if (!values.ContainsKey(key)) values[key] = line.Substring(colon + 1);
            }
            return result;
        }

        public static List<PlannerItem> ReadCsv(string path)
        {
            var rows = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
            if (rows.Count < 2) return new List<PlannerItem>();
            var header = rows[0].Select((x, i) => new { x, i }).ToDictionary(x => x.x.Trim(), x => x.i);
            Func<List<string>, string, string> get = (row, name) => header.ContainsKey(name) && header[name] < row.Count ? row[header[name]] : "";
            var result = new List<PlannerItem>();
            foreach (var row in rows.Skip(1))
            {
                DateTime date; if (!DateTime.TryParse(get(row, "날짜"), out date)) continue;
                DateTime start, end; var allDay = Yes(get(row, "하루종일"));
                if (!DateTime.TryParse(get(row, "시작"), out start)) start = date.Date;
                if (!DateTime.TryParse(get(row, "종료"), out end)) end = allDay ? date.Date.AddDays(1) : start.AddMinutes(30);
                var sourceId = get(row, "ONHARU ID");
                var item = new PlannerItem { Id = string.IsNullOrWhiteSpace(sourceId) ? Guid.NewGuid().ToString() : sourceId, Title = Unformula(get(row, "제목")), Start = start, End = end,
                    AllDay = allDay, Category = NormalizeCategory(get(row, "카테고리")), IsTodo = Yes(get(row, "할일")), Completed = Yes(get(row, "완료")),
                    Important = Yes(get(row, "중요")), ShowDday = Yes(get(row, "D-Day 표시")), Notes = Unformula(get(row, "메모")), CreatedInOnharu = true,
                    AnniversaryType = get(row, "기념일 종류"), RecurrenceMode = get(row, "반복 방식"), RecurrenceDays = get(row, "반복 요일"),
                    ReminderMinutes = -1, SnoozeUntil = new DateTime(2000, 1, 1), RecurrenceUntil = start };
                DateTime anniversary; if (DateTime.TryParse(get(row, "기념일 기준일"), out anniversary)) item.AnniversaryDate = anniversary;
                else if (item.Category == "기념일") item.AnniversaryDate = item.Start.Date;
                int reminder; if (int.TryParse(get(row, "알림(분 전)"), out reminder)) { item.ReminderConfigured = true; item.ReminderMinutes = reminder; }
                ParseCsvRecurrence(get(row, "반복"), item);
                result.Add(item);
            }
            return result;
        }

        static PlannerItem ParseEvent(Dictionary<string, string> v)
        {
            var allDay = v.ContainsKey("DTSTART") && v["DTSTART"].Length == 8;
            var start = Date(v.ContainsKey("DTSTART") ? v["DTSTART"] : "", allDay);
            var end = Date(v.ContainsKey("DTEND") ? v["DTEND"] : "", allDay);
            if (end <= start) end = allDay ? start.AddDays(1) : start.AddMinutes(30);
            var uid = Value(v, "UID"); var at = uid.IndexOf('@'); if (at > 0) uid = uid.Substring(0, at);
            var item = new PlannerItem { Id = string.IsNullOrWhiteSpace(uid) ? Guid.NewGuid().ToString() : uid, Title = Unescape(Value(v, "SUMMARY")),
                Notes = Unescape(Value(v, "DESCRIPTION")), Category = NormalizeCategory(Unescape(Value(v, "CATEGORIES"))), Start = start, End = end,
                AllDay = allDay, IsTodo = Bool(v, "X-ONHARU-TODO"), Completed = Bool(v, "X-ONHARU-COMPLETED"), Important = Bool(v, "X-ONHARU-IMPORTANT"),
                ShowDday = Bool(v, "X-ONHARU-DDAY"), AnniversaryType = Unescape(Value(v, "X-ONHARU-ANNIVERSARY-TYPE")), CreatedInOnharu = true,
                ReminderMinutes = -1, SnoozeUntil = new DateTime(2000, 1, 1), RecurrenceUntil = start };
            DateTime anniversary; if (DateTime.TryParseExact(Value(v, "X-ONHARU-ANNIVERSARY-DATE"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out anniversary)) item.AnniversaryDate = anniversary;
            int reminder; if (int.TryParse(Value(v, "X-ONHARU-REMINDER-MINUTES"), out reminder)) { item.ReminderConfigured = true; item.ReminderMinutes = reminder; }
            ParseRRule(Value(v, "RRULE"), item);
            if (!string.IsNullOrWhiteSpace(item.AnniversaryType)) item.RecurrenceMode = "date";
            return item;
        }

        static string RRule(PlannerItem item)
        {
            var frequency = item.RecurrenceFrequency == "daily" ? "DAILY" : item.RecurrenceFrequency == "weekly" ? "WEEKLY" : item.RecurrenceFrequency == "monthly" ? "MONTHLY" : "YEARLY";
            var value = "FREQ=" + frequency; if (item.RecurrenceCount > 0) value += ";COUNT=" + item.RecurrenceCount;
            else if (item.RecurrenceUntil > item.Start.Date) value += ";UNTIL=" + item.RecurrenceUntil.ToString("yyyyMMdd'T'235959");
            if (frequency == "WEEKLY" && !string.IsNullOrWhiteSpace(item.RecurrenceDays)) value += ";BYDAY=" + item.RecurrenceDays.ToUpperInvariant();
            return value;
        }
        static void ParseRRule(string value, PlannerItem item)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var fields = value.Split(';').Select(x => x.Split(new[] { '=' }, 2)).Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);
            string frequency; if (fields.TryGetValue("FREQ", out frequency)) item.RecurrenceFrequency = frequency.ToLowerInvariant();
            string count; if (fields.TryGetValue("COUNT", out count)) int.TryParse(count, out item.RecurrenceCount);
            string until; DateTime date; if (fields.TryGetValue("UNTIL", out until) && TryDate(until, out date)) item.RecurrenceUntil = date;
            string days; if (fields.TryGetValue("BYDAY", out days)) item.RecurrenceDays = days.ToLowerInvariant();
        }
        static DateTime Date(string value, bool allDay) { DateTime result; return TryDate(value, out result) ? result : DateTime.Today; }
        static bool TryDate(string value, out DateTime result)
        {
            var formats = new[] { "yyyyMMdd", "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmmss'Z'" };
            return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out result) || DateTime.TryParse(value, out result);
        }
        static string Value(Dictionary<string, string> v, string key) { string value; return v.TryGetValue(key, out value) ? value : ""; }
        static bool Bool(Dictionary<string, string> v, string key) { return Value(v, key).Equals("TRUE", StringComparison.OrdinalIgnoreCase); }
        static bool Yes(string value) { return value == "예" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1"; }
        static string NormalizeCategory(string value) { return value == "업무" || value == "업무일정" ? "업무일정" : value == "야구" ? "야구" : value == "기념일" ? "기념일" : "개인일정"; }
        static string Escape(string value) { return (value ?? "").Replace("\\", "\\\\").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace(",", "\\,").Replace(";", "\\;"); }
        static string Unescape(string value) { return (value ?? "").Replace("\\n", "\n").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\"); }
        static IEnumerable<string> Fold(string value)
        {
            while (Encoding.UTF8.GetByteCount(value) > 73)
            {
                var take = 1; while (take < value.Length && Encoding.UTF8.GetByteCount(value.Substring(0, take + 1)) <= 73) take++;
                yield return value.Substring(0, take); value = " " + value.Substring(take);
            }
            yield return value;
        }
        static void ParseCsvRecurrence(string value, PlannerItem item)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            item.RecurrenceFrequency = value.StartsWith("매일") ? "daily" : value.StartsWith("매주") ? "weekly" : value.StartsWith("매월") ? "monthly" : value.StartsWith("매년") ? "yearly" : null;
            if (item.RecurrenceFrequency == null) return;
            var count = System.Text.RegularExpressions.Regex.Match(value, @"(\d+)회"); if (count.Success) int.TryParse(count.Groups[1].Value, out item.RecurrenceCount);
            var until = System.Text.RegularExpressions.Regex.Match(value, @"(\d{4}-\d{2}-\d{2})까지"); DateTime date; if (until.Success && DateTime.TryParse(until.Groups[1].Value, out date)) item.RecurrenceUntil = date;
        }
        static string Unformula(string value) { return value != null && value.StartsWith("'") && value.Length > 1 && "=+-@".IndexOf(value.TrimStart('\'', ' ')[0]) >= 0 ? value.Substring(1) : value ?? ""; }
        static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i]; if (quoted && c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = !quoted;
                else if (!quoted && c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (!quoted && (c == '\r' || c == '\n')) { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; row.Add(cell.ToString()); cell.Clear(); if (row.Any(x => x.Length > 0)) rows.Add(row); row = new List<string>(); }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); } return rows;
        }
    }
}
