using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    internal static class OnharuPopupChrome
    {
        const double TopDragHeight = 60;
        static readonly DependencyProperty TopDragEnabledProperty = DependencyProperty.RegisterAttached(
            "TopDragEnabled", typeof(bool), typeof(OnharuPopupChrome), new PropertyMetadata(false));
        static readonly DependencyProperty FirstFrameStyledProperty = DependencyProperty.RegisterAttached(
            "FirstFrameStyled", typeof(bool), typeof(OnharuPopupChrome), new PropertyMetadata(false));

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

        internal static Button PrimaryButton(string text, double width)
        {
            var button = Button(text, width, "#4F46E5", "#FFFFFF");
            button.FontWeight = FontWeights.Bold;
            button.Background = new LinearGradientBrush(Brush("#4F46E5").Color, Brush("#7C3AED").Color, 0);
            return button;
        }

        internal static FrameworkElement FeatureTitle(string glyph, string title)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new Border { Width = 34, Height = 34, Margin = new Thickness(0, 0, 10, 0),
                Background = Brush("#EEF2FF"), BorderBrush = Brush("#C7D2FE"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Child = new TextBlock { Text = glyph, FontSize = 17,
                    FontFamily = new FontFamily("Segoe UI Symbol"), FontWeight = FontWeights.Bold,
                    Foreground = Brush("#4F46E5"), HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center } });
            row.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = Brush("#1E293B"), VerticalAlignment = VerticalAlignment.Center });
            return row;
        }

        internal static void EnableDrag(Window window, UIElement handle)
        {
            handle.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(delegate(object sender, MouseButtonEventArgs e)
            {
                if (IsInteractive(e.OriginalSource as DependencyObject)) return;
                if (Mouse.LeftButton != MouseButtonState.Pressed) return;
                try { window.DragMove(); e.Handled = true; } catch (InvalidOperationException) { }
            }), true);
        }

        internal static void EnableTopDrag(Window window)
        {
            if (window == null || (bool)window.GetValue(TopDragEnabledProperty)) return;
            window.SetValue(TopDragEnabledProperty, true);
            StyleFirstFrame(window);
            window.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left || e.GetPosition(window).Y > TopDragHeight || IsInteractive(e.OriginalSource as DependencyObject)) return;
                try { window.DragMove(); e.Handled = true; } catch (InvalidOperationException) { }
            }), true);
        }

        static void StyleFirstFrame(Window window)
        {
            if ((bool)window.GetValue(FirstFrameStyledProperty)) return;
            window.SetValue(FirstFrameStyledProperty, true);
            RoutedEventHandler apply = null;
            apply = delegate
            {
                window.Loaded -= apply;
                var content = window.Content as DependencyObject;
                if (content != null) UiRound.SoftenScrollBars(content);
            };
            if (window.IsLoaded) apply(window, new RoutedEventArgs());
            else window.Loaded += apply;
        }

        static bool IsInteractive(DependencyObject current)
        {
            while (current != null)
            {
                if (current is ButtonBase || current is TextBoxBase || current is PasswordBox || current is Selector ||
                    current is RangeBase || current is DatePicker || current is Hyperlink) return true;
                var visual = current as Visual;
                current = visual != null ? VisualTreeHelper.GetParent(visual) : LogicalTreeHelper.GetParent(current);
            }
            return false;
        }

        internal static Border Shell(UIElement content)
        {
            var shell = UiRound.EmphasizePopup(new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#A5B4FC"),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(18), Child = content });
            shell.Loaded += delegate { EnableTopDrag(Window.GetWindow(shell)); };
            return shell;
        }

        internal static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
