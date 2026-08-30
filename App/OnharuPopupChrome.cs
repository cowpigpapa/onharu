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
        internal const string SurfaceColor = "#FFF7F7FA";
        internal const string HeaderSurfaceColor = "#E8E9ED";
        internal const string ContentSurfaceColor = "#FFFFFF";
        internal const string SupportSurfaceColor = "#F3F1EF";
        internal const string BorderColor = "#A9AFBA";
        internal const string PrimarySurfaceColor = "#DDF3F1";
        internal const string PrimaryTextColor = "#0F665F";
        internal const string PrimaryBorderColor = "#82C9C2";
        internal const string ActionSurfaceColor = "#147D75";
        internal const string ActionTextColor = "#FFFFFF";
        internal const string SelectionSurfaceColor = "#FBE8DE";
        internal const string SelectionTextColor = "#9A3412";
        internal const string SelectionBorderColor = "#E4AA8D";
        static readonly DependencyProperty TopDragEnabledProperty = DependencyProperty.RegisterAttached(
            "TopDragEnabled", typeof(bool), typeof(OnharuPopupChrome), new PropertyMetadata(false));
        static readonly DependencyProperty FirstFrameStyledProperty = DependencyProperty.RegisterAttached(
            "FirstFrameStyled", typeof(bool), typeof(OnharuPopupChrome), new PropertyMetadata(false));

        internal static Button CloseButton(Window window)
        {
            var button = Button("", 32, "#475569", "#FFFFFF");
            button.BorderBrush = Brush("#334155");
            button.Padding = new Thickness(0); button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Content = new TextBlock { Text = "×", FontSize = 18, FontFamily = new FontFamily("Segoe UI Symbol"), FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#FFFFFF"), Padding = new Thickness(0), TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            button.ToolTip = "닫기";
            button.Click += delegate(object sender, RoutedEventArgs e) { e.Handled = true; window.Close(); };
            return button;
        }

        internal static Button ToolCloseButton(Window window)
        {
            var button = Button("", 26, "#FFFFFF", "#111827");
            button.Height = 26; button.Padding = new Thickness(0); button.BorderBrush = Brush("#D6DCE8"); button.ToolTip = "닫기";
            button.Content = new System.Windows.Shapes.Path { Data = Geometry.Parse("M5,5 L13,13 M13,5 L5,13"), Stroke = Brush("#111827"),
                StrokeThickness = 1.7, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Width = 18, Height = 18, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            button.Click += delegate(object sender, RoutedEventArgs e) { e.Handled = true; window.Close(); };
            return button;
        }

        internal static Button Button(string text, double width, string background, string foreground)
        {
            if (background == "#4F46E5" || background == "#5F3DC4" || background == "#7C3AED")
            { background = PrimarySurfaceColor; foreground = PrimaryTextColor; }
            else if (background == "#EEF2FF" || background == "#E0E7FF")
            { background = SelectionSurfaceColor; foreground = SelectionTextColor; }
            var button = new Button { Content = text, Width = width, Height = 32, Background = Brush(background),
                Foreground = Brush(foreground), BorderBrush = Brush(ButtonBorder(background)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter); button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            return button;
        }

        internal static void StyleSegment(OnharuSegmentedSwitch control)
        {
            if (control == null) return;
            control.SetPalette(Brush(SelectionSurfaceColor), Brush(SelectionTextColor), Brush("#F8FAFC"), Brush("#64748B"), Brush(SelectionBorderColor));
        }

        static string ButtonBorder(string background)
        {
            return background == PrimarySurfaceColor ? PrimaryBorderColor
                : background == SelectionSurfaceColor || background == "#E0E7FF" || background == "#EEF2FF" ? SelectionBorderColor
                : background == "#ECFDF5" ? "#86D7C5" : background == "#FFF7ED" ? "#F4B892"
                : background == "#FCE7F3" || background == "#FFF1F2" || background == "#FEE2E2" ? "#F2A9BD" : "#C7C3D6";
        }

        internal static DockPanel Header(Window window, string title, string color)
        {
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 11), Background = Brush(HeaderSurfaceColor) };
            var close = CloseButton(window); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(new TextBlock { Text = title, FontSize = 19, FontWeight = FontWeights.Bold,
                Foreground = Brush("#334155"), VerticalAlignment = VerticalAlignment.Center });
            EnableDrag(window, header); return header;
        }

        internal static void StyleHeader(Panel header)
        {
            if (header == null) return;
            header.Background = Brush(HeaderSurfaceColor);
            Action clip = delegate
            {
                if (header.ActualWidth > 0 && header.ActualHeight > 0)
                    header.Clip = new RectangleGeometry(new Rect(0, 0, header.ActualWidth, header.ActualHeight), 10, 10);
            };
            header.Loaded += delegate { clip(); };
            header.SizeChanged += delegate { clip(); };
        }

        internal static Button FooterButton(string text, string background, string foreground)
        {
            var button = Button(text, double.NaN, background, foreground); button.Height = 40;
            button.FontWeight = FontWeights.SemiBold; return button;
        }

        internal static Button PrimaryButton(string text, double width)
        {
            var button = Button(text, width, PrimarySurfaceColor, PrimaryTextColor);
            button.FontWeight = FontWeights.Bold; button.BorderBrush = Brush(PrimaryBorderColor); button.BorderThickness = new Thickness(1);
            return button;
        }

        // 공통 펼침 토글: 설정 콤보박스와 같은 표면에 제목은 왼쪽, 화살표는 오른쪽에 둔다.
        internal static Button DisclosureButton(string text, double width, bool expanded)
        {
            var button = Button("", width, "#FFFFFF", "#334155");
            button.BorderBrush = Brush("#CBD5E1");
            SetDisclosure(button, text, expanded); return button;
        }

        internal static void SetDisclosure(Button button, string text, bool expanded)
        {
            var row = new DockPanel { Margin = new Thickness(10, 0, 13, 0), LastChildFill = true };
            var arrow = new TextBlock { Text = expanded ? "▴" : "▾", FontSize = 11.5,
                Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(arrow, Dock.Right); row.Children.Add(arrow);
            row.Children.Add(new TextBlock { Text = text, Foreground = Brush("#334155"), VerticalAlignment = VerticalAlignment.Center });
            button.Content = row;
        }

        internal static Button ActionButton(string text, double width)
        {
            var button = Button(text, width, ActionSurfaceColor, ActionTextColor);
            button.FontWeight = FontWeights.Bold; button.BorderBrush = Brush(ActionSurfaceColor);
            return button;
        }

        internal static FrameworkElement FeatureTitle(string glyph, string title)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new Border { Width = 34, Height = 34, Margin = new Thickness(0, 0, 10, 0),
                Background = Brush(SupportSurfaceColor), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Child = new TextBlock { Text = glyph, FontSize = 17,
                    FontFamily = new FontFamily("Segoe UI Symbol"), FontWeight = FontWeights.Bold,
                    Foreground = Brush("#1F2937"), HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center } });
            row.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = Brush("#334155"), VerticalAlignment = VerticalAlignment.Center });
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
            var shell = UiRound.EmphasizePopup(new Border { Background = Brush(SurfaceColor), BorderBrush = Brush(BorderColor),
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
