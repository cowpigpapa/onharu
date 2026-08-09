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
        static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyPlanner");
        static readonly string LegacyFilePath = Path.Combine(Folder, "items.json");
        static readonly string SettingsPath = Path.Combine(Folder, "settings.json");
        static readonly string BackupFolder = Path.Combine(Folder, "backups");
        static readonly Mutex DataFileMutex = new Mutex(false, "Local\\OnharuDataFileLock");
        static string accountKey = "local";
        static string FilePath { get { return Path.Combine(Folder, "items-" + accountKey + ".json"); } }

        public static void SetAccount(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) { accountKey = "local"; return; }
            using (var sha = SHA256.Create())
                accountKey = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        public static List<PlannerItem> Load()
        {
            if (!File.Exists(FilePath) && File.Exists(LegacyFilePath)) File.Copy(LegacyFilePath, FilePath);
            if (!File.Exists(FilePath)) return accountKey == "local" ? Samples() : new List<PlannerItem>();
            try
            {
                using (var stream = File.OpenRead(FilePath))
                {
                    var items = (List<PlannerItem>)new DataContractJsonSerializer(typeof(List<PlannerItem>)).ReadObject(stream);
                    foreach (var item in items)
                    {
                        item.Category = item.Category == "업무" || item.Category == "업무일정" ? "업무일정" : item.Category == "국경일" ? "국경일" : "개인일정";
                        if (item.AutoRollover && string.IsNullOrWhiteSpace(item.RolloverMode)) item.RolloverMode = "next_day";
                        NormalizeDates(item);
                    }
                    return items;
                }
            }
            catch (Exception ex) { ErrorLog.Write("Load calendar data", ex); return new List<PlannerItem>(); }
        }

        public static void Save(List<PlannerItem> items)
        {
            foreach (var item in items) NormalizeDates(item);
            WriteAtomic(FilePath, items, typeof(List<PlannerItem>));
            BackupDaily();
        }

        static void NormalizeDates(PlannerItem item)
        {
            if (item.SnoozeUntil.Year < 1900) item.SnoozeUntil = new DateTime(2000, 1, 1);
            if (item.RecurrenceUntil.Year < 1900) item.RecurrenceUntil = item.Start.Date;
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

        static void BackupDaily()
        {
            DataFileMutex.WaitOne();
            try
            {
                Directory.CreateDirectory(BackupFolder);
                var target = Path.Combine(BackupFolder, accountKey + "-" + DateTime.Today.ToString("yyyyMMdd") + ".json");
                if (!File.Exists(target)) File.Copy(FilePath, target);
                foreach (var old in Directory.GetFiles(BackupFolder, accountKey + "-*.json").OrderByDescending(x => x).Skip(30)) File.Delete(old);
            }
            finally { DataFileMutex.ReleaseMutex(); }
        }

        public static string[] Backups() { return Directory.Exists(BackupFolder) ? Directory.GetFiles(BackupFolder, accountKey + "-*.json").OrderByDescending(x => x).ToArray() : new string[0]; }
        public static List<PlannerItem> Restore(string path) { File.Copy(path, FilePath, true); return Load(); }

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
                    if (settings.GoogleCalendars == null) settings.GoogleCalendars = new List<GoogleCalendarSetting>();
                    if (settings.CustomPalette == null) settings.CustomPalette = new List<string>();
                    if (settings.PaletteNames == null) settings.PaletteNames = new List<string>();
                    if (settings.SavedPalettes == null) settings.SavedPalettes = new List<string>();
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
            WriteAtomic(SettingsPath, settings, typeof(PlannerSettings));
        }

        static List<PlannerItem> Samples()
        {
            var today = DateTime.Today;
            return new List<PlannerItem>
            {
                New("가족 저녁 식사", today.AddHours(19), today.AddHours(20), false, false, "개인일정"),
                New("주간 업무 보고", today.AddHours(10), today.AddHours(10.5), false, true, "업무일정"),
                New("결혼기념일", today.AddDays(3), today.AddDays(4), true, false, "개인일정"),
                New("자동차 보험 갱신", today.AddDays(1).AddHours(14), today.AddDays(1).AddHours(14.5), false, true, "개인일정")
            };
        }

        static PlannerItem New(string title, DateTime start, DateTime end, bool allDay, bool todo, string category)
        {
            return new PlannerItem { Id = Guid.NewGuid().ToString(), Title = title, Start = start,
                End = end, AllDay = allDay, IsTodo = todo, Category = category };
        }
    }
}
