using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FamilyPlanner
{
    static class LayerHostController
    {
        static string hostPath;
        static bool startedHere;

        public static bool Start()
        {
            try
            {
                if (Process.GetProcessesByName("OnharuV3.LayerHost").Any()) return true;
                var app = AppDomain.CurrentDomain.BaseDirectory;
                var candidates = new[]
                {
                    Path.Combine(app, "OnharuV3.LayerHost.exe"),
                    Path.GetFullPath(Path.Combine(app, "..", "ExplorerLayer", "OnharuV3.LayerHost.exe"))
                };
                hostPath = candidates.FirstOrDefault(File.Exists);
                if (hostPath == null) return false;
                Process.Start(new ProcessStartInfo(hostPath) { WorkingDirectory = Path.GetDirectoryName(hostPath), UseShellExecute = false, CreateNoWindow = true });
                startedHere = true;
                return true;
            }
            catch { startedHere = false; return false; }
        }

        public static void Stop()
        {
            if (!startedHere || string.IsNullOrWhiteSpace(hostPath) || !File.Exists(hostPath)) return;
            try
            {
                using (var process = Process.Start(new ProcessStartInfo(hostPath, "--stop")
                { WorkingDirectory = Path.GetDirectoryName(hostPath), UseShellExecute = false, CreateNoWindow = true }))
                    if (process != null) process.WaitForExit(3000);
            }
            catch { }
        }
    }
}
