using System;
using System.Collections.Generic;

namespace FamilyPlanner
{
    public sealed class OnharuThemePalette
    {
        readonly Dictionary<string, string> colors;
        OnharuThemePalette(Dictionary<string, string> colors) { this.colors = colors; }

        public string this[string role] { get { return colors.ContainsKey(role) ? colors[role] : "#FF00FF"; } }

        public static string Normalize(string id)
        {
            return id == "dark" ? id : "classic";
        }

        public static OnharuThemePalette For(string id)
        {
            id = Normalize(id);
            if (id == "dark") return Dark();
            return Classic();
        }

        static OnharuThemePalette Classic() { return Create(
            "#BFF1F5F9", "#D9FFFFFF", "#E6FFFFFF", "#99FFFFFF", "#99CBD5E1",
            "#0F172A", "#475569", "#64748B", "#4338CA", "#EEF2FF", "#C7D2FE", "#FFFFFF", "#94A3B8", "#0F766E", "#111827", "#FFFFFF"); }

        static OnharuThemePalette Dark() { return Create(
            "#F2121212", "#F21A1A1A", "#F2181818", "#FF52525B", "#FF3F3F46",
            "#F8FAFC", "#E2E8F0", "#CBD5E1", "#A5B4FC", "#FF312E4B", "#FF6366F1", "#FF1F2937", "#94A3B8", "#5EEAD4", "#FFFFFF", "#FF27272A"); }

        // 2026-09-03: `Card`가 빠져 있었다. 인덱서는 없는 키에 마젠타 `#FF00FF`를 돌려주는데,
        // 상세 패널의 시간순·미완료 카드가 `T("Card")`를 써서 블랙 스킨에서 형광 분홍으로 보였다.
        // 파스텔은 같은 자리에서 `Brushes.White`를 직접 써서 드러나지 않았다.
        static OnharuThemePalette Create(string shell, string calendar, string sidebar, string cardBorder, string grid,
            string text, string heading, string muted, string accent, string accentSoft, string accentBorder,
            string button, string disabled, string weekday, string icon, string card)
        {
            return new OnharuThemePalette(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Shell", shell }, { "Calendar", calendar }, { "Sidebar", sidebar }, { "CardBorder", cardBorder }, { "Grid", grid },
                { "Text", text }, { "Heading", heading }, { "Muted", muted }, { "Accent", accent }, { "AccentSoft", accentSoft },
                { "AccentBorder", accentBorder }, { "Button", button }, { "Disabled", disabled }, { "Weekday", weekday }, { "Icon", icon },
                { "Card", card }
            });
        }
    }
}
