using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FamilyPlanner
{
    // 아래 DTO들은 GoogleCalendarService(구 GoogleCalendar) 내부에서 JSON 직렬화용으로만
    // 쓰이므로 원본과 동일하게 internal 접근 범위를 유지합니다.

    [DataContract]
    class GoogleToken
    {
        [DataMember(Name = "access_token", EmitDefaultValue = false)] public string AccessToken = null;
        [DataMember(Name = "refresh_token", EmitDefaultValue = false)] public string RefreshToken = null;
        [DataMember(Name = "expires_in", EmitDefaultValue = false)] public int ExpiresIn = 0;
        [DataMember(Name = "error", EmitDefaultValue = false)] public string Error = null;
        [DataMember(Name = "error_description", EmitDefaultValue = false)] public string ErrorDescription = null;
    }

    [DataContract]
    class GoogleEvents
    {
        [DataMember(Name = "items", EmitDefaultValue = false)] public List<GoogleEvent> Items = null;
        [DataMember(Name = "nextPageToken", EmitDefaultValue = false)] public string NextPageToken = null;
    }

    [DataContract]
    class GoogleCalendarList
    {
        [DataMember(Name = "items", EmitDefaultValue = false)] public List<GoogleCalendarEntry> Items = null;
        [DataMember(Name = "nextPageToken", EmitDefaultValue = false)] public string NextPageToken = null;
    }

    [DataContract]
    class GoogleCalendarEntry
    {
        [DataMember(Name = "id", EmitDefaultValue = false)] public string Id = null;
        [DataMember(Name = "summary", EmitDefaultValue = false)] public string Summary = null;
        [DataMember(Name = "backgroundColor", EmitDefaultValue = false)] public string BackgroundColor = null;
        [DataMember(Name = "accessRole", EmitDefaultValue = false)] public string AccessRole = null;
        [DataMember(Name = "primary", EmitDefaultValue = false)] public bool Primary = false;
        [DataMember(Name = "selected", EmitDefaultValue = false)] public bool Selected = false;
        [DataMember(Name = "hidden", EmitDefaultValue = false)] public bool Hidden = false;
    }

    [DataContract]
    class GoogleEvent
    {
        [DataMember(Name = "id", EmitDefaultValue = false)] public string Id = null;
        [DataMember(Name = "eventType", EmitDefaultValue = false)] public string EventType = null;
        [DataMember(Name = "summary", EmitDefaultValue = false)] public string Summary;
        [DataMember(Name = "description", EmitDefaultValue = false)] public string Description;
        [DataMember(Name = "status", EmitDefaultValue = false)] public string Status = null;
        [DataMember(Name = "start", EmitDefaultValue = false)] public GoogleDate Start;
        [DataMember(Name = "end", EmitDefaultValue = false)] public GoogleDate End;
        [DataMember(Name = "extendedProperties", EmitDefaultValue = false)] public GoogleExtended ExtendedProperties;
        [DataMember(Name = "recurrence", EmitDefaultValue = false)] public List<string> Recurrence;
        [DataMember(Name = "recurringEventId", EmitDefaultValue = false)] public string RecurringEventId = null;
    }

    [DataContract]
    class GoogleDate
    {
        // PATCH로 하루종일 <-> 시간 일정을 바꿀 때 이전 형식의 필드를 null로 명시해 제거한다.
        [DataMember(Name = "date")] public string Date;
        [DataMember(Name = "dateTime")] public string DateTime;
        [DataMember(Name = "timeZone")] public string TimeZone;
    }

    [DataContract]
    class GoogleExtended { [DataMember(Name = "private", EmitDefaultValue = false)] public Dictionary<string, string> Private; }
}
