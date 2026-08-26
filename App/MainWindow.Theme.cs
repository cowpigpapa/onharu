using System;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        readonly Dictionary<string, SolidColorBrush> themeBrushes = new Dictionary<string, SolidColorBrush>();
        static ControlTemplate colorCheckBoxTemplate;

        Brush T(string role)
        {
            SolidColorBrush brush;
            if (!themeBrushes.TryGetValue(role, out brush))
            {
                brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(OnharuThemePalette.For(settings.ThemeId)[role]));
                themeBrushes[role] = brush;
            }
            return brush;
        }

        void ApplyTheme(string id)
        {
            settings.ThemeId = OnharuThemePalette.Normalize(id);
            if (Application.Current != null)
            {
                Application.Current.Resources["OnharuPopupAccent"] = ActionAccentBrush();
                Application.Current.Resources["OnharuScrollThumb"] = new SolidColorBrush(
                    CategoryColorSystem.Background("classic", ActionAccentColor()));
            }
            if (themeQuickSwitch != null) themeQuickSwitch.SetSelected(settings.ThemeId == "dark" ? 1 : 0, false);
            UpdateThemeQuickSwitchStyle();
            var palette = OnharuThemePalette.For(settings.ThemeId);
            foreach (var entry in themeBrushes) entry.Value.Color = (Color)ColorConverter.ConvertFromString(palette[entry.Key]);
            monthTitle.Foreground = BrandBrush(); selectedTitle.Foreground = T("Text");
            if (opacitySlider != null) opacitySlider.Foreground = ActionAccentBrush();
            if (googleLoginButton != null)
            {
                googleLoginButton.Background = Brush("#6750C8"); googleLoginButton.BorderBrush = Brush("#5B45B8");
                googleLoginButton.Foreground = Brushes.White;
            }
            ApplyActionSwitchPalette(calendarRangeSwitch);
            ApplyActionSwitchPalette(positionModeSwitch);
            ApplyActionSwitchPalette(detailOrderSwitch);
            if (collapseSidebarButton != null) collapseSidebarButton.BorderBrush = ActionAccentBrush();
            StyleDetailScrollBar();
            UpdateTodayButtonStyle();
            foreach (var entry in filters)
            {
                var color = FilterColor(entry.Key, entry.Value);
                StyleThemeCheckBox(entry.Value, color);
            }
            RenderAll();
        }

        void UpdateThemeQuickSwitchStyle()
        {
            if (themeQuickSwitch == null) return;
            if (settings.ThemeId == "dark") themeQuickSwitch.SetAccent("#111827", "#FFFFFF");
            else themeQuickSwitch.SetAccent(
                new SolidColorBrush(CategoryColorSystem.Background("classic", ActionAccentColor())),
                new SolidColorBrush(CategoryColorSystem.Foreground("classic", ActionAccentColor())));
        }

        void UpdateTodayButtonStyle()
        {
            if (todayButton == null) return;
            var color = string.IsNullOrWhiteSpace(settings.TodayBorderColor) ? "#4F7BFF" : settings.TodayBorderColor;
            var gradient = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
            gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0EA5E9"), 0));
            try { gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(color), .52)); }
            catch { gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#4F7BFF"), .52)); }
            gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C3AED"), 1));
            todayButton.Background = gradient;
            todayButton.Foreground = Brushes.White;
            todayButton.BorderBrush = Brushes.Transparent;
        }

        string ActionAccentColor()
        {
            return OnharuColorPresets.RepresentativeColor(settings.SelectedPaletteIndex);
        }

        Brush ActionAccentBrush() { return Brush(ActionAccentColor()); }

        void StyleDetailScrollBar()
        {
            if (detailScroll == null) return;
            UiRound.SoftenScrollBars(detailScroll);
        }

        void ApplyActionSwitchPalette(OnharuSegmentedSwitch control)
        {
            if (control == null) return;
            var accent = (Color)ColorConverter.ConvertFromString(ActionAccentColor());
            control.SetPalette(new SolidColorBrush(accent), Brushes.White,
                new SolidColorBrush(CategoryColorSystem.Background("classic", accent)),
                new SolidColorBrush(CategoryColorSystem.Foreground("classic", accent)),
                new SolidColorBrush(CategoryColorSystem.EditorBorder("classic", accent)));
        }

        string FilterColor(string key, CheckBox box)
        {
            string color;
            if (Colors.TryGetValue(key, out color)) return color;
            color = box == null ? null : box.Tag as string;
            if (!string.IsNullOrWhiteSpace(color))
            {
                try { ColorConverter.ConvertFromString(color); return color; }
                catch { }
            }
            return OnharuThemePalette.For(settings.ThemeId)["Accent"];
        }

        void StyleThemeCheckBox(CheckBox box, string color)
        {
            if (box == null) return;
            box.Template = ColorCheckBoxTemplate();
            box.Background = new SolidColorBrush(CategoryColorSystem.CheckBoxBackground(settings.ThemeId, color));
            box.BorderBrush = box.Background;
            // The label sits on the sidebar, not inside the colored square.
            // Dark skin therefore needs the shell text color even when the
            // optimal text over the checkbox square itself would be dark.
            box.Foreground = settings.ThemeId == "dark" ? T("Text")
                : new SolidColorBrush(CategoryColorSystem.Foreground(settings.ThemeId, color));
        }

        static ControlTemplate ColorCheckBoxTemplate()
        {
            if (colorCheckBoxTemplate == null)
                colorCheckBoxTemplate = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type CheckBox}'><StackPanel Orientation='Horizontal'><Border x:Name='Box' Width='14' Height='14' CornerRadius='4' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' VerticalAlignment='Center'><TextBlock x:Name='Tick' Text='✓' Foreground='White' FontWeight='Bold' FontSize='10' HorizontalAlignment='Center' VerticalAlignment='Center' Visibility='Collapsed'/></Border><ContentPresenter Margin='4,0,0,0' VerticalAlignment='Center'/></StackPanel><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Tick' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.38'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            return colorCheckBoxTemplate;
        }

        Brush EventTextBrush(PlannerItem item)
        {
            if (item.Important)
            {
                var background = SafeColor(item.ImportantBackgroundColor, "#FFF1F7");
                var preferred = SafeColor(item.ImportantTextColor, "#F20D7A");
                return new SolidColorBrush(CategoryColorSystem.ReadableForeground(background, preferred));
            }
            return new SolidColorBrush(CategoryColorSystem.Foreground(settings.ThemeId, ItemColor(item)));
        }

        Brush EventBackgroundBrush(PlannerItem item)
        {
            if (item.Important) return SafeBrush(item.ImportantBackgroundColor, "#FFF1F7");
            return new SolidColorBrush(CategoryColorSystem.Background(settings.ThemeId, ItemColor(item)));
        }

        static Brush SafeBrush(string value, string fallback)
        {
            try { return Brush(string.IsNullOrWhiteSpace(value) ? fallback : value); }
            catch { return Brush(fallback); }
        }

        static Color SafeColor(string value, string fallback)
        {
            try { return (Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(value) ? fallback : value); }
            catch { return (Color)ColorConverter.ConvertFromString(fallback); }
        }

        static Brush PastelBrush(Color color, double whiteRatio)
        {
            var r = (byte)(color.R + (255 - color.R) * whiteRatio);
            var g = (byte)(color.G + (255 - color.G) * whiteRatio);
            var b = (byte)(color.B + (255 - color.B) * whiteRatio);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        static Brush PastelBrush(string hex, double whiteRatio)
        {
            return PastelBrush((Color)ColorConverter.ConvertFromString(hex), whiteRatio);
        }

        double Ui(double baseSize) { return baseSize * (settings.FontSize > 0 ? settings.FontSize / 12.0 : 1); }

        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }
}
