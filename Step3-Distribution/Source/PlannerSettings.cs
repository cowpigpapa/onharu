using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FamilyPlanner
{
    [DataContract]
    public class PlannerSettings
    {
        [DataMember] public int Version = 3;
        [DataMember] public bool HasPosition;
        [DataMember] public double Left;
        [DataMember] public double Top;
        [DataMember] public bool PositionLocked;
        [DataMember] public double Width;
        [DataMember] public double Height;
        [DataMember] public string BusinessColor;
        [DataMember] public string PersonalColor;
        [DataMember] public bool BusinessVisible = true;
        [DataMember] public bool PersonalVisible = true;
        [DataMember] public bool HolidayVisible = true;
        [DataMember] public double FontSize = 12;
        [DataMember] public double Opacity = .95;
        [DataMember] public bool SidebarVisible = true;
        [DataMember] public List<GoogleCalendarSetting> GoogleCalendars = new List<GoogleCalendarSetting>();
        [DataMember] public int GoogleOptionsVersion = 1;
        [DataMember] public string CalendarOrderMode = "category";
        [DataMember] public bool MultiDayFirst;
        [DataMember] public bool ShowWeekNumbers;
        [DataMember] public string WeekNumberRule = "iso";
        [DataMember] public bool PastelEventStyle;
        [DataMember] public int AutoSyncMinutes;
        [DataMember] public string ActiveGoogleAccountId;
        [DataMember] public bool ShowLunar;
        [DataMember] public string CategoryOrderPreset = "business";
        [DataMember] public List<string> CategoryOrder = new List<string>();
    }

    [DataContract]
    public class GoogleCalendarSetting
    {
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember] public string Color;
        [DataMember] public string OriginalColor;
        [DataMember] public bool Visible = true;
        [DataMember] public bool Primary;
        [DataMember] public string AccessRole;
        [DataMember] public bool Editable;
    }
}
