using System.Windows.Media;

namespace FamilyPlanner
{
    sealed class OnharuStateColorSet
    {
        public Color Background;
        public Color Foreground;
        public Color Border;
    }

    static class OnharuStateColors
    {
        public static string ActionAccent(string theme) { return theme == "dark" ? "#7462CF" : "#6D5CC6"; }
        public static string ActionFill(string theme) { return theme == "dark" ? "#7462CF" : "#D8D2F3"; }
        public static string ActionText(string theme) { return theme == "dark" ? "#FFFFFF" : "#493A91"; }
        public static string ActionBorder(string theme) { return theme == "dark" ? "#7462CF" : "#B6AAE3"; }
        public static string SupportAccent(string theme) { return theme == "dark" ? "#4FAFA5" : "#3B8F89"; }
        public static string GoogleSurface(string theme) { return theme == "dark" ? "#2B2342" : "#EEEAFE"; }
        public static string GoogleText(string theme) { return theme == "dark" ? "#D8CCFF" : "#5A43A4"; }
        public static string GoogleButtonSurface(string theme) { return theme == "dark" ? "#6D5CC6" : "#6750C8"; }
        public static string GoogleButtonText(string theme) { return theme == "dark" ? "#F4F1FF" : "#FFFFFF"; }
        public static Brush BrandGradient() { return Gradient("#0EA5E9", "#7C3AED"); }
        public static Brush GoogleSurfaceBrush(string theme) { return BrandGradient(); }
        public static string ScrollThumb(string theme) { return theme == "dark" ? "#8C8C96" : "#B0B0B8"; }
        public static string OpacityControl(string theme) { return theme == "dark" ? "#E5E7EB" : "#303744"; }
        public static string HeaderSurface(string theme) { return theme == "dark" ? "#2A2E36" : "#303744"; }
        public static string HeaderText(string theme) { return "#FFFFFF"; }
        public static string HeaderBorder(string theme) { return theme == "dark" ? "#4A505B" : "#555E6D"; }
        public static string CalendarCell(string theme) { return theme == "dark" ? "#45454D" : OnharuThemePalette.For(theme)["CardBorder"]; }

        public static OnharuStateColorSet MoreButton(string theme)
        {
            return theme == "dark" ? Set("#24242B", "#E0E7FF", "#6366F1") : Set("#F8FAFF", "#4338CA", "#C7D2FE");
        }

        public static OnharuStateColorSet DetailTab(string theme, bool selected)
        {
            return DetailTab(theme, selected, ActionAccent(theme));
        }

        public static OnharuStateColorSet DetailPeriodTab(string theme, bool selected)
        {
            var palette = OnharuThemePalette.For(theme);
            return selected ? Set("#C2410C", "#FFFFFF", "#FB923C")
                : Set(palette["Button"], palette["Muted"], palette["Grid"]);
        }

        public static OnharuStateColorSet DetailTab(string theme, bool selected, string accent)
        {
            var palette = OnharuThemePalette.For(theme);
            return selected
                ? Set("#1D4ED8", "#FFFFFF", "#60A5FA")
                : Set(palette["Button"], palette["Muted"], palette["Grid"]);
        }

        public static OnharuStateColorSet DetailTab(string theme, string mode, bool selected)
        {
            var palette = OnharuThemePalette.For(theme);
            if (!selected) return Set(palette["Button"], palette["Muted"], palette["Grid"]);
            if (theme == "dark") return Set("#0E7490", "#FFFFFF", "#22D3EE");
            return Set("#A985D8", "#FFFFFF", "#8C69BE");
        }

        public static string DetailScrollThumb(string theme, string mode)
        {
            return theme == "dark" ? ScrollThumb(theme) : "#B7ACE8";
        }

        public static string DetailScrollTrack(string theme) { return theme == "dark" ? "#00000000" : "#F1F5F9"; }

        public static OnharuStateColorSet NeutralSwitch(string theme, bool selected)
        {
            var palette = OnharuThemePalette.For(theme);
            return selected ? Set(theme == "dark" ? "#4B5563" : "#475569", "#FFFFFF", theme == "dark" ? "#6B7280" : "#64748B")
                : Set(palette["Button"], palette["Muted"], palette["Grid"]);
        }

        public static OnharuStateColorSet ImportantDay(string selectedColor)
        {
            if (string.IsNullOrWhiteSpace(selectedColor)) return Set("#F1F5F9", "#475569", "#94A3B8");
            return new OnharuStateColorSet {
                Background = CategoryColorSystem.CheckBoxBackground("classic", selectedColor),
                Foreground = Parse("#BE185D"), Border = Parse("#F472B6") };
        }

        static OnharuStateColorSet Set(string background, string foreground, string border)
        {
            return new OnharuStateColorSet { Background = Parse(background), Foreground = Parse(foreground), Border = Parse(border) };
        }

        static Color Parse(string value) { return (Color)ColorConverter.ConvertFromString(value); }
        static Brush Gradient(string start, string end)
        {
            return new LinearGradientBrush(Parse(start), Parse(end), 0);
        }
    }
}
