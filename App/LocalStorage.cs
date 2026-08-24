using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FamilyPlanner
{
    public static class Store
    {
        static readonly string Folder = AppDataPaths.Root;
        static readonly string LegacyFilePath = Path.Combine(Folder, "items.json");
        static readonly string SettingsPath = Path.Combine(Folder, "settings.json");
        static readonly string LegacyBackupFolder = Path.Combine(Folder, "backups");
        static readonly string BackupFolder = AppDataPaths.Backups;
        static readonly Mutex DataFileMutex = new Mutex(false, "Local\\Onharu.DataFileLock");
        static readonly object BackupFolderInitLock = new object();
        static bool backupFolderReady;
        static string accountKey = "local";
        static string externalBackupFolder;
        static string FilePath { get { return Path.Combine(Folder, "items-" + accountKey + ".json"); } }

        public static void SetAccount(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) { accountKey = "local"; return; }
            using (var sha = SHA256.Create())
                accountKey = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        public static void SetExternalBackupFolder(string path)
        {
            externalBackupFolder = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        }

        public static List<PlannerItem> Load()
        {
            if (!File.Exists(FilePath) && File.Exists(LegacyFilePath)) File.Copy(LegacyFilePath, FilePath);
            if (!File.Exists(FilePath)) return new List<PlannerItem>();
            try
            {
                return ReadItems(FilePath);
            }
            catch (Exception ex) { ErrorLog.Write("Load calendar data", ex); return new List<PlannerItem>(); }
        }

        public static List<PlannerItem> ReadImportFile(string path)
        {
            int ignored; return ReadImportFile(path, out ignored);
        }

        public static List<PlannerItem> ReadImportFile(string path, out int googleExcluded)
        {
            var items = ReadItems(path);
            googleExcluded = items.Count(IsGoogleItem);
            return items.Where(x => !IsGoogleItem(x)).ToList();
        }

        public static bool IsGoogleItem(PlannerItem item)
        {
            return item != null && (!string.IsNullOrWhiteSpace(item.GoogleCalendarId) ||
                !string.IsNullOrWhiteSpace(item.GoogleEventId) || !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId));
        }

        static List<PlannerItem> ReadItems(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var items = (List<PlannerItem>)new DataContractJsonSerializer(typeof(List<PlannerItem>)).ReadObject(stream);
                if (items == null) throw new InvalidDataException("일정 목록이 없는 파일입니다.");
                items = items.Where(x => x != null && x.Start.Year >= 1900 && x.Start.Year <= 9998).ToList();
                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString();
                    if (!IsGoogleItem(item))
                        item.Category = item.Category == "업무" || item.Category == "업무일정" ? "업무일정" : item.Category == "야구" ? "야구" : item.Category == "국경일" ? "국경일" : item.Category == "기념일" || !string.IsNullOrWhiteSpace(item.AnniversaryType) ? "기념일" : "개인일정";
                    if (item.AllDay && !IsGoogleItem(item) && string.IsNullOrWhiteSpace(item.AnniversaryType) && (item.Category == "업무일정" || item.Category == "개인일정")) item.IsTodo = true;
                    if (item.AutoRollover && string.IsNullOrWhiteSpace(item.RolloverMode)) item.RolloverMode = "next_day";
                    NormalizeDates(item);
                }
                return CollapseMaterializedAnniversaries(items);
            }
        }

        public static void Save(List<PlannerItem> items)
        {
            foreach (var item in items) NormalizeDates(item);
            WriteAtomic(FilePath, items, typeof(List<PlannerItem>));
            BackupDaily(items);
            BackupExternalItems(items, accountKey + "-" + DateTime.Today.ToString("yyyyMMdd") + ".json", accountKey + "-*.json");
        }

        public static void BackupBeforeDestructiveChange(List<PlannerItem> items)
        {
            EnsureBackupFolder();
            var target = Path.Combine(BackupFolder, accountKey + "-before-delete-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            using (var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                new DataContractJsonSerializer(typeof(List<PlannerItem>)).WriteObject(stream, LocalOnly(items));
        }

        static List<PlannerItem> CollapseMaterializedAnniversaries(List<PlannerItem> source)
        {
            var ordinary = source.Where(x => IsGoogleItem(x) || string.IsNullOrWhiteSpace(x.AnniversaryType)).ToList();
            var anniversaries = source.Where(x => !IsGoogleItem(x) && !string.IsNullOrWhiteSpace(x.AnniversaryType))
                .GroupBy(x => !string.IsNullOrWhiteSpace(x.SeriesId) ? "s:" + x.SeriesId : "a:" + x.Title + "|" + x.AnniversaryType + "|" + x.AnniversaryDate.ToString("yyyyMMdd"));
            foreach (var group in anniversaries)
            {
                var master = group.OrderBy(x => x.AnniversaryDate.Year >= 1900 ? x.AnniversaryDate : x.Start).First();
                var start = master.AnniversaryDate.Year >= 1900 ? master.AnniversaryDate.Date : master.Start.Date;
                master.Start = start; master.End = start.AddDays(1); master.AnniversaryDate = start;
                master.SeriesId = null; master.RecurrenceFrequency = "yearly"; master.RecurrenceMode = "date";
                master.RecurrenceCount = 0; master.RecurrenceUntil = start;
                ordinary.Add(master);
            }
            return ordinary;
        }

        static void NormalizeDates(PlannerItem item)
        {
            if (item.End <= item.Start) item.End = item.AllDay ? item.Start.Date.AddDays(1) : item.Start.AddMinutes(30);
            if (item.SnoozeUntil.Year < 1900) item.SnoozeUntil = new DateTime(2000, 1, 1);
            if (item.RecurrenceUntil.Year < 1900) item.RecurrenceUntil = item.Start.Date;
            if (item.AnniversaryDate.Year < 1900 || item.AnniversaryDate.Year > 9998) item.AnniversaryDate = item.Start.Date;
            if (item.ShowDday && item.Start.Date < DateTime.Today && item.AnniversaryDate.Date > DateTime.Today)
                item.AnniversaryDate = item.Start.Date;
            item.RecurrenceCount = Math.Max(0, Math.Min(500, item.RecurrenceCount));
            if (item.ReminderConfigured) item.ReminderMinutes = GoogleCalendar.NormalizeReminderMinutes(item.ReminderMinutes, item.AllDay);
        }

        static void WriteAtomic(string path, object value, Type type)
        {
            Directory.CreateDirectory(Folder);
            var temp = path + "." + System.Diagnostics.Process.GetCurrentProcess().Id + "." + Guid.NewGuid().ToString("N") + ".tmp";
            DataFileMutex.WaitOne();
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(type).WriteObject(stream, value);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
                DataFileMutex.ReleaseMutex();
            }
        }

        static List<PlannerItem> LocalOnly(IEnumerable<PlannerItem> items) { return items.Where(x => !IsGoogleItem(x)).ToList(); }

        static void EnsureBackupFolder()
        {
            if (backupFolderReady) return;
            lock (BackupFolderInitLock)
            {
                if (backupFolderReady) return;
                Directory.CreateDirectory(BackupFolder);
                if (Directory.Exists(LegacyBackupFolder))
                {
                    foreach (var source in Directory.GetFiles(LegacyBackupFolder, "*.json"))
                    {
                        var target = Path.Combine(BackupFolder, Path.GetFileName(source));
                        if (!File.Exists(target)) File.Copy(source, target);
                    }
                }
                backupFolderReady = true;
            }
        }

        static void BackupDaily(List<PlannerItem> items)
        {
            DataFileMutex.WaitOne();
            try
            {
                EnsureBackupFolder();
                var target = Path.Combine(BackupFolder, accountKey + "-" + DateTime.Today.ToString("yyyyMMdd") + ".json");
                using (var stream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                    new DataContractJsonSerializer(typeof(List<PlannerItem>)).WriteObject(stream, LocalOnly(items));
                foreach (var old in Directory.GetFiles(BackupFolder, accountKey + "-*.json").OrderByDescending(x => x).Skip(30)) File.Delete(old);
            }
            finally { DataFileMutex.ReleaseMutex(); }
        }

        public static string[] Backups() { EnsureBackupFolder(); return Directory.GetFiles(BackupFolder, accountKey + "-*.json").OrderByDescending(x => x).ToArray(); }
        public static string BackupDirectory() { EnsureBackupFolder(); return BackupFolder; }
        public static string[] ExternalBackups()
        {
            if (string.IsNullOrWhiteSpace(externalBackupFolder)) return new string[0];
            try
            {
                var folder = Path.Combine(externalBackupFolder, "ONHARU-Backups");
                return Directory.Exists(folder) ? Directory.GetFiles(folder, accountKey + "-*.json").OrderByDescending(x => x).ToArray() : new string[0];
            }
            catch (Exception ex) { ErrorLog.Write("Read external backups", ex); return new string[0]; }
        }
        public static List<PlannerItem> Restore(string path)
        {
            return LocalOnly(ReadItems(path));
        }

        public static List<PlannerItem> LoadLocal()
        {
            var current = accountKey; accountKey = "local";
            var result = File.Exists(FilePath) ? Load() : new List<PlannerItem>();
            accountKey = current; return result;
        }

        public static void SaveLocal(List<PlannerItem> items)
        {
            var current = accountKey; accountKey = "local"; Save(items); accountKey = current;
        }

        public static PlannerSettings LoadSettings()
        {
            if (!File.Exists(SettingsPath)) return new PlannerSettings();
            try
            {
                using (var stream = File.OpenRead(SettingsPath))
                {
                    var settings = (PlannerSettings)new DataContractJsonSerializer(typeof(PlannerSettings)).ReadObject(stream);
                    if (settings.Version == 0) { settings.BusinessVisible = true; settings.PersonalVisible = true; settings.HolidayVisible = true; settings.Version = 1; }
                    if (settings.Version < 2) { settings.FontSize = 12; settings.Opacity = .95; settings.Version = 2; }
                    if (settings.Version < 3) { settings.SidebarVisible = true; settings.Version = 3; }
                    if (settings.Version < 5) { settings.Use24HourTime = true; settings.Version = 5; }
                    if (settings.Version < 6) { settings.CompletedLast = true; settings.Version = 6; }
                    if (settings.Version < 7)
                    {
                        settings.CalendarRangeMode = "month6"; settings.VisibleWeekCount = 1; settings.TodayRow = 1; settings.Version = 7;
                    }
                    if (settings.Version < 8)
                    {
                        settings.DefaultCalendarKey = "local:business"; settings.DefaultAllDay = true;
                        settings.DefaultStartHour = 9; settings.DefaultStartMinute = 0;
                        settings.DefaultDurationMinutes = 30; settings.DefaultReminderMinutes = -1; settings.Version = 8;
                    }
                    if (settings.Version < 9) { settings.CompletedDisplayMode = "normal"; settings.Version = 9; }
                    if (settings.Version < 10) { settings.StartViewMode = "today"; settings.LastShownDate = DateTime.Today; settings.Version = 10; }
                    if (settings.Version < 11) { settings.ReminderSound = true; settings.QuietStartHour = 22; settings.QuietEndHour = 7; settings.Version = 11; }
                    if (settings.Version < 12) { settings.StartupPositionMode = "remember"; settings.Version = 12; }
                    if (settings.Version < 13) { settings.CloseButtonAction = "minimize"; settings.Version = 13; }
                    if (settings.Version < 14) { settings.AnniversaryVisible = true; settings.Version = 14; }
                    if (settings.Version < 15) settings.Version = 15;
                    if (settings.Version < 16) { settings.DdayPanelVisible = true; settings.Version = 16; }
                    if (settings.Version < 17) { settings.RestDays = new List<int> { 0, 6 }; settings.Version = 17; }
                    if (settings.Version < 18) { settings.UseTimetable = false; settings.Version = 18; }
                    if (settings.Version < 19) { settings.UseRollover = true; settings.Version = 19; }
                    if (settings.Version < 20)
                    {
                        settings.TodayStyle = settings.TodayColor == "none" ? "none" : "fill";
                        settings.TodayBorderColor = "#F59E0B"; settings.Version = 20;
                    }
                    if (settings.Version < 21) settings.Version = 21;
                    if (settings.Version < 22) { settings.UseDiary = true; settings.Version = 22; }
                    if (settings.Version < 23) { settings.ShowGoogleTasks = false; settings.Version = 23; }
                    if (settings.Version < 24) { settings.UseProBaseball = false; settings.Version = 24; }
                    if (settings.Version < 25) { settings.BaseballVisible = true; settings.Version = 25; }
                    if (settings.Version < 26) { settings.FavoriteBaseballTeam = ""; settings.Version = 26; }
                    if (settings.Version < 27) { settings.AutomaticUpdateChecks = true; settings.LastUpdateCheckUtc = SafeUpdateEpoch(); settings.Version = 27; }
                    if (settings.Version < 28) { settings.ThemeId = "classic"; settings.Version = 28; }
                    if (settings.Version < 29)
                    {
                        settings.MonthRangeMode = settings.CalendarRangeMode == "weeks" ? "monthAuto" : settings.CalendarRangeMode;
                        settings.UseMonthView = settings.CalendarRangeMode != "weeks";
                        settings.Version = 29;
                    }
                    if (settings.Version < 30)
                    {
                        settings.BaseballColor = "#16A085"; settings.DdayColor = "#38A7D8";
                        settings.AnniversaryColor = "#A78BFA"; settings.HolidayColor = "#EF4444";
                        settings.Version = 30;
                    }
                    if (settings.Version < 31) { settings.SelectedPaletteIndex = 8; settings.Version = 31; }
                    if (settings.Version < 32)
                    {
                        if (settings.SavedPalettes == null) settings.SavedPalettes = new List<string>();
                        for (var i = 0; i < Math.Min(5, settings.SavedPalettes.Count); i++) settings.SavedPalettes[i] = "";
                        settings.Version = 32;
                    }
                    if (settings.Version < 33)
                    {
                        if (settings.SavedPalettes == null) settings.SavedPalettes = new List<string>();
                        for (var i = 0; i < Math.Min(5, settings.SavedPalettes.Count); i++) settings.SavedPalettes[i] = "";
                        settings.Version = 33;
                    }
                    if (settings.Version < 34)
                    {
                        if (settings.SavedPalettes == null) settings.SavedPalettes = new List<string>();
                        for (var i = 0; i < Math.Min(5, settings.SavedPalettes.Count); i++) settings.SavedPalettes[i] = "";
                        settings.Version = 34;
                    }
                    if (settings.Version < 35)
                    {
                        if (string.IsNullOrWhiteSpace(settings.BaseballColor) || settings.BaseballColor.Equals("#16A085", StringComparison.OrdinalIgnoreCase)) settings.BaseballColor = "#00FF66";
                        if (string.IsNullOrWhiteSpace(settings.DdayColor) || settings.DdayColor.Equals("#38A7D8", StringComparison.OrdinalIgnoreCase)) settings.DdayColor = "#FF5722";
                        if (string.IsNullOrWhiteSpace(settings.AnniversaryColor) || settings.AnniversaryColor.Equals("#A78BFA", StringComparison.OrdinalIgnoreCase)) settings.AnniversaryColor = "#FF1744";
                        if (string.IsNullOrWhiteSpace(settings.HolidayColor) || settings.HolidayColor.Equals("#EF4444", StringComparison.OrdinalIgnoreCase)) settings.HolidayColor = "#FF2A2A";
                        if (settings.SavedPalettes == null) settings.SavedPalettes = new List<string>();
                        for (var i = 0; i < Math.Min(5, settings.SavedPalettes.Count); i++) settings.SavedPalettes[i] = "";
                        settings.Version = 35;
                    }
                    if (settings.Version < 36)
                    {
                        if (string.IsNullOrWhiteSpace(settings.BaseballColor) || settings.BaseballColor.Equals("#00FF66", StringComparison.OrdinalIgnoreCase)) settings.BaseballColor = "#38A169";
                        if (string.IsNullOrWhiteSpace(settings.DdayColor) || settings.DdayColor.Equals("#FF5722", StringComparison.OrdinalIgnoreCase)) settings.DdayColor = "#DD6B20";
                        if (string.IsNullOrWhiteSpace(settings.AnniversaryColor) || settings.AnniversaryColor.Equals("#FF1744", StringComparison.OrdinalIgnoreCase)) settings.AnniversaryColor = "#E52E71";
                        if (string.IsNullOrWhiteSpace(settings.HolidayColor) || settings.HolidayColor.Equals("#FF2A2A", StringComparison.OrdinalIgnoreCase)) settings.HolidayColor = "#E53E3E";
                        if (settings.SavedPalettes == null) settings.SavedPalettes = new List<string>();
                        for (var i = 0; i < Math.Min(5, settings.SavedPalettes.Count); i++) settings.SavedPalettes[i] = "";
                        settings.Version = 36;
                    }
                    if (settings.Version < 37)
                    {
                        settings.ThemeId = OnharuThemePalette.Normalize(settings.ThemeId);
                        settings.Version = 37;
                    }
                    if (settings.Version < 38) settings.Version = 38;
                    if (settings.Version < 39)
                    {
                        settings.SportsCalendarScale = 1.0;
                        settings.Version = 39;
                    }
                    if (settings.Version < 40)
                    {
                        settings.ImportantFirst = true;
                        settings.Version = 40;
                    }
                    if (settings.Version < 41)
                    {
                        settings.LockPalettePlacement = false;
                        settings.Version = 41;
                    }
                    if (settings.LastUpdateCheckUtc.Year < 1900) settings.LastUpdateCheckUtc = SafeUpdateEpoch();
                    settings.ThemeId = OnharuThemePalette.Normalize(settings.ThemeId);
                    if (string.IsNullOrWhiteSpace(settings.BaseballColor)) settings.BaseballColor = "#38A169";
                    if (!new[] { .90, 1.0, 1.15 }.Contains(settings.SportsCalendarScale)) settings.SportsCalendarScale = 1.0;
                    if (string.IsNullOrWhiteSpace(settings.DdayColor)) settings.DdayColor = "#DD6B20";
                    if (string.IsNullOrWhiteSpace(settings.AnniversaryColor)) settings.AnniversaryColor = "#E52E71";
                    if (string.IsNullOrWhiteSpace(settings.HolidayColor)) settings.HolidayColor = "#E53E3E";
                    if (settings.RestDays == null) settings.RestDays = new List<int> { 0, 6 };
                    settings.RestDays = settings.RestDays.Where(x => x >= 0 && x <= 6).Distinct().ToList();
                    if (string.IsNullOrWhiteSpace(settings.CalendarRangeMode)) settings.CalendarRangeMode = "month6";
                    if (!new[] { "monthAuto", "month5", "month6", "weeks" }.Contains(settings.CalendarRangeMode)) settings.CalendarRangeMode = "month6";
                    if (!new[] { "monthAuto", "month5", "month6" }.Contains(settings.MonthRangeMode)) settings.MonthRangeMode = "monthAuto";
                    if (string.IsNullOrWhiteSpace(settings.SelectedDateStyle)) settings.SelectedDateStyle = "fill";
                    if (settings.SelectedDateStyle != "fill" && settings.SelectedDateStyle != "border" && settings.SelectedDateStyle != "both" && settings.SelectedDateStyle != "none") settings.SelectedDateStyle = "fill";
                    if (string.IsNullOrWhiteSpace(settings.TodayColor)) settings.TodayColor = "#CCFCE7F3";
                    if (settings.TodayColor != "none" && !settings.TodayColor.StartsWith("#")) settings.TodayColor = "#CCFCE7F3";
                    if (settings.TodayStyle == "border") settings.TodayStyle = "icon";
                    if (settings.TodayStyle == "both") settings.TodayStyle = "fill_icon";
                    if (!new[] { "none", "fill", "icon", "fill_icon" }.Contains(settings.TodayStyle)) settings.TodayStyle = "fill";
                    if (string.IsNullOrWhiteSpace(settings.TodayBorderColor) || !settings.TodayBorderColor.StartsWith("#")) settings.TodayBorderColor = "#4F7BFF";
                    if (string.IsNullOrWhiteSpace(settings.SelectedDateFillColor)) settings.SelectedDateFillColor = "#CCDBEAFE";
                    if (string.IsNullOrWhiteSpace(settings.SelectedDateBorderColor)) settings.SelectedDateBorderColor = "#3B82F6";
                    if (!new[] { "normal", "fade", "hide" }.Contains(settings.CompletedDisplayMode)) settings.CompletedDisplayMode = "normal";
                    if (settings.StartViewMode != "today" && settings.StartViewMode != "last") settings.StartViewMode = "today";
                    if (settings.LastShownDate.Year < 1900 || settings.LastShownDate.Year > 9998) settings.LastShownDate = DateTime.Today;
                    if (!new[] { "remember", "locked", "editable" }.Contains(settings.StartupPositionMode)) settings.StartupPositionMode = "remember";
                    if (!new[] { "minimize", "confirm_exit" }.Contains(settings.CloseButtonAction)) settings.CloseButtonAction = "minimize";
                    if (!new[] { 11.0, 12.0, 14.0 }.Contains(settings.FontSize)) settings.FontSize = 12;
                    if (!new[] { 0, 5, 15, 30, 60 }.Contains(settings.AutoSyncMinutes)) settings.AutoSyncMinutes = 0;
                    if (settings.CalendarOrderMode != "category" && settings.CalendarOrderMode != "time") settings.CalendarOrderMode = "category";
                    if (settings.WeekNumberRule != "iso" && settings.WeekNumberRule != "jan1") settings.WeekNumberRule = "iso";
                    if (!new[] { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" }.Contains(settings.WeekStartDay))
                        settings.WeekStartDay = settings.WeekNumberRule == "jan1" ? "sunday" : "monday";
                    settings.Opacity = Math.Max(.10, Math.Min(1.0, settings.Opacity));
                    settings.VisibleWeekCount = Math.Max(1, Math.Min(6, settings.VisibleWeekCount));
                    settings.TodayRow = Math.Max(1, Math.Min(settings.VisibleWeekCount, settings.TodayRow));
                    settings.DefaultStartHour = Math.Max(0, Math.Min(23, settings.DefaultStartHour));
                    settings.DefaultStartMinute = Math.Max(0, Math.Min(59, settings.DefaultStartMinute));
                    if (!new[] { 30, 60, 90, 120 }.Contains(settings.DefaultDurationMinutes)) settings.DefaultDurationMinutes = 30;
                    settings.QuietStartHour = Math.Max(0, Math.Min(23, settings.QuietStartHour));
                    settings.QuietEndHour = Math.Max(0, Math.Min(23, settings.QuietEndHour));
                    if (settings.GoogleCalendars == null) settings.GoogleCalendars = new List<GoogleCalendarSetting>();
                    if (settings.CustomPalette == null) settings.CustomPalette = new List<string>();
                    if (settings.PaletteNames == null) settings.PaletteNames = new List<string>();
                    if (settings.SavedPalettes == null) settings.SavedPalettes = new List<string>();
                    settings.SelectedPaletteIndex = Math.Max(0, Math.Min(8, settings.SelectedPaletteIndex));
                    if (settings.DateBackgroundColors == null) settings.DateBackgroundColors = new Dictionary<string, string>();
                    if (settings.GoogleOptionsVersion == 0)
                    {
                        foreach (var source in settings.GoogleCalendars) source.Editable = source.Primary;
                        settings.GoogleOptionsVersion = 1;
                    }
                    return settings;
                }
            }
            catch (Exception ex) { ErrorLog.Write("Load settings", ex); return new PlannerSettings(); }
        }

        public static void SaveSettings(PlannerSettings settings)
        {
            if (settings.LastShownDate.Year < 1900 || settings.LastShownDate.Year > 9998) settings.LastShownDate = DateTime.Today;
            if (settings.LastUpdateCheckUtc.Year < 1900) settings.LastUpdateCheckUtc = SafeUpdateEpoch();
            WriteAtomic(SettingsPath, settings, typeof(PlannerSettings));
            BackupExternal(SettingsPath, "settings.json", null);
        }

        static DateTime SafeUpdateEpoch() { return new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc); }

        static void BackupExternal(string source, string fileName, string cleanupPattern)
        {
            if (string.IsNullOrWhiteSpace(externalBackupFolder)) return;
            try
            {
                var folder = Path.Combine(externalBackupFolder, "ONHARU-Backups");
                Directory.CreateDirectory(folder);
                File.Copy(source, Path.Combine(folder, fileName), true);
                if (!string.IsNullOrWhiteSpace(cleanupPattern))
                    foreach (var old in Directory.GetFiles(folder, cleanupPattern).OrderByDescending(x => x).Skip(30)) File.Delete(old);
            }
            catch (Exception ex) { ErrorLog.Write("External backup", ex); }
        }

        static void BackupExternalItems(List<PlannerItem> items, string fileName, string cleanupPattern)
        {
            if (string.IsNullOrWhiteSpace(externalBackupFolder)) return;
            try
            {
                var folder = Path.Combine(externalBackupFolder, "ONHARU-Backups"); Directory.CreateDirectory(folder);
                using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create, FileAccess.Write, FileShare.None))
                    new DataContractJsonSerializer(typeof(List<PlannerItem>)).WriteObject(stream, LocalOnly(items));
                foreach (var old in Directory.GetFiles(folder, cleanupPattern).OrderByDescending(x => x).Skip(30)) File.Delete(old);
            }
            catch (Exception ex) { ErrorLog.Write("External item backup", ex); }
        }

    }
}
