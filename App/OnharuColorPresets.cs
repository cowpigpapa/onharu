using System;
using System.Collections.Generic;

namespace FamilyPlanner
{
    static class OnharuColorPresets
    {
        public static readonly string[] Names = { "차분한 중간톤", "밝고 산뜻한 조합", "맑고 선명한 조합" };

        // A preset owns the exact pastel background and text pair. Runtime
        // rendering must not alter these authored HEX values.
        static readonly string[][] foregrounds = {
            new[] { "#3F5F85", "#884B60", "#35684B", "#705A18", "#674F80", "#9F3F46", "#356763", "#505584", "#8F4B28", "#346575", "#536C28", "#7B496F" },
            new[] { "#3457A4", "#A92E5C", "#16704B", "#755C00", "#70429B", "#B4234D", "#146D67", "#4F4AA3", "#A84616", "#176C82", "#4F7812", "#943782" },
            new[] { "#1D4ED8", "#BE185D", "#087A4B", "#806000", "#6D28D9", "#C62828", "#0F766E", "#4338CA", "#C2410C", "#0E7490", "#4D7C0F", "#A21CAF" } };

        static readonly string[][] backgrounds = {
            new[] { "#E9EEF5", "#F6EBEF", "#E8F2EC", "#F7F2DD", "#F0ECF3", "#F8ECEE", "#E7F1F0", "#ECECF4", "#F8EFE9", "#E8F1F4", "#EFF3E4", "#F3EBF1" },
            new[] { "#EDF3FF", "#FFF1F6", "#E9FAF1", "#FFF9DE", "#F6F0FF", "#FFF0F3", "#E7FAF7", "#F0EFFF", "#FFF3EB", "#E8F8FC", "#F4FBDD", "#FCEFFC" },
            new[] { "#EAF2FF", "#FFF0F5", "#E7F8EF", "#FFF8D6", "#F4EDFF", "#FFEBEE", "#E5F8F5", "#EEECFF", "#FFF1E8", "#E5F7FB", "#F2FAD8", "#FCEBFC" } };

        public static string[][] Palettes()
        {
            var copy = new string[foregrounds.Length][];
            for (var i = 0; i < foregrounds.Length; i++) copy[i] = (string[])foregrounds[i].Clone();
            return copy;
        }

        public static bool TryPastelPair(string hex, out string background, out string foreground)
        {
            for (var row = 0; row < foregrounds.Length; row++)
                for (var column = 0; column < foregrounds[row].Length; column++)
                    if (string.Equals(foregrounds[row][column], hex, StringComparison.OrdinalIgnoreCase))
                    {
                        background = backgrounds[row][column]; foreground = foregrounds[row][column]; return true;
                    }
            background = null; foreground = null; return false;
        }

        public static string VividColor(string hex)
        {
            for (var row = 0; row < foregrounds.Length; row++)
                for (var column = 0; column < foregrounds[row].Length; column++)
                    if (string.Equals(foregrounds[row][column], hex, StringComparison.OrdinalIgnoreCase)) return foregrounds[2][column];
            return hex;
        }

        public static List<string> SoftWorkspacePalette()
        {
            return new List<string>(foregrounds[2]);
        }

        public static string HolidayColor(int presetIndex)
        {
            var index = Math.Max(0, Math.Min(foregrounds.Length - 1, presetIndex));
            return foregrounds[index][5];
        }

        public static Dictionary<string, string> DefaultCategories()
        {
            var first = foregrounds[0];
            return new Dictionary<string, string> {
                { "업무일정", first[0] }, { "개인일정", first[1] }, { "야구", first[2] },
                { "D-Day", first[3] }, { "기념일", first[4] }, { "국경일", "#DC2626" } };
        }
    }
}
