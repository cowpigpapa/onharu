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
            "#0F172A", "#475569", "#64748B", "#4338CA", "#EEF2FF", "#C7D2FE", "#FFFFFF", "#94A3B8", "#0F766E", "#111827"); }

        static OnharuThemePalette Dark() { return Create(
            "#F2121212", "#F21A1A1A", "#F2181818", "#FF333333", "#FF3F3F46",
            "#F8FAFC", "#E2E8F0", "#CBD5E1", "#A5B4FC", "#FF312E4B", "#FF6366F1", "#FF1F2937", "#94A3B8", "#5EEAD4", "#FFFFFF"); }

        static OnharuThemePalette Create(string shell, string calendar, string sidebar, string cardBorder, string grid,
            string text, string heading, string muted, string accent, string accentSoft, string accentBorder,
            string button, string disabled, string weekday, string icon)
        {
            return new OnharuThemePalette(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Shell", shell }, { "Calendar", calendar }, { "Sidebar", sidebar }, { "CardBorder", cardBorder }, { "Grid", grid },
                { "Text", text }, { "Heading", heading }, { "Muted", muted }, { "Accent", accent }, { "AccentSoft", accentSoft },
                { "AccentBorder", accentBorder }, { "Button", button }, { "Disabled", disabled }, { "Weekday", weekday }, { "Icon", icon }
            });
        }
    }
}
