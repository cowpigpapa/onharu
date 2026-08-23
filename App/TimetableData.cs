using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace FamilyPlanner
{
    [DataContract]
    public sealed class TimetableData
    {
        [DataMember] public List<string> Times = new List<string>();
        [DataMember] public List<TimetableSlot> Slots = new List<TimetableSlot>();
        [DataMember] public List<int> VisibleDays = new List<int>();
        [DataMember] public int PeriodCount = 9;
        [DataMember] public int StartHour = 9;
        [DataMember] public int StartMinute;
        [DataMember] public int LessonMinutes = 50;
        [DataMember] public int BreakMinutes = 10;
        [DataMember] public double FontSize = 13;
    }

    [DataContract]
    public sealed class TimetableSlot
    {
        [DataMember] public int Day;
        [DataMember] public int Period;
        [DataMember] public string Text;
    }

    internal static class TimetableStorage
    {
        static readonly string Folder = AppDataPaths.Root;
        static readonly string PathName = Path.Combine(Folder, "timetable.json");

        internal static TimetableData Load()
        {
            try
            {
                if (File.Exists(PathName))
                    using (var stream = File.OpenRead(PathName))
                    {
                        var value = (TimetableData)new DataContractJsonSerializer(typeof(TimetableData)).ReadObject(stream);
                        if (value != null) return Normalize(value);
                    }
            }
            catch (Exception ex) { ErrorLog.Write("Load timetable", ex); }
            return Normalize(new TimetableData());
        }

        internal static void Save(TimetableData value)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var temp = PathName + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(TimetableData)).WriteObject(stream, Normalize(value));
                    stream.Flush(true);
                }
                if (File.Exists(PathName)) File.Replace(temp, PathName, null); else File.Move(temp, PathName);
            }
            catch (Exception ex) { ErrorLog.Write("Save timetable", ex); throw; }
        }

        static TimetableData Normalize(TimetableData value)
        {
            value.PeriodCount = Math.Max(1, Math.Min(12, value.PeriodCount <= 0 ? 9 : value.PeriodCount));
            value.StartHour = Math.Max(0, Math.Min(23, value.StartHour)); value.StartMinute = Math.Max(0, Math.Min(59, value.StartMinute));
            value.LessonMinutes = Math.Max(10, Math.Min(180, value.LessonMinutes <= 0 ? 50 : value.LessonMinutes));
            value.BreakMinutes = Math.Max(0, Math.Min(120, value.BreakMinutes < 0 ? 10 : value.BreakMinutes));
            value.FontSize = Math.Max(11.5, Math.Min(15, value.FontSize <= 0 ? 13 : value.FontSize));
            if (value.VisibleDays == null || value.VisibleDays.Count == 0) value.VisibleDays = new List<int> { 0, 1, 2, 3, 4, 5 };
            value.VisibleDays = new List<int>(new HashSet<int>(value.VisibleDays.FindAll(x => x >= 0 && x <= 6)));
            value.VisibleDays.Sort();
            if (value.Times == null) value.Times = new List<string>();
            while (value.Times.Count < value.PeriodCount) value.Times.Add(DefaultTime(value, value.Times.Count));
            if (value.Times.Count > value.PeriodCount) value.Times.RemoveRange(value.PeriodCount, value.Times.Count - value.PeriodCount);
            if (value.Slots == null) value.Slots = new List<TimetableSlot>();
            return value;
        }

        internal static string DefaultTime(TimetableData value, int index)
        {
            var start = new TimeSpan(value.StartHour, value.StartMinute, 0).Add(TimeSpan.FromMinutes(index * (value.LessonMinutes + value.BreakMinutes)));
            var end = start.Add(TimeSpan.FromMinutes(value.LessonMinutes));
            return start.ToString(@"hh\:mm") + "~" + end.ToString(@"hh\:mm");
        }
    }
}
