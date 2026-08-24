using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace FamilyPlanner
{
    static class EmailBackupService
    {
        const string Endpoint = "https://onharu.app/api/v1/backup-email";
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        [DataContract]
        sealed class Request
        {
            [DataMember(Name = "recipient")] public string Recipient;
            [DataMember(Name = "fileName")] public string FileName;
            [DataMember(Name = "contentType")] public string ContentType;
            [DataMember(Name = "contentBase64")] public string ContentBase64;
            [DataMember(Name = "itemCount")] public int ItemCount;
            [DataMember(Name = "clientVersion")] public string ClientVersion;
            [DataMember(Name = "googleIdToken")] public string GoogleIdToken;
        }

        public static async Task Send(string recipient, string googleIdToken, string fileName, string contentType, byte[] content, int itemCount)
        {
            if (string.IsNullOrWhiteSpace(googleIdToken)) throw new InvalidOperationException("Google 사용자 인증을 확인할 수 없습니다.");
            if (content == null || content.Length == 0) throw new InvalidOperationException("보낼 일정이 없습니다.");
            if (content.Length > 1024 * 1024) throw new InvalidOperationException("메일 첨부 파일은 1MB까지 보낼 수 있습니다.");
            var request = new Request { Recipient = recipient, FileName = fileName, ContentType = contentType,
                ContentBase64 = Convert.ToBase64String(content), ItemCount = itemCount, ClientVersion = "2.2.2", GoogleIdToken = googleIdToken };
            byte[] json;
            using (var stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(Request)).WriteObject(stream, request); json = stream.ToArray(); }
            using (var body = new ByteArrayContent(json))
            {
                body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                using (var response = await Http.PostAsync(Endpoint, body))
                {
                    if (response.IsSuccessStatusCode) return;
                    var detail = await response.Content.ReadAsStringAsync();
                    if ((int)response.StatusCode == 429) throw new InvalidOperationException("메일 발송 횟수가 너무 많습니다. 잠시 후 다시 시도해 주세요.");
                    throw new InvalidOperationException("메일 서버가 요청을 처리하지 못했습니다. (" + (int)response.StatusCode + ") " + Short(detail));
                }
            }
        }

        static string Short(string value)
        {
            var text = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length > 100 ? text.Substring(0, 100) : text;
        }
    }
}
