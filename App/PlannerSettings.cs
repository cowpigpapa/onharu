using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FamilyPlanner
{
    [DataContract]
    public class PlannerSettings
    {
        [DataMember] public int Version = 41;
        [DataMember] public string ThemeId = "classic";
        [DataMember] public bool HasPosition;
        [DataMember] public double Left;
        [DataMember] public double Top;
        [DataMember] public bool PositionLocked;
        [DataMember] public double Width = 1120;
        [DataMember] public double Height = 700;
        [DataMember] public string MonitorDeviceName;
        [DataMember] public int PhysicalLeft;
        [DataMember] public int PhysicalTop;
        [DataMember] public int PhysicalWidth;
        [DataMember] public int PhysicalHeight;
        [DataMember] public string BusinessColor = "#00A6C8";
        [DataMember] public string PersonalColor = "#2859C5";
        [DataMember] public string BaseballColor = "#38A169";
        [DataMember] public string DdayColor = "#E67E22";
        [DataMember] public string AnniversaryColor = "#C2418C";
        [DataMember] public string HolidayColor = "#DC2626";
        [DataMember] public bool BusinessVisible = true;
        [DataMember] public bool PersonalVisible = true;
        [DataMember] public bool AnniversaryVisible = true;
        [DataMember] public bool DdayPanelVisible = true;
        [DataMember] public bool HolidayVisible = true;
        [DataMember] public double FontSize = 12;
        [DataMember] public double Opacity = .95;
        [DataMember] public bool SidebarVisible = true;
        [DataMember] public List<GoogleCalendarSetting> GoogleCalendars = new List<GoogleCalendarSetting>();
        [DataMember] public int GoogleOptionsVersion = 1;
        [DataMember] public string CalendarOrderMode = "category";
        [DataMember] public bool ImportantFirst = true;
        [DataMember] public bool MultiDayFirst = true;
        [DataMember] public bool CompletedLast = true;
        [DataMember] public string CompletedDisplayMode = "fade";
        [DataMember] public string StartViewMode = "today";
        [DataMember] public DateTime LastShownDate;
        [DataMember] public bool ReminderSound = true;
        [DataMember] public int QuietStartHour = 22;
        [DataMember] public int QuietEndHour = 7;
        [DataMember] public string StartupPositionMode = "editable";
        [DataMember] public string CloseButtonAction = "confirm_exit";
        [DataMember] public bool AnniversarySeparationComplete;
        [DataMember] public string DefaultCalendarKey = "local:business";
        [DataMember] public bool DefaultAllDay = true;
        [DataMember] public int DefaultStartHour = 9;
        [DataMember] public int DefaultStartMinute;
        [DataMember] public int DefaultDurationMinutes = 30;
        [DataMember] public int DefaultReminderMinutes = -1;
        [DataMember] public bool Use24HourTime = true;
        [DataMember] public bool ShowWeekNumbers = true;
        [DataMember] public string WeekNumberRule = "iso";
        [DataMember] public string WeekStartDay = "sunday";
        [DataMember] public List<int> RestDays = new List<int> { 0, 6 };
        // Clean-install defaults only. LoadSettings keeps every value from an
        // existing settings.json, so updates never reset the user's view.
        [DataMember] public string CalendarRangeMode = "weeks";
        [DataMember] public string MonthRangeMode = "monthAuto";
        [DataMember] public bool UseMonthView;
        [DataMember] public int VisibleWeekCount = 4;
        [DataMember] public int TodayRow = 2;
        [DataMember] public string SelectedDateStyle = "border";
        [DataMember] public string SelectedDateFillColor = "#CCDBEAFE";
        [DataMember] public string SelectedDateBorderColor = "#EC4899";
        [DataMember] public string TodayColor = "#CCFCE7F3";
        [DataMember] public string TodayStyle = "icon";
        // Kept under the legacy serialized name so existing 2.1 settings load;
        // 2.2 uses this value for the date-circle color, not a cell border.
        [DataMember] public string TodayBorderColor = "#4F7BFF";
        [DataMember] public bool PastelEventStyle = true;
        [DataMember] public int AutoSyncMinutes = 5;
        [DataMember] public string ActiveGoogleAccountId;
        [DataMember] public bool ShowLunar = true;
        [DataMember] public bool ShowSolarTerms = true;
        [DataMember] public bool UseTimetable;
        [DataMember] public bool UseDiary = true;
        [DataMember] public bool UseRollover = true;
        [DataMember] public bool ShowGoogleTasks;
        [DataMember] public bool UseProBaseball;
        [DataMember] public bool BaseballVisible = true;
        [DataMember] public string FavoriteBaseballTeam;
        [DataMember] public double SportsCalendarScale = 1.0;
        [DataMember] public Dictionary<string, string> DateBackgroundColors = new Dictionary<string, string>();
        [DataMember] public string BackupFolder;
        [DataMember] public string LastDataFolder;
        [DataMember] public string CategoryOrderPreset = "business";
        [DataMember] public List<string> CategoryOrder = new List<string>();
        [DataMember] public List<string> CustomPalette = new List<string>();
        [DataMember] public bool CustomPalettePastelStyle;
        [DataMember] public bool AutomaticUpdateChecks = true;
        [DataMember] public DateTime LastUpdateCheckUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        [DataMember] public List<string> PaletteNames = new List<string>();
        [DataMember] public List<string> SavedPalettes = new List<string>();
        [DataMember] public int SelectedPaletteIndex;
        [DataMember] public bool LockPalettePlacement;
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
