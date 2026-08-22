using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    internal static class OnharuPopupChrome
    {
        internal static Button CloseButton(Window window)
        {
            var button = Button("×", 32, "#FEE2E2", "#DC2626");
            button.FontSize = 17; button.FontWeight = FontWeights.SemiBold; button.ToolTip = "닫기";
            button.Click += delegate(object sender, RoutedEventArgs e) { e.Handled = true; window.Close(); };
            return button;
        }

        internal static Button Button(string text, double width, string background, string foreground)
        {
            var button = new Button { Content = text, Width = width, Height = 32, Background = Brush(background),
                Foreground = Brush(foreground), BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter); button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            return button;
        }

        internal static DockPanel Header(Window window, string title, string color)
        {
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 11) };
            var close = CloseButton(window); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(new TextBlock { Text = title, FontSize = 19, FontWeight = FontWeights.Bold,
                Foreground = Brush(color), VerticalAlignment = VerticalAlignment.Center });
            EnableDrag(window, header); return header;
        }

        internal static Button FooterButton(string text, string background, string foreground)
        {
            var button = Button(text, double.NaN, background, foreground); button.Height = 40;
            button.FontWeight = FontWeights.SemiBold; return button;
        }

        internal static void EnableDrag(Window window, UIElement handle)
        {
            handle.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(delegate(object sender, MouseButtonEventArgs e)
            {
                var current = e.OriginalSource as DependencyObject;
                while (current != null) { if (current is Button || current is ComboBox) return; current = VisualTreeHelper.GetParent(current); }
                if (Mouse.LeftButton != MouseButtonState.Pressed) return;
                window.DragMove(); e.Handled = true;
            }), true);
        }

        internal static Border Shell(UIElement content)
        {
            return UiRound.EmphasizePopup(new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#A5B4FC"),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(18), Child = content });
        }

        internal static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
