using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        static Brush PastelBrush(string hex, double whiteRatio)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var r = (byte)(color.R + (255 - color.R) * whiteRatio);
            var g = (byte)(color.G + (255 - color.G) * whiteRatio);
            var b = (byte)(color.B + (255 - color.B) * whiteRatio);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        double Ui(double baseSize) { return baseSize * (settings.FontSize > 0 ? settings.FontSize / 12.0 : 1); }

        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }
}
