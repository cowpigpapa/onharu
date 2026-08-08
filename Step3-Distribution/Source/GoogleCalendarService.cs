using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FamilyPlanner
{
    static class GoogleCalendar
    {
        const string ClientId = "397166784516-v2rap5v944sp38g0h5mnoo3v3nsqkjgg.apps.googleusercontent.com";
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
            var token = await TokenRequest("code=" + E(query["code"]) + "&client_id=" + E(ClientId) +
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
                body.Recurrence = new List<string> { RecurrenceService.GoogleRecurrenceRule(item) };
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
            var token = await TokenRequest("client_id=" + E(ClientId) + "&refresh_token=" + E(LoadRefreshToken()) + "&grant_type=refresh_token");
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
                e.Recurrence = new List<string> { RecurrenceService.GoogleRecurrenceRule(item) };
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
}
