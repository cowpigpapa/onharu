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
        const string ClientId = "397166784516-g8l18umimg4uvp3l4tjcnlguedoa4c1j.apps.googleusercontent.com";
        const string ClientSecret = OAuthCredentials.ClientSecret;
        const string Scope = "openid email https://www.googleapis.com/auth/calendar.events https://www.googleapis.com/auth/calendar.calendarlist.readonly https://www.googleapis.com/auth/tasks";
        static readonly string TokenPath = Path.Combine(AppDataPaths.Root, "google-token.dat");
        static readonly string AccountPath = Path.Combine(AppDataPaths.Root, "google-account.dat");
        static readonly string TasksScopePath = Path.Combine(AppDataPaths.Root, "google-tasks-scope.dat");
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        static string accessToken;
        static string identityToken;
        static DateTime expiresAt;
        static HttpListener pendingListener;

        public static bool IsConnected { get { return File.Exists(TokenPath); } }
        public static bool HasTasksPermission { get { return File.Exists(TasksScopePath); } }
        public static async Task<string> AccessTokenAsync() { await EnsureToken(); return accessToken; }
        public static async Task<string> IdentityTokenAsync()
        {
            await EnsureToken();
            if (string.IsNullOrWhiteSpace(identityToken))
                throw new InvalidOperationException("메일 백업 보안을 위해 Google 계정을 한 번 다시 연결해 주세요.");
            return identityToken;
        }
        public static string ConnectedAccountId
        {
            get
            {
                try { return File.Exists(AccountPath) ? Unprotect(File.ReadAllBytes(AccountPath)) : null; }
                catch (Exception ex) { ErrorLog.Write("Read connected Google account", ex); return null; }
            }
        }
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
            string returnedError = null; string returnedState = null; string returnedCode = null;
            try
            {
                var contextTask = listener.GetContextAsync();
                if (await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(5))) != contextTask)
                    throw new TimeoutException("Google 로그인이 취소되었거나 시간이 초과되었습니다.");
                var context = await contextTask;
                var query = context.Request.QueryString;
                returnedError = query["error"]; returnedState = query["state"]; returnedCode = query["code"];
                var responseText = returnedError == null ? "온하루 연결이 완료되었습니다. 이 창을 닫아도 됩니다." : "온하루 연결이 취소되었습니다.";
                var bytes = Encoding.UTF8.GetBytes("<html><meta charset='utf-8'><body style='font-family:sans-serif;padding:40px'><h2>" + responseText + "</h2></body></html>");
                context.Response.ContentType = "text/html; charset=utf-8"; context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length); context.Response.Close();
            }
            finally
            {
                if (ReferenceEquals(pendingListener, listener)) pendingListener = null;
                try { if (listener.IsListening) listener.Stop(); } catch { }
                try { listener.Close(); } catch { }
            }
            if (returnedError != null) throw new InvalidOperationException(returnedError);
            if (returnedState != state) throw new InvalidOperationException("Google 로그인 응답을 확인할 수 없습니다.");
            var token = await TokenRequest("code=" + E(returnedCode) + "&client_id=" + E(ClientId) + "&client_secret=" + E(ClientSecret) +
                "&redirect_uri=" + E(redirect) + "&grant_type=authorization_code&code_verifier=" + E(verifier));
            if (string.IsNullOrWhiteSpace(token.RefreshToken)) throw new InvalidOperationException("Google 갱신 토큰을 받지 못했습니다.");
            SaveRefreshToken(token.RefreshToken); SetAccessToken(token); File.WriteAllText(TasksScopePath, "1");
            var calendars = await ReadCalendarListAsync();
            var primary = calendars.FirstOrDefault(x => x.Primary);
            if (primary != null) File.WriteAllBytes(AccountPath, Protect(primary.Id));
        }

        public static void Disconnect()
        {
            accessToken = null; identityToken = null; expiresAt = DateTime.MinValue;
            if (File.Exists(TokenPath)) File.Delete(TokenPath);
            if (File.Exists(AccountPath)) File.Delete(AccountPath);
            if (File.Exists(TasksScopePath)) File.Delete(TasksScopePath);
        }

        public static void CancelConnect()
        {
            var listener = pendingListener; pendingListener = null;
            if (listener != null && listener.IsListening) listener.Stop();
        }

        public static async Task<List<GoogleCalendarSetting>> SyncAsync(List<PlannerItem> local, List<GoogleCalendarSetting> saved)
        {
            await EnsureToken();
            var uploadedEventIds = new HashSet<string>();
            foreach (var item in local.Where(x => !x.GoogleTaskEvent && !string.IsNullOrWhiteSpace(x.GoogleCalendarId) && (string.IsNullOrWhiteSpace(x.GoogleEventId) || x.PendingGoogleSync)).ToList())
            {
                await UpsertAsync(item); item.PendingGoogleSync = false;
                if (!string.IsNullOrWhiteSpace(item.GoogleEventId)) uploadedEventIds.Add(item.GoogleEventId);
            }

            var from = DateTime.Today.AddYears(-1).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            var to = DateTime.Today.AddYears(2).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            var entries = await ReadCalendarListAsync();
            var calendars = new List<GoogleCalendarSetting>();
            var calendarReads = new List<Tuple<GoogleCalendarEntry, GoogleCalendarSetting, Task<List<GoogleEvent>>>>();
            foreach (var entry in entries.Where(x => !x.Hidden))
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
                calendarReads.Add(Tuple.Create(entry, calendar, ReadEventsAsync(entry.Id, from, to)));
            }
            foreach (var read in calendarReads)
            {
                var entry = read.Item1; var calendar = read.Item2; var remoteEvents = await read.Item3;
                var remoteIds = new HashSet<string>();
                foreach (var remote in remoteEvents.Where(x => x.Status != "cancelled" && x.Start != null && x.End != null))
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
                local.RemoveAll(x => !string.IsNullOrWhiteSpace(x.GoogleEventId) && x.GoogleCalendarId == entry.Id &&
                    InSyncRange(x.Start) && !x.PendingGoogleSync && !uploadedEventIds.Contains(x.GoogleEventId) &&
                    !remoteIds.Contains(x.GoogleEventId));
            }
            return calendars;
        }

        public static async Task UpsertAsync(PlannerItem item)
        {
            if (item.GoogleEventType == "birthday") { item.PendingGoogleSync = false; return; }
            await EnsureToken();
            try
            {
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
            catch (InvalidOperationException ex)
            {
                if (!IsBirthdayRestriction(ex)) throw;
                item.GoogleEventType = "birthday"; item.PendingGoogleSync = false;
            }
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
            try
            {
                await Send<object>(HttpMethod.Delete, "https://www.googleapis.com/calendar/v3/calendars/" + E(calendarId) + "/events/" + E(eventId), null);
            }
            catch (InvalidOperationException error)
            {
                // 동기화 전에 Google 쪽에서 먼저 삭제된 일정은 원하는 최종 상태가
                // 이미 달성된 것이므로 로컬 캐시 삭제를 계속 진행한다.
                if (!IsAlreadyDeleted(error)) throw;
            }
        }

        internal static bool IsAlreadyDeleted(Exception error)
        {
            var message = error == null ? null : error.Message;
            return !string.IsNullOrWhiteSpace(message) &&
                (message.IndexOf("Resource has been deleted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 message.IndexOf("\"code\": 410", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 message.IndexOf("\"code\": 404", StringComparison.OrdinalIgnoreCase) >= 0);
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
            if (!response.IsSuccessStatusCode)
            {
                var message = token.ErrorDescription ?? token.Error ?? "Google 로그인에 실패했습니다.";
                var error = new InvalidOperationException(message);
                ErrorLog.Write("Google OAuth token request", error, "HTTP " + (int)response.StatusCode + " · " + (token.Error ?? "unknown_error"));
                throw error;
            }
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
                { "onharu", "1" }, { "onharuTodo", item.IsTodo ? "1" : "0" },
                { "onharuCategory", item.Category ?? "개인일정" },
                { "onharuRollover", string.IsNullOrWhiteSpace(item.RolloverMode) ? "0" : "1" },
                { "onharuRolloverMode", item.RolloverMode ?? "none" }, { "onharuReminder", item.ReminderMinutes.ToString() },
                { "onharuImportant", item.Important ? "1" : "0" }, { "onharuDday", item.ShowDday ? "1" : "0" },
                { "onharuImportantBackground", item.ImportantBackgroundColor ?? "" }, { "onharuImportantText", item.ImportantTextColor ?? "" },
                { "onharuSportsGameId", item.SportsGameId ?? "" },
                { "onharuAnniversaryDate", item.AnniversaryDate.Year >= 1900 ? item.AnniversaryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "" },
                { "onharuRecurrence", item.RecurrenceFrequency ?? "" },
                { "onharuRecurrenceMode", item.RecurrenceMode ?? "" }, { "onharuRecurrenceDays", item.RecurrenceDays ?? "" },
                { "onharuRecurrenceCount", item.RecurrenceCount.ToString(CultureInfo.InvariantCulture) } } } };
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
            item.GoogleEventType = e.EventType;
            item.GoogleCalendarId = calendar.Id; item.GoogleCalendarName = calendar.Name; item.GoogleCalendarColor = calendar.Color;
            var special = !string.IsNullOrWhiteSpace(e.EventType) && e.EventType != "default";
            item.GoogleReadOnly = !calendar.Editable || special;
            item.GoogleRecurringEventId = e.RecurringEventId;
            var holiday = (calendar.Name ?? "").Contains("공휴일") || (calendar.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
            string value; var p = e.ExtendedProperties == null ? null : e.ExtendedProperties.Private;
            item.Category = holiday ? "국경일" : p != null && p.TryGetValue("onharuCategory", out value) && value == "야구" ? "야구" : "개인일정";
            item.GoogleTaskEvent = false;
            item.Notes = e.Description;
            item.AllDay = !string.IsNullOrWhiteSpace(e.Start.Date);
            item.Start = item.AllDay ? System.DateTime.ParseExact(e.Start.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture) : DateTimeOffset.Parse(e.Start.DateTime, CultureInfo.InvariantCulture).LocalDateTime;
            item.End = item.AllDay ? System.DateTime.ParseExact(e.End.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture) : DateTimeOffset.Parse(e.End.DateTime, CultureInfo.InvariantCulture).LocalDateTime;
            item.OnharuManaged = p != null && p.TryGetValue("onharu", out value) && value == "1";
            if (p != null && p.TryGetValue("onharuSportsGameId", out value) && !string.IsNullOrWhiteSpace(value)) item.SportsGameId = value;
            item.IsTodo = !special && (p != null && p.TryGetValue("onharuTodo", out value) ? value == "1" : !item.AllDay);
            int reminder;
            if (p != null && p.TryGetValue("onharuReminder", out value) && int.TryParse(value, out reminder))
            { item.ReminderMinutes = NormalizeReminderMinutes(reminder, item.AllDay); item.ReminderConfigured = true; }
            else if (!item.ReminderConfigured) { item.ReminderMinutes = item.AllDay ? -1 : 10; item.ReminderConfigured = true; }
            if (p != null && p.TryGetValue("onharuImportant", out value)) item.Important = value == "1";
            item.ImportantBackgroundColor = p != null && p.TryGetValue("onharuImportantBackground", out value) ? value : null;
            item.ImportantTextColor = p != null && p.TryGetValue("onharuImportantText", out value) ? value : null;
            if (p != null && p.TryGetValue("onharuDday", out value)) item.ShowDday = value == "1";
            DateTime anniversaryDate;
            if (p != null && p.TryGetValue("onharuAnniversaryDate", out value) &&
                DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out anniversaryDate))
                item.AnniversaryDate = anniversaryDate.Date;
            item.RecurrenceFrequency = p != null && p.TryGetValue("onharuRecurrence", out value) ? NormalizeRecurrenceFrequency(value) : null;
            item.RecurrenceMode = p != null && p.TryGetValue("onharuRecurrenceMode", out value) ? NormalizeRecurrenceMode(item.RecurrenceFrequency, value) : null;
            item.RecurrenceDays = p != null && p.TryGetValue("onharuRecurrenceDays", out value) ? NormalizeRecurrenceDays(item.RecurrenceFrequency, item.RecurrenceMode, value, item.Start) : null;
            int recurrenceCount;
            item.RecurrenceCount = p != null && p.TryGetValue("onharuRecurrenceCount", out value) && int.TryParse(value, out recurrenceCount) ? Math.Max(0, recurrenceCount) : 0;
            item.RolloverMode = !item.GoogleTaskEvent && p != null && p.TryGetValue("onharuRolloverMode", out value) ? NormalizeRolloverMode(value) : null;
            if (string.IsNullOrWhiteSpace(item.RolloverMode) && !item.GoogleTaskEvent && p != null && p.TryGetValue("onharuRollover", out value) && value == "1") item.RolloverMode = "next_day";
            item.AutoRollover = !string.IsNullOrWhiteSpace(item.RolloverMode);
        }

        static async Task<List<GoogleCalendarEntry>> ReadCalendarListAsync()
        {
            var result = new List<GoogleCalendarEntry>(); var seen = new HashSet<string>(); string token = null;
            do
            {
                var page = await Send<GoogleCalendarList>(HttpMethod.Get, PageUrl("https://www.googleapis.com/calendar/v3/users/me/calendarList?maxResults=250", token), null);
                result.AddRange(page.Items ?? new List<GoogleCalendarEntry>()); token = page.NextPageToken;
                if (!string.IsNullOrWhiteSpace(token) && !seen.Add(token)) throw new InvalidOperationException("Google 캘린더 목록 페이지가 반복되었습니다.");
            } while (!string.IsNullOrWhiteSpace(token));
            return result;
        }

        static async Task<List<GoogleEvent>> ReadEventsAsync(string calendarId, string from, string to)
        {
            var url = "https://www.googleapis.com/calendar/v3/calendars/" + E(calendarId) +
                "/events?singleEvents=true&maxResults=2500&timeMin=" + E(from) + "&timeMax=" + E(to);
            var result = new List<GoogleEvent>(); var seen = new HashSet<string>(); string token = null;
            do
            {
                var page = await Send<GoogleEvents>(HttpMethod.Get, PageUrl(url, token), null);
                result.AddRange(page.Items ?? new List<GoogleEvent>()); token = page.NextPageToken;
                if (!string.IsNullOrWhiteSpace(token) && !seen.Add(token)) throw new InvalidOperationException("Google 일정 페이지가 반복되었습니다.");
            } while (!string.IsNullOrWhiteSpace(token));
            return result;
        }

        internal static string PageUrl(string url, string token)
        {
            return string.IsNullOrWhiteSpace(token) ? url : url + (url.Contains("?") ? "&" : "?") + "pageToken=" + E(token);
        }

        internal static int NormalizeReminderMinutes(int value, bool allDay)
        {
            return value == -1 || (value >= 0 && value <= 999 * 1440) ? value : allDay ? -1 : 10;
        }

        internal static bool IsBirthdayRestriction(Exception error)
        {
            var message = error == null ? null : error.Message;
            return !string.IsNullOrWhiteSpace(message) && message.IndexOf("eventTypeRestriction", StringComparison.OrdinalIgnoreCase) >= 0 &&
                message.IndexOf("'birthday'", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string NormalizeRolloverMode(string value)
        {
            return value == "next_day" || value == "next_week" || value == "next_weekday" ? value : null;
        }

        internal static string NormalizeRecurrenceFrequency(string value)
        {
            return value == "daily" || value == "weekly" || value == "monthly" || value == "yearly" ? value : null;
        }

        internal static string NormalizeRecurrenceMode(string frequency, string value)
        {
            if (frequency == "daily") return value == "weekdays" ? "weekdays" : "daily";
            if (frequency == "weekly") return "weekly";
            if (frequency == "monthly") return value == "monthly_last" || value == "monthly_nth" ? value : "monthly_date";
            return frequency == "yearly" ? (value == "yearly_nth" ? "yearly_nth" : "yearly_date") : null;
        }

        internal static string NormalizeRecurrenceDays(string frequency, string mode, string value, DateTime start)
        {
            var days = new HashSet<string>(new[] { "MO", "TU", "WE", "TH", "FR", "SA", "SU" });
            if (frequency == "weekly")
            {
                var selected = (value ?? "").Split(',').Where(days.Contains).Distinct().ToList();
                return selected.Count == 0 ? RecurrenceService.DayCode(start.DayOfWeek) : string.Join(",", selected);
            }
            if (frequency == "monthly" && mode == "monthly_nth")
            {
                var code = string.IsNullOrWhiteSpace(value) || value.Length < 3 ? null : value.Substring(value.Length - 2);
                int ordinal;
                if (code == null || !days.Contains(code) || !int.TryParse(value.Substring(0, value.Length - 2), out ordinal) ||
                    (ordinal != -1 && (ordinal < 1 || ordinal > 5))) return RecurrenceService.MonthlyNthCode(start);
                return ordinal + code;
            }
            if (frequency == "yearly" && mode == "yearly_nth")
            {
                var code = string.IsNullOrWhiteSpace(value) || value.Length < 3 ? null : value.Substring(value.Length - 2);
                int ordinal;
                if (code == null || !days.Contains(code) || !int.TryParse(value.Substring(0, value.Length - 2), out ordinal) ||
                    (ordinal != -1 && (ordinal < 1 || ordinal > 5))) return RecurrenceService.MonthlyNthCode(start);
                return ordinal + code;
            }
            return null;
        }

        static bool InSyncRange(DateTime value) { return value >= DateTime.Today.AddYears(-1) && value < DateTime.Today.AddYears(2); }
        static void SetAccessToken(GoogleToken token)
        {
            accessToken = token.AccessToken;
            if (!string.IsNullOrWhiteSpace(token.IdToken)) identityToken = token.IdToken;
            expiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn));
        }
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
