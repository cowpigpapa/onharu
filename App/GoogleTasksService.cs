using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace FamilyPlanner
{
    static class GoogleTasks
    {
        const string SourcePrefix = "tasks:";
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public static bool IsSource(string id) { return !string.IsNullOrWhiteSpace(id) && id.StartsWith(SourcePrefix, StringComparison.Ordinal); }
        public static bool IsTask(PlannerItem item) { return item != null && item.GoogleTaskEvent && IsSource(item.GoogleCalendarId); }

        public static async Task<List<GoogleCalendarSetting>> SyncAsync(List<PlannerItem> local, List<GoogleCalendarSetting> saved)
        {
            foreach (var pending in local.Where(x => IsTask(x) && x.PendingGoogleSync).ToList())
            {
                if (pending.OnharuManaged) await UpsertAsync(pending);
                else await SetCompletedAsync(pending, pending.Completed);
                pending.PendingGoogleSync = false;
            }

            var lists = await ReadTaskListsAsync();
            var settings = new List<GoogleCalendarSetting>();
            foreach (var list in lists)
            {
                var sourceId = SourcePrefix + list.Id;
                var old = saved == null ? null : saved.FirstOrDefault(x => x.Id == sourceId);
                var source = new GoogleCalendarSetting { Id = sourceId, Name = "Tasks · " + list.Title,
                    Color = old == null || string.IsNullOrWhiteSpace(old.Color) ? "#5B8DEF" : old.Color,
                    OriginalColor = "#5B8DEF", Visible = old == null || old.Visible, Primary = false,
                    AccessRole = "tasks", Editable = old == null || old.Editable };
                settings.Add(source);

                var tasks = await ReadTasksAsync(list.Id);
                var remoteIds = new HashSet<string>();
                foreach (var task in tasks.Where(x => !x.Deleted && !string.IsNullOrWhiteSpace(x.Due)))
                {
                    DateTime due;
                    if (!TryDueDate(task.Due, out due)) continue;
                    remoteIds.Add(task.Id);
                    var item = local.FirstOrDefault(x => IsTask(x) && x.GoogleCalendarId == sourceId && x.GoogleEventId == task.Id);
                    if (item == null) { item = new PlannerItem { Id = Guid.NewGuid().ToString() }; local.Add(item); }
                    Apply(item, task, source, due);
                }
                local.RemoveAll(x => IsTask(x) && x.GoogleCalendarId == sourceId && InSyncRange(x.Start) && !x.PendingGoogleSync && !remoteIds.Contains(x.GoogleEventId));
            }
            return settings;
        }

        public static async Task SetCompletedAsync(PlannerItem item, bool completed)
        {
            if (!IsTask(item)) return;
            var listId = item.GoogleCalendarId.Substring(SourcePrefix.Length);
            var json = completed
                ? "{\"status\":\"completed\",\"completed\":\"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\"}"
                : "{\"status\":\"needsAction\",\"completed\":null}";
            await Send<GoogleTask>(new HttpMethod("PATCH"), "https://tasks.googleapis.com/tasks/v1/lists/" + E(listId) + "/tasks/" + E(item.GoogleEventId), json);
        }

        public static async Task UpsertAsync(PlannerItem item)
        {
            if (!IsTask(item) || !item.OnharuManaged) throw new InvalidOperationException("온하루에서 등록한 Google Task만 수정할 수 있습니다.");
            var listId = item.GoogleCalendarId.Substring(SourcePrefix.Length);
            var payload = new GoogleTaskWrite { Title = item.Title, Notes = item.Notes,
                Due = item.Start.Date.ToString("yyyy-MM-dd'T'00:00:00.000'Z'", CultureInfo.InvariantCulture),
                Status = item.Completed ? "completed" : "needsAction",
                Completed = item.Completed ? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) : null };
            var creating = string.IsNullOrWhiteSpace(item.GoogleEventId);
            var url = "https://tasks.googleapis.com/tasks/v1/lists/" + E(listId) + "/tasks" +
                (creating ? "" : "/" + E(item.GoogleEventId));
            var saved = await Send<GoogleTask>(creating ? HttpMethod.Post : new HttpMethod("PATCH"), url, Json(payload));
            if (saved != null && !string.IsNullOrWhiteSpace(saved.Id)) item.GoogleEventId = saved.Id;
            item.GoogleEventType = "task"; item.GoogleTaskEvent = true; item.OnharuManaged = true;
            item.CreatedInOnharu = true; item.GoogleReadOnly = false; item.PendingGoogleSync = false;
        }

        public static async Task DeleteAsync(PlannerItem item)
        {
            if (!IsTask(item) || string.IsNullOrWhiteSpace(item.GoogleEventId)) return;
            var listId = item.GoogleCalendarId.Substring(SourcePrefix.Length);
            await Send<object>(HttpMethod.Delete, "https://tasks.googleapis.com/tasks/v1/lists/" + E(listId) + "/tasks/" + E(item.GoogleEventId), null);
        }

        static void Apply(PlannerItem item, GoogleTask task, GoogleCalendarSetting source, DateTime due)
        {
            var managed = item.OnharuManaged && item.CreatedInOnharu;
            item.Title = string.IsNullOrWhiteSpace(task.Title) ? "제목 없음" : task.Title;
            item.Start = due.Date; item.End = due.Date.AddDays(1); item.AllDay = true;
            item.IsTodo = true; item.Completed = task.Status == "completed"; item.Category = "개인일정"; item.Notes = task.Notes;
            item.GoogleEventId = task.Id; item.GoogleEventType = "task"; item.GoogleCalendarId = source.Id;
            item.GoogleCalendarName = source.Name; item.GoogleCalendarColor = source.Color; item.GoogleReadOnly = !managed || !source.Editable;
            item.GoogleTaskEvent = true; item.OnharuManaged = managed; item.CreatedInOnharu = managed;
            item.PendingGoogleSync = false;
        }

        static string Json<T>(T value)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        static bool TryDueDate(string value, out DateTime due)
        {
            due = DateTime.MinValue;
            return !string.IsNullOrWhiteSpace(value) && value.Length >= 10 &&
                DateTime.TryParseExact(value.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out due);
        }

        static bool InSyncRange(DateTime date) { return date.Date >= DateTime.Today.AddYears(-1) && date.Date <= DateTime.Today.AddYears(2); }

        static async Task<List<GoogleTaskList>> ReadTaskListsAsync()
        {
            var result = new List<GoogleTaskList>(); string token = null;
            do
            {
                var url = "https://tasks.googleapis.com/tasks/v1/users/@me/lists?maxResults=1000" + (string.IsNullOrWhiteSpace(token) ? "" : "&pageToken=" + E(token));
                var page = await Send<GoogleTaskLists>(HttpMethod.Get, url, null);
                result.AddRange(page.Items ?? new List<GoogleTaskList>()); token = page.NextPageToken;
            } while (!string.IsNullOrWhiteSpace(token));
            return result;
        }

        static async Task<List<GoogleTask>> ReadTasksAsync(string listId)
        {
            var result = new List<GoogleTask>(); string token = null;
            var from = DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd'T'00:00:00.000'Z'", CultureInfo.InvariantCulture);
            var to = DateTime.Today.AddYears(2).AddDays(1).ToString("yyyy-MM-dd'T'00:00:00.000'Z'", CultureInfo.InvariantCulture);
            do
            {
                var url = "https://tasks.googleapis.com/tasks/v1/lists/" + E(listId) + "/tasks?maxResults=100&showCompleted=true&showHidden=true&showAssigned=true&dueMin=" + E(from) + "&dueMax=" + E(to) +
                    (string.IsNullOrWhiteSpace(token) ? "" : "&pageToken=" + E(token));
                var page = await Send<GoogleTaskPage>(HttpMethod.Get, url, null);
                result.AddRange(page.Items ?? new List<GoogleTask>()); token = page.NextPageToken;
            } while (!string.IsNullOrWhiteSpace(token));
            return result;
        }

        static async Task<T> Send<T>(HttpMethod method, string url, string json)
        {
            var token = await GoogleCalendar.AccessTokenAsync();
            using (var request = new HttpRequestMessage(method, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await Http.SendAsync(request); var text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Google Tasks 오류: " + (string.IsNullOrWhiteSpace(text) ? response.StatusCode.ToString() : text));
                if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(text)) return default(T);
                using (var stream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(text)))
                    return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }

        static string E(string value) { return Uri.EscapeDataString(value ?? ""); }
    }

    [DataContract] class GoogleTaskLists
    {
        [DataMember(Name = "items", EmitDefaultValue = false)] public List<GoogleTaskList> Items;
        [DataMember(Name = "nextPageToken", EmitDefaultValue = false)] public string NextPageToken;
    }
    [DataContract] class GoogleTaskList
    {
        [DataMember(Name = "id", EmitDefaultValue = false)] public string Id;
        [DataMember(Name = "title", EmitDefaultValue = false)] public string Title;
    }
    [DataContract] class GoogleTaskPage
    {
        [DataMember(Name = "items", EmitDefaultValue = false)] public List<GoogleTask> Items;
        [DataMember(Name = "nextPageToken", EmitDefaultValue = false)] public string NextPageToken;
    }
    [DataContract] class GoogleTask
    {
        [DataMember(Name = "id", EmitDefaultValue = false)] public string Id;
        [DataMember(Name = "title", EmitDefaultValue = false)] public string Title;
        [DataMember(Name = "notes", EmitDefaultValue = false)] public string Notes;
        [DataMember(Name = "due", EmitDefaultValue = false)] public string Due;
        [DataMember(Name = "status", EmitDefaultValue = false)] public string Status;
        [DataMember(Name = "deleted", EmitDefaultValue = false)] public bool Deleted;
    }
    [DataContract] class GoogleTaskWrite
    {
        [DataMember(Name = "title", EmitDefaultValue = false)] public string Title;
        [DataMember(Name = "notes", EmitDefaultValue = false)] public string Notes;
        [DataMember(Name = "due", EmitDefaultValue = false)] public string Due;
        [DataMember(Name = "status", EmitDefaultValue = false)] public string Status;
        [DataMember(Name = "completed", EmitDefaultValue = false)] public string Completed;
    }
}
