using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace FamilyPlanner
{
    static class UiRound
    {
        public static void Apply(Button button, double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        public static void SoftenScrollBars(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var bar = child as ScrollBar;
                if (bar != null)
                {
                    bar.Width = 10; bar.Margin = new Thickness(2, 3, 2, 3); bar.Background = Brushes.Transparent; bar.BorderThickness = new Thickness(0);
                    bar.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'><Grid Background='Transparent'><Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='#A5B4FC' CornerRadius='4' Margin='1'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>");
                }
                var thumb = child as Thumb;
                if (thumb != null) { thumb.Background = new SolidColorBrush(Color.FromRgb(165, 180, 252)); thumb.BorderThickness = new Thickness(0); }
                SoftenScrollBars(child);
            }
        }
    }

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
        [DataMember] public int ReminderMinutes;
        [DataMember] public bool ReminderConfigured;
        [DataMember] public DateTime SnoozeUntil;
        [DataMember] public string ReminderDismissedKey;
        [DataMember] public string RecurrenceFrequency;
        [DataMember] public string RecurrenceMode;
        [DataMember] public string RecurrenceDays;
        [DataMember] public DateTime RecurrenceUntil;
        [DataMember] public string SeriesId;
        [DataMember] public string GoogleRecurringEventId;
        [DataMember] public bool PendingGoogleSync;
    }

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
            catch { return new List<PlannerItem>(); }
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
            var temp = path + "." + Process.GetCurrentProcess().Id + "." + Guid.NewGuid().ToString("N") + ".tmp";
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
                    if (settings.GoogleCalendars == null) settings.GoogleCalendars = new List<GoogleCalendarSetting>();
                    if (settings.GoogleOptionsVersion == 0)
                    {
                        foreach (var source in settings.GoogleCalendars) source.Editable = source.Primary;
                        settings.GoogleOptionsVersion = 1;
                    }
                    return settings;
                }
            }
            catch { return new PlannerSettings(); }
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

    [DataContract]
    class GoogleToken
    {
        [DataMember(Name = "access_token", EmitDefaultValue = false)] public string AccessToken;
        [DataMember(Name = "refresh_token", EmitDefaultValue = false)] public string RefreshToken;
        [DataMember(Name = "expires_in", EmitDefaultValue = false)] public int ExpiresIn;
        [DataMember(Name = "error", EmitDefaultValue = false)] public string Error;
        [DataMember(Name = "error_description", EmitDefaultValue = false)] public string ErrorDescription;
    }

    [DataContract]
    class GoogleEvents { [DataMember(Name = "items", EmitDefaultValue = false)] public List<GoogleEvent> Items; }

    [DataContract]
    class GoogleCalendarList { [DataMember(Name = "items", EmitDefaultValue = false)] public List<GoogleCalendarEntry> Items; }

    [DataContract]
    class GoogleCalendarEntry
    {
        [DataMember(Name = "id", EmitDefaultValue = false)] public string Id;
        [DataMember(Name = "summary", EmitDefaultValue = false)] public string Summary;
        [DataMember(Name = "backgroundColor", EmitDefaultValue = false)] public string BackgroundColor;
        [DataMember(Name = "accessRole", EmitDefaultValue = false)] public string AccessRole;
        [DataMember(Name = "primary", EmitDefaultValue = false)] public bool Primary;
        [DataMember(Name = "selected", EmitDefaultValue = false)] public bool Selected;
        [DataMember(Name = "hidden", EmitDefaultValue = false)] public bool Hidden;
    }

    [DataContract]
    class GoogleEvent
    {
        [DataMember(Name = "id", EmitDefaultValue = false)] public string Id;
        [DataMember(Name = "summary", EmitDefaultValue = false)] public string Summary;
        [DataMember(Name = "description", EmitDefaultValue = false)] public string Description;
        [DataMember(Name = "status", EmitDefaultValue = false)] public string Status;
        [DataMember(Name = "start", EmitDefaultValue = false)] public GoogleDate Start;
        [DataMember(Name = "end", EmitDefaultValue = false)] public GoogleDate End;
        [DataMember(Name = "extendedProperties", EmitDefaultValue = false)] public GoogleExtended ExtendedProperties;
        [DataMember(Name = "recurrence", EmitDefaultValue = false)] public List<string> Recurrence;
        [DataMember(Name = "recurringEventId", EmitDefaultValue = false)] public string RecurringEventId;
    }

    [DataContract]
    class GoogleDate
    {
        [DataMember(Name = "date", EmitDefaultValue = false)] public string Date;
        [DataMember(Name = "dateTime", EmitDefaultValue = false)] public string DateTime;
        [DataMember(Name = "timeZone", EmitDefaultValue = false)] public string TimeZone;
    }

    [DataContract]
    class GoogleExtended { [DataMember(Name = "private", EmitDefaultValue = false)] public Dictionary<string, string> Private; }

    static class GoogleCalendar
    {
        const string ClientId = "397166784516-v2rap5v944sp38g0h5mnoo3v3nsqkjgg.apps.googleusercontent.com";
        static string ClientSecret
        {
            get
            {
                var value = Environment.GetEnvironmentVariable("ONHARU_GOOGLE_CLIENT_SECRET") ??
                    Environment.GetEnvironmentVariable("ONHARU_GOOGLE_CLIENT_SECRET", EnvironmentVariableTarget.User);
                if (string.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException("Google 연결 설정이 없습니다.");
                return value;
            }
        }
        const string Scope = "https://www.googleapis.com/auth/calendar.events https://www.googleapis.com/auth/calendar.calendarlist.readonly";
        static readonly string TokenPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyPlanner", "google-v2.token");
        static readonly string AccountPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyPlanner", "google-account-v2.dat");
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        static string accessToken;
        static DateTime expiresAt;
        static HttpListener pendingListener;

        public static bool IsConnected { get { return File.Exists(TokenPath); } }
        public static string ConnectedAccountId { get { try { return File.Exists(AccountPath) ? Unprotect(File.ReadAllBytes(AccountPath)) : null; } catch { return null; } } }
        public static void RememberAccount(string id) { if (!string.IsNullOrWhiteSpace(id)) File.WriteAllBytes(AccountPath, Protect(id)); }

        public static async Task ConnectAsync()
        {
            var verifier = Base64Url(RandomBytes(48));
            string challenge;
            using (var sha = SHA256.Create()) challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            var listener = new HttpListener();
            var port = FreePort();
            var redirect = "http://127.0.0.1:" + port + "/";
            listener.Prefixes.Add(redirect); listener.Start(); pendingListener = listener;
            var state = Base64Url(RandomBytes(24));
            var url = "https://accounts.google.com/o/oauth2/v2/auth?client_id=" + E(ClientId) +
                "&redirect_uri=" + E(redirect) + "&response_type=code&scope=" + E(Scope) +
                "&access_type=offline&prompt=consent&code_challenge=" + E(challenge) + "&code_challenge_method=S256&state=" + E(state);
            Process.Start(url);
            var contextTask = listener.GetContextAsync();
            if (await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(1))) != contextTask)
            { listener.Stop(); pendingListener = null; throw new TimeoutException("Google 로그인이 취소되었거나 시간이 초과되었습니다."); }
            var context = await contextTask;
            var query = context.Request.QueryString;
            var responseText = query["error"] == null ? "온하루 연결이 완료되었습니다. 이 창을 닫아도 됩니다." : "온하루 연결이 취소되었습니다.";
            var bytes = Encoding.UTF8.GetBytes("<html><meta charset='utf-8'><body style='font-family:sans-serif;padding:40px'><h2>" + responseText + "</h2></body></html>");
            context.Response.ContentType = "text/html; charset=utf-8"; context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length); context.Response.Close(); listener.Stop(); pendingListener = null;
            if (query["error"] != null) throw new InvalidOperationException(query["error"]);
            if (query["state"] != state) throw new InvalidOperationException("Google 로그인 응답을 확인할 수 없습니다.");
            var token = await TokenRequest("code=" + E(query["code"]) + "&client_id=" + E(ClientId) + "&client_secret=" + E(ClientSecret) +
                "&redirect_uri=" + E(redirect) + "&grant_type=authorization_code&code_verifier=" + E(verifier));
            if (string.IsNullOrWhiteSpace(token.RefreshToken)) throw new InvalidOperationException("Google 갱신 토큰을 받지 못했습니다.");
            SaveRefreshToken(token.RefreshToken); SetAccessToken(token);
            var calendars = await Send<GoogleCalendarList>(HttpMethod.Get, "https://www.googleapis.com/calendar/v3/users/me/calendarList?maxResults=250", null);
            var primary = (calendars.Items ?? new List<GoogleCalendarEntry>()).FirstOrDefault(x => x.Primary);
            if (primary != null) File.WriteAllBytes(AccountPath, Protect(primary.Id));
        }

        public static void Disconnect()
        {
            accessToken = null; expiresAt = DateTime.MinValue;
            if (File.Exists(TokenPath)) File.Delete(TokenPath);
            if (File.Exists(AccountPath)) File.Delete(AccountPath);
        }

        public static void CancelConnect()
        {
            var listener = pendingListener; pendingListener = null;
            if (listener != null && listener.IsListening) listener.Stop();
        }

        public static async Task<List<GoogleCalendarSetting>> SyncAsync(List<PlannerItem> local, List<GoogleCalendarSetting> saved)
        {
            await EnsureToken();
            foreach (var item in local.Where(x => x.Category == "개인일정" && !string.IsNullOrWhiteSpace(x.GoogleCalendarId) && (string.IsNullOrWhiteSpace(x.GoogleEventId) || x.PendingGoogleSync)).ToList())
            { await UpsertAsync(item); item.PendingGoogleSync = false; }

            var from = DateTime.Today.AddYears(-1).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            var to = DateTime.Today.AddYears(2).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            var list = await Send<GoogleCalendarList>(HttpMethod.Get, "https://www.googleapis.com/calendar/v3/users/me/calendarList?maxResults=250", null);
            var calendars = new List<GoogleCalendarSetting>();
            foreach (var entry in (list.Items ?? new List<GoogleCalendarEntry>()).Where(x => !x.Hidden))
            {
                var old = saved == null ? null : saved.FirstOrDefault(x => x.Id == entry.Id);
                var holidaySource = (entry.Summary ?? "").Contains("휴일") || (entry.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
                var canWrite = entry.AccessRole == "owner" || entry.AccessRole == "writer";
                var calendar = new GoogleCalendarSetting { Id = entry.Id, Name = entry.Summary,
                    Color = holidaySource ? "#CF2B36" : old == null ? entry.BackgroundColor : old.Color,
                    OriginalColor = entry.BackgroundColor,
                    Primary = entry.Primary, AccessRole = entry.AccessRole, Visible = old == null ? entry.Selected || entry.Primary : old.Visible,
                    Editable = canWrite && !holidaySource && (old == null ? entry.Primary : old.Editable) };
                calendars.Add(calendar);
                var data = await Send<GoogleEvents>(HttpMethod.Get, "https://www.googleapis.com/calendar/v3/calendars/" + E(entry.Id) +
                    "/events?singleEvents=true&maxResults=2500&timeMin=" + E(from) + "&timeMax=" + E(to), null);
                var remoteIds = new HashSet<string>();
                foreach (var remote in (data.Items ?? new List<GoogleEvent>()).Where(x => x.Status != "cancelled" && x.Start != null && x.End != null))
                {
                    remoteIds.Add(remote.Id);
                    var item = local.FirstOrDefault(x => x.GoogleEventId == remote.Id && (x.GoogleCalendarId == entry.Id || (entry.Primary && string.IsNullOrWhiteSpace(x.GoogleCalendarId))));
                    if (item == null && !string.IsNullOrWhiteSpace(remote.RecurringEventId))
                    {
                        var master = local.FirstOrDefault(x => x.GoogleEventId == remote.RecurringEventId && !string.IsNullOrWhiteSpace(x.RecurrenceFrequency));
                        if (master != null) { item = master; item.GoogleEventId = remote.Id; item.GoogleRecurringEventId = remote.RecurringEventId; }
                    }
                    if (item == null) { item = new PlannerItem { Id = Guid.NewGuid().ToString(), GoogleEventId = remote.Id }; local.Add(item); }
                    ApplyRemote(item, remote, calendar);
                }
                local.RemoveAll(x => !string.IsNullOrWhiteSpace(x.GoogleEventId) && !x.CreatedInOnharu && x.GoogleCalendarId == entry.Id &&
                    InSyncRange(x.Start) && !remoteIds.Contains(x.GoogleEventId));
            }
            return calendars;
        }

        public static async Task UpsertAsync(PlannerItem item)
        {
            await EnsureToken();
            var json = EventJson(item);
            var calendarId = string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "primary" : item.GoogleCalendarId;
            if (string.IsNullOrWhiteSpace(item.GoogleEventId))
            {
                var created = await Send<GoogleEvent>(HttpMethod.Post, "https://www.googleapis.com/calendar/v3/calendars/" + E(calendarId) + "/events", json);
                item.GoogleEventId = created.Id;
            }
            else await Send<GoogleEvent>(new HttpMethod("PATCH"), "https://www.googleapis.com/calendar/v3/calendars/" + E(calendarId) + "/events/" + E(item.GoogleEventId), json);
            item.OnharuManaged = true;
            item.PendingGoogleSync = false;
        }

        public static async Task UpsertSeriesAsync(PlannerItem item)
        {
            if (string.IsNullOrWhiteSpace(item.GoogleRecurringEventId)) { await UpsertAsync(item); return; }
            await EnsureToken();
            var body = Read<GoogleEvent>(EventJson(item)); body.Start = null; body.End = null;
            if (!string.IsNullOrWhiteSpace(item.RecurrenceFrequency))
                body.Recurrence = new List<string> { RecurrenceRule(item) };
            var calendarId = string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "primary" : item.GoogleCalendarId;
            await Send<GoogleEvent>(new HttpMethod("PATCH"), "https://www.googleapis.com/calendar/v3/calendars/" + E(calendarId) + "/events/" + E(item.GoogleRecurringEventId), Write(body));
            item.PendingGoogleSync = false;
        }

        public static async Task DeleteAsync(PlannerItem item, bool wholeSeries = false)
        {
            if (string.IsNullOrWhiteSpace(item.GoogleEventId)) return;
            await EnsureToken();
            var calendarId = string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "primary" : item.GoogleCalendarId;
            var eventId = wholeSeries && !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId) ? item.GoogleRecurringEventId : item.GoogleEventId;
            await Send<object>(HttpMethod.Delete, "https://www.googleapis.com/calendar/v3/calendars/" + E(calendarId) + "/events/" + E(eventId), null);
        }

        public static async Task TrimSeriesBeforeAsync(PlannerItem item)
        {
            if (string.IsNullOrWhiteSpace(item.GoogleRecurringEventId)) { await DeleteAsync(item); return; }
            await EnsureToken();
            var calendarId = string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "primary" : item.GoogleCalendarId;
            var url = "https://www.googleapis.com/calendar/v3/calendars/" + E(calendarId) + "/events/" + E(item.GoogleRecurringEventId);
            var parent = await Send<GoogleEvent>(HttpMethod.Get, url, null);
            var recurrence = parent.Recurrence == null ? new List<string>() : parent.Recurrence.ToList();
            for (var i = 0; i < recurrence.Count; i++)
            {
                if (!recurrence[i].StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = recurrence[i].Split(';').Where(x => !x.StartsWith("UNTIL=", StringComparison.OrdinalIgnoreCase) && !x.StartsWith("COUNT=", StringComparison.OrdinalIgnoreCase)).ToList();
                parts.Add("UNTIL=" + item.Start.AddSeconds(-1).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'"));
                recurrence[i] = string.Join(";", parts);
            }
            parent.Start = null; parent.End = null; parent.Recurrence = recurrence;
            await Send<GoogleEvent>(new HttpMethod("PATCH"), url, Write(parent));
        }

        static async Task EnsureToken()
        {
            if (!string.IsNullOrWhiteSpace(accessToken) && expiresAt > DateTime.UtcNow.AddMinutes(1)) return;
            if (!IsConnected) throw new InvalidOperationException("Google 캘린더가 연결되지 않았습니다.");
            var token = await TokenRequest("client_id=" + E(ClientId) + "&client_secret=" + E(ClientSecret) + "&refresh_token=" + E(LoadRefreshToken()) + "&grant_type=refresh_token");
            SetAccessToken(token);
        }

        static async Task<GoogleToken> TokenRequest(string body)
        {
            var response = await Http.PostAsync("https://oauth2.googleapis.com/token", new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded"));
            var token = Read<GoogleToken>(await response.Content.ReadAsStringAsync());
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(token.ErrorDescription ?? token.Error ?? "Google 로그인에 실패했습니다.");
            return token;
        }

        static async Task<T> Send<T>(HttpMethod method, string url, string json)
        {
            using (var request = new HttpRequestMessage(method, url))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await Http.SendAsync(request);
                var text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Google Calendar 오류: " + (string.IsNullOrWhiteSpace(text) ? response.StatusCode.ToString() : text));
                if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(text)) return default(T);
                return Read<T>(text);
            }
        }

        static string EventJson(PlannerItem item)
        {
            var e = new GoogleEvent { Summary = item.Title, Description = item.Notes, ExtendedProperties = new GoogleExtended { Private = new Dictionary<string, string> {
                { "onharu", "1" }, { "onharuTodo", item.IsTodo ? "1" : "0" }, { "onharuCompleted", item.Completed ? "1" : "0" },
                { "onharuRollover", string.IsNullOrWhiteSpace(item.RolloverMode) ? "0" : "1" },
                { "onharuRolloverMode", item.RolloverMode ?? "none" }, { "onharuReminder", item.ReminderMinutes.ToString() },
                { "onharuImportant", item.Important ? "1" : "0" }, { "onharuRecurrence", item.RecurrenceFrequency ?? "" },
                { "onharuRecurrenceMode", item.RecurrenceMode ?? "" }, { "onharuRecurrenceDays", item.RecurrenceDays ?? "" } } } };
            if (string.IsNullOrWhiteSpace(item.GoogleRecurringEventId) && !string.IsNullOrWhiteSpace(item.RecurrenceFrequency))
                e.Recurrence = new List<string> { RecurrenceRule(item) };
            if (item.AllDay)
            {
                e.Start = new GoogleDate { Date = item.Start.ToString("yyyy-MM-dd") };
                e.End = new GoogleDate { Date = (item.End.Date <= item.Start.Date ? item.Start.Date.AddDays(1) : item.End.Date).ToString("yyyy-MM-dd") };
            }
            else
            {
                e.Start = new GoogleDate { DateTime = new DateTimeOffset(item.Start).ToString("o"), TimeZone = "Asia/Seoul" };
                e.End = new GoogleDate { DateTime = new DateTimeOffset(item.End).ToString("o"), TimeZone = "Asia/Seoul" };
            }
            return Write(e);
        }

        static string RecurrenceRule(PlannerItem item)
        {
            var rule = "RRULE:FREQ=" + item.RecurrenceFrequency.ToUpperInvariant();
            if (item.RecurrenceFrequency == "daily" && item.RecurrenceMode == "weekdays") rule = "RRULE:FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR";
            else if (item.RecurrenceFrequency == "weekly" && !string.IsNullOrWhiteSpace(item.RecurrenceDays)) rule += ";BYDAY=" + item.RecurrenceDays;
            else if (item.RecurrenceFrequency == "monthly" && item.RecurrenceMode == "monthly_last") rule += ";BYMONTHDAY=-1";
            else if (item.RecurrenceFrequency == "monthly" && item.RecurrenceMode == "monthly_nth" && !string.IsNullOrWhiteSpace(item.RecurrenceDays)) rule += ";BYDAY=" + item.RecurrenceDays;
            else if (item.RecurrenceFrequency == "monthly") rule += ";BYMONTHDAY=" + item.Start.Day;
            if (item.RecurrenceUntil <= item.Start.Date) return rule;
            return rule + ";UNTIL=" + (item.AllDay
                ? item.RecurrenceUntil.Date.ToString("yyyyMMdd")
                : item.RecurrenceUntil.Date.AddDays(1).AddSeconds(-1).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'"));
        }

        static void ApplyRemote(PlannerItem item, GoogleEvent e, GoogleCalendarSetting calendar)
        {
            item.Title = string.IsNullOrWhiteSpace(e.Summary) ? "제목 없음" : e.Summary;
            item.GoogleCalendarId = calendar.Id; item.GoogleCalendarName = calendar.Name; item.GoogleCalendarColor = calendar.Color;
            item.GoogleReadOnly = !calendar.Editable;
            item.GoogleRecurringEventId = e.RecurringEventId;
            var holiday = (calendar.Name ?? "").Contains("공휴일") || (calendar.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
            item.Category = holiday ? "국경일" : "개인일정";
            item.GoogleTaskEvent = !string.IsNullOrWhiteSpace(e.Description) && e.Description.IndexOf("https://tasks.google.com/task/", StringComparison.OrdinalIgnoreCase) >= 0;
            item.Notes = item.GoogleTaskEvent ? null : e.Description;
            item.AllDay = !string.IsNullOrWhiteSpace(e.Start.Date);
            item.Start = item.AllDay ? System.DateTime.ParseExact(e.Start.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture) : DateTimeOffset.Parse(e.Start.DateTime, CultureInfo.InvariantCulture).LocalDateTime;
            item.End = item.AllDay ? System.DateTime.ParseExact(e.End.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture) : DateTimeOffset.Parse(e.End.DateTime, CultureInfo.InvariantCulture).LocalDateTime;
            string value; var p = e.ExtendedProperties == null ? null : e.ExtendedProperties.Private;
            item.OnharuManaged = !item.GoogleTaskEvent && p != null && p.TryGetValue("onharu", out value) && value == "1";
            item.IsTodo = item.GoogleTaskEvent || (p != null && p.TryGetValue("onharuTodo", out value) ? value == "1" : !item.AllDay);
            if (p != null && p.TryGetValue("onharuCompleted", out value)) item.Completed = value == "1";
            int reminder;
            if (p != null && p.TryGetValue("onharuReminder", out value) && int.TryParse(value, out reminder)) { item.ReminderMinutes = reminder; item.ReminderConfigured = true; }
            else if (!item.ReminderConfigured) { item.ReminderMinutes = item.AllDay ? -1 : 10; item.ReminderConfigured = true; }
            if (p != null && p.TryGetValue("onharuImportant", out value)) item.Important = value == "1";
            if (p != null && p.TryGetValue("onharuRecurrence", out value) && !string.IsNullOrWhiteSpace(value)) item.RecurrenceFrequency = value;
            if (p != null && p.TryGetValue("onharuRecurrenceMode", out value)) item.RecurrenceMode = value;
            if (p != null && p.TryGetValue("onharuRecurrenceDays", out value)) item.RecurrenceDays = value;
            item.RolloverMode = !item.GoogleTaskEvent && p != null && p.TryGetValue("onharuRolloverMode", out value) && value != "none" ? value : null;
            if (string.IsNullOrWhiteSpace(item.RolloverMode) && !item.GoogleTaskEvent && p != null && p.TryGetValue("onharuRollover", out value) && value == "1") item.RolloverMode = "next_day";
            item.AutoRollover = !string.IsNullOrWhiteSpace(item.RolloverMode);
        }

        static bool InSyncRange(DateTime value) { return value >= DateTime.Today.AddYears(-1) && value < DateTime.Today.AddYears(2); }
        static void SetAccessToken(GoogleToken token) { accessToken = token.AccessToken; expiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn)); }
        static byte[] RandomBytes(int count) { var bytes = new byte[count]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes); return bytes; }
        static string Base64Url(byte[] bytes) { return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        static string E(string value) { return Uri.EscapeDataString(value ?? ""); }
        static int FreePort() { var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop(); return port; }
        static byte[] Protect(string value) { return ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser); }
        static string Unprotect(byte[] value) { return Encoding.UTF8.GetString(ProtectedData.Unprotect(value, null, DataProtectionScope.CurrentUser)); }
        static void SaveRefreshToken(string value) { Directory.CreateDirectory(Path.GetDirectoryName(TokenPath)); File.WriteAllBytes(TokenPath, Protect(value)); }
        static string LoadRefreshToken() { return Unprotect(File.ReadAllBytes(TokenPath)); }
        static DataContractJsonSerializer Serializer(Type type) { return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true }); }
        static T Read<T>(string json) { using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) return (T)Serializer(typeof(T)).ReadObject(stream); }
        static string Write<T>(T value) { using (var stream = new MemoryStream()) { Serializer(typeof(T)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); } }
    }

    public class AddItemWindow : Window
    {
        readonly TextBox title = new TextBox { Margin = new Thickness(0, 6, 0, 4), Height = 46,
            FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) };
        readonly TextBlock validationMessage = new TextBlock { Text = "제목을 입력해 주세요.", Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            FontSize = 12, FontWeight = FontWeights.SemiBold, Height = 18, Margin = new Thickness(4, 0, 0, 6), Visibility = Visibility.Collapsed };
        DateTime selectedDate;
        readonly RadioButton allDay = new RadioButton { Content = "하루 종일", GroupName = "TimeMode", IsChecked = true, Margin = new Thickness(0, 0, 18, 0) };
        readonly RadioButton morning = new RadioButton { Content = "오전", GroupName = "TimeMode", Margin = new Thickness(0, 0, 18, 0) };
        readonly RadioButton afternoon = new RadioButton { Content = "오후", GroupName = "TimeMode" };
        readonly UniformGrid hourGrid = new UniformGrid { Columns = 6, Margin = new Thickness(0, 8, 0, 12), IsEnabled = false };
        readonly UniformGrid minuteGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 5, 0, 10), IsEnabled = false };
        readonly RadioButton noRollover = new RadioButton { Content = "이월 안 함", GroupName = "Rollover", IsChecked = true, Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
        readonly RadioButton nextDayRollover = new RadioButton { Content = "다음 날", Tag = "next_day", GroupName = "Rollover", Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
        readonly RadioButton nextWeekRollover = new RadioButton { Content = "다음 주 같은 요일", Tag = "next_week", GroupName = "Rollover", Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
        readonly RadioButton nextWeekdayRollover = new RadioButton { Content = "다음 평일", Tag = "next_weekday", GroupName = "Rollover", FontSize = 12 };
        readonly WrapPanel rolloverOptions = new WrapPanel { Margin = new Thickness(0, 5, 0, 8), IsEnabled = false };
        readonly StackPanel categories = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };
        readonly List<RadioButton> categoryOptions = new List<RadioButton>();
        readonly WrapPanel reminderOptions = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
        readonly CheckBox important = new CheckBox { Content = "★ 중요 일정", Foreground = new SolidColorBrush(Color.FromRgb(242, 13, 122)), VerticalAlignment = VerticalAlignment.Center };
        readonly WrapPanel recurrenceOptions = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
        readonly Border recurrenceAdvancedCard = new Border { Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 8, 10, 4), Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
        readonly StackPanel recurrenceAdvanced = new StackPanel();
        readonly RadioButton dailyEvery = new RadioButton { Content = "매일", GroupName = "DailyMode", IsChecked = true, Margin = new Thickness(0, 0, 18, 4) };
        readonly RadioButton dailyWeekdays = new RadioButton { Content = "평일만 · 월~금", GroupName = "DailyMode", Margin = new Thickness(0, 0, 0, 4) };
        readonly List<CheckBox> weeklyDays = new List<CheckBox>();
        readonly RadioButton monthlyDate = new RadioButton { GroupName = "MonthlyMode", IsChecked = true, Margin = new Thickness(0, 0, 14, 4) };
        readonly RadioButton monthlyNth = new RadioButton { GroupName = "MonthlyMode", Margin = new Thickness(0, 0, 14, 4) };
        readonly RadioButton monthlyLast = new RadioButton { Content = "매월 마지막 날", GroupName = "MonthlyMode", Margin = new Thickness(0, 0, 0, 4) };
        readonly Button recurrenceUntilButton = new Button { Height = 34, IsEnabled = false, Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
        DateTime recurrenceUntilDate;
        readonly List<GoogleCalendarSetting> googleSources;
        readonly TextBox notes = new TextBox { Margin = new Thickness(0, 4, 0, 14), Height = 72,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) };
        readonly PlannerItem editingItem;
        public PlannerItem Result;
        public bool DeleteRequested;
        public bool ApplyToSeries;
        readonly CheckBox editSingleOccurrence = new CheckBox { Content = "이번 일정만 변경", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        bool editingSeries;

        public AddItemWindow(DateTime selected, PlannerItem existing = null, List<GoogleCalendarSetting> sources = null, bool googleConnected = true)
        {
            editingItem = existing;
            googleSources = sources ?? new List<GoogleCalendarSetting>();
            selectedDate = selected.Date;
            editingSeries = existing != null && (!string.IsNullOrWhiteSpace(existing.SeriesId) || !string.IsNullOrWhiteSpace(existing.GoogleRecurringEventId) || !string.IsNullOrWhiteSpace(existing.RecurrenceFrequency));
            recurrenceUntilDate = selectedDate.AddYears(1);
            Title = existing == null ? "새 일정" : "일정 수정";
            Width = 460; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.GetPosition(this).Y < 70 && !HasButtonParent(e.OriginalSource as DependencyObject) && Mouse.LeftButton == MouseButtonState.Pressed)
                    DragMove();
            };
            for (var h = 1; h <= 12; h++)
                hourGrid.Children.Add(new RadioButton { Content = h + "시", Tag = h, GroupName = "Hour", IsChecked = h == 9, Margin = new Thickness(2, 4, 2, 4) });
            foreach (var minute in new[] { 0, 15, 30, 45 })
                minuteGrid.Children.Add(new RadioButton { Content = minute + "분", Tag = minute, GroupName = "Minute",
                    IsChecked = minute == 0, Margin = new Thickness(2, 4, 2, 4) });
            allDay.Checked += delegate { hourGrid.IsEnabled = false; minuteGrid.IsEnabled = false; rolloverOptions.IsEnabled = false; };
            morning.Checked += delegate { hourGrid.IsEnabled = true; minuteGrid.IsEnabled = true; rolloverOptions.IsEnabled = true; };
            afternoon.Checked += delegate { hourGrid.IsEnabled = true; minuteGrid.IsEnabled = true; rolloverOptions.IsEnabled = true; };
            categories.Children.Add(new TextBlock { Text = "온하루 · 로컬 전용", Foreground = Brush("#64748B"), FontSize = 11, Margin = new Thickness(0, 0, 0, 5) });
            var localChoices = new WrapPanel();
            AddCategoryChoice(localChoices, "업무일정", "local:business", true, true);
            AddCategoryChoice(localChoices, "개인일정", "local:personal", false, true); categories.Children.Add(localChoices);
            if (googleSources.Count > 0)
            {
                categories.Children.Add(new TextBlock { Text = "Google · 동기화", Foreground = Brush("#64748B"), FontSize = 11, Margin = new Thickness(0, 9, 0, 5) });
                var googleChoices = new WrapPanel();
                foreach (var source in OrderedSources(googleSources))
                    AddCategoryChoice(googleChoices, (source.Primary ? "내 캘린더 · " : "") + source.Name,
                        "google:" + source.Id, false, source.Editable);
                categories.Children.Add(googleChoices);
            }

            StyleInput(title); StyleInput(notes);
            var panel = new StackPanel { Margin = new Thickness(26, 12, 18, 20) };
            var header = new DockPanel { Margin = new Thickness(26, 14, 12, 12) };
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"),
                Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            Round(close, 10);
            close.Click += delegate { DialogResult = false; };
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var headerTitle = new StackPanel { Orientation = Orientation.Horizontal };
            headerTitle.Children.Add(new TextBlock { Text = existing == null ? "✦  새 일정" : "✎  일정 수정", FontSize = 22, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center });
            if (existing != null)
                headerTitle.Children.Add(new Border { Background = string.IsNullOrWhiteSpace(existing.GoogleCalendarId) ? Brush("#EEF2FF") : Brush("#F0FDF4"),
                    CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(12, 2, 0, 0),
                    Child = new TextBlock { Text = string.IsNullOrWhiteSpace(existing.GoogleCalendarId) ? "온하루 등록" : "Google Calendar",
                        Foreground = string.IsNullOrWhiteSpace(existing.GoogleCalendarId) ? Brush("#4338CA") : Brush("#15803D"), FontSize = 11, FontWeight = FontWeights.SemiBold } });
            header.Children.Add(headerTitle);
            var dateCard = new Border { Background = Brush("#EFF6FF"), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 18) };
            Popup datePopup = null;
            System.Windows.Controls.Calendar inlineCalendar = null;
            TextBlock editableDateText = null;
            Button changeDateButton = null;
            var pendingDate = selectedDate;
            if (existing == null)
                dateCard.Child = new TextBlock { Text = "날짜  ·  " + selectedDate.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")),
                    Foreground = Brush("#1D4ED8"), FontWeight = FontWeights.SemiBold, FontSize = 14 };
            else
            {
                var dateRow = new Grid(); dateRow.ColumnDefinitions.Add(new ColumnDefinition());
                dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
                editableDateText = new TextBlock { Text = selectedDate.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")),
                    Foreground = Brush("#1D4ED8"), FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
                dateRow.Children.Add(editableDateText);
                changeDateButton = new Button { Content = "📅 날짜 변경", Height = 34, Background = Brushes.White,
                    Foreground = Brush("#2563EB"), BorderBrush = Brush("#BFDBFE"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                Round(changeDateButton, 9);
                inlineCalendar = new System.Windows.Controls.Calendar { SelectedDate = selectedDate, DisplayDate = selectedDate,
                    SelectionMode = CalendarSelectionMode.SingleDate, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center,
                    LayoutTransform = new ScaleTransform(1.20, 1.20) };
                StyleCalendar(inlineCalendar);
                datePopup = new Popup { PlacementTarget = changeDateButton, Placement = PlacementMode.Bottom,
                    AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.Fade,
                    VerticalOffset = 6 };
                datePopup.Child = inlineCalendar;
                inlineCalendar.SelectedDatesChanged += delegate
                { if (inlineCalendar.SelectedDate.HasValue) pendingDate = inlineCalendar.SelectedDate.Value.Date; };
                inlineCalendar.PreviewMouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
                {
                    if (HasDayButtonParent(e.OriginalSource as DependencyObject))
                    { selectedDate = pendingDate; editableDateText.Text = FormatDate(selectedDate); UpdateRecurrenceOptions(); datePopup.IsOpen = false; e.Handled = true; }
                };
                changeDateButton.Click += delegate
                {
                    if (!datePopup.IsOpen)
                    {
                        inlineCalendar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        datePopup.HorizontalOffset = changeDateButton.ActualWidth - inlineCalendar.DesiredSize.Width + 120;
                    }
                    datePopup.IsOpen = !datePopup.IsOpen;
                };
                datePopup.Closed += delegate
                { selectedDate = pendingDate; editableDateText.Text = FormatDate(selectedDate); UpdateRecurrenceOptions(); };
                Grid.SetColumn(changeDateButton, 1); dateRow.Children.Add(changeDateButton); dateCard.Child = dateRow;
            }
            panel.Children.Add(dateCard);
            var titleLabelRow = new Grid(); titleLabelRow.ColumnDefinitions.Add(new ColumnDefinition()); titleLabelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleLabelRow.Children.Add(Label("제목")); Grid.SetColumn(important, 1); titleLabelRow.Children.Add(important);
            panel.Children.Add(titleLabelRow); panel.Children.Add(title); panel.Children.Add(validationMessage);
            var timeCardContent = new StackPanel();
            timeCardContent.Children.Add(new TextBlock { Text = "시간을 지정하면 완료 체크 항목으로 등록됩니다.", FontSize = 11,
                Foreground = Brush("#64748B"), Margin = new Thickness(0, 0, 0, 8) });
            var timeModes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 2) };
            timeModes.Children.Add(allDay); timeModes.Children.Add(morning); timeModes.Children.Add(afternoon);
            timeCardContent.Children.Add(timeModes); timeCardContent.Children.Add(hourGrid);
            timeCardContent.Children.Add(new TextBlock { Text = "분", FontSize = 11, Foreground = Brush("#64748B") });
            timeCardContent.Children.Add(minuteGrid);
            rolloverOptions.Children.Add(noRollover); rolloverOptions.Children.Add(nextDayRollover);
            rolloverOptions.Children.Add(nextWeekRollover); rolloverOptions.Children.Add(nextWeekdayRollover);
            timeCardContent.Children.Add(new TextBlock { Text = "자동 이월", FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(0, 2, 0, 0) });
            timeCardContent.Children.Add(rolloverOptions);
            panel.Children.Add(new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12, 14, 6),
                Margin = new Thickness(0, 2, 0, 12), Child = timeCardContent });
            var categoryContent = new StackPanel();
            categoryContent.Children.Add(categories);
            panel.Children.Add(new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 7, 14, 2),
                Margin = new Thickness(0, 0, 0, 12), Child = categoryContent });
            panel.Children.Add(new TextBlock { Text = "알림", FontWeight = FontWeights.SemiBold, FontSize = 13 });
            foreach (var option in new[] { new { Name = "없음", Value = -1 }, new { Name = "정시", Value = 0 }, new { Name = "10분 전", Value = 10 }, new { Name = "30분 전", Value = 30 }, new { Name = "하루 전", Value = 1440 } })
                reminderOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Value, GroupName = "Reminder",
                    IsChecked = option.Value == -1, Margin = new Thickness(0, 0, 16, 5) });
            panel.Children.Add(reminderOptions);
            var recurrenceLine = new StackPanel { Margin = new Thickness(0, 1, 0, 10) };
            var recurrenceHeader = new Grid(); recurrenceHeader.ColumnDefinitions.Add(new ColumnDefinition()); recurrenceHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            recurrenceHeader.Children.Add(new TextBlock { Text = "반복", FontWeight = FontWeights.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            var recurrenceRight = new StackPanel { Orientation = Orientation.Horizontal };
            recurrenceRight.Children.Add(new TextBlock { Text = "종료일", Foreground = Brush("#64748B"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            recurrenceUntilButton.Width = 104; recurrenceUntilButton.Height = 30; recurrenceUntilButton.Content = recurrenceUntilDate.ToString("yyyy.MM.dd"); Round(recurrenceUntilButton, 9); recurrenceRight.Children.Add(recurrenceUntilButton);
            Grid.SetColumn(recurrenceRight, 1); recurrenceHeader.Children.Add(recurrenceRight); recurrenceLine.Children.Add(recurrenceHeader);
            foreach (var option in new[] { new { Name = "없음", Value = "" }, new { Name = "매일", Value = "daily" }, new { Name = "매주", Value = "weekly" }, new { Name = "매월", Value = "monthly" }, new { Name = "매년", Value = "yearly" } })
            {
                var radio = new RadioButton { Content = option.Name, Tag = option.Value, GroupName = "Recurrence", IsChecked = option.Value == "", Margin = new Thickness(0, 0, 9, 5), FontSize = 12 };
                radio.Checked += delegate { recurrenceUntilButton.IsEnabled = !string.IsNullOrWhiteSpace(radio.Tag.ToString()); UpdateRecurrenceOptions(); }; recurrenceOptions.Children.Add(radio);
            }
            if (editingSeries) { editSingleOccurrence.Margin = new Thickness(8, 0, 0, 5); recurrenceOptions.Children.Add(editSingleOccurrence); }
            recurrenceLine.Children.Add(recurrenceOptions); recurrenceAdvancedCard.Child = recurrenceAdvanced; recurrenceLine.Children.Add(recurrenceAdvancedCard); panel.Children.Add(recurrenceLine);
            var recurrenceCalendar = new System.Windows.Controls.Calendar { SelectedDate = recurrenceUntilDate, DisplayDate = recurrenceUntilDate,
                SelectionMode = CalendarSelectionMode.SingleDate, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center,
                LayoutTransform = new ScaleTransform(1.20, 1.20) };
            StyleCalendar(recurrenceCalendar);
            var recurrencePopup = new Popup { PlacementTarget = recurrenceUntilButton, Placement = PlacementMode.Bottom,
                AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.Fade, VerticalOffset = 6, Child = recurrenceCalendar };
            recurrenceCalendar.SelectedDatesChanged += delegate
            {
                if (!recurrenceCalendar.SelectedDate.HasValue) return;
                recurrenceUntilDate = recurrenceCalendar.SelectedDate.Value.Date;
                recurrenceUntilButton.Content = recurrenceUntilDate.ToString("yyyy.MM.dd");
            };
            recurrenceCalendar.PreviewMouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
            { if (HasDayButtonParent(e.OriginalSource as DependencyObject)) { recurrencePopup.IsOpen = false; e.Handled = true; } };
            recurrenceUntilButton.Click += delegate
            {
                if (!recurrencePopup.IsOpen)
                {
                    recurrenceCalendar.DisplayDate = recurrenceUntilDate; recurrenceCalendar.SelectedDate = recurrenceUntilDate;
                    recurrenceCalendar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    recurrencePopup.HorizontalOffset = recurrenceUntilButton.ActualWidth - recurrenceCalendar.DesiredSize.Width + 120;
                }
                recurrencePopup.IsOpen = !recurrencePopup.IsOpen;
            };
            panel.Children.Add(Label("메모")); panel.Children.Add(notes);
            if (existing == null && !googleConnected)
                panel.Children.Add(new TextBlock { Text = "Google 로그아웃 상태입니다. 이 일정은 이 PC에만 저장됩니다.",
                    Foreground = Brush("#DC2626"), FontSize = 12, FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center, Margin = new Thickness(0, -5, 0, 9) });
            var saveGradient = new LinearGradientBrush(); saveGradient.StartPoint = new Point(0, .5); saveGradient.EndPoint = new Point(1, .5);
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3977E8"), 0));
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C5CE5"), 1));
            var save = new Button { Content = existing == null ? "✓  일정 저장" : "✓  수정 저장", Height = 44, Background = saveGradient,
                Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, FontSize = 14 };
            Round(save, 13);
            save.Click += Save;
            if (existing == null) panel.Children.Add(save);
            else
            {
                var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) }); footer.ColumnDefinitions.Add(new ColumnDefinition());
                var delete = new Button { Content = "삭제", Height = 44, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"),
                    BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) };
                Round(delete, 13); delete.Click += delegate { DeleteRequested = true; DialogResult = true; }; footer.Children.Add(delete);
                Grid.SetColumn(save, 1); footer.Children.Add(save); panel.Children.Add(footer);
            }
            var contentScroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Min(930, Math.Max(340, SystemParameters.WorkArea.Height - 104)) };
            var popupLayout = new Grid(); popupLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); popupLayout.RowDefinitions.Add(new RowDefinition());
            popupLayout.Children.Add(header); Grid.SetRow(contentScroll, 1); popupLayout.Children.Add(contentScroll);
            Loaded += delegate
            {
                contentScroll.MaxHeight = Math.Min(930, Math.Max(340, Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea.Height - 104));
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { UiRound.SoftenScrollBars(contentScroll); }));
            };
            var shell = new Border { Background = Brush("#FFF8FAFC"), CornerRadius = new CornerRadius(18),
                BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Child = popupLayout };
            Content = shell;
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter && !notes.IsKeyboardFocusWithin)
                { Save(sender, e); e.Handled = true; }
            };
            if (existing != null) LoadExisting(existing);
            Loaded += delegate { title.Focus(); };
        }

        void UpdateRecurrenceOptions()
        {
            var selected = recurrenceOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true);
            var frequency = selected == null ? "" : selected.Tag.ToString();
            recurrenceAdvanced.Children.Clear(); recurrenceAdvancedCard.Visibility = string.IsNullOrWhiteSpace(frequency) ? Visibility.Collapsed : Visibility.Visible;
            if (frequency == "daily")
            {
                Detach(dailyEvery); Detach(dailyWeekdays);
                var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(dailyEvery); row.Children.Add(dailyWeekdays); recurrenceAdvanced.Children.Add(row);
            }
            else if (frequency == "weekly")
            {
                if (weeklyDays.Count == 0)
                    foreach (var day in new[] { Tuple.Create("월", "MO", DayOfWeek.Monday), Tuple.Create("화", "TU", DayOfWeek.Tuesday), Tuple.Create("수", "WE", DayOfWeek.Wednesday), Tuple.Create("목", "TH", DayOfWeek.Thursday), Tuple.Create("금", "FR", DayOfWeek.Friday), Tuple.Create("토", "SA", DayOfWeek.Saturday), Tuple.Create("일", "SU", DayOfWeek.Sunday) })
                        weeklyDays.Add(new CheckBox { Content = day.Item1, Tag = day.Item2, IsChecked = day.Item3 == selectedDate.DayOfWeek, Margin = new Thickness(0, 0, 13, 4) });
                var row = new WrapPanel(); foreach (var day in weeklyDays) { Detach(day); row.Children.Add(day); } recurrenceAdvanced.Children.Add(row);
            }
            else if (frequency == "monthly")
            {
                monthlyDate.Content = "매월 " + selectedDate.Day + "일"; monthlyNth.Content = "매월 " + MonthlyPositionText(selectedDate);
                Detach(monthlyDate); Detach(monthlyNth); Detach(monthlyLast);
                var row = new WrapPanel(); row.Children.Add(monthlyDate); row.Children.Add(monthlyNth); row.Children.Add(monthlyLast); recurrenceAdvanced.Children.Add(row);
            }
            else if (frequency == "yearly")
                recurrenceAdvanced.Children.Add(new TextBlock { Text = "매년 " + selectedDate.Month + "월 " + selectedDate.Day + "일", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(2, 0, 0, 4) });
        }

        static void Detach(UIElement element)
        {
            var parent = VisualTreeHelper.GetParent(element) as Panel;
            if (parent != null) parent.Children.Remove(element);
        }

        static string DayCode(DayOfWeek day)
        {
            return day == DayOfWeek.Monday ? "MO" : day == DayOfWeek.Tuesday ? "TU" : day == DayOfWeek.Wednesday ? "WE" :
                day == DayOfWeek.Thursday ? "TH" : day == DayOfWeek.Friday ? "FR" : day == DayOfWeek.Saturday ? "SA" : "SU";
        }

        static string MonthlyNthCode(DateTime date)
        {
            var ordinal = date.Day + 7 > DateTime.DaysInMonth(date.Year, date.Month) ? -1 : (date.Day - 1) / 7 + 1;
            return ordinal.ToString(CultureInfo.InvariantCulture) + DayCode(date.DayOfWeek);
        }

        static string MonthlyPositionText(DateTime date)
        {
            var ordinal = date.Day + 7 > DateTime.DaysInMonth(date.Year, date.Month) ? "마지막" : new[] { "첫째", "둘째", "셋째", "넷째", "다섯째" }[(date.Day - 1) / 7];
            var day = new[] { "일", "월", "화", "수", "목", "금", "토" }[(int)date.DayOfWeek];
            return ordinal + " " + day + "요일";
        }

        void Save(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(title.Text))
            { ShowValidation(); return; }
            var start = selectedDate;
            if (allDay.IsChecked != true)
            {
                var hour = (int)hourGrid.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                if (afternoon.IsChecked == true && hour < 12) hour += 12;
                if (morning.IsChecked == true && hour == 12) hour = 0;
                var minute = (int)minuteGrid.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                start = start.AddHours(hour).AddMinutes(minute);
            }
            var selectedOption = categoryOptions.First(x => x.IsChecked == true);
            var target = selectedOption.Tag.ToString();
            var selectedSource = target.StartsWith("google:") ? googleSources.FirstOrDefault(x => "google:" + x.Id == target) : null;
            var selectedCategory = target == "local:business" ? "업무일정" : "개인일정";
            var recurrenceFrequency = recurrenceOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
            var recurrenceMode = recurrenceFrequency == "daily" ? (dailyWeekdays.IsChecked == true ? "weekdays" : "daily") :
                recurrenceFrequency == "monthly" ? (monthlyLast.IsChecked == true ? "monthly_last" : monthlyNth.IsChecked == true ? "monthly_nth" : "monthly_date") : recurrenceFrequency;
            var recurrenceDays = recurrenceFrequency == "weekly" ? string.Join(",", weeklyDays.Where(x => x.IsChecked == true).Select(x => x.Tag.ToString())) :
                recurrenceMode == "monthly_nth" ? MonthlyNthCode(selectedDate) : null;
            if (recurrenceFrequency == "weekly" && string.IsNullOrWhiteSpace(recurrenceDays)) recurrenceDays = DayCode(selectedDate.DayOfWeek);
            Result = new PlannerItem { Id = editingItem == null ? Guid.NewGuid().ToString() : editingItem.Id, Title = title.Text.Trim(), Start = start,
                End = allDay.IsChecked == true ? start.AddDays(1) : start.AddMinutes(30),
                AllDay = allDay.IsChecked == true, IsTodo = allDay.IsChecked != true,
                Category = selectedCategory, Notes = notes.Text.Trim(),
                GoogleEventId = editingItem == null ? null : editingItem.GoogleEventId,
                OnharuManaged = editingItem != null && editingItem.OnharuManaged,
                GoogleTaskEvent = editingItem != null && editingItem.GoogleTaskEvent,
                CreatedInOnharu = editingItem == null || editingItem.CreatedInOnharu,
                Completed = editingItem != null && editingItem.Completed,
                GoogleCalendarId = editingItem == null ? null : editingItem.GoogleCalendarId,
                GoogleCalendarName = editingItem == null ? null : editingItem.GoogleCalendarName,
                GoogleCalendarColor = editingItem == null ? null : editingItem.GoogleCalendarColor,
                GoogleReadOnly = editingItem != null && editingItem.GoogleReadOnly,
                RolloverMode = allDay.IsChecked == true ? null : SelectedRolloverMode(),
                AutoRollover = allDay.IsChecked != true && noRollover.IsChecked != true,
                Important = important.IsChecked == true,
                ReminderMinutes = (int)reminderOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag,
                ReminderConfigured = true,
                RecurrenceFrequency = recurrenceFrequency, RecurrenceMode = recurrenceMode, RecurrenceDays = recurrenceDays,
                RecurrenceUntil = recurrenceUntilDate,
                SeriesId = editingItem == null ? null : editingItem.SeriesId,
                GoogleRecurringEventId = editingItem == null ? null : editingItem.GoogleRecurringEventId,
                PendingGoogleSync = editingItem != null && editingItem.PendingGoogleSync };
            if (selectedSource != null)
            {
                Result.GoogleCalendarId = selectedSource.Id; Result.GoogleCalendarName = selectedSource.Name;
                Result.GoogleCalendarColor = selectedSource.Color; Result.GoogleReadOnly = !selectedSource.Editable;
                if (editingItem != null && editingItem.GoogleCalendarId != selectedSource.Id) Result.GoogleEventId = null;
            }
            else if (editingItem == null || !target.StartsWith("google:"))
            {
                Result.GoogleCalendarId = null; Result.GoogleCalendarName = null; Result.GoogleCalendarColor = null;
                Result.GoogleReadOnly = false; Result.GoogleEventId = null; Result.OnharuManaged = false;
            }
            ApplyToSeries = editingSeries && editSingleOccurrence.IsChecked != true;
            DialogResult = true;
        }

        void LoadExisting(PlannerItem item)
        {
            title.Text = item.Title; notes.Text = item.Notes ?? "";
            important.IsChecked = item.Important;
            recurrenceUntilDate = item.RecurrenceUntil.Year >= 1900 ? item.RecurrenceUntil : item.Start.Date.AddYears(1);
            recurrenceUntilButton.Content = recurrenceUntilDate.ToString("yyyy.MM.dd");
            foreach (var radio in recurrenceOptions.Children.OfType<RadioButton>()) radio.IsChecked = radio.Tag.ToString() == (item.RecurrenceFrequency ?? "");
            dailyWeekdays.IsChecked = item.RecurrenceMode == "weekdays"; dailyEvery.IsChecked = item.RecurrenceMode != "weekdays";
            if (item.RecurrenceFrequency == "weekly")
            {
                UpdateRecurrenceOptions(); var selectedDays = (item.RecurrenceDays ?? DayCode(item.Start.DayOfWeek)).Split(',');
                foreach (var day in weeklyDays) day.IsChecked = selectedDays.Contains(day.Tag.ToString());
            }
            monthlyLast.IsChecked = item.RecurrenceMode == "monthly_last"; monthlyNth.IsChecked = item.RecurrenceMode == "monthly_nth";
            monthlyDate.IsChecked = item.RecurrenceMode != "monthly_last" && item.RecurrenceMode != "monthly_nth";
            UpdateRecurrenceOptions();
            var reminder = item.ReminderConfigured ? item.ReminderMinutes : -1;
            foreach (var radio in reminderOptions.Children.OfType<RadioButton>()) radio.IsChecked = (int)radio.Tag == reminder;
            var mode = string.IsNullOrWhiteSpace(item.RolloverMode) && item.AutoRollover ? "next_day" : item.RolloverMode;
            noRollover.IsChecked = string.IsNullOrWhiteSpace(mode); nextDayRollover.IsChecked = mode == "next_day";
            nextWeekRollover.IsChecked = mode == "next_week"; nextWeekdayRollover.IsChecked = mode == "next_weekday";
            var target = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "google:" + item.GoogleCalendarId :
                item.Category == "업무일정" ? "local:business" : "local:personal";
            foreach (var radio in categoryOptions) radio.IsChecked = radio.Tag.ToString() == target;
            if (item.AllDay) { allDay.IsChecked = true; return; }
            var hour = item.Start.Hour; afternoon.IsChecked = hour >= 12; morning.IsChecked = hour < 12;
            var displayHour = hour % 12; if (displayHour == 0) displayHour = 12;
            foreach (var radio in hourGrid.Children.OfType<RadioButton>()) radio.IsChecked = (int)radio.Tag == displayHour;
            var minute = new[] { 0, 15, 30, 45 }.OrderBy(x => Math.Abs(x - item.Start.Minute)).First();
            foreach (var radio in minuteGrid.Children.OfType<RadioButton>()) radio.IsChecked = (int)radio.Tag == minute;
        }

        void AddCategoryChoice(Panel panel, string text, string tag, bool selected, bool enabled)
        {
            var radio = new RadioButton { Content = text + (enabled ? "" : " · 읽기 전용"), Tag = tag, GroupName = "CategoryTarget",
                IsChecked = selected, IsEnabled = enabled, Margin = new Thickness(0, 0, 16, 5) };
            categoryOptions.Add(radio); panel.Children.Add(radio);
        }

        static IEnumerable<GoogleCalendarSetting> OrderedSources(IEnumerable<GoogleCalendarSetting> sources)
        {
            return sources.OrderBy(x => IsHolidaySource(x) ? 2 : x.Primary ? 0 : 1).ThenBy(x => x.Name);
        }

        static bool IsHolidaySource(GoogleCalendarSetting source)
        {
            return (source.Name ?? "").Contains("휴일") || (source.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        async void ShowValidation()
        {
            validationMessage.Visibility = Visibility.Visible; title.Focus();
            await Task.Delay(2000);
            validationMessage.Visibility = Visibility.Collapsed;
        }

        string SelectedRolloverMode()
        {
            var selected = rolloverOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true && x.Tag != null);
            return selected == null ? null : selected.Tag.ToString();
        }

        static TextBlock Header(string text, double size) { return new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 18) }; }
        static TextBlock Label(string text) { return new TextBlock { Text = text, Foreground = Brush("#475569"), FontSize = 12 }; }
        static void Round(Button button, double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }
        static void StyleInput(TextBox input)
        {
            input.Background = Brushes.White; input.BorderBrush = Brush("#CBD5E1"); input.BorderThickness = new Thickness(1);
            input.Padding = new Thickness(10, 4, 10, 8);
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TextBox.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(TextBox.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(TextBox.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(TextBox.PaddingProperty));
            var host = new FrameworkElementFactory(typeof(ScrollViewer)); host.Name = "PART_ContentHost"; border.AppendChild(host);
            input.Template = new ControlTemplate(typeof(TextBox)) { VisualTree = border };
        }
        static bool HasButtonParent(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        static bool HasDayButtonParent(DependencyObject source)
        {
            while (source != null)
            {
                if (source is CalendarDayButton) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        static string FormatDate(DateTime date)
        { return date.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")); }
        static void StyleCalendar(System.Windows.Controls.Calendar calendar)
        {
            calendar.Background = Brush("#FFF8F2"); calendar.BorderBrush = Brushes.Transparent;
            calendar.BorderThickness = new Thickness(0); calendar.Foreground = Brush("#6D3B47");

            var dayTemplate = new ControlTemplate(typeof(CalendarDayButton));
            var dayBorder = new FrameworkElementFactory(typeof(Border)); dayBorder.Name = "DayBorder";
            dayBorder.SetValue(Border.BackgroundProperty, Brush("#FFFEFC"));
            dayBorder.SetValue(Border.BorderBrushProperty, Brush("#F3D4C7"));
            dayBorder.SetValue(Border.BorderThicknessProperty, new Thickness(.6));
            dayBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            var dayContent = new FrameworkElementFactory(typeof(ContentPresenter));
            dayContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            dayContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            dayBorder.AppendChild(dayContent); dayTemplate.VisualTree = dayBorder;
            var today = new Trigger { Property = CalendarDayButton.IsTodayProperty, Value = true };
            today.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#DDF7F0"), "DayBorder"));
            today.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("#34B89A"), "DayBorder")); dayTemplate.Triggers.Add(today);
            var selected = new Trigger { Property = CalendarDayButton.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#E56B6F"), "DayBorder"));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); dayTemplate.Triggers.Add(selected);
            var inactive = new Trigger { Property = CalendarDayButton.IsInactiveProperty, Value = true };
            inactive.Setters.Add(new Setter(Control.OpacityProperty, .38)); dayTemplate.Triggers.Add(inactive);
            var dayStyle = new Style(typeof(CalendarDayButton)); dayStyle.Setters.Add(new Setter(Control.TemplateProperty, dayTemplate));
            dayStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(1.5)));
            dayStyle.Setters.Add(new Setter(Control.MinWidthProperty, 29.0)); dayStyle.Setters.Add(new Setter(Control.MinHeightProperty, 27.0));
            dayStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0)); dayStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#5B3540")));
            calendar.CalendarDayButtonStyle = dayStyle;

            var monthTemplate = new ControlTemplate(typeof(CalendarButton));
            var monthBorder = new FrameworkElementFactory(typeof(Border)); monthBorder.SetValue(Border.BackgroundProperty, Brush("#FDE8E3"));
            monthBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); monthBorder.SetValue(Border.MarginProperty, new Thickness(2));
            var monthContent = new FrameworkElementFactory(typeof(ContentPresenter));
            monthContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            monthContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); monthBorder.AppendChild(monthContent);
            monthTemplate.VisualTree = monthBorder;
            var monthStyle = new Style(typeof(CalendarButton)); monthStyle.Setters.Add(new Setter(Control.TemplateProperty, monthTemplate));
            monthStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#B4474D"))); monthStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            monthStyle.Setters.Add(new Setter(Control.FontSizeProperty, 16.0));
            calendar.CalendarButtonStyle = monthStyle;
        }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }

    public class DateSelectWindow : Window
    {
        public DateTime SelectedDate;

        public DateSelectWindow(DateTime current)
        {
            SelectedDate = current.Date; Title = "날짜 변경"; Width = 400; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 14) };
            header.Children.Add(new TextBlock { Text = "📅  날짜 선택", FontSize = 21, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center }); panel.Children.Add(header);
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
            var selectedText = new TextBlock { Text = SelectedDate.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")),
                Foreground = Brush("#1D4ED8"), FontWeight = FontWeights.Bold, FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(selectedText);
            var calendarControl = new System.Windows.Controls.Calendar { SelectedDate = SelectedDate, DisplayDate = SelectedDate,
                SelectionMode = CalendarSelectionMode.SingleDate, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center,
                LayoutTransform = new ScaleTransform(1.05, 1.05), Margin = new Thickness(0, 2, 0, 10) };
            var dayStyle = new Style(typeof(CalendarDayButton));
            dayStyle.Setters.Add(new Setter(Control.FontSizeProperty, 14.0));
            dayStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(1.5)));
            dayStyle.Setters.Add(new Setter(Control.MinWidthProperty, 28.0));
            dayStyle.Setters.Add(new Setter(Control.MinHeightProperty, 26.0));
            calendarControl.CalendarDayButtonStyle = dayStyle;
            calendarControl.SelectedDatesChanged += delegate
            {
                if (calendarControl.SelectedDate.HasValue) { SelectedDate = calendarControl.SelectedDate.Value.Date;
                    selectedText.Text = SelectedDate.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")); }
            };
            calendarControl.PreviewMouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
            {
                if (HasDayButtonParent(e.OriginalSource as DependencyObject) && calendarControl.SelectedDate.HasValue)
                { SelectedDate = calendarControl.SelectedDate.Value.Date; DialogResult = true; e.Handled = true; }
            };
            panel.Children.Add(new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(10), Child = calendarControl });
            var confirmText = new TextBlock { Text = "✓  이 날짜 선택", Cursor = Cursors.Hand };
            var confirm = new Button { Content = confirmText, Width = 180, Height = 40, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.White,
                Background = Brush("#3977E8"), BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold,
                FontSize = 13, Margin = new Thickness(0, 12, 0, 0), Cursor = Cursors.Hand, ForceCursor = true };
            Round(confirm, 13); confirm.Click += delegate { DialogResult = true; }; panel.Children.Add(confirm);
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"),
                Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            Round(close, 10); close.Click += delegate { DialogResult = false; };
            var shell = new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel };
            var frame = new Grid(); frame.Children.Add(shell); close.HorizontalAlignment = HorizontalAlignment.Right;
            close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); Panel.SetZIndex(close, 10);
            frame.Children.Add(close); Content = frame;
        }

        static void Round(Button button, double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }
        static bool HasDayButtonParent(DependencyObject source)
        {
            while (source != null)
            {
                if (source is CalendarDayButton) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }

    public class SearchWindow : Window
    {
        readonly StackPanel results = new StackPanel(); readonly TextBox query = new TextBox { Height = 38, FontSize = 14 };
        readonly ScrollViewer resultScroller;
        FrameworkElement todayAnchor;
        readonly List<PlannerItem> source; public PlannerItem SelectedItem;
        public SearchWindow(List<PlannerItem> items)
        {
            source = items; Title = "일정 검색"; Width = 520; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 10) };
            header.Children.Add(new TextBlock { Text = "⌕  일정 검색", FontSize = 21, FontWeight = FontWeights.Bold });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; panel.Children.Add(header);
            var searchRow = new Grid(); searchRow.ColumnDefinitions.Add(new ColumnDefinition());
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) }); searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            query.BorderThickness = new Thickness(0); query.Background = Brushes.Transparent; query.Padding = new Thickness(11, 6, 10, 6);
            searchRow.Children.Add(new Border { Child = query, Height = 40, Background = Brush("#F8FAFF"), BorderBrush = Brush("#C7D2FE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11) });
            var search = new Button { Content = "⌕  검색", Height = 40, Margin = new Thickness(7, 0, 0, 0), Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
            UiRound.Apply(search, 11); search.Click += delegate { Render(); }; Grid.SetColumn(search, 1); searchRow.Children.Add(search);
            var todayButton = new Button { Content = "◎ 오늘", Height = 40, Margin = new Thickness(7, 0, 0, 0), Background = Brush("#FCE7F3"), Foreground = Brush("#DB2777"), BorderBrush = Brush("#FBCFE8"), BorderThickness = new Thickness(1), FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
            UiRound.Apply(todayButton, 11); todayButton.Click += delegate { ScrollToToday(); }; Grid.SetColumn(todayButton, 2); searchRow.Children.Add(todayButton); panel.Children.Add(searchRow);
            panel.Children.Add(new TextBlock { Text = "조회는 오늘 기준 과거 1년부터 미래 1년까지 가능합니다.", Foreground = Brush("#64748B"), FontSize = 11, Margin = new Thickness(2, 8, 0, 2) });
            resultScroller = new ScrollViewer { Content = results, Height = 370, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 7, 0, 0) };
            panel.Children.Add(resultScroller);
            query.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Render(); e.Handled = true; } };
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            UiRound.Apply(close, 10);
            close.Click += delegate { DialogResult = false; };
            var frame = new Grid(); frame.Children.Add(new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel });
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
            Loaded += delegate { query.Focus(); Render(); };
        }
        void Render()
        {
            results.Children.Clear(); todayAnchor = null; var text = query.Text.Trim();
            var from = DateTime.Today.AddYears(-1); var to = DateTime.Today.AddYears(1).AddDays(1);
            var matches = source.Where(x => x.Start >= from && x.Start < to &&
                (text.Length == 0 || (x.Title ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0 || (x.Notes ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0)).OrderBy(x => x.Start).Take(500).ToList();
            if (matches.Count == 0)
            { results.Children.Add(new TextBlock { Text = "조건에 맞는 일정이 없습니다.", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 35, 0, 0) }); return; }
            var todayInserted = false;
            foreach (var item in matches)
            {
                if (!todayInserted && item.Start.Date >= DateTime.Today) { AddTodayMarker(); todayInserted = true; }
                var status = item.Start.Date == DateTime.Today ? "오늘" : item.Start >= DateTime.Today ? "예정" : "지난";
                var statusColor = status == "오늘" ? "#DB2777" : status == "예정" ? "#4F46E5" : "#64748B";
                var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.Children.Add(new TextBlock { Text = status, Foreground = Brush(statusColor), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
                var info = new StackPanel(); info.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, Foreground = Brush("#1E293B"), TextTrimming = TextTrimming.CharacterEllipsis });
                info.Children.Add(new TextBlock { Text = item.Start.ToString(item.AllDay ? "yyyy.MM.dd ddd" : "yyyy.MM.dd ddd  HH:mm", new CultureInfo("ko-KR")), FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(0, 2, 0, 0) });
                Grid.SetColumn(info, 1); row.Children.Add(info);
                var button = new Button { Content = row, Tag = item, Height = 54, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brush("#F8FAFC"), BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 3, 0, 3), Cursor = Cursors.Hand };
                UiRound.Apply(button, 11);
                button.Click += delegate { SelectedItem = (PlannerItem)button.Tag; DialogResult = true; }; results.Children.Add(button);
            }
            if (!todayInserted) AddTodayMarker();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ScrollToToday));
        }
        void ScrollToToday()
        {
            if (todayAnchor == null) return;
            resultScroller.UpdateLayout();
            var y = todayAnchor.TranslatePoint(new Point(0, 0), results).Y;
            resultScroller.ScrollToVerticalOffset(Math.Max(0, y - resultScroller.ViewportHeight / 2));
        }
        void AddTodayMarker()
        {
            todayAnchor = new Border { Height = 32, Background = Brush("#FCE7F3"), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 7, 0, 7),
                Child = new TextBlock { Text = "오늘 · " + DateTime.Today.ToString("yyyy.MM.dd dddd", new CultureInfo("ko-KR")), Foreground = Brush("#DB2777"), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            results.Children.Add(todayAnchor);
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    public class ReminderWindow : Window
    {
        bool completed;
        public ReminderWindow(List<PlannerItem> due, Action<int?> complete)
        {
            Width = 410; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true; ShowActivated = false; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var content = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
            content.Children.Add(new TextBlock { Text = "✦  온하루 알림", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brush("#4338CA") });
            foreach (var item in due) content.Children.Add(new TextBlock { Text = (item.AllDay ? "오늘" : item.Start.ToString("HH:mm")) + "  ·  " + item.Title,
                FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 9, 0, 0), TextWrapping = TextWrapping.Wrap });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            foreach (var option in new[] { new { Name = "5분 뒤", Minutes = 5 }, new { Name = "10분 뒤", Minutes = 10 }, new { Name = "30분 뒤", Minutes = 30 } })
            {
                var button = new Button { Content = option.Name, Tag = option.Minutes, Height = 32, Width = 72, Margin = new Thickness(0, 0, 6, 0), Background = Brush("#E0E7FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0) };
                UiRound.Apply(button, 9); button.Click += delegate { if (!completed) { completed = true; complete((int)button.Tag); Close(); } }; actions.Children.Add(button);
            }
            var done = new Button { Content = "확인", Height = 32, Width = 64, Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            UiRound.Apply(done, 9); done.Click += delegate { if (!completed) { completed = true; complete(null); Close(); } }; actions.Children.Add(done); content.Children.Add(actions);
            Content = new Border { Background = Brush("#F7F7FF"), BorderBrush = Brush("#C7D2FE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = content };
            Closed += delegate { if (!completed) { completed = true; complete(null); } };
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    public class LocalImportWindow : Window
    {
        readonly List<Tuple<CheckBox, PlannerItem>> choices = new List<Tuple<CheckBox, PlannerItem>>();
        public List<PlannerItem> SelectedItems = new List<PlannerItem>();

        public LocalImportWindow(List<PlannerItem> localItems)
        {
            Title = "로컬 일정 가져오기"; Width = 500; Height = 520; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            panel.Children.Add(new TextBlock { Text = "로컬 일정 가져오기", FontSize = 21, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = "현재 계정으로 옮길 일정을 선택하세요.", Foreground = Brush("#64748B"), Margin = new Thickness(0, 5, 0, 12) });
            var list = new StackPanel();
            foreach (var item in localItems.OrderBy(x => x.Start))
            {
                var check = new CheckBox { Content = item.Start.ToString("yyyy.MM.dd") + "  ·  " + item.Title,
                    Margin = new Thickness(4, 6, 4, 6), FontSize = 13 };
                choices.Add(Tuple.Create(check, item)); list.Children.Add(check);
            }
            panel.Children.Add(new ScrollViewer { Content = list, Height = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var buttons = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = new Button { Content = "취소", Height = 42, Margin = new Thickness(0, 0, 5, 0), Background = Brush("#E2E8F0"), BorderThickness = new Thickness(0) };
            var import = new Button { Content = "선택 일정 가져오기", Height = 42, Margin = new Thickness(5, 0, 0, 0), Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            cancel.Click += delegate { DialogResult = false; };
            import.Click += delegate { SelectedItems = choices.Where(x => x.Item1.IsChecked == true).Select(x => x.Item2).ToList(); if (SelectedItems.Count > 0) DialogResult = true; };
            buttons.Children.Add(cancel); Grid.SetColumn(import, 1); buttons.Children.Add(import); panel.Children.Add(buttons);
            Content = new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel };
        }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    public class BackupWindow : Window
    {
        public string SelectedPath;
        public BackupWindow(string[] files)
        {
            Title = "백업 복원"; Width = 430; Height = 500; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 4) };
            header.Children.Add(new TextBlock { Text = "↶  백업 복원", FontSize = 21, FontWeight = FontWeights.Bold });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; panel.Children.Add(header);
            panel.Children.Add(new TextBlock { Text = "복원할 날짜를 선택하세요 · 최근 30일 보관", Foreground = Brush("#64748B"), Margin = new Thickness(0, 3, 0, 12) });
            var list = new StackPanel();
            foreach (var file in files.Take(30))
            {
                var name = Path.GetFileNameWithoutExtension(file); var date = name.Substring(name.Length - 8);
                var button = new Button { Content = "↶   " + date.Substring(0, 4) + "년 " + date.Substring(4, 2) + "월 " + date.Substring(6, 2) + "일 백업", Tag = file,
                    Height = 42, Margin = new Thickness(0, 3, 0, 3), Background = Brush("#EEF2FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 0, 0, 0), Cursor = Cursors.Hand };
                UiRound.Apply(button, 10);
                button.Click += delegate { SelectedPath = button.Tag.ToString(); DialogResult = true; }; list.Children.Add(button);
            }
            panel.Children.Add(new ScrollViewer { Content = list, Height = 390, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            UiRound.Apply(close, 10);
            close.Click += delegate { DialogResult = false; };
            var frame = new Grid(); frame.Children.Add(new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel });
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    public class CategoryOrderWindow : Window
    {
        readonly StackPanel list = new StackPanel(); readonly List<Tuple<string, string>> entries;
        public List<string> Result;
        public CategoryOrderWindow(List<Tuple<string, string>> values)
        {
            entries = values; Title = "카테고리 순서"; Width = 440; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 4) };
            header.Children.Add(new TextBlock { Text = "☷  카테고리 표시 순서", FontSize = 21, FontWeight = FontWeights.Bold });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; panel.Children.Add(header);
            panel.Children.Add(new TextBlock { Text = "온하루와 Google 캘린더를 원하는 순서로 이동하세요.", Foreground = Brush("#64748B"), Margin = new Thickness(0, 3, 0, 10) });
            if (entries.Count <= 6) panel.Children.Add(list);
            else panel.Children.Add(new ScrollViewer { Content = list, Height = 288, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Render();
            var save = new Button { Content = "✓  순서 적용", Height = 42, Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 10, 0, 0) };
            UiRound.Apply(save, 12);
            save.Click += delegate { Result = entries.Select(x => x.Item1).ToList(); DialogResult = true; }; panel.Children.Add(save);
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            UiRound.Apply(close, 10);
            close.Click += delegate { DialogResult = false; };
            var frame = new Grid(); frame.Children.Add(new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel });
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
        }
        void Render()
        {
            list.Children.Clear();
            for (var i = 0; i < entries.Count; i++)
            {
                var index = i; var row = new Grid { Height = 42 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                var number = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(9), Background = Brush(entries[i].Item1.StartsWith("google:") ? "#DBEAFE" : "#FCE7F3"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = (i + 1).ToString(), Foreground = Brush(entries[i].Item1.StartsWith("google:") ? "#2563EB" : "#DB2777"), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                row.Children.Add(number);
                row.Children.Add(new TextBlock { Text = entries[i].Item2, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#312E81"), FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(2, 0, 5, 0) }); Grid.SetColumn(row.Children[row.Children.Count - 1], 1);
                var up = new Button { Content = "↑", IsEnabled = i > 0, Width = 28, Height = 28, BorderThickness = new Thickness(0), Background = Brush("#E0E7FF"), Foreground = Brush("#4338CA") };
                var down = new Button { Content = "↓", IsEnabled = i < entries.Count - 1, Width = 28, Height = 28, BorderThickness = new Thickness(0), Background = Brush("#E0E7FF"), Foreground = Brush("#4338CA") };
                UiRound.Apply(up, 9); UiRound.Apply(down, 9);
                up.Click += delegate { var value = entries[index]; entries.RemoveAt(index); entries.Insert(index - 1, value); Render(); };
                down.Click += delegate { var value = entries[index]; entries.RemoveAt(index); entries.Insert(index + 1, value); Render(); };
                Grid.SetColumn(up, 2); Grid.SetColumn(down, 3); row.Children.Add(up); row.Children.Add(down);
                list.Children.Add(new Border { Child = row, Background = Brush("#F8FAFF"), BorderBrush = Brush("#E0E7FF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(4, 0, 7, 0) });
            }
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    public class SettingsWindow : Window
    {
        readonly Dictionary<string, Slider[]> sliders = new Dictionary<string, Slider[]>();
        readonly Dictionary<string, Border> previews = new Dictionary<string, Border>();
        readonly Dictionary<string, Border> editorCards = new Dictionary<string, Border>();
        readonly Dictionary<string, TextBlock[]> values = new Dictionary<string, TextBlock[]>();
        readonly List<CheckBox> colorSelections = new List<CheckBox>();
        public string BusinessColor;
        public string PersonalColor;
        public double SelectedFontSize;
        public string OrderMode;
        public string CategoryOrderPreset;
        public List<string> CategoryOrder;
        public bool ShowWeekNumbers;
        public bool ShowLunar;
        public string WeekRule;
        public bool PastelEventStyle;
        public int AutoSyncMinutes;
        public bool ChangeGoogleAccount;
        public bool LogoutGoogleAccount;
        public bool ImportLocalItems;
        public bool RestoreBackup;
        bool selectedPastelStyle;
        readonly StackPanel fontOptions = new StackPanel { Orientation = Orientation.Horizontal };
        readonly List<Tuple<string, GoogleCalendarSetting>> sourceEditors = new List<Tuple<string, GoogleCalendarSetting>>();
        readonly Dictionary<string, CheckBox> editBoxes = new Dictionary<string, CheckBox>();

        public SettingsWindow(string business, string personal, double fontSize, string orderMode, bool showWeeks,
            string weekRule, bool pastelEventStyle, int autoSyncMinutes, List<GoogleCalendarSetting> sources, bool googleConnected, int localItemCount, bool showLunar, int backupCount, List<string> categoryOrder)
        {
            selectedPastelStyle = pastelEventStyle;
            Title = "온하루 설정"; Width = 620; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(26, 12, 18, 20) };
            var header = new DockPanel { Margin = new Thickness(26, 14, 12, 12) };
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"),
                Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            Round(close, 10);
            close.Click += delegate { DialogResult = false; };
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(new TextBlock { Text = "⚙  온하루 설정", FontSize = 21, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
            panel.Children.Add(new TextBlock { Text = "추천 색상 조합", Foreground = Brush("#475569"), FontSize = 12 });
            panel.Children.Add(new TextBlock { Text = "위 5개 · 선명한 조합     아래 4개 · 파스텔 조합     Google 기본 · 원래 색상",
                Foreground = Brush("#94A3B8"), FontSize = 10, Margin = new Thickness(0, 3, 0, 2) });
            var presets = new UniformGrid { Columns = 5, Margin = new Thickness(0, 3, 0, 14) };
            var names = new[] { "오션", "핫 핑크", "라임 블루", "선셋", "네온 베리", "로즈 밀크", "라벤더", "민트", "피치 스카이", "Google 기본" };
            var palettes = new[] {
                new[] { "#2563EB", "#DB2777", "#059669", "#D97706", "#0F766E", "#7C3AED", "#0284C7", "#C2410C", "#4F46E5", "#BE185D" },
                new[] { "#F20D7A", "#FF3D9A", "#7C3AED", "#EC4899", "#2563EB", "#E11D48", "#9333EA", "#0891B2", "#DB2777", "#EA580C" },
                new[] { "#65A30D", "#0284C7", "#7C3AED", "#EA580C", "#0891B2", "#DB2777", "#0F766E", "#4F46E5", "#CA8A04", "#C026D3" },
                new[] { "#E11D48", "#F97316", "#7C2D12", "#C026D3", "#0F766E", "#2563EB", "#CA8A04", "#9333EA", "#0891B2", "#BE123C" },
                new[] { "#FF1493", "#6D28D9", "#00A6A6", "#FF6B00", "#2563EB", "#E11D48", "#65A30D", "#C026D3", "#0891B2", "#D97706" },
                new[] { "#E8798E", "#F2A65A", "#69A6A6", "#8196D1", "#B58AC8", "#D98CA3", "#78B6A4", "#E0B36A", "#8EA8D8", "#C394B7" },
                new[] { "#A78BFA", "#F0A6CA", "#7EA6E0", "#F4A27C", "#8FCB9B", "#D7A1E5", "#79C8C3", "#E8BD73", "#9CB7E8", "#E58FAE" },
                new[] { "#64B5A6", "#8FC7B5", "#78A7C8", "#D9A66C", "#B795C9", "#E29A9A", "#8BBE87", "#D6B66D", "#89A6D5", "#C58AAF" },
                new[] { "#F4A38C", "#F7C58B", "#8EC5D6", "#B7A0D8", "#8FCB9B", "#E78DB0", "#78BFB3", "#DDA76D", "#91A9DC", "#C58FC2" } };
            var activeSources = (sources ?? new List<GoogleCalendarSetting>())
                .OrderBy(x => IsHoliday(x) ? 2 : x.Primary ? 0 : 1).ThenBy(x => x.Name).ToList();
            for (var i = 0; i < activeSources.Count; i++) sourceEditors.Add(Tuple.Create("google_" + i, activeSources[i]));
            var orderEntries = new List<Tuple<string, string>> { Tuple.Create("local:business", "업무일정"), Tuple.Create("local:personal", "개인일정") };
            orderEntries.AddRange(activeSources.Select(x => Tuple.Create("google:" + x.Id, "Google · " + x.Name)));
            var savedOrder = categoryOrder ?? new List<string>();
            orderEntries = orderEntries.OrderBy(x => { var p = savedOrder.IndexOf(x.Item1); return p < 0 ? 999 : p; }).ThenBy(x => x.Item2).ToList();
            CategoryOrder = orderEntries.Select(x => x.Item1).ToList();
            for (var i = 0; i < names.Length; i++)
            {
                var index = i; var option = new RadioButton { Content = names[i], GroupName = "Palette", Margin = new Thickness(2, 5, 8, 5) };
                option.Checked += delegate
                {
                    if (index == names.Length - 1)
                    {
                        foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                            SetHex(editor.Item1, string.IsNullOrWhiteSpace(editor.Item2.OriginalColor) ? editor.Item2.Color : editor.Item2.OriginalColor);
                        return;
                    }
                    selectedPastelStyle = index >= 5;
                    SetHex("업무일정", palettes[index][0]);
                    SetHex("개인일정", palettes[index][1]);
                    var colorIndex = 2;
                    foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                        SetHex(editor.Item1, palettes[index][colorIndex++ % palettes[index].Length]);
                };
                presets.Children.Add(option);
            }
            panel.Children.Add(presets);
            var colorGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 4) };
            colorGrid.Children.Add(ColorEditor("업무일정", business));
            colorGrid.Children.Add(ColorEditor("개인일정", personal));
            foreach (var editor in sourceEditors.Where(x => !IsHoliday(x.Item2)))
                colorGrid.Children.Add(ColorEditor(editor.Item1, string.IsNullOrWhiteSpace(editor.Item2.Color) ? "#E9799A" : editor.Item2.Color, editor.Item2.Name));
            panel.Children.Add(colorGrid);
            var swap = new Button { Content = "선택한 두 색상 교환", Height = 32, Background = Brush("#FCE7F3"),
                Foreground = Brush("#BE185D"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 2, 0, 10), Cursor = Cursors.Hand };
            Round(swap, 9);
            swap.Click += delegate
            {
                var selected = colorSelections.Where(x => x.IsChecked == true).Select(x => x.Tag.ToString()).ToList();
                if (selected.Count != 2) return;
                var first = Hex(selected[0]); SetHex(selected[0], Hex(selected[1])); SetHex(selected[1], first);
                foreach (var check in colorSelections) check.IsChecked = false;
            };
            panel.Children.Add(swap);
            foreach (var editor in sourceEditors.Where(x => IsHoliday(x.Item2))) panel.Children.Add(FixedHolidayColor(editor.Item2.Name));
            panel.Children.Add(new TextBlock { Text = "글자 크기", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
            foreach (var option in new[] { new { Name = "작게", Size = 11.0 }, new { Name = "보통", Size = 12.0 }, new { Name = "크게", Size = 14.0 } })
            {
                fontOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Size, GroupName = "FontSize",
                    IsChecked = Math.Abs(fontSize - option.Size) < .5, Margin = new Thickness(0, 0, 22, 12) });
            }
            panel.Children.Add(fontOptions);
            panel.Children.Add(new TextBlock { Text = "일정 표시 순서", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 3, 0, 7) });
            var orderRow = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            orderRow.ColumnDefinitions.Add(new ColumnDefinition()); orderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            var orderOptions = new StackPanel { Orientation = Orientation.Horizontal };
            orderOptions.Children.Add(new RadioButton { Content = "카테고리별 · 하루 종일 우선", Tag = "category", GroupName = "OrderMode",
                IsChecked = orderMode != "time", Margin = new Thickness(0, 0, 20, 0) });
            orderOptions.Children.Add(new RadioButton { Content = "전체 시간순", Tag = "time", GroupName = "OrderMode", IsChecked = orderMode == "time" });
            orderRow.Children.Add(orderOptions);
            var categoryOrderButton = new Button { Content = "☷  카테고리 순서 설정", Height = 32, Background = Brush("#E0E7FF"), Foreground = Brush("#4338CA"),
                BorderBrush = Brush("#C7D2FE"), BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand, Margin = new Thickness(8, -7, 0, 0) };
            Round(categoryOrderButton, 10);
            categoryOrderButton.Click += delegate
            {
                var ordered = CategoryOrder.Select(key => orderEntries.First(x => x.Item1 == key)).ToList();
                var window = new CategoryOrderWindow(ordered) { Owner = this };
                if (window.ShowDialog() == true) CategoryOrder = window.Result;
            };
            Grid.SetColumn(categoryOrderButton, 1); orderRow.Children.Add(categoryOrderButton); panel.Children.Add(orderRow);
            panel.Children.Add(new TextBlock { Text = "표시 옵션", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 7) });
            var displayOptions = new Grid(); displayOptions.ColumnDefinitions.Add(new ColumnDefinition()); displayOptions.ColumnDefinitions.Add(new ColumnDefinition());
            var showWeek = new CheckBox { Content = "달력 왼쪽에 주차 표시", IsChecked = showWeeks, Margin = new Thickness(0, 0, 0, 7) };
            var lunar = new CheckBox { Content = "날짜 아래에 음력 표시", IsChecked = showLunar, Margin = new Thickness(0, 0, 0, 7) };
            displayOptions.Children.Add(showWeek); Grid.SetColumn(lunar, 1); displayOptions.Children.Add(lunar); panel.Children.Add(displayOptions);
            var weekRules = new StackPanel { Orientation = Orientation.Horizontal, IsEnabled = showWeeks };
            weekRules.Children.Add(new RadioButton { Content = "ISO · 월요일 시작", Tag = "iso", GroupName = "WeekRule",
                IsChecked = weekRule != "jan1", Margin = new Thickness(18, 0, 22, 0) });
            weekRules.Children.Add(new RadioButton { Content = "일반 · 일요일 시작", Tag = "jan1", GroupName = "WeekRule", IsChecked = weekRule == "jan1" });
            showWeek.Click += delegate { weekRules.IsEnabled = showWeek.IsChecked == true; };
            weekRules.Margin = new Thickness(0, 0, 0, 14); panel.Children.Add(weekRules);
            panel.Children.Add(new TextBlock { Text = "Google 자동 동기화", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 7) });
            var syncOptions = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            foreach (var option in new[] { new { Name = "사용 안 함", Minutes = 0 }, new { Name = "5분", Minutes = 5 },
                new { Name = "15분", Minutes = 15 }, new { Name = "30분", Minutes = 30 }, new { Name = "60분", Minutes = 60 } })
                syncOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Minutes, GroupName = "AutoSync",
                    IsChecked = autoSyncMinutes == option.Minutes, Margin = new Thickness(0, 0, 22, 5) });
            panel.Children.Add(syncOptions);
            if (activeSources.Count > 0)
            {
                panel.Children.Add(new TextBlock { Text = "Google 일정 수정 권한", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(0, 0, 0, 7) });
                var permissionGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 3) };
                foreach (var editor in sourceEditors)
                {
                    var source = editor.Item2; var holiday = IsHoliday(source);
                    var canWrite = source.AccessRole == "owner" || source.AccessRole == "writer";
                    var box = new CheckBox { Content = source.Name + (holiday || !canWrite ? " · 읽기 전용" : " · 수정 가능"),
                        IsChecked = source.Editable && canWrite && !holiday, IsEnabled = canWrite && !holiday, Margin = new Thickness(0, 0, 10, 7),
                        ToolTip = source.Name + (holiday || !canWrite ? " · 읽기 전용" : " · 수정 가능") };
                    editBoxes[editor.Item1] = box; permissionGrid.Children.Add(box);
                }
                panel.Children.Add(permissionGrid);
            }
            var saveGradient = new LinearGradientBrush();
            saveGradient.StartPoint = new Point(0, .5); saveGradient.EndPoint = new Point(1, .5);
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3977E8"), 0));
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C5CE5"), 1));
            var save = new Button { Content = "✓  설정 저장", Height = 44, Background = saveGradient, Foreground = Brushes.White,
                BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 10, 0, 0), Cursor = Cursors.Hand };
            Round(save, 13);
            save.Click += delegate
            {
                BusinessColor = Hex("업무일정"); PersonalColor = Hex("개인일정");
                foreach (var editor in sourceEditors)
                {
                    editor.Item2.Color = IsHoliday(editor.Item2) ? "#CF2B36" : Hex(editor.Item1);
                    editor.Item2.Editable = editBoxes.ContainsKey(editor.Item1) && editBoxes[editor.Item1].IsChecked == true;
                }
                SelectedFontSize = (double)fontOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                OrderMode = orderOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                ShowWeekNumbers = showWeek.IsChecked == true;
                ShowLunar = lunar.IsChecked == true;
                WeekRule = weekRules.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
                PastelEventStyle = selectedPastelStyle;
                AutoSyncMinutes = (int)syncOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                DialogResult = true;
            };
            var account = new Button { Content = "Google 계정 변경", Height = 44, Background = Brush("#E2E8F0"),
                Foreground = Brush("#334155"), BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 10, 4, 0), Cursor = Cursors.Hand };
            Round(account, 13);
            account.Click += delegate { ChangeGoogleAccount = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            if (googleConnected && localItemCount > 0)
            {
                var importLocal = new Button { Content = "로컬 일정 가져오기  ·  " + localItemCount + "개", Height = 34,
                    Background = Brush("#EEF2FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 8, 0, 0), Cursor = Cursors.Hand };
                Round(importLocal, 10);
                importLocal.Click += delegate { ImportLocalItems = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
                panel.Children.Add(importLocal);
            }
            if (backupCount > 0)
            {
                var restore = new Button { Content = "↶  백업 복원  ·  최근 " + backupCount + "개", Height = 34,
                    Background = Brush("#EEF2FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 6, 0, 0), Cursor = Cursors.Hand };
                Round(restore, 10); restore.Click += delegate { RestoreBackup = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }; panel.Children.Add(restore);
            }
            var logout = new Button { Content = "로그아웃", Height = 44, Background = Brush("#F1F5F9"), Foreground = Brush("#64748B"),
                BorderThickness = new Thickness(0), Margin = new Thickness(0, 10, 4, 0), Cursor = Cursors.Hand };
            Round(logout, 13);
            logout.Click += delegate { LogoutGoogleAccount = true; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            save.Margin = new Thickness(4, 10, 0, 0);
            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.72, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.14, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.14, GridUnitType.Star) });
            actions.Children.Add(logout); Grid.SetColumn(account, 1); actions.Children.Add(account); Grid.SetColumn(save, 2); actions.Children.Add(save);
            panel.Children.Add(actions);
            var contentScroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Min(930, Math.Max(360, SystemParameters.WorkArea.Height - 104)) };
            var popupLayout = new Grid(); popupLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); popupLayout.RowDefinitions.Add(new RowDefinition());
            popupLayout.Children.Add(header); Grid.SetRow(contentScroll, 1); popupLayout.Children.Add(contentScroll);
            Loaded += delegate
            {
                contentScroll.MaxHeight = Math.Min(930, Math.Max(360, Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea.Height - 104));
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { UiRound.SoftenScrollBars(contentScroll); }));
            };
            var shell = new Border { Background = Brush("#FFF8FAFC"), CornerRadius = new CornerRadius(18),
                BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Child = popupLayout };
            Content = shell;
        }

        UIElement FixedHolidayColor(string name)
        {
            var row = new DockPanel();
            var swatch = new Border { Width = 42, Height = 24, CornerRadius = new CornerRadius(7), Background = Brush("#CF2B36") };
            DockPanel.SetDock(swatch, Dock.Right); row.Children.Add(swatch);
            row.Children.Add(new TextBlock { Text = name + " 색상 · 빨간색 고정", FontWeight = FontWeights.SemiBold,
                FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#991B1B") });
            return new Border { Background = Brush("#FEF2F2"), BorderBrush = Brush("#FECACA"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 8), Child = row };
        }

        UIElement ColorEditor(string name, string hex, string displayName = null)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            var title = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var preview = new Border { Width = 42, Height = 24, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(color) };
            previews[name] = preview; DockPanel.SetDock(preview, Dock.Right); title.Children.Add(preview);
            var select = new CheckBox { Tag = name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) };
            select.Checked += delegate { if (colorSelections.Count(x => x.IsChecked == true) > 2) select.IsChecked = false; };
            colorSelections.Add(select); DockPanel.SetDock(select, Dock.Left); title.Children.Add(select);
            title.Children.Add(new TextBlock { Text = (displayName ?? name) + " 색상", FontWeight = FontWeights.SemiBold, FontSize = 14 }); box.Children.Add(title);
            var rgb = new[] { color.R, color.G, color.B }; var set = new Slider[3]; var labels = new TextBlock[3];
            for (var i = 0; i < 3; i++)
            {
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                var channel = new TextBlock { Text = new[] { "R", "G", "B" }[i], Foreground = Brush("#64748B") }; row.Children.Add(channel);
                var slider = new Slider { Minimum = 0, Maximum = 255, Value = rgb[i], Tag = name }; Grid.SetColumn(slider, 1); row.Children.Add(slider); set[i] = slider;
                var value = new TextBlock { Text = rgb[i].ToString(), HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetColumn(value, 2); row.Children.Add(value); labels[i] = value;
                slider.ValueChanged += delegate { UpdatePreview(name); }; box.Children.Add(row);
            }
            sliders[name] = set; values[name] = labels;
            var card = new Border { Background = Pastel(color, .88), BorderBrush = Pastel(color, .65),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(12, 8, 12, 7),
                Margin = new Thickness(4, 0, 4, 8), Child = box };
            editorCards[name] = card; UpdatePreview(name); return card;
        }

        void UpdatePreview(string name)
        {
            if (!sliders.ContainsKey(name)) return;
            var s = sliders[name]; var c = Color.FromRgb((byte)s[0].Value, (byte)s[1].Value, (byte)s[2].Value);
            previews[name].Background = new SolidColorBrush(c);
            if (editorCards.ContainsKey(name))
            {
                editorCards[name].Background = Pastel(c, .88);
                editorCards[name].BorderBrush = Pastel(c, .65);
            }
            for (var i = 0; i < 3; i++) values[name][i].Text = ((int)s[i].Value).ToString();
        }
        string Hex(string name)
        {
            var s = sliders[name]; return string.Format("#{0:X2}{1:X2}{2:X2}", (byte)s[0].Value, (byte)s[1].Value, (byte)s[2].Value);
        }
        void SetHex(string name, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex); var s = sliders[name];
            s[0].Value = color.R; s[1].Value = color.G; s[2].Value = color.B; UpdatePreview(name);
        }
        static bool IsHoliday(GoogleCalendarSetting source)
        {
            return (source.Name ?? "").Contains("휴일") || (source.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        static void Round(Button button, double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }
        static Brush Pastel(Color color, double whiteRatio)
        {
            return new SolidColorBrush(Color.FromRgb(
                (byte)(color.R + (255 - color.R) * whiteRatio),
                (byte)(color.G + (255 - color.G) * whiteRatio),
                (byte)(color.B + (255 - color.B) * whiteRatio)));
        }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }

    public class RepeatDeleteWindow : Window
    {
        public string Scope = "single";
        public RepeatDeleteWindow(PlannerItem item)
        {
            Title = "반복 일정 삭제"; Width = 390; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel { Margin = new Thickness(24, 21, 24, 22) };
            panel.Children.Add(new TextBlock { Text = "반복 일정 삭제", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = B("#1E293B") });
            panel.Children.Add(new TextBlock { Text = "‘" + item.Title + "’의 삭제 범위를 선택해 주세요.", FontSize = 12, Foreground = B("#64748B"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 17) });
            AddChoice(panel, "이번 일정만", "선택한 하루만 삭제합니다.", "single", "#EFF6FF", "#2563EB");
            AddChoice(panel, "이번 일정부터 미래", "지난 기록은 남기고 이후 반복을 종료합니다.", "future", "#FFF7ED", "#EA580C");
            AddChoice(panel, "과거 포함 전체", "이 반복 일정의 모든 기록을 삭제합니다.", "all", "#FFF1F2", "#E11D48");
            var cancel = new Button { Content = "취소", Height = 38, Margin = new Thickness(0, 8, 0, 0), Background = B("#F1F5F9"), Foreground = B("#475569"), BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            cancel.Click += delegate { DialogResult = false; }; panel.Children.Add(cancel);
            var shell = new Border { Background = B("#FFFDFD"), BorderBrush = B("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(17), Child = panel };
            Content = shell; panel.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (e.GetPosition(panel).Y < 58) DragMove(); };
        }
        void AddChoice(Panel panel, string title, string description, string scope, string background, string foreground)
        {
            var text = new StackPanel(); text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 14, Foreground = B(foreground) });
            text.Children.Add(new TextBlock { Text = description, FontSize = 11, Foreground = B("#64748B"), Margin = new Thickness(0, 3, 0, 0) });
            var button = new Button { Content = text, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 8), Background = B(background), BorderBrush = B(foreground), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
            button.Click += delegate { Scope = scope; DialogResult = true; }; panel.Children.Add(button);
        }
        static Brush B(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
    }

    public class PendingSyncWindow : Window
    {
        public PendingSyncWindow(List<PlannerItem> pending)
        {
            Title = "동기화 대기"; Width = 440; MaxHeight = 560; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel { Margin = new Thickness(25, 22, 25, 21) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 38, 17) };
            header.Children.Add(new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(13), Background = B("#EEF2FF"),
                Child = new TextBlock { Text = "G", Foreground = B("#4F46E5"), FontSize = 20, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            var heading = new StackPanel { Margin = new Thickness(12, 1, 0, 0) };
            heading.Children.Add(new TextBlock { Text = "동기화 대기 일정", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = B("#1E293B") });
            heading.Children.Add(new TextBlock { Text = "Google Calendar에 아직 반영되지 않은 일정", FontSize = 11, Foreground = B("#64748B"), Margin = new Thickness(0, 3, 0, 0) });
            header.Children.Add(heading); panel.Children.Add(header);
            var status = new Border { Background = pending.Count == 0 ? B("#F0FDF4") : B("#FFF7ED"), CornerRadius = new CornerRadius(11),
                BorderBrush = pending.Count == 0 ? B("#BBF7D0") : B("#FED7AA"), BorderThickness = new Thickness(1), Padding = new Thickness(13, 10, 13, 10), Margin = new Thickness(0, 0, 0, 13),
                Child = new TextBlock { Text = pending.Count == 0 ? "✓  모든 일정이 동기화되었습니다." : "●  동기화를 기다리는 일정이 " + pending.Count + "개 있습니다.",
                    Foreground = pending.Count == 0 ? B("#15803D") : B("#C2410C"), FontSize = 12, FontWeight = FontWeights.SemiBold } };
            panel.Children.Add(status);
            var list = new StackPanel();
            foreach (var item in pending)
            {
                var card = new Grid(); card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(53) }); card.ColumnDefinitions.Add(new ColumnDefinition());
                var date = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(10), Background = B("#EEF2FF"), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = item.Start.ToString("MM.dd"), Foreground = B("#4338CA"), FontSize = 12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                card.Children.Add(date);
                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock { Text = item.Title, Foreground = B("#1E293B"), FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                info.Children.Add(new TextBlock { Text = (item.AllDay ? "하루 종일" : item.Start.ToString("HH:mm")) + "  ·  " + (item.GoogleCalendarName ?? "Google 캘린더"), Foreground = B("#64748B"), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
                Grid.SetColumn(info, 1); card.Children.Add(info);
                list.Children.Add(new Border { Background = B("#F8FAFC"), BorderBrush = B("#E2E8F0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 9, 12, 9), Margin = new Thickness(0, 0, 0, 8), Child = card });
            }
            panel.Children.Add(new ScrollViewer { Content = list, MaxHeight = 270, VerticalScrollBarVisibility = pending.Count > 4 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled });
            panel.Children.Add(new Border { Background = B("#EEF2FF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 4, 0, 0),
                Child = new TextBlock { Text = "재시도하려면 이 창을 닫고 상단의 G 동기화를 눌러 주세요.", Foreground = B("#4338CA"), FontSize = 11, TextAlignment = TextAlignment.Center } });
            var shell = new Border { Background = B("#FFFCFD"), BorderBrush = B("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel };
            var frame = new Grid(); frame.Children.Add(shell);
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = B("#FEE2E2"), Foreground = B("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17, Cursor = Cursors.Hand };
            UiRound.Apply(close, 10); close.Click += delegate { DialogResult = false; }; close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
            header.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
        }
        static Brush B(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
    }

    public class MainWindow : Window
    {
        readonly List<PlannerItem> items;
        readonly Grid calendar = new Grid();
        readonly StackPanel detail = new StackPanel();
        readonly TextBlock monthTitle = new TextBlock { FontSize = 25, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock selectedTitle = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold };
        readonly TextBlock accountStatus = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        readonly Canvas accountStatusViewport = new Canvas { ClipToBounds = true, Height = 18 };
        readonly TranslateTransform accountStatusShift = new TranslateTransform();
        readonly Dictionary<string, CheckBox> filters = new Dictionary<string, CheckBox>();
        DateTime shownMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime selectedDate = DateTime.Today;
        bool positionLocked;
        Button lockButton;
        Border resizeHandle;
        Border sidebarPanel;
        ColumnDefinition sidebarColumn;
        Button sidebarButton;
        Button collapseSidebarButton;
        Button googleButton;
        TextBlock googleStatus;
        StackPanel googleFilterPanel;
        string itemNoticeId;
        string itemNoticeText;
        int itemNoticeVersion;
        DispatcherTimer autoSyncTimer;
        DispatcherTimer reminderTimer;
        DispatcherTimer syncRetryTimer;
        readonly HashSet<string> shownReminders = new HashSet<string>();
        string syncProblem;
        bool googleSyncing;
        bool googleConnecting;
        bool resizing;
        Point resizeStart;
        double resizeWidth;
        double resizeHeight;
        Forms.NotifyIcon trayIcon;
        readonly PlannerSettings settings;

        static readonly Dictionary<string, string> Colors = new Dictionary<string, string>
        { { "업무일정", "#5B7CFA" }, { "개인일정", "#F08CA6" }, { "국경일", "#EF4444" } };

        public MainWindow()
        {
            settings = Store.LoadSettings();
            if (!GoogleCalendar.IsConnected) settings.ActiveGoogleAccountId = null;
            var connectedAccount = GoogleCalendar.ConnectedAccountId;
            if (GoogleCalendar.IsConnected && !string.IsNullOrWhiteSpace(connectedAccount)) settings.ActiveGoogleAccountId = connectedAccount;
            else if (GoogleCalendar.IsConnected && string.IsNullOrWhiteSpace(settings.ActiveGoogleAccountId) && settings.GoogleCalendars != null)
            {
                var savedPrimary = settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
                if (savedPrimary != null) settings.ActiveGoogleAccountId = savedPrimary.Id;
            }
            Store.SetAccount(settings.ActiveGoogleAccountId);
            items = Store.Load();
            var clearedOrphanPending = false;
            foreach (var orphan in items.Where(x => x.PendingGoogleSync && string.IsNullOrWhiteSpace(x.GoogleCalendarId)))
            { orphan.PendingGoogleSync = false; clearedOrphanPending = true; }
            if (clearedOrphanPending) Store.Save(items);
            var orphanSeries = items.Where(x => string.IsNullOrWhiteSpace(x.GoogleCalendarId) && !string.IsNullOrWhiteSpace(x.RecurrenceFrequency) && string.IsNullOrWhiteSpace(x.SeriesId)).ToList();
            foreach (var master in orphanSeries) ExpandLocalRecurrence(master);
            if (orphanSeries.Count > 0) Store.Save(items);
            if (!string.IsNullOrWhiteSpace(settings.BusinessColor)) Colors["업무일정"] = settings.BusinessColor;
            if (!string.IsNullOrWhiteSpace(settings.PersonalColor)) Colors["개인일정"] = settings.PersonalColor;
            positionLocked = settings.HasPosition ? settings.PositionLocked : true;
            Title = "온하루"; Width = settings.Width >= 820 ? settings.Width : 1120;
            Height = settings.Height >= 560 ? settings.Height : 700; MinWidth = 820; MinHeight = 560;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            FontSize = settings.FontSize > 0 ? settings.FontSize : 12;
            monthTitle.FontSize = Ui(24); monthTitle.Foreground = Brush("#4338CA"); selectedTitle.FontSize = Ui(18);
            Opacity = settings.Opacity > 0 ? settings.Opacity : .95;
            if (settings.HasPosition)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.Left; Top = settings.Top;
            }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = false; ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Content = BuildLayout();
            RenderAll();
            SourceInitialized += delegate
            {
                DesktopLayer.Attach(this);
            };
            Activated += delegate { Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { DesktopLayer.Lower(this); })); };
            Loaded += async delegate
            {
                CreateTrayIcon(); UpdateModeButtons(); UpdateGoogleButton();
                if (GoogleCalendar.IsConnected) await SyncGoogle(false);
                StartAutoSync();
            };
            Closing += delegate
            {
                Store.Save(items); SaveWindowSettings();
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
            };
            new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Normal, delegate { Rollover(); }, Dispatcher);
            reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            reminderTimer.Tick += delegate { CheckReminders(); }; reminderTimer.Start();
            syncRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            syncRetryTimer.Tick += async delegate { if (GoogleCalendar.IsConnected && (syncProblem != null || items.Any(x => x.PendingGoogleSync))) await SyncGoogle(false); };
            syncRetryTimer.Start();
        }

        UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(12), Background = Brushes.Transparent };
            root.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!positionLocked && e.GetPosition(root).Y <= 72 && !HasInteractiveParent(e.OriginalSource as DependencyObject))
                    DragMove();
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            var header = new Grid { Margin = new Thickness(16, 10, 48, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            var logo = new Border { Width = 44, Height = 44, Background = Brushes.White, BorderBrush = Brush("#BAE6FD"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 14, 0), Padding = new Thickness(7) };
            var logoTiles = new UniformGrid { Rows = 3, Columns = 3 };
            foreach (var color in new[] { "#38BDF8", "#60A5FA", "#818CF8", "#34D399", "#22C55E", "#A3E635", "#FBBF24", "#FB923C", "#F472B6" })
                logoTiles.Children.Add(new Border { Background = Brush(color), CornerRadius = new CornerRadius(2), Margin = new Thickness(1) });
            logo.Child = logoTiles;
            titleRow.Children.Add(logo);
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameBrush = new LinearGradientBrush(); nameBrush.StartPoint = new Point(0, .5); nameBrush.EndPoint = new Point(1, .5);
            nameBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0EA5E9"), 0));
            nameBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C3AED"), 1));
            titleStack.Children.Add(new TextBlock { Text = "온하루 · ONHARU", FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = nameBrush });
            titleStack.Children.Add(monthTitle); titleRow.Children.Add(titleStack); header.Children.Add(titleRow);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -3, 0, 0) };
            actions.Children.Add(Button("◀", delegate { shownMonth = shownMonth.AddMonths(-1); RenderAll(); }, 42));
            actions.Children.Add(Button("오늘", delegate { shownMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); selectedDate = DateTime.Today; RenderAll(); }, 62));
            actions.Children.Add(Button("▶", delegate { shownMonth = shownMonth.AddMonths(1); RenderAll(); }, 42));
            lockButton = Button("📌 고정됨", null, 112);
            lockButton.Click += delegate
            {
                positionLocked = !positionLocked;
                if (positionLocked)
                {
                    settings.HasPosition = true; settings.Left = Left; settings.Top = Top;
                    settings.Width = ActualWidth; settings.Height = ActualHeight;
                }
                settings.PositionLocked = positionLocked; Store.SaveSettings(settings);
                UpdateModeButtons();
            };
            actions.Children.Add(lockButton);
            actions.Children.Add(new TextBlock { Text = "투명도", VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#64748B"), Margin = new Thickness(12, 0, 5, 0), FontSize = 11 });
            var opacitySlider = new Slider { Minimum = .45, Maximum = .98, Value = settings.Opacity,
                Width = 72, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Arrow };
            opacitySlider.ValueChanged += delegate { Opacity = opacitySlider.Value; settings.Opacity = opacitySlider.Value; };
            actions.Children.Add(opacitySlider);
            googleButton = Button("G 연결", GoogleClick, 92); googleButton.Foreground = Brush("#2563EB");
            googleButton.ToolTip = "개인일정을 Google 기본 캘린더와 동기화"; actions.Children.Add(googleButton);
            var searchButton = Button("⌕", OpenSearch, 38); searchButton.FontSize = 20; searchButton.ToolTip = "일정 검색"; actions.Children.Add(searchButton);
            var settingsButton = Button("⚙", OpenSettings, 38); settingsButton.FontSize = 17; settingsButton.ToolTip = "색상 및 설정";
            actions.Children.Add(settingsButton);
            var actionArea = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            actionArea.Children.Add(actions);
            googleStatus = new TextBlock { Text = "동기화가 완료되었습니다", Foreground = Brush("#DC2626"),
                FontSize = 11, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 1, 44, 0), Visibility = Visibility.Hidden };
            actionArea.Children.Add(googleStatus);
            Grid.SetColumn(actionArea, 1); header.Children.Add(actionArea);
            root.Children.Add(header);

            var close = Button("×", delegate { Close(); }, 32); close.Height = 32;
            close.Foreground = Brush("#DC2626"); close.FontSize = 17; close.ToolTip = "종료";
            close.Background = Brush("#FEE2E2"); close.BorderBrush = Brushes.Transparent;
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top;
            close.Margin = new Thickness(0, 8, 8, 0); Panel.SetZIndex(close, 30);

            resizeHandle = new Border { Width = 28, Height = 28, Background = Brush("#D9E0F2FE"),
                CornerRadius = new CornerRadius(9), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE, ToolTip = "창 크기 조절" };
            resizeHandle.Child = new TextBlock { Text = "◢", Foreground = Brush("#64748B"), FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            resizeHandle.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (positionLocked) return;
                resizing = true; resizeStart = PointToScreen(e.GetPosition(this)); resizeWidth = Width; resizeHeight = Height;
                resizeHandle.CaptureMouse(); e.Handled = true;
            };
            resizeHandle.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!resizing) return;
                var point = PointToScreen(e.GetPosition(this)); Width = Math.Max(MinWidth, resizeWidth + point.X - resizeStart.X);
                Height = Math.Max(MinHeight, resizeHeight + point.Y - resizeStart.Y);
            };
            resizeHandle.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            { resizing = false; resizeHandle.ReleaseMouseCapture(); e.Handled = true; };
            resizeHandle.Visibility = positionLocked ? Visibility.Collapsed : Visibility.Visible;
            resizeHandle.Margin = new Thickness(0, 0, 3, 3); Grid.SetRow(resizeHandle, 1);
            Panel.SetZIndex(resizeHandle, 20); root.Children.Add(resizeHandle);

            var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition());
            sidebarColumn = new ColumnDefinition { Width = settings.SidebarVisible ? new GridLength(310) : new GridLength(34) };
            body.ColumnDefinitions.Add(sidebarColumn);
            var calendarCard = new Border { Background = Brush("#D9FFFFFF"), CornerRadius = new CornerRadius(14),
                BorderBrush = Brush("#80FFFFFF"), BorderThickness = new Thickness(1), Padding = new Thickness(5), Child = calendar };
            body.Children.Add(calendarCard);
            sidebarPanel = new Border { Background = Brush("#E6FFFFFF"), CornerRadius = new CornerRadius(14), Margin = new Thickness(12, 0, 0, 0), Padding = new Thickness(18),
                Visibility = settings.SidebarVisible ? Visibility.Visible : Visibility.Collapsed };
            var sideStack = new StackPanel();
            var categoryHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            collapseSidebarButton = Button("❯", ToggleSidebar, 28); collapseSidebarButton.Height = 28; collapseSidebarButton.FontSize = 15;
            collapseSidebarButton.VerticalAlignment = VerticalAlignment.Center; collapseSidebarButton.Margin = new Thickness(-8, 0, 7, 0);
            collapseSidebarButton.ToolTip = "일정 패널 접기"; DockPanel.SetDock(collapseSidebarButton, Dock.Left); categoryHeader.Children.Add(collapseSidebarButton);
            accountStatus.RenderTransform = accountStatusShift; Canvas.SetTop(accountStatus, 1); accountStatusViewport.Children.Add(accountStatus);
            accountStatusViewport.SizeChanged += delegate { StartAccountMarquee(); };
            var accountCard = new Border { Background = Brush("#EEF2FF"), CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 6, 10, 6), Child = accountStatusViewport, Cursor = Cursors.Hand,
                ToolTip = "Google 계정 및 동기화 대기 일정 보기" };
            accountCard.MouseLeftButtonDown += OpenPendingSync; categoryHeader.Children.Add(accountCard);
            UpdateAccountStatus();
            sideStack.Children.Add(categoryHeader);
            sideStack.Children.Add(new TextBlock { Text = "온하루 등록", Foreground = Brush("#64748B"), FontSize = Ui(11), Margin = new Thickness(0, 0, 0, 7) });
            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            foreach (var category in new[] { "업무일정", "개인일정" })
            {
                var visible = category == "업무일정" ? settings.BusinessVisible : settings.PersonalVisible;
                var box = new CheckBox { Content = category, IsChecked = visible, Foreground = Brush(Colors[category]), Margin = new Thickness(0, 0, 14, 0) };
                box.Click += delegate { SaveWindowSettings(); RenderAll(); }; filters[category] = box; filterRow.Children.Add(box);
            }
            sideStack.Children.Add(filterRow);
            sideStack.Children.Add(new TextBlock { Text = "Google", Foreground = Brush("#64748B"), FontSize = Ui(11), Margin = new Thickness(0, 0, 0, 7) });
            googleFilterPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            sideStack.Children.Add(googleFilterPanel); BuildGoogleFilters(); sideStack.Children.Add(selectedTitle);
            sideStack.Children.Add(new Border { Height = 1, Background = Brush("#E2E8F0"), Margin = new Thickness(0, 12, 0, 12) });
            sideStack.Children.Add(detail); sidebarPanel.Child = sideStack; Grid.SetColumn(sidebarPanel, 1); body.Children.Add(sidebarPanel);
            sidebarButton = Button("❮", ToggleSidebar, 28);
            sidebarButton.Height = 32; sidebarButton.FontSize = 20; sidebarButton.ToolTip = settings.SidebarVisible ? "일정 패널 접기" : "일정 패널 펼치기";
            sidebarButton.HorizontalAlignment = HorizontalAlignment.Right; sidebarButton.VerticalAlignment = VerticalAlignment.Top;
            sidebarButton.Margin = new Thickness(0, 8, 3, 0); sidebarButton.Visibility = settings.SidebarVisible ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetColumn(sidebarButton, 1); Panel.SetZIndex(sidebarButton, 30); body.Children.Add(sidebarButton);
            Grid.SetRow(body, 1); body.Margin = new Thickness(12, 0, 12, 30); root.Children.Add(body);
            var credit = new TextBlock { Text = "MADE BY JUAN.HJLEE · ONHARU (step88)", FontSize = 10,
                FontWeight = FontWeights.SemiBold, Foreground = Brush("#475569"),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 50, 5) };
            Grid.SetRow(credit, 1); Panel.SetZIndex(credit, 25); root.Children.Add(credit);
            var shell = new Border { CornerRadius = new CornerRadius(18), Background = Brush("#BFF1F5F9"),
                BorderBrush = Brush("#99FFFFFF"), BorderThickness = new Thickness(1), Child = root };
            var frame = new Grid(); frame.Children.Add(shell); frame.Children.Add(close); return frame;
        }

        void RenderAll()
        {
            monthTitle.Text = shownMonth.ToString("yyyy년 M월");
            calendar.Children.Clear(); calendar.RowDefinitions.Clear(); calendar.ColumnDefinitions.Clear();
            var weekOffset = settings.ShowWeekNumbers ? 1 : 0;
            if (settings.ShowWeekNumbers) calendar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            for (var c = 0; c < 7; c++) calendar.ColumnDefinitions.Add(new ColumnDefinition());
            calendar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            for (var r = 0; r < 6; r++) calendar.RowDefinitions.Add(new RowDefinition());
            var mondayFirst = settings.WeekNumberRule == "iso";
            var weekdays = mondayFirst ? new[] { "월", "화", "수", "목", "금", "토", "일" } : new[] { "일", "월", "화", "수", "목", "금", "토" };
            if (settings.ShowWeekNumbers)
            {
                var weekHeader = new TextBlock { Text = "주", HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#94A3B8"), FontSize = Ui(10), FontWeight = FontWeights.Bold };
                calendar.Children.Add(weekHeader);
            }
            for (var c = 0; c < 7; c++)
            {
                var day = new TextBlock { Text = weekdays[c], HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold, FontSize = Ui(13),
                    Foreground = weekdays[c] == "일" ? Brush("#DC2626") : weekdays[c] == "토" ? Brush("#2563EB") : Brush("#0F766E") };
                Grid.SetColumn(day, c + weekOffset); calendar.Children.Add(day);
            }
            var firstOffset = mondayFirst ? ((int)shownMonth.DayOfWeek + 6) % 7 : (int)shownMonth.DayOfWeek;
            var first = shownMonth.AddDays(-firstOffset);
            if (settings.ShowWeekNumbers)
                for (var r = 0; r < 6; r++)
                {
                    var week = new TextBlock { Text = "W" + GetWeekNumber(first.AddDays(r * 7)).ToString("00"),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brush("#64748B"), FontSize = Ui(10), FontWeight = FontWeights.SemiBold };
                    Grid.SetRow(week, r + 1); calendar.Children.Add(week);
                }
            for (var i = 0; i < 42; i++) AddDayCell(first.AddDays(i), i / 7 + 1, i % 7 + weekOffset);
            RenderDetail();
        }

        void AddDayCell(DateTime date, int row, int col)
        {
            var stack = new StackPanel();
            var dateItems = VisibleItems(date).ToList();
            var isHoliday = dateItems.Any(x => x.Category == "국경일");
            var dateHeader = new StackPanel { Orientation = Orientation.Horizontal };
            var number = new TextBlock { Text = date.Day.ToString(), FontSize = Ui(13), FontWeight = date == DateTime.Today ? FontWeights.Bold : FontWeights.Normal,
                Foreground = date.Month != shownMonth.Month ? Brush("#CBD5E1") : isHoliday || date.DayOfWeek == DayOfWeek.Sunday ? Brush("#EF4444") : date.DayOfWeek == DayOfWeek.Saturday ? Brush("#3B82F6") : Brush("#0F172A"),
                Margin = new Thickness(5, 3, 2, 4) };
            dateHeader.Children.Add(number);
            if (settings.ShowLunar)
                dateHeader.Children.Add(new TextBlock { Text = Lunar(date), Foreground = Brush("#8B5CF6"), FontSize = Ui(9),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 1, 1, 2) });
            var holidays = string.Join(", ", dateItems.Where(x => x.Category == "국경일").Select(x => x.Title).ToArray());
            if (date == DateTime.Today)
                dateHeader.Children.Add(new TextBlock { Text = "오늘", Foreground = Brush("#2563EB"), FontSize = Ui(10),
                    FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 1, 0, 2) });
            if (!string.IsNullOrWhiteSpace(holidays))
                dateHeader.Children.Add(new TextBlock { Text = (date == DateTime.Today ? ". " : "") + holidays, Foreground = Brush("#EF4444"), FontSize = Ui(10),
                    FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(3, 1, 2, 2), TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(dateHeader);
            foreach (var item in dateItems.Where(x => x.Category != "국경일").Take(6))
            {
                var eventText = new TextBlock { Text = (item.IsTodo ? (item.Completed ? "✓ " : "□ ") : "") + (item.AllDay ? "" : item.Start.ToString("HH:mm ")) + (item.Important ? "★ " : "") + item.Title,
                    FontSize = Ui(11), Foreground = item.Important ? Brush("#F20D7A") : settings.PastelEventStyle ? Brush("#1F2937") : Brushes.White,
                    FontWeight = item.Important ? FontWeights.Bold : FontWeights.Normal,
                    Padding = new Thickness(4, 2, 3, 2), TextTrimming = TextTrimming.CharacterEllipsis,
                    TextDecorations = item.Completed ? TextDecorations.Strikethrough : null };
                var eventBorder = new Border { Child = eventText, CornerRadius = new CornerRadius(4),
                    Background = item.Important ? Brush("#FFF1F7") : settings.PastelEventStyle ? PastelBrush(ItemColor(item), .72) : Brush(ItemColor(item)),
                    Margin = new Thickness(3, 1, 3, 0), Cursor = Cursors.Hand, ToolTip = "더블클릭하여 수정" };
                eventBorder.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                { if (e.ClickCount == 2) { selectedDate = date; OpenEdit(item); e.Handled = true; } };
                stack.Children.Add(eventBorder);
            }
            var border = new Border { Child = stack, BorderBrush = Brush("#99CBD5E1"), BorderThickness = new Thickness(.5),
                Background = date == DateTime.Today ? Brush("#CCFCE7F3") : date == selectedDate ? Brush("#CCDBEAFE") : Brush("#99FFFFFF"), Cursor = Cursors.Hand };
            border.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                selectedDate = date;
                if (e.ClickCount == 2) AddItem(sender, e); else RenderAll();
            };
            Grid.SetRow(border, row); Grid.SetColumn(border, col); calendar.Children.Add(border);
        }

        void RenderDetail()
        {
            selectedTitle.Text = selectedDate.ToString("M월 d일 dddd", new CultureInfo("ko-KR")); detail.Children.Clear();
            var dayItems = VisibleItems(selectedDate).ToList();
            if (dayItems.Count == 0) detail.Children.Add(new TextBlock { Text = "일정이 없습니다.", Foreground = Brush("#94A3B8"), Margin = new Thickness(0, 8, 0, 0) });
            foreach (var sourceGroup in dayItems.GroupBy(DisplayGroup).OrderBy(x => GroupOrder(x.First())))
            {
                var categoryItems = sourceGroup.ToList();
                var groupColor = ItemColor(categoryItems[0]);
                var group = new StackPanel();
                group.Children.Add(new TextBlock { Text = "●  " + sourceGroup.Key, Foreground = Brush(groupColor),
                    FontWeight = FontWeights.Bold, FontSize = Ui(12), Margin = new Thickness(0, 0, 0, 7) });
                foreach (var item in categoryItems)
                {
                    var row = new DockPanel { Margin = new Thickness(0, 3, 0, 8) };
                    if (item.IsTodo)
                    {
                        var check = new CheckBox { IsChecked = item.Completed,
                            ToolTip = item.GoogleTaskEvent ? "완료 상태는 온하루에 저장됩니다." : null,
                            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 8, 0) };
                        check.Click += async delegate
                        {
                            item.Completed = check.IsChecked == true; Store.Save(items); RenderAll();
                            if (item.Category == "개인일정" && !item.GoogleTaskEvent && !item.GoogleReadOnly && GoogleCalendar.IsConnected) await SaveGoogleItem(item);
                        };
                        DockPanel.SetDock(check, Dock.Left); row.Children.Add(check);
                    }
                    var text = new StackPanel();
                    text.Children.Add(new TextBlock { Text = (item.Important ? "★ " : "") + item.Title,
                        FontWeight = item.Important ? FontWeights.Bold : FontWeights.SemiBold,
                        Foreground = item.Important ? Brush("#F20D7A") : item.Category == "국경일" ? Brush("#EF4444") : Brush("#1E293B"),
                        TextDecorations = item.Completed ? TextDecorations.Strikethrough : null });
                    text.Children.Add(new TextBlock { Text = item.AllDay ? "하루 종일" : item.Start.ToString("HH:mm"),
                        FontSize = Ui(11), Foreground = Brush(ItemColor(item)) });
                    if (!string.IsNullOrWhiteSpace(item.Notes))
                        text.Children.Add(new TextBlock { Text = item.Notes, FontSize = Ui(11), Foreground = Brush("#64748B"),
                            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                    text.Cursor = Cursors.Hand; text.ToolTip = "더블클릭하여 수정";
                    text.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                    { if (e.ClickCount == 2) { OpenEdit(item); e.Handled = true; } };
                    row.Children.Add(text);
                    if (itemNoticeId == item.Id) row.Margin = new Thickness(0, 3, 0, 2);
                    group.Children.Add(row);
                    if (itemNoticeId == item.Id)
                        group.Children.Add(new TextBlock { Text = itemNoticeText, Foreground = Brush("#DC2626"), FontSize = Ui(11),
                            FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(item.IsTodo ? 24 : 0, 0, 0, 8) });
                }
                detail.Children.Add(new Border { Background = PastelBrush(groupColor, .86),
                    BorderBrush = PastelBrush(groupColor, .62), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(11), Padding = new Thickness(12), Margin = new Thickness(0, 5, 0, 8), Child = group });
            }
            var add = Button("+ 이 날짜에 추가", AddItem, 150); add.Margin = new Thickness(0, 14, 0, 0); detail.Children.Add(add);
        }

        IEnumerable<PlannerItem> VisibleItems(DateTime date)
        {
            var day = items.Where(x => x.Start.Date == date.Date && IsItemVisible(x));
            if (settings.CalendarOrderMode == "time")
                return day.OrderBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title);
            return day.OrderBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start);
        }

        bool IsItemVisible(PlannerItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.GoogleCalendarId))
            {
                if (settings.GoogleCalendars == null || !settings.GoogleCalendars.Any(x => x.Id == item.GoogleCalendarId)) return false;
                var key = "google:" + item.GoogleCalendarId;
                return !filters.ContainsKey(key) || filters[key].IsChecked == true;
            }
            return !filters.ContainsKey(item.Category) || filters[item.Category].IsChecked == true;
        }

        string ItemColor(PlannerItem item)
        {
            if (item.Category == "국경일") return Colors["국경일"];
            return !string.IsNullOrWhiteSpace(item.GoogleCalendarColor) ? item.GoogleCalendarColor : Colors.ContainsKey(item.Category) ? Colors[item.Category] : Colors["개인일정"];
        }

        string DisplayGroup(PlannerItem item)
        {
            if (string.IsNullOrWhiteSpace(item.GoogleCalendarId)) return item.Category;
            var source = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Id == item.GoogleCalendarId);
            var name = !string.IsNullOrWhiteSpace(item.GoogleCalendarName) ? item.GoogleCalendarName : source == null ? "Google" : source.Name;
            return source != null && source.Primary ? "내 캘린더 · " + name : name;
        }

        int GroupOrder(PlannerItem item)
        {
            var key = item.Category == "업무일정" ? "local:business" : string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "local:personal" : "google:" + item.GoogleCalendarId;
            var index = settings.CategoryOrder == null ? -1 : settings.CategoryOrder.IndexOf(key);
            return index < 0 ? 999 : index;
        }

        int GetWeekNumber(DateTime date)
        {
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date,
                settings.WeekNumberRule == "iso" ? CalendarWeekRule.FirstFourDayWeek : CalendarWeekRule.FirstDay,
                settings.WeekNumberRule == "iso" ? DayOfWeek.Monday : DayOfWeek.Sunday);
        }

        static string Lunar(DateTime date)
        {
            try { var c = new KoreanLunisolarCalendar(); return "음 " + c.GetMonth(date) + "/" + c.GetDayOfMonth(date); }
            catch { return ""; }
        }

        void BuildGoogleFilters()
        {
            if (googleFilterPanel == null) return;
            UpdateAccountStatus();
            foreach (var key in filters.Keys.Where(x => x.StartsWith("google:")).ToList()) filters.Remove(key);
            googleFilterPanel.Children.Clear();
            if (settings.GoogleCalendars == null || settings.GoogleCalendars.Count == 0)
            {
                googleFilterPanel.Children.Add(new TextBlock { Text = "G 연결 후 목록이 표시됩니다.", Foreground = Brush("#94A3B8"), FontSize = Ui(11) });
                return;
            }
            foreach (var source in settings.GoogleCalendars.OrderBy(x => CategoryRank("google:" + x.Id)).ThenBy(x => x.Name))
            {
                var key = "google:" + source.Id;
                var color = string.IsNullOrWhiteSpace(source.Color) ? Colors["개인일정"] : source.Color;
                var box = new CheckBox { Content = (source.Primary ? "내 캘린더 · " : "") + source.Name,
                    IsChecked = source.Visible, Foreground = Brush(color), Margin = new Thickness(0, 0, 0, 6) };
                box.Click += delegate { source.Visible = box.IsChecked == true; Store.SaveSettings(settings); RenderAll(); };
                filters[key] = box; googleFilterPanel.Children.Add(box);
            }
        }

        static bool IsHolidayCalendar(GoogleCalendarSetting source)
        {
            return (source.Name ?? "").Contains("휴일") || (source.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        int CategoryRank(string key)
        {
            var index = settings.CategoryOrder == null ? -1 : settings.CategoryOrder.IndexOf(key);
            return index < 0 ? 999 : index;
        }

        void ToggleSidebar(object sender, RoutedEventArgs e)
        {
            settings.SidebarVisible = !settings.SidebarVisible;
            sidebarPanel.Visibility = settings.SidebarVisible ? Visibility.Visible : Visibility.Collapsed;
            sidebarColumn.Width = settings.SidebarVisible ? new GridLength(310) : new GridLength(34);
            sidebarButton.Visibility = settings.SidebarVisible ? Visibility.Collapsed : Visibility.Visible;
            sidebarButton.ToolTip = "일정 패널 펼치기";
            Store.SaveSettings(settings);
        }

        async void OpenSettings(object sender, RoutedEventArgs e)
        {
            var allLocalItems = Store.LoadLocal();
            var localItems = allLocalItems.Where(x => !items.Any(y => y.Id == x.Id)).ToList();
            if (localItems.Count != allLocalItems.Count) Store.SaveLocal(localItems);
            var window = new SettingsWindow(Colors["업무일정"], Colors["개인일정"], settings.FontSize,
                settings.CalendarOrderMode, settings.ShowWeekNumbers, settings.WeekNumberRule,
                settings.PastelEventStyle, settings.AutoSyncMinutes, settings.GoogleCalendars,
                GoogleCalendar.IsConnected, localItems.Count, settings.ShowLunar, Store.Backups().Length, settings.CategoryOrder) { Owner = this };
            if (window.ShowDialog() != true) return;
            Colors["업무일정"] = window.BusinessColor; Colors["개인일정"] = window.PersonalColor;
            settings.BusinessColor = window.BusinessColor; settings.PersonalColor = window.PersonalColor;
            settings.FontSize = window.SelectedFontSize; settings.CalendarOrderMode = window.OrderMode;
            settings.CategoryOrder = window.CategoryOrder;
            settings.ShowWeekNumbers = window.ShowWeekNumbers; settings.WeekNumberRule = window.WeekRule;
            settings.ShowLunar = window.ShowLunar;
            settings.PastelEventStyle = window.PastelEventStyle;
            settings.AutoSyncMinutes = window.AutoSyncMinutes;
            FontSize = settings.FontSize;
            monthTitle.FontSize = Ui(24); selectedTitle.FontSize = Ui(18);
            Store.SaveSettings(settings);
            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId)))
            {
                var source = settings.GoogleCalendars.FirstOrDefault(x => x.Id == item.GoogleCalendarId);
                if (source != null) { item.GoogleCalendarColor = source.Color; item.GoogleReadOnly = !source.Editable; }
            }
            Store.Save(items); BuildGoogleFilters(); StartAutoSync();
            foreach (var category in filters.Keys.Where(Colors.ContainsKey)) filters[category].Foreground = Brush(Colors[category]);
            RenderAll();
            if (window.ImportLocalItems)
            {
                var importWindow = new LocalImportWindow(localItems) { Owner = this };
                if (importWindow.ShowDialog() == true)
                {
                    foreach (var item in importWindow.SelectedItems)
                        if (!items.Any(x => x.Id == item.Id)) items.Add(item);
                    localItems.RemoveAll(x => importWindow.SelectedItems.Any(y => y.Id == x.Id));
                    Store.Save(items); Store.SaveLocal(localItems); RenderAll();
                }
            }
            if (window.RestoreBackup)
            {
                var backup = new BackupWindow(Store.Backups()) { Owner = this };
                if (backup.ShowDialog() == true)
                { items.Clear(); items.AddRange(Store.Restore(backup.SelectedPath)); RenderAll(); }
            }
            if (window.ChangeGoogleAccount || window.LogoutGoogleAccount)
            {
                GoogleCalendar.Disconnect();
                Store.SetAccount(null);
                items.Clear();
                settings.ActiveGoogleAccountId = null;
                settings.GoogleCalendars.Clear();
                if (window.LogoutGoogleAccount) items.AddRange(Store.LoadLocal());
                Store.SaveSettings(settings); BuildGoogleFilters(); RenderAll(); UpdateGoogleButton();
                if (window.LogoutGoogleAccount) { StartAutoSync(); return; }
                if (await ConnectGoogle(false)) await SyncGoogle(true);
                StartAutoSync();
            }
        }

        void OpenSearch(object sender, RoutedEventArgs e)
        {
            var window = new SearchWindow(items.Where(IsItemVisible).ToList()) { Owner = this };
            if (window.ShowDialog() == true && window.SelectedItem != null)
            { selectedDate = window.SelectedItem.Start.Date; shownMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1); RenderAll(); OpenEdit(window.SelectedItem); }
        }

        void SaveWindowSettings()
        {
            settings.HasPosition = true; settings.Left = Left; settings.Top = Top;
            settings.Width = ActualWidth; settings.Height = ActualHeight; settings.PositionLocked = positionLocked;
            settings.FontSize = FontSize; settings.Opacity = Opacity;
            if (filters.ContainsKey("업무일정")) settings.BusinessVisible = filters["업무일정"].IsChecked == true;
            if (filters.ContainsKey("개인일정")) settings.PersonalVisible = filters["개인일정"].IsChecked == true;
            if (filters.ContainsKey("국경일")) settings.HolidayVisible = filters["국경일"].IsChecked == true;
            Store.SaveSettings(settings);
        }

        async void AddItem(object sender, RoutedEventArgs e)
        {
            var window = new AddItemWindow(selectedDate, null, settings.GoogleCalendars, GoogleCalendar.IsConnected) { Owner = this };
            if (window.ShowDialog() == true)
            {
                items.Add(window.Result);
                if (string.IsNullOrWhiteSpace(window.Result.GoogleCalendarId)) ExpandLocalRecurrence(window.Result);
                Store.Save(items); RenderAll();
                if (!string.IsNullOrWhiteSpace(window.Result.GoogleCalendarId) && GoogleCalendar.IsConnected)
                { await SaveGoogleItem(window.Result); if (!string.IsNullOrWhiteSpace(window.Result.RecurrenceFrequency)) await SyncGoogle(false); }
            }
        }

        void ExpandLocalRecurrence(PlannerItem master)
        {
            if (string.IsNullOrWhiteSpace(master.RecurrenceFrequency) || master.RecurrenceUntil <= master.Start.Date) return;
            master.SeriesId = string.IsNullOrWhiteSpace(master.SeriesId) ? Guid.NewGuid().ToString() : master.SeriesId;
            var start = master.Start; var duration = master.End - master.Start; var count = 0;
            while (count++ < 500)
            {
                start = NextOccurrence(master, start);
                if (start.Date > master.RecurrenceUntil.Date) break;
                items.Add(new PlannerItem { Id = Guid.NewGuid().ToString(), Title = master.Title, Start = start, End = start.Add(duration), AllDay = master.AllDay,
                    IsTodo = master.IsTodo, Category = master.Category, Notes = master.Notes, CreatedInOnharu = true, RolloverMode = master.RolloverMode,
                    AutoRollover = master.AutoRollover, Important = master.Important, ReminderMinutes = master.ReminderMinutes, ReminderConfigured = master.ReminderConfigured,
                    RecurrenceFrequency = master.RecurrenceFrequency, RecurrenceMode = master.RecurrenceMode, RecurrenceDays = master.RecurrenceDays,
                    RecurrenceUntil = master.RecurrenceUntil, SeriesId = master.SeriesId });
            }
        }

        static DateTime NextOccurrence(PlannerItem item, DateTime current)
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
                var selected = new HashSet<string>((item.RecurrenceDays ?? DayCode(item.Start.DayOfWeek)).Split(','));
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

        static int NthWeekdayOfMonth(int year, int month, string rule)
        {
            var code = rule.Substring(rule.Length - 2); var ordinal = int.Parse(rule.Substring(0, rule.Length - 2), CultureInfo.InvariantCulture);
            var target = CodeDay(code); var days = DateTime.DaysInMonth(year, month);
            if (ordinal < 0)
            {
                var last = new DateTime(year, month, days); return days - ((7 + (int)last.DayOfWeek - (int)target) % 7);
            }
            var first = new DateTime(year, month, 1); return 1 + ((7 + (int)target - (int)first.DayOfWeek) % 7) + (ordinal - 1) * 7;
        }

        static string DayCode(DayOfWeek day)
        {
            return day == DayOfWeek.Monday ? "MO" : day == DayOfWeek.Tuesday ? "TU" : day == DayOfWeek.Wednesday ? "WE" :
                day == DayOfWeek.Thursday ? "TH" : day == DayOfWeek.Friday ? "FR" : day == DayOfWeek.Saturday ? "SA" : "SU";
        }

        static DayOfWeek CodeDay(string code)
        {
            return code == "MO" ? DayOfWeek.Monday : code == "TU" ? DayOfWeek.Tuesday : code == "WE" ? DayOfWeek.Wednesday :
                code == "TH" ? DayOfWeek.Thursday : code == "FR" ? DayOfWeek.Friday : code == "SA" ? DayOfWeek.Saturday : DayOfWeek.Sunday;
        }

        async void OpenEdit(PlannerItem item)
        {
            if (item.GoogleReadOnly)
            {
                ShowItemNotice(item, item.Category == "국경일" ? "읽기 전용 일정입니다." : "설정에서 수정 가능을 선택해주세요.");
                return;
            }
            var oldRecurrence = item.RecurrenceFrequency; var oldMode = item.RecurrenceMode; var oldDays = item.RecurrenceDays; var oldUntil = item.RecurrenceUntil;
            var originalSeriesStart = string.IsNullOrWhiteSpace(item.SeriesId) ? item.Start : items.Where(x => x.SeriesId == item.SeriesId).Min(x => x.Start);
            var window = new AddItemWindow(item.Start.Date, item, settings.GoogleCalendars, GoogleCalendar.IsConnected) { Owner = this };
            if (window.ShowDialog() != true) return;
            if (window.DeleteRequested)
            {
                var recurring = !string.IsNullOrWhiteSpace(item.SeriesId) || !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId) || !string.IsNullOrWhiteSpace(item.RecurrenceFrequency);
                var deleteScope = "single";
                if (recurring)
                {
                    var deleteWindow = new RepeatDeleteWindow(item) { Owner = this };
                    if (deleteWindow.ShowDialog() != true) return;
                    deleteScope = deleteWindow.Scope;
                }
                var isGoogle = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) || !string.IsNullOrWhiteSpace(item.GoogleEventId);
                if (isGoogle && GoogleCalendar.IsConnected)
                {
                    try
                    {
                        if (deleteScope == "future") await GoogleCalendar.TrimSeriesBeforeAsync(item);
                        else await GoogleCalendar.DeleteAsync(item, deleteScope == "all");
                    }
                    catch { ShowItemNotice(item, "Google에서 삭제하지 못했습니다 · 일정은 유지됩니다."); return; }
                }
                if (deleteScope == "all" && !string.IsNullOrWhiteSpace(item.SeriesId)) items.RemoveAll(x => x.SeriesId == item.SeriesId);
                else if (deleteScope == "all" && !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId)) items.RemoveAll(x => x.GoogleRecurringEventId == item.GoogleRecurringEventId);
                else if (deleteScope == "future" && !string.IsNullOrWhiteSpace(item.SeriesId)) items.RemoveAll(x => x.SeriesId == item.SeriesId && x.Start >= item.Start);
                else if (deleteScope == "future" && !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId)) items.RemoveAll(x => x.GoogleRecurringEventId == item.GoogleRecurringEventId && x.Start >= item.Start);
                else items.RemoveAll(x => x.Id == item.Id);
            }
            else
            {
                var oldGoogle = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) || !string.IsNullOrWhiteSpace(item.GoogleEventId);
                var newGoogle = !string.IsNullOrWhiteSpace(window.Result.GoogleCalendarId);
                if (!window.ApplyToSeries && !string.IsNullOrWhiteSpace(item.SeriesId) && !oldGoogle)
                { window.Result.SeriesId = null; window.Result.RecurrenceFrequency = null; window.Result.RecurrenceMode = null; window.Result.RecurrenceDays = null; window.Result.RecurrenceUntil = window.Result.Start.Date; }
                var movedCalendar = oldGoogle && (!newGoogle || item.GoogleCalendarId != window.Result.GoogleCalendarId);
                if (movedCalendar && GoogleCalendar.IsConnected)
                    try { await GoogleCalendar.DeleteAsync(item, window.ApplyToSeries); } catch { ShowItemNotice(item, "Google 캘린더를 변경하지 못했습니다."); return; }
                var index = items.FindIndex(x => x.Id == item.Id);
                if (index >= 0) items[index] = window.Result;
                if (window.ApplyToSeries && !string.IsNullOrWhiteSpace(item.SeriesId) && string.IsNullOrWhiteSpace(item.GoogleCalendarId) &&
                    (oldRecurrence != window.Result.RecurrenceFrequency || oldMode != window.Result.RecurrenceMode || oldDays != window.Result.RecurrenceDays || oldUntil.Date != window.Result.RecurrenceUntil.Date))
                    RebuildLocalSeries(window.Result, originalSeriesStart);
                if (window.ApplyToSeries && !string.IsNullOrWhiteSpace(item.SeriesId))
                {
                    var duration = window.Result.End - window.Result.Start;
                    foreach (var sibling in items.Where(x => x.SeriesId == item.SeriesId && x.Id != window.Result.Id))
                    {
                        sibling.Title = window.Result.Title; sibling.Notes = window.Result.Notes; sibling.Category = window.Result.Category;
                        sibling.Important = window.Result.Important; sibling.ReminderMinutes = window.Result.ReminderMinutes; sibling.ReminderConfigured = true;
                        sibling.RecurrenceFrequency = window.Result.RecurrenceFrequency; sibling.RecurrenceMode = window.Result.RecurrenceMode;
                        sibling.RecurrenceDays = window.Result.RecurrenceDays; sibling.RecurrenceUntil = window.Result.RecurrenceUntil;
                        sibling.Start = sibling.Start.Date.Add(window.Result.Start.TimeOfDay); sibling.End = sibling.Start.Add(duration);
                    }
                }
                if (newGoogle && GoogleCalendar.IsConnected) await SaveGoogleItem(window.Result, window.ApplyToSeries);
            }
            Store.Save(items); RenderAll();
        }

        void RebuildLocalSeries(PlannerItem edited, DateTime originalStart)
        {
            var seriesId = edited.SeriesId; var duration = edited.End - edited.Start; items.RemoveAll(x => x.SeriesId == seriesId && x.Id != edited.Id);
            edited.Start = originalStart.Date.Add(edited.Start.TimeOfDay); edited.End = edited.Start.Add(edited.AllDay ? TimeSpan.FromDays(1) : duration);
            if (!string.IsNullOrWhiteSpace(edited.RecurrenceFrequency)) ExpandLocalRecurrence(edited);
        }

        async void GoogleClick(object sender, RoutedEventArgs e)
        {
            if (googleConnecting)
            {
                GoogleCalendar.CancelConnect(); googleConnecting = false; UpdateGoogleButton();
                ShowGoogleStatus("Google 로그인을 취소했습니다.", 1500); return;
            }
            if (!GoogleCalendar.IsConnected)
            {
                if (!await ConnectGoogle(true)) return;
                items.Clear();
            }
            await SyncGoogle(true);
        }

        async Task<bool> ConnectGoogle(bool saveLocal)
        {
            try
            {
                if (saveLocal) Store.SaveLocal(items);
                googleConnecting = true; googleButton.IsEnabled = true; googleButton.Content = "로그인 취소";
                await GoogleCalendar.ConnectAsync(); return true;
            }
            catch
            {
                ShowGoogleStatus("Google 로그인 실패 또는 취소", 2000); return false;
            }
            finally { googleConnecting = false; UpdateGoogleButton(); }
        }

        void ShowGoogleStatus(string message, int milliseconds)
        {
            googleStatus.Text = message; googleStatus.Visibility = Visibility.Visible;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); googleStatus.Visibility = Visibility.Hidden; googleStatus.Text = "동기화가 완료되었습니다"; };
            timer.Start();
        }

        async Task SyncGoogle(bool showSuccess)
        {
            if (googleSyncing || !GoogleCalendar.IsConnected) return;
            googleSyncing = true;
            try
            {
                if (showSuccess) { googleButton.IsEnabled = false; googleButton.Content = "동기화 중…"; }
                settings.GoogleCalendars = await GoogleCalendar.SyncAsync(items, settings.GoogleCalendars);
                syncProblem = null;
                var primary = settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
                if (primary != null)
                {
                    settings.ActiveGoogleAccountId = primary.Id;
                    GoogleCalendar.RememberAccount(primary.Id);
                    Store.SetAccount(primary.Id);
                }
                var allowedCalendars = new HashSet<string>(settings.GoogleCalendars.Select(x => x.Id));
                items.RemoveAll(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId) && !allowedCalendars.Contains(x.GoogleCalendarId) && !x.PendingGoogleSync);
                settings.CategoryOrder = (settings.CategoryOrder ?? new List<string>()).Where(x => !x.StartsWith("google:") || allowedCalendars.Contains(x.Substring(7))).ToList();
                Store.Save(items); Store.SaveSettings(settings); BuildGoogleFilters(); RenderAll();
                if (showSuccess)
                {
                    googleStatus.Visibility = Visibility.Visible;
                    await Task.Delay(1000);
                    googleStatus.Visibility = Visibility.Hidden;
                }
            }
            catch (Exception ex)
            {
                syncProblem = IsOffline(ex) ? "오프라인" : "Google 오류"; UpdateAccountStatus();
                if (showSuccess)
                {
                    googleStatus.Text = syncProblem + " · " + ShortGoogleError(ex.Message); googleStatus.Visibility = Visibility.Visible;
                    var hideStatus = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    hideStatus.Tick += delegate { hideStatus.Stop(); googleStatus.Visibility = Visibility.Hidden; googleStatus.Text = "동기화가 완료되었습니다"; };
                    hideStatus.Start();
                }
            }
            finally { googleSyncing = false; if (showSuccess) { googleButton.IsEnabled = true; UpdateGoogleButton(); } }
        }

        void StartAutoSync()
        {
            if (autoSyncTimer != null) autoSyncTimer.Stop();
            if (settings.AutoSyncMinutes <= 0 || !GoogleCalendar.IsConnected) return;
            autoSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(settings.AutoSyncMinutes) };
            autoSyncTimer.Tick += async delegate { await SyncGoogle(false); };
            autoSyncTimer.Start();
        }

        async Task SaveGoogleItem(PlannerItem item, bool wholeSeries = false)
        {
            try { if (wholeSeries) await GoogleCalendar.UpsertSeriesAsync(item); else await GoogleCalendar.UpsertAsync(item); item.PendingGoogleSync = false; syncProblem = null; AttachPrimaryCalendar(item); Store.Save(items); RenderAll(); UpdateAccountStatus(); }
            catch (Exception ex)
            {
                item.PendingGoogleSync = true; syncProblem = IsOffline(ex) ? "오프라인" : "Google 오류"; Store.Save(items); UpdateAccountStatus();
                ShowItemNotice(item, "로컬 저장됨 · " + ShortGoogleError(ex.Message));
            }
        }

        static string ShortGoogleError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "다시 동기화해 주세요";
            if (message.IndexOf("Bad Request", StringComparison.OrdinalIgnoreCase) >= 0) return "반복 일정 형식을 확인해 주세요";
            if (message.IndexOf("time zone", StringComparison.OrdinalIgnoreCase) >= 0) return "일정 시간대를 확인해 주세요";
            if (message.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0) return "수정 권한을 확인해 주세요";
            if (message.IndexOf("Unauthorized", StringComparison.OrdinalIgnoreCase) >= 0) return "Google 계정을 다시 연결해 주세요";
            return "Google 요청을 처리하지 못했습니다";
        }

        static bool IsOffline(Exception ex) { return ex is HttpRequestException || ex is TaskCanceledException; }

        void AttachPrimaryCalendar(PlannerItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.GoogleCalendarId)) return;
            var primary = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
            if (primary == null) return;
            item.GoogleCalendarId = primary.Id; item.GoogleCalendarName = primary.Name;
            item.GoogleCalendarColor = primary.Color; item.GoogleReadOnly = false;
        }

        void UpdateGoogleButton()
        {
            if (googleButton == null) return;
            googleButton.Content = GoogleCalendar.IsConnected ? "G 동기화" : "G 연결";
            googleButton.Background = GoogleCalendar.IsConnected ? Brush("#DBEAFE") : Brushes.White;
            UpdateAccountStatus();
        }

        void UpdateAccountStatus()
        {
            if (accountStatus == null) return;
            var primary = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
            if (GoogleCalendar.IsConnected)
            {
                var pending = items.Count(x => x.PendingGoogleSync && !string.IsNullOrWhiteSpace(x.GoogleCalendarId));
                var state = syncProblem != null
                    ? " · " + syncProblem + (pending > 0 ? " (동기화 대기 " + pending + "건)" : "")
                    : pending > 0 ? " · 동기화 대기 " + pending + "건" : " · Gmail";
                accountStatus.Text = "G  " + (primary == null ? "Google 계정" : primary.Name) + state;
                accountStatus.Foreground = syncProblem != null || pending > 0 ? Brush("#DB2777") : Brush("#4338CA");
            }
            else
            {
                accountStatus.Text = "●  로그아웃됨 · 로컬 저장";
                accountStatus.Foreground = Brush("#7C3AED");
            }
            accountStatus.ToolTip = accountStatus.Text;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(StartAccountMarquee));
        }

        void StartAccountMarquee()
        {
            accountStatusShift.BeginAnimation(TranslateTransform.XProperty, null); accountStatusShift.X = 0;
            var overflow = accountStatus.ActualWidth - accountStatusViewport.ActualWidth;
            if (overflow <= 2) return;
            var animation = new System.Windows.Media.Animation.DoubleAnimation(0, -overflow,
                TimeSpan.FromSeconds(Math.Max(3, overflow / 18))) { AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(1) };
            accountStatusShift.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        void OpenPendingSync(object sender, MouseButtonEventArgs e)
        {
            var pending = items.Where(x => x.PendingGoogleSync && !string.IsNullOrWhiteSpace(x.GoogleCalendarId)).OrderBy(x => x.Start).ToList();
            if (pending.Count == 0) { ShowGoogleStatus("모든 일정이 동기화되었습니다", 1200); return; }
            new PendingSyncWindow(pending) { Owner = this }.ShowDialog();
        }

        async void ShowItemNotice(PlannerItem item, string message)
        {
            itemNoticeId = item.Id; itemNoticeText = message; var version = ++itemNoticeVersion;
            selectedDate = item.Start.Date; RenderDetail();
            await Task.Delay(2000);
            if (version != itemNoticeVersion) return;
            itemNoticeId = null; itemNoticeText = null; RenderDetail();
        }

        async void Rollover()
        {
            var changed = items.Where(x => x.IsTodo && !string.IsNullOrWhiteSpace(x.RolloverMode) && !x.GoogleTaskEvent && !x.Completed && x.Start.Date < DateTime.Today).ToList();
            foreach (var item in changed)
            {
                var duration = item.End - item.Start;
                var nextDate = NextRolloverDate(item.Start.Date, item.RolloverMode);
                item.Start = nextDate.Add(item.AllDay ? TimeSpan.Zero : item.Start.TimeOfDay);
                item.End = item.Start.Add(item.AllDay ? TimeSpan.FromDays(1) : duration);
            }
            if (changed.Count == 0) return;
            Store.Save(items); RenderAll();
            if (GoogleCalendar.IsConnected)
                foreach (var item in changed.Where(x => x.Category == "개인일정" && !string.IsNullOrWhiteSpace(x.GoogleEventId)))
                    try { await GoogleCalendar.UpsertAsync(item); } catch { }
            Store.Save(items);
        }

        void CheckReminders()
        {
            var now = DateTime.Now; var due = new List<PlannerItem>(); var keys = new Dictionary<string, string>();
            foreach (var item in items.Where(x => x.ReminderConfigured && x.ReminderMinutes >= 0 && !x.Completed))
            {
                var key = item.Id + "|" + item.Start.ToString("o") + "|" + item.ReminderMinutes;
                if (item.ReminderDismissedKey == key) continue;
                var target = item.SnoozeUntil > now.AddMinutes(-2) ? item.SnoozeUntil : (item.AllDay ? item.Start.Date.AddHours(9) : item.Start).AddMinutes(-item.ReminderMinutes);
                if (now >= target && now < target.AddMinutes(2) && shownReminders.Add(key)) { due.Add(item); keys[item.Id] = key; }
            }
            if (due.Count > 0) new ReminderWindow(due, delegate(int? snooze)
            {
                foreach (var item in due)
                {
                    var key = keys[item.Id];
                    if (snooze.HasValue) { item.SnoozeUntil = DateTime.Now.AddMinutes(snooze.Value); shownReminders.Remove(key); }
                    else { item.ReminderDismissedKey = key; item.SnoozeUntil = DateTime.MinValue; }
                }
                Store.Save(items);
            }) { Owner = null }.Show();
        }

        static DateTime NextRolloverDate(DateTime date, string mode)
        {
            do
            {
                date = mode == "next_week" ? date.AddDays(7) : date.AddDays(1);
                if (mode == "next_weekday")
                    while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) date = date.AddDays(1);
            }
            while (date < DateTime.Today);
            return date;
        }

        void UpdateModeButtons()
        {
            if (lockButton == null) return;
            lockButton.Content = positionLocked ? "📌 고정됨" : "📍 이동 가능";
            lockButton.ToolTip = positionLocked ? "클릭하면 위치 잠금 해제" : "클릭하면 현재 위치에 고정";
            lockButton.Background = positionLocked ? Brush("#DCFCE7") : Brushes.White;
            lockButton.Foreground = positionLocked ? Brush("#15803D") : Brush("#475569");
            if (resizeHandle != null) resizeHandle.Visibility = positionLocked ? Visibility.Collapsed : Visibility.Visible;
        }

        void CreateTrayIcon()
        {
            trayIcon = new Forms.NotifyIcon { Icon = Drawing.SystemIcons.Application, Text = "온하루", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("편집 모드 열기", null, delegate
            {
                Show(); Activate();
            });
            menu.Items.Add("종료", null, delegate { Close(); });
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate
            {
                Show(); Activate();
            };
        }

        static Button Button(string text, RoutedEventHandler click, double width)
        {
            var button = new Button { Content = text, Width = width, Height = 34, Margin = new Thickness(5, 0, 0, 0),
                Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(8, 0, 8, 0));
            border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = border };
            if (click != null) button.Click += click; return button;
        }
        static bool HasInteractiveParent(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button || source is Slider || source is CheckBox) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        static Brush PastelBrush(string hex, double whiteRatio)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var r = (byte)(color.R + (255 - color.R) * whiteRatio);
            var g = (byte)(color.G + (255 - color.G) * whiteRatio);
            var b = (byte)(color.B + (255 - color.B) * whiteRatio);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
        double Ui(double baseSize) { return baseSize * (settings.FontSize > 0 ? settings.FontSize / 12.0 : 1); }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }

    public static class DesktopLayer
    {
        const int GWLP_HWNDPARENT = -8;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x20;
        const long WS_EX_NOACTIVATE = 0x08000000L;
        const int WM_HOTKEY = 0x0312;
        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int HTBOTTOMRIGHT = 17;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_ALT = 0x0001;
        const uint VK_F = 0x46;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        static extern int SetWindowLong32(IntPtr window, int index, int value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        static extern int GetWindowLong32(IntPtr window, int index);
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        static void SetOwner(IntPtr window, IntPtr owner)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(window, GWLP_HWNDPARENT, owner);
            else SetWindowLong32(window, GWLP_HWNDPARENT, owner.ToInt32());
        }

        static long GetStyle(IntPtr window)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(window, GWL_EXSTYLE).ToInt64() : GetWindowLong32(window, GWL_EXSTYLE);
        }

        static void SetStyle(IntPtr window, long style)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(window, GWL_EXSTYLE, new IntPtr(style));
            else SetWindowLong32(window, GWL_EXSTYLE, (int)style);
        }

        public static void SetClickThrough(Window window, bool enabled)
        {
            var handle = new WindowInteropHelper(window).Handle;
            var style = GetStyle(handle);
            SetStyle(handle, enabled ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT);
        }

        public static void InstallInteractionToggle(Window window, Action toggle)
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            RegisterHotKey(source.Handle, 7419, MOD_CONTROL | MOD_ALT, VK_F);
            source.AddHook(delegate(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (message == WM_HOTKEY && wParam.ToInt32() == 7419) { toggle(); handled = true; }
                return IntPtr.Zero;
            });
        }

        public static void Attach(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            var desktop = FindWindow("Progman", null);
            if (handle == IntPtr.Zero || desktop == IntPtr.Zero) return;
            SetOwner(handle, desktop);
            SetStyle(handle, GetStyle(handle) | WS_EX_NOACTIVATE);
            window.Topmost = false;
            window.ShowInTaskbar = false;
            Lower(window);
        }

        public static void Detach(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            SetOwner(handle, IntPtr.Zero);
            window.Topmost = false;
            window.ShowInTaskbar = true;
        }

        public static void Lower(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
                SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public static void BeginResize(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            ReleaseCapture();
            SendMessage(handle, WM_NCLBUTTONDOWN, new IntPtr(HTBOTTOMRIGHT), IntPtr.Zero);
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            bool first;
            using (var singleInstance = new Mutex(true, "Local\\OnharuSingleInstance", out first))
            {
                if (!first) return;
                try { var app = new Application(); app.Run(new MainWindow()); }
                finally { singleInstance.ReleaseMutex(); }
            }
        }
    }
}
