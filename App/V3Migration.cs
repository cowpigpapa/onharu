using System;
using System.IO;

namespace FamilyPlanner
{
    static class V3Migration
    {
        public static void CopyV1UserStateOnce()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var source = Path.Combine(local, "FamilyPlanner");
            var target = Path.Combine(local, "OnharuV3");
            var marker = Path.Combine(target, ".v1-import-complete");
            if (File.Exists(marker) || !Directory.Exists(source)) return;

            Directory.CreateDirectory(target);
            CopyFiles(source, target);
            File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
        }

        static void CopyFiles(string source, string target)
        {
            foreach (var file in Directory.GetFiles(source))
            {
                var destination = Path.Combine(target, Path.GetFileName(file));
                if (!File.Exists(destination)) File.Copy(file, destination);
            }
            foreach (var folder in Directory.GetDirectories(source))
            {
                var destination = Path.Combine(target, Path.GetFileName(folder));
                Directory.CreateDirectory(destination);
                CopyFiles(folder, destination);
            }
        }
    }
}
