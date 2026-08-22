using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FamilyPlanner
{
    internal static class SportsTeamLogoStore
    {
        const string RawRoot = "https://raw.githubusercontent.com/fernandokkang/baseball_community/develop/src/main/resources/static/images/emblems/";
        static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnharuV3", "kbo-team-logos-v1");
        static readonly string[] Teams = { "KIA", "KT", "LG", "NC", "SSG", "두산", "롯데", "삼성", "키움", "한화" };
        static bool ready;
        internal static IEnumerable<string> Names { get { return Teams; } }

        internal static async Task EnsureDownloaded()
        {
            if (ready) return;
            Directory.CreateDirectory(Folder);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                foreach (var team in Teams)
                {
                    var path = PathFor(team); if (File.Exists(path)) continue;
                    try { File.WriteAllBytes(path, await client.GetByteArrayAsync(RawRoot + Uri.EscapeDataString(team) + ".png")); }
                    catch (Exception ex) { ErrorLog.Write("Download KBO team logo " + team, ex); }
                }
            ready = Teams.All(team => File.Exists(PathFor(team)));
        }

        internal static BitmapImage Image(string team)
        {
            var path = PathFor(Normalize(team)); if (!File.Exists(path)) return null;
            try { var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); return image; }
            catch { return null; }
        }

        static string PathFor(string team) { return Path.Combine(Folder, team + ".png"); }
        static string Normalize(string team) { foreach (var value in Teams) if ((team ?? "").IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0) return value; return team ?? ""; }
    }
}
