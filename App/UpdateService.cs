using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FamilyPlanner
{
    [DataContract]
    sealed class GithubRelease
    {
        [DataMember(Name = "tag_name")] public string TagName;
        [DataMember(Name = "html_url")] public string PageUrl;
        [DataMember(Name = "body")] public string Notes;
        [DataMember(Name = "assets")] public List<GithubReleaseAsset> Assets;
    }

    [DataContract]
    sealed class GithubReleaseAsset
    {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "browser_download_url")] public string DownloadUrl;
    }

    sealed class UpdateInfo
    {
        public Version Version;
        public string VersionText;
        public string Notes;
        public string PageUrl;
        public string InstallerUrl;
        public string ChecksumsUrl;
        public string InstallerName;
    }

    static class UpdateService
    {
        const string LatestReleaseUrl = "https://api.github.com/repos/cowpigpapa/onharu/releases/latest";
        static readonly HttpClient Http = CreateClient();

        static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ONHARU/2.2");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        public static async Task<UpdateInfo> CheckAsync()
        {
            using (var response = await Http.GetAsync(LatestReleaseUrl))
            {
                response.EnsureSuccessStatusCode();
                var release = Read<GithubRelease>(await response.Content.ReadAsStreamAsync());
                Version version;
                if (release == null || !TryVersion(release.TagName, out version) || version <= CurrentVersion()) return null;
                var assets = release.Assets ?? new List<GithubReleaseAsset>();
                var installer = assets.FirstOrDefault(x => (x.Name ?? "").EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase));
                var sums = assets.FirstOrDefault(x => string.Equals(x.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
                if (installer == null || sums == null) return null;
                return new UpdateInfo { Version = version, VersionText = version.ToString(), Notes = release.Notes,
                    PageUrl = release.PageUrl, InstallerUrl = installer.DownloadUrl, InstallerName = installer.Name,
                    ChecksumsUrl = sums.DownloadUrl };
            }
        }

        public static async Task<string> DownloadVerifiedInstallerAsync(UpdateInfo update)
        {
            if (update == null) throw new ArgumentNullException("update");
            var folder = Path.Combine(Path.GetTempPath(), "ONHARU", "Updates", update.VersionText);
            Directory.CreateDirectory(folder);
            var installerPath = Path.Combine(folder, Path.GetFileName(update.InstallerName));
            var installer = await Http.GetByteArrayAsync(update.InstallerUrl);
            var sums = await Http.GetStringAsync(update.ChecksumsUrl);
            var expected = ExpectedHash(sums, update.InstallerName);
            var actual = Hash(installer);
            if (string.IsNullOrWhiteSpace(expected) || !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("업데이트 설치 파일의 무결성을 확인하지 못했습니다.");
            File.WriteAllBytes(installerPath, installer);
            return installerPath;
        }

        public static void LaunchInstaller(string path) { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }

        static T Read<T>(Stream stream)
        { return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }

        static Version CurrentVersion()
        { return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0); }

        static bool TryVersion(string value, out Version version)
        {
            value = (value ?? "").Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            return Version.TryParse(value, out version);
        }

        static string ExpectedHash(string text, string fileName)
        {
            foreach (var raw in (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim(); var split = line.IndexOfAny(new[] { ' ', '\t' });
                if (split <= 0) continue;
                var name = line.Substring(split).Trim().TrimStart('*');
                if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)) return line.Substring(0, split).Trim();
            }
            return null;
        }

        static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
    }
}
