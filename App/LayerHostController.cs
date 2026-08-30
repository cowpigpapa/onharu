using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FamilyPlanner
{
    static class LayerHostController
    {
        static string hostPath;

        static string ResolveHostPath()
        {
            var app = AppDomain.CurrentDomain.BaseDirectory;
            return new[]
            {
                Path.Combine(app, "Onharu.LayerHost.exe"),
                Path.GetFullPath(Path.Combine(app, "..", "ExplorerLayer", "Onharu.LayerHost.exe"))
            }.FirstOrDefault(File.Exists);
        }

        public static bool Start()
        {
            try
            {
                hostPath = ResolveHostPath();
                if (Process.GetProcessesByName("Onharu.LayerHost").Any()) return true;
                if (hostPath == null) return false;
                Process.Start(new ProcessStartInfo(hostPath) { WorkingDirectory = Path.GetDirectoryName(hostPath), UseShellExecute = false, CreateNoWindow = true });
                return true;
            }
            catch { return false; }
        }

        public static void Stop()
        {
            if (string.IsNullOrWhiteSpace(hostPath)) hostPath = ResolveHostPath();
            if (string.IsNullOrWhiteSpace(hostPath) || !File.Exists(hostPath)) return;
            try
            {
                using (var process = Process.Start(new ProcessStartInfo(hostPath, "--stop")
                { WorkingDirectory = Path.GetDirectoryName(hostPath), UseShellExecute = false, CreateNoWindow = true }))
                    if (process != null) process.WaitForExit(3000);
                var hosts = Process.GetProcessesByName("Onharu.LayerHost");
                foreach (var host in hosts)
                    try { if (!host.WaitForExit(4000)) { host.Kill(); host.WaitForExit(2000); } }
                    finally { host.Dispose(); }
            }
            catch { }
        }
    }
}
