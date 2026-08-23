using System;
using System.IO;

namespace FamilyPlanner
{
    static class AppDataPaths
    {
        public static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Onharu");
        public static readonly string Backups = Path.Combine(Root, "backups");
        public static readonly string Logs = Path.Combine(Root, "logs");
    }
}
