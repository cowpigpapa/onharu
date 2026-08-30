using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FamilyPlanner
{
    internal static class OnharuCalendarStyle
    {
        internal static void Apply(Calendar calendar)
        {
            calendar.Style = Create();
            calendar.HorizontalAlignment = HorizontalAlignment.Center;
            calendar.LayoutTransform = Transform.Identity;
        }

        internal static Border PopupHost(UIElement content, double padding)
        {
            return new Border { Background = Brush("#FFF8F2"), BorderBrush = Brush("#D5D8DE"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(padding), Margin = new Thickness(0, 4, 0, 0), Child = content };
        }

        internal static Style Create()
        {
            var style = new Style(typeof(Calendar));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#FFF8F2")));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#6D3B47")));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(Calendar.CalendarDayButtonStyleProperty, DayStyle()));
            style.Setters.Add(new Setter(Calendar.CalendarButtonStyleProperty, MonthStyle()));
            style.Setters.Add(new EventSetter(FrameworkElement.LoadedEvent, new RoutedEventHandler(CalendarLoaded)));
            return style;
        }

        static Style DayStyle()
        {
            var template = new ControlTemplate(typeof(CalendarDayButton));
            var border = new FrameworkElementFactory(typeof(Border)); border.Name = "DayBorder";
            border.SetValue(Border.BackgroundProperty, Brush("#FFFEFC")); border.SetValue(Border.BorderBrushProperty, Brush("#F3D4C7"));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(.6)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content); template.VisualTree = border;
            var today = new Trigger { Property = CalendarDayButton.IsTodayProperty, Value = true };
            today.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#DDF7F0"), "DayBorder")); today.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("#34B89A"), "DayBorder")); template.Triggers.Add(today);
            var selected = new Trigger { Property = CalendarDayButton.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#E56B6F"), "DayBorder")); selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); template.Triggers.Add(selected);
            var inactive = new Trigger { Property = CalendarDayButton.IsInactiveProperty, Value = true };
            inactive.Setters.Add(new Setter(Control.OpacityProperty, .38)); template.Triggers.Add(inactive);
            var style = new Style(typeof(CalendarDayButton)); style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(1.5))); style.Setters.Add(new Setter(Control.MinWidthProperty, 29.0));
            style.Setters.Add(new Setter(Control.MinHeightProperty, 27.0)); style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#5B3540"))); return style;
        }

        static Style MonthStyle()
        {
            var template = new ControlTemplate(typeof(CalendarButton));
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.BackgroundProperty, Brush("#FDE8E3"));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); border.SetValue(Border.MarginProperty, new Thickness(2));
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content); template.VisualTree = border;
            var style = new Style(typeof(CalendarButton)); style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#B4474D"))); style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0)); return style;
        }

        static void CalendarLoaded(object sender, RoutedEventArgs e)
        {
            var calendar = sender as Calendar; if (calendar == null) return;
            calendar.Dispatcher.BeginInvoke(new Action(delegate
            {
                var item = Find<CalendarItem>(calendar); if (item == null) return; item.ApplyTemplate();
                var header = item.Template.FindName("PART_HeaderButton", item) as Button;
                if (header != null) { header.FontSize = 13; header.FontWeight = FontWeights.SemiBold; }
                var month = item.Template.FindName("PART_MonthView", item) as Grid;
                if (month == null) return;
                foreach (var text in FindAll<TextBlock>(month))
                    if (FindParent<CalendarDayButton>(text) == null) { text.FontSize = 13; text.FontWeight = FontWeights.SemiBold; }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        static T Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T) return (T)root;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var found = Find<T>(VisualTreeHelper.GetChild(root, i)); if (found != null) return found; }
            return null;
        }

        static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i); var match = child as T; if (match != null) yield return match;
                foreach (var nested in FindAll<T>(child)) yield return nested;
            }
        }

        static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null) { child = VisualTreeHelper.GetParent(child); if (child is T) return (T)child; } return null;
        }

        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
