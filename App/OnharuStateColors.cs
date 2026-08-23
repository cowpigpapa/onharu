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
        public static OnharuStateColorSet DetailTab(string theme, bool selected)
        {
            var palette = OnharuThemePalette.For(theme);
            return selected
                ? Set(theme == "dark" ? "#6366F1" : "#4F46E5", "#FFFFFF", theme == "dark" ? "#6366F1" : "#4F46E5")
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
    }
}
