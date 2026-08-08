using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FamilyPlanner
{
    // 아래 DTO들은 GoogleCalendarService(구 GoogleCalendar) 내부에서 JSON 직렬화용으로만
    // 쓰이므로 원본과 동일하게 internal 접근 범위를 유지합니다.

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
}
