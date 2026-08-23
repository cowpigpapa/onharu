using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FamilyPlanner
{
    static class ErrorLog
    {
        static readonly string Folder = AppDataPaths.Logs;
        static readonly object Sync = new object();

        public static void Write(string area, Exception error, string detail = null)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(Folder);
                    var path = Path.Combine(Folder, "onharu-" + DateTime.Today.ToString("yyyyMMdd") + ".log");
                    var message = error == null ? "Unknown error" : error.GetType().Name + ": " + error.Message;
                    var entry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + Clean(area) + Environment.NewLine +
                        Clean(message) + (string.IsNullOrWhiteSpace(detail) ? "" : Environment.NewLine + Clean(detail)) +
                        Environment.NewLine + Environment.NewLine;
                    File.AppendAllText(path, entry, Encoding.UTF8);
                    foreach (var old in Directory.GetFiles(Folder, "onharu-*.log").OrderByDescending(x => x).Skip(14))
                        File.Delete(old);
                }
            }
            catch { }
        }

        static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "-";
            var clean = value.Length > 1200 ? value.Substring(0, 1200) : value;
            clean = Regex.Replace(clean, "GOCSPX-[A-Za-z0-9_-]+", "[CLIENT_SECRET]");
            clean = Regex.Replace(clean, "(?i)(access_token|refresh_token|client_secret)([\\\"'=:\\s]+)[^&\\s\\\",}]+", "$1$2[REDACTED]");
            clean = Regex.Replace(clean, "[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}", "[EMAIL]", RegexOptions.IgnoreCase);
            return clean.Replace("\r", " ").Replace("\n", " ");
        }
    }
}
