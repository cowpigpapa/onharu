using System.Collections.Generic;

namespace FamilyPlanner
{
    static class OnharuColorPresets
    {
        public static readonly string[] Names = { "오션블루", "핫핑크", "라임펄스", "바이올렛", "앰버선셋" };
        static readonly string[] representativeColors = { "#00A6C8", "#D62976", "#86B51B", "#7442C8", "#D97706" };

        static readonly string[][] palettes = {
            Preset(new[] { "#00A6C8", "#2859C5", "#159E83", "#7041C6", "#D58A17", "#E25555", "#7FAE22", "#B23BC8" }, "#E67E22", "#C2418C"),
            Preset(new[] { "#D62976", "#315FAE", "#168D7A", "#D28A18", "#7046B8", "#6C9E2B", "#1496B5", "#D86A22" }, "#F97316", "#DB2777"),
            Preset(new[] { "#86B51B", "#3567C4", "#793A9B", "#D57B14", "#168E7A", "#C93C70", "#158FA5", "#D7A826" }, "#C65D18", "#A855C7"),
            Preset(new[] { "#7442C8", "#198F83", "#D18B18", "#3F6FC4", "#C93B55", "#75A62C", "#168FAE", "#D56A24" }, "#E06422", "#C02678"),
            Preset(new[] { "#D97706", "#2563A6", "#0F8A7B", "#7C3AED", "#C58A12", "#3F6FC4", "#D35D7E", "#6C9D2B" }, "#EA580C", "#B83280") };

        public static string[][] Palettes()
        {
            var copy = new string[palettes.Length][];
            for (var i = 0; i < palettes.Length; i++) copy[i] = (string[])palettes[i].Clone();
            return copy;
        }

        public static string RepresentativeColor(int index)
        {
            if (index >= 0 && index < representativeColors.Length) return representativeColors[index];
            if (index == 6 || index == 9) return "#4285F4"; // Google
            return "#4F46E5"; // 내 설정 및 이전 버전 값
        }

        public static Dictionary<string, string> DefaultCategories()
        {
            var first = palettes[0];
            return new Dictionary<string, string> {
                { "업무일정", first[0] }, { "개인일정", first[1] }, { "야구", first[2] },
                { "D-Day", first[3] }, { "기념일", first[4] }, { "국경일", first[5] } };
        }

        static string[] Preset(string[] colors, string dday, string anniversary)
        {
            return new[] { colors[0], colors[1], "#38A169", dday, anniversary, "#DC2626",
                colors[2], colors[3], colors[4], colors[5], colors[6], colors[7] };
        }
    }
}
