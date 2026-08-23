using System;
using System.IO;

namespace FamilyPlanner
{
    static class LegacyMigration
    {
        public static void CopyV1UserStateOnce()
        {
            var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyPlanner");
            var target = AppDataPaths.Root;
            var marker = Path.Combine(target, ".v1-import-complete");
            if (File.Exists(Path.Combine(target, "settings.json")) ||
                (Directory.Exists(target) && Directory.GetFiles(target, "items-*.json").Length > 0)) return;
            if (File.Exists(marker) || !Directory.Exists(source)) return;

            Directory.CreateDirectory(target);
            CopyFile(source, target, "settings.json");
            CopyFile(source, target, "items.json");
            File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
        }

        static void CopyFile(string source, string target, string name)
        {
            var file = Path.Combine(source, name);
            var destination = Path.Combine(target, name);
            if (File.Exists(file) && !File.Exists(destination)) File.Copy(file, destination);
        }
    }
}
