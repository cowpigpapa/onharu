using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace FamilyPlanner
{
    static class PlacementTrace
    {
        static readonly bool Enabled = string.Equals(
            Environment.GetEnvironmentVariable("ONHARU_PLACEMENT_TRACE"), "1", StringComparison.Ordinal);
        static readonly object Sync = new object();
        static readonly string Path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OnharuV3", "logs", "placement-transition.log");

        public static bool IsEnabled { get { return Enabled; } }

        public static void Write(string message)
        {
            if (!Enabled) return;
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                    File.AppendAllText(Path,
                        DateTime.Now.ToString("HH:mm:ss.fff") + " | qpc=" + Stopwatch.GetTimestamp() + " | " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}
