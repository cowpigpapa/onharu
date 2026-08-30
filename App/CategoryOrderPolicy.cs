using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyPlanner
{
    internal static class CategoryOrderPolicy
    {
        internal static readonly string[] LocalKeys = { "local:business", "local:personal", "local:baseball" };
        internal static readonly string[] SpecialKeys = { "special:dday", "special:anniversary" };

        internal static int Rank(IList<string> order, string key)
        {
            var index = order == null ? -1 : order.IndexOf(key);
            return index < 0 ? int.MaxValue : index;
        }

        internal static string ItemKey(PlannerItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.GoogleCalendarId)) return "google:" + item.GoogleCalendarId;
            if (item.Category == "업무일정") return "local:business";
            if (item.Category == "야구") return "local:baseball";
            return "local:personal";
        }

        internal static IEnumerable<GoogleCalendarSetting> GoogleSources(IEnumerable<GoogleCalendarSetting> sources, IList<string> order)
        {
            return sources.OrderBy(x => Rank(order, "google:" + x.Id))
                .ThenBy(x => GoogleTasks.IsSource(x.Id) ? 3 : IsHoliday(x) ? 2 : x.Primary ? 0 : 1)
                .ThenBy(x => x.Name);
        }

        internal static bool IsHoliday(GoogleCalendarSetting source)
        {
            return (source.Name ?? "").Contains("휴일") ||
                (source.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
