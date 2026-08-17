using System;
using System.Runtime.Serialization;

namespace FamilyPlanner
{
    [DataContract]
    public class PlannerItem
    {
        [DataMember] public string Id;
        [DataMember] public string Title;
        [DataMember] public DateTime Start;
        [DataMember] public DateTime End;
        [DataMember] public bool AllDay;
        [DataMember] public bool IsTodo;
        [DataMember] public bool Completed;
        [DataMember] public string Category;
        [DataMember] public string Notes;
        [DataMember] public string GoogleEventId;
        [DataMember] public string GoogleEventType;
        [DataMember] public bool OnharuManaged;
        [DataMember] public bool GoogleTaskEvent;
        [DataMember] public bool CreatedInOnharu;
        [DataMember] public bool AutoRollover;
        [DataMember] public string RolloverMode;
        [DataMember] public string GoogleCalendarId;
        [DataMember] public string GoogleCalendarName;
        [DataMember] public string GoogleCalendarColor;
        [DataMember] public bool GoogleReadOnly;
        [DataMember] public bool Important;
        [DataMember] public bool ShowDday;
        [DataMember(EmitDefaultValue = false)] public DateTime AnniversaryDate;
        [DataMember(EmitDefaultValue = false)] public string AnniversaryType;
        [DataMember] public int ReminderMinutes;
        [DataMember] public bool ReminderConfigured;
        [DataMember] public DateTime SnoozeUntil;
        [DataMember] public string ReminderDismissedKey;
        [DataMember] public string RecurrenceFrequency;
        [DataMember] public string RecurrenceMode;
        [DataMember] public string RecurrenceDays;
        [DataMember] public DateTime RecurrenceUntil;
        [DataMember] public int RecurrenceCount;
        [DataMember] public string SeriesId;
        [DataMember] public string GoogleRecurringEventId;
        [DataMember] public bool PendingGoogleSync;
        [DataMember(EmitDefaultValue = false)] public string ExportSource;
    }
}
