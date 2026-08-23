using System;
using System.Windows.Media;

namespace FamilyPlanner
{
    static class CategoryColorSystem
    {
        const double MinimumContrast = 4.5;

        public static Color Background(string theme, string hex)
        {
            return Background(theme, Parse(hex));
        }

        public static Color Background(string theme, Color color)
        {
            if (theme == "dark") return WhiteReadableBackground(Vivid(color));
            return MixWhite(Vivid(color), .80);
        }

        public static Color Foreground(string theme, string hex)
        {
            return Foreground(theme, Parse(hex));
        }

        public static Color Foreground(string theme, Color color)
        {
            var background = Background(theme, color);
            var vivid = Vivid(color);
            var dark = Scale(vivid, .30);
            var light = MixWhite(vivid, .86);
            if (theme == "dark") return Colors.White;
            return SameHueReadable(background, vivid, dark, light);
        }

        public static Color EditorBorder(string theme, Color color)
        {
            if (theme == "dark") return MixWhite(color, .18);
            return MixWhite(color, .48);
        }

        // Detail cards must use the exact same fill and text calculation as
        // calendar event bars. Keep these aliases instead of a second formula.
        public static Color DetailBackground(string theme, string hex)
        {
            return Background(theme, hex);
        }
        public static Color DetailBorder(string theme, string hex)
        {
            var color = Vivid(Parse(hex));
            return theme == "dark" ? MixWhite(color, .18) : MixWhite(color, .60);
        }
        public static Color DetailForeground(string theme, string hex)
        {
            return Foreground(theme, hex);
        }

        public static Color CheckBoxBackground(string theme, string hex)
        {
            var color = Vivid(Parse(hex));
            // Pastel check marks are controls, so they need one stronger color
            // step than event/card fills. Dark skin keeps its vivid category fill.
            return theme == "dark" ? WhiteReadableBackground(color) : MixWhite(color, .58);
        }

        static Color WhiteReadableBackground(Color color)
        {
            for (var ratio = 1.0; ratio >= .36; ratio -= .04)
            {
                var candidate = Scale(color, ratio);
                if (ContrastRatio(candidate, Colors.White) >= MinimumContrast) return candidate;
            }
            return Scale(color, .32);
        }

        public static Color SelectionBackground(string theme, string hex)
        {
            var color = Parse(hex);
            return theme == "dark" ? Scale(Vivid(color), .52) : color;
        }

        public static Color SelectionForeground(string theme, string hex)
        {
            var color = Parse(hex); var background = SelectionBackground(theme, hex);
            return Readable(background, MixWhite(Vivid(color), .90), Scale(Vivid(color), .25));
        }

        public static Color SelectionBorder(string theme, string hex)
        {
            var color = Parse(hex);
            return theme == "dark" ? MixWhite(Vivid(color), .22) : EditorBorder(theme, color);
        }

        public static Color Vivid(Color color)
        {
            var gray = (color.R + color.G + color.B) / 3.0;
            return Color.FromRgb(VividChannel(color.R, gray), VividChannel(color.G, gray), VividChannel(color.B, gray));
        }

        public static double ContrastRatio(Color first, Color second)
        {
            var a = RelativeLuminance(first); var b = RelativeLuminance(second);
            return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        }

        static Color Readable(Color background, Color preferred, Color alternate)
        {
            if (ContrastRatio(background, preferred) >= MinimumContrast) return preferred;
            if (ContrastRatio(background, alternate) >= MinimumContrast) return alternate;
            var dark = Parse("#111827"); var light = Colors.White;
            if (ContrastRatio(background, dark) >= MinimumContrast) return dark;
            if (ContrastRatio(background, light) >= MinimumContrast) return light;
            return ContrastRatio(background, Colors.Black) >= ContrastRatio(background, light) ? Colors.Black : light;
        }

        static Color SameHueReadable(Color background, Color vivid, Color fallback, Color alternate)
        {
            // Keep as much of the category hue as possible. Start with a rich
            // same-hue text and darken only until normal-size text reaches the
            // required contrast; generic black is the final safety fallback.
            for (var ratio = .58; ratio >= .18; ratio -= .04)
            {
                var candidate = Scale(vivid, ratio);
                if (ContrastRatio(background, candidate) >= MinimumContrast) return candidate;
            }
            return Readable(background, fallback, alternate);
        }

        static Color Parse(string hex)
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return Color.FromRgb(79, 123, 255); }
        }

        static Color MixWhite(Color color, double ratio)
        {
            return Color.FromRgb(Mix(color.R, ratio), Mix(color.G, ratio), Mix(color.B, ratio));
        }

        static byte Mix(byte value, double ratio) { return (byte)(value + (255 - value) * ratio); }
        static Color Scale(Color color, double ratio)
        {
            return Color.FromRgb((byte)(color.R * ratio), (byte)(color.G * ratio), (byte)(color.B * ratio));
        }
        static byte VividChannel(byte value, double gray)
        {
            var saturated = gray + (value - gray) * 1.22;
            return (byte)Math.Max(0, Math.Min(255, saturated + (255 - saturated) * .05));
        }
        static double RelativeLuminance(Color color)
        {
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        }
        static double Linear(byte value)
        {
            var channel = value / 255.0;
            return channel <= .03928 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4);
        }
    }
}
