using System;
using System.Globalization;
using System.Linq;

namespace FamilyPlanner
{
    // 반복 일정(RecurrenceFrequency/Mode/Days/Until)과 관련된 순수 계산만 모은 클래스.
    // 기존에 MainWindow / AddItemWindow / GoogleCalendar 세 곳에 나뉘어 있던
    // DayCode, CodeDay, NextOccurrence, NthWeekdayOfMonth, MonthlyNthCode,
    // MonthlyPositionText, RRULE 생성 로직을 이곳으로 옮겼습니다.
    // 동작(로직)은 원본과 100% 동일하며, 위치만 옮긴 것입니다.
    public static class RecurrenceService
    {
        public static string DayCode(DayOfWeek day)
        {
            return day == DayOfWeek.Monday ? "MO" : day == DayOfWeek.Tuesday ? "TU" : day == DayOfWeek.Wednesday ? "WE" :
                day == DayOfWeek.Thursday ? "TH" : day == DayOfWeek.Friday ? "FR" : day == DayOfWeek.Saturday ? "SA" : "SU";
        }

        public static DayOfWeek CodeDay(string code)
        {
            return code == "MO" ? DayOfWeek.Monday : code == "TU" ? DayOfWeek.Tuesday : code == "WE" ? DayOfWeek.Wednesday :
                code == "TH" ? DayOfWeek.Thursday : code == "FR" ? DayOfWeek.Friday : code == "SA" ? DayOfWeek.Saturday : DayOfWeek.Sunday;
        }

        // 다음 반복 발생일 계산 (원본: MainWindow.NextOccurrence)
        public static DateTime NextOccurrence(PlannerItem item, DateTime current)
        {
            var time = current.TimeOfDay;
            if (item.RecurrenceFrequency == "daily")
            {
                var next = current.Date.AddDays(1);
                if (item.RecurrenceMode == "weekdays") while (next.DayOfWeek == DayOfWeek.Saturday || next.DayOfWeek == DayOfWeek.Sunday) next = next.AddDays(1);
                return next.Add(time);
            }
            if (item.RecurrenceFrequency == "weekly")
            {
                var selected = new System.Collections.Generic.HashSet<string>((item.RecurrenceDays ?? DayCode(item.Start.DayOfWeek)).Split(','));
                var next = current.Date;
                do { next = next.AddDays(1); } while (!selected.Contains(DayCode(next.DayOfWeek)));
                return next.Add(time);
            }
            if (item.RecurrenceFrequency == "monthly")
            {
                var month = new DateTime(current.Year, current.Month, 1).AddMonths(1);
                for (var attempt = 0; attempt < 24; attempt++, month = month.AddMonths(1))
                {
                    int day;
                    if (item.RecurrenceMode == "monthly_last") day = DateTime.DaysInMonth(month.Year, month.Month);
                    else if (item.RecurrenceMode == "monthly_nth" && !string.IsNullOrWhiteSpace(item.RecurrenceDays)) day = NthWeekdayOfMonth(month.Year, month.Month, item.RecurrenceDays);
                    else { day = item.Start.Day; if (day > DateTime.DaysInMonth(month.Year, month.Month)) continue; }
                    return new DateTime(month.Year, month.Month, day).Add(time);
                }
            }
            return current.AddYears(1);
        }

        public static int NthWeekdayOfMonth(int year, int month, string rule)
        {
            var code = rule.Substring(rule.Length - 2); var ordinal = int.Parse(rule.Substring(0, rule.Length - 2), CultureInfo.InvariantCulture);
            var target = CodeDay(code); var days = DateTime.DaysInMonth(year, month);
            if (ordinal < 0)
            {
                var last = new DateTime(year, month, days); return days - ((7 + (int)last.DayOfWeek - (int)target) % 7);
            }
            var first = new DateTime(year, month, 1); return 1 + ((7 + (int)target - (int)first.DayOfWeek) % 7) + (ordinal - 1) * 7;
        }

        // 화면에 "3번째 화요일" 처럼 보여주기 위한 규칙 코드 (원본: AddItemWindow.MonthlyNthCode)
        public static string MonthlyNthCode(DateTime date)
        {
            var ordinal = date.Day + 7 > DateTime.DaysInMonth(date.Year, date.Month) ? -1 : (date.Day - 1) / 7 + 1;
            return ordinal.ToString(CultureInfo.InvariantCulture) + DayCode(date.DayOfWeek);
        }

        // 화면 표시용 한글 텍스트 (원본: AddItemWindow.MonthlyPositionText)
        public static string MonthlyPositionText(DateTime date)
        {
            var ordinal = date.Day + 7 > DateTime.DaysInMonth(date.Year, date.Month) ? "마지막" : new[] { "첫째", "둘째", "셋째", "넷째", "다섯째" }[(date.Day - 1) / 7];
            var day = new[] { "일", "월", "화", "수", "목", "금", "토" }[(int)date.DayOfWeek];
            return ordinal + " " + day + "요일";
        }

        // Google Calendar RRULE 문자열 생성 (원본: GoogleCalendar.RecurrenceRule)
        public static string GoogleRecurrenceRule(PlannerItem item)
        {
            var rule = "RRULE:FREQ=" + item.RecurrenceFrequency.ToUpperInvariant();
            if (item.RecurrenceFrequency == "daily" && item.RecurrenceMode == "weekdays") rule = "RRULE:FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR";
            else if (item.RecurrenceFrequency == "weekly" && !string.IsNullOrWhiteSpace(item.RecurrenceDays)) rule += ";BYDAY=" + item.RecurrenceDays;
            else if (item.RecurrenceFrequency == "monthly" && item.RecurrenceMode == "monthly_last") rule += ";BYMONTHDAY=-1";
            else if (item.RecurrenceFrequency == "monthly" && item.RecurrenceMode == "monthly_nth" && !string.IsNullOrWhiteSpace(item.RecurrenceDays)) rule += ";BYDAY=" + item.RecurrenceDays;
            else if (item.RecurrenceFrequency == "monthly") rule += ";BYMONTHDAY=" + item.Start.Day;
            if (item.RecurrenceCount > 0) return rule + ";COUNT=" + item.RecurrenceCount;
            if (item.RecurrenceUntil <= item.Start.Date) return rule;
            return rule + ";UNTIL=" + (item.AllDay
                ? item.RecurrenceUntil.Date.ToString("yyyyMMdd")
                : item.RecurrenceUntil.Date.AddDays(1).AddSeconds(-1).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'"));
        }
    }
}
