using System;
using System.IO;
using System.Linq;

namespace FamilyPlanner
{
    static class V21Migration
    {
        public static void BackupPreUpgradeOnce()
        {
            var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnharuV3");
            var target = Path.Combine(source, "pre-2.1-backup");
            BackupPreUpgrade(source, target);
        }

        public static void BackupPreUpgrade(string source, string target)
        {
            var marker = Path.Combine(target, "completed.txt");
            if (!Directory.Exists(source) || File.Exists(marker)) return;
            try
            {
                Directory.CreateDirectory(target);
                var files = Directory.GetFiles(source, "items-*.json").ToList();
                var settings = Path.Combine(source, "settings.json");
                if (File.Exists(settings)) files.Add(settings);
                foreach (var path in files) File.Copy(path, Path.Combine(target, Path.GetFileName(path)), true);
                File.WriteAllText(marker, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex) { ErrorLog.Write("Create pre-2.1 backup", ex); }
        }
    }
}
