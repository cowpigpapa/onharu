using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FamilyPlanner
{
    internal static class SportsApiKeyStore
    {
        static readonly string KeyPath = Path.Combine(AppDataPaths.Root, "kbo-api-key.dat");
        internal static bool HasKey { get { return !string.IsNullOrWhiteSpace(Load()); } }
        internal static string Load()
        {
            try { return File.Exists(KeyPath) ? Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser)) : null; }
            catch (Exception ex) { ErrorLog.Write("Load KBO API key", ex); return null; }
        }
        internal static void Save(string key)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(KeyPath));
            File.WriteAllBytes(KeyPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser));
        }
        internal static void Delete()
        {
            try { if (File.Exists(KeyPath)) File.Delete(KeyPath); }
            catch (Exception ex) { ErrorLog.Write("Delete KBO API key", ex); }
        }
    }

    internal static class SportsApi
    {
        const string Endpoint = "https://api.parse.bot/scraper/91af86ff-58ff-41cd-98e1-26887b18cb09/get_schedule";
        static readonly Dictionary<string, List<SportsGame>> MemoryCache = new Dictionary<string, List<SportsGame>>();
        internal static async Task<string> ValidateKey(string key)
        {
            try { SaveCache(DateTime.Today.Year, DateTime.Today.Month, await Fetch(DateTime.Today.Year, DateTime.Today.Month, key)); return null; }
            catch (UnauthorizedAccessException) { return "Parse.bot API 키를 확인해 주세요."; }
            catch (TaskCanceledException) { return "Parse.bot 응답 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요."; }
            catch (SerializationException ex) { ErrorLog.Write("Parse KBO response format", ex); return "Parse.bot 응답 형식이 예상과 다릅니다. ONHARU 기록을 확인해 주세요."; }
            catch (HttpRequestException ex) { ErrorLog.Write("Connect Parse KBO API", ex); return "Parse.bot HTTPS 연결에 실패했습니다. 방화벽 또는 보안 프로그램을 확인해 주세요."; }
            catch (InvalidOperationException ex) { return ex.Message; }
            catch (Exception ex) { ErrorLog.Write("Validate Parse KBO API", ex); return "API 연결을 확인하지 못했습니다: " + ex.Message; }
        }
        internal static async Task<List<SportsGame>> KboGames(int year, int month, bool refresh)
        {
            var cacheKey = year + month.ToString("00"); List<SportsGame> memory;
            if (!refresh && MemoryCache.TryGetValue(cacheKey, out memory)) return memory;
            var cachePath = CachePath(year, month);
            if (!refresh && File.Exists(cachePath)) { memory = Read<List<SportsGame>>(File.ReadAllBytes(cachePath)) ?? new List<SportsGame>(); MemoryCache[cacheKey] = memory; return memory; }
            var key = SportsApiKeyStore.Load();
            if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Parse.bot API 키가 연결되지 않았습니다.");
            var games = await Fetch(year, month, key);
            SaveCache(year, month, games); MemoryCache[cacheKey] = games;
            return games;
        }
        internal static bool HasCachedMonth(int year, int month) { return File.Exists(CachePath(year, month)); }
        internal static string RegistrationId(SportsGame game) { return "parse-kbo:" + game.LocalStart.ToString("yyyyMMdd") + "-" + game.Title; }
        internal static string RegistrationId(PlannerItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.SportsGameId) &&
                (string.IsNullOrWhiteSpace(item.Notes) || !item.Notes.StartsWith("KBO 경기 일정", StringComparison.Ordinal))) return null;
            return "parse-kbo:" + item.Start.ToString("yyyyMMdd") + "-" + (item.Title ?? "").TrimStart('⚾', ' ');
        }
        static async Task<List<SportsGame>> Fetch(int year, int month, string key)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.TryAddWithoutValidation("X-API-Key", key.Trim());
                request.Content = new StringContent("{\"year\":\"" + year + "\",\"month\":\"" + month.ToString("00") + "\"}", Encoding.UTF8, "application/json");
                using (var response = await client.SendAsync(request))
                {
                    if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403) throw new UnauthorizedAccessException();
                    if ((int)response.StatusCode == 429) throw new InvalidOperationException("Parse.bot 호출 한도에 도달했습니다. 잠시 후 다시 시도해 주세요.");
                    response.EnsureSuccessStatusCode();
                    var result = Read<ParseKboResponse>(await response.Content.ReadAsByteArrayAsync());
                    if (result == null || !string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("KBO 일정 응답을 확인하지 못했습니다.");
                    var games = result.Data == null || result.Data.Games == null ? new List<SportsGame>() : result.Data.Games;
                    foreach (var game in games) { game.Year = year; game.Month = month; }
                    return games;
                }
            }
        }
        static string CachePath(int year, int month) { return Path.Combine(AppDataPaths.Root, "sports-kbo-parse-" + year + month.ToString("00") + ".json"); }
        static void SaveCache(int year, int month, List<SportsGame> games)
        {
            var path = CachePath(year, month); Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = File.Create(path)) new DataContractJsonSerializer(typeof(List<SportsGame>)).WriteObject(stream, games);
        }
        static T Read<T>(byte[] bytes) { using (var stream = new MemoryStream(bytes)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
    }

    [DataContract] internal sealed class ParseKboResponse { [DataMember(Name = "status")] public string Status; [DataMember(Name = "data")] public ParseKboData Data; }
    [DataContract] internal sealed class ParseKboData { [DataMember(Name = "games")] public List<SportsGame> Games = new List<SportsGame>(); }
    [DataContract] internal sealed class SportsGame
    {
        [DataMember(Name = "date")] public string DateText;
        [DataMember(Name = "time")] public string Time;
        [DataMember(Name = "away_team")] public string AwayTeam;
        [DataMember(Name = "home_team")] public string HomeTeam;
        [DataMember(Name = "stadium")] public string Stadium;
        [DataMember(Name = "away_score", EmitDefaultValue = false)] public string AwayScore;
        [DataMember(Name = "home_score", EmitDefaultValue = false)] public string HomeScore;
        [DataMember(Name = "game_status", EmitDefaultValue = false)] public string GameStatus;
        [DataMember] public int Year;
        [DataMember] public int Month;
        internal string Id { get { return Year + "-" + Digits(DateText) + "-" + Time + "-" + AwayTeam + "-" + HomeTeam; } }
        internal string MatchKey { get { return Digits(DateText) + "-" + AwayTeam + "-" + HomeTeam; } }
        internal string Fingerprint { get { return MatchKey + "-" + Time + "-" + Stadium + "-" + AwayScore + "-" + HomeScore + "-" + GameStatus; } }
        internal bool IsCancelled { get { var value = (Time ?? "") + " " + (GameStatus ?? ""); return value.Contains("취소") || value.Contains("우천"); } }
        internal bool HasScore { get { return !string.IsNullOrWhiteSpace(AwayScore) && !string.IsNullOrWhiteSpace(HomeScore); } }
        internal DateTime LocalStart
        {
            get
            {
                var digits = Digits(DateText); int parsedMonth = Month, day = 1, hour = 0, minute = 0;
                if (digits.Length >= 4) { int.TryParse(digits.Substring(0, 2), out parsedMonth); int.TryParse(digits.Substring(2, 2), out day); }
                TimeSpan parsed; if (TimeSpan.TryParse(Time, out parsed)) { hour = parsed.Hours; minute = parsed.Minutes; }
                parsedMonth = Math.Max(1, Math.Min(12, parsedMonth)); day = Math.Max(1, Math.Min(DateTime.DaysInMonth(Year, parsedMonth), day));
                return new DateTime(Year, parsedMonth, day, hour, minute, 0);
            }
        }
        internal string Title { get { return (string.IsNullOrWhiteSpace(AwayTeam) ? "원정팀" : AwayTeam) + " vs " + (string.IsNullOrWhiteSpace(HomeTeam) ? "홈팀" : HomeTeam); } }
        static string Digits(string value) { var builder = new StringBuilder(); foreach (var c in value ?? "") if (char.IsDigit(c)) builder.Append(c); return builder.ToString(); }
    }
}
