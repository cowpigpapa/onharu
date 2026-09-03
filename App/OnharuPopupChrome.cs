using System;
using System.Linq;
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
        internal const string TodaySurfaceColor = "#E0F2FE";
        internal const string TodayTextColor = "#0369A1";
        internal const string TodayBorderColor = "#7DD3FC";
        internal const string BrandGradientStartColor = "#0369A1";
        internal const string BrandGradientEndColor = "#7C3AED";
        static readonly DependencyProperty TopDragEnabledProperty = DependencyProperty.RegisterAttached(
            "TopDragEnabled", typeof(bool), typeof(OnharuPopupChrome), new PropertyMetadata(false));
        static readonly DependencyProperty FirstFrameStyledProperty = DependencyProperty.RegisterAttached(
            "FirstFrameStyled", typeof(bool), typeof(OnharuPopupChrome), new PropertyMetadata(false));

        internal static Button CloseButton(Window window)
        {
            return ToolCloseButton(window);
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

        internal static FrameworkElement FeatureHeading(string glyph, string title)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            // 아이콘은 메인 헤더와 같은 OnharuIcons 도형을 쓴다. 이전에는 여기만 글꼴 기호라
            // 같은 기능인데 메인과 팝업의 그림이 서로 달랐다.
            var icon = OnharuIcons.Draw(glyph, Brush("#334155"), 21);
            icon.Width = 24; icon.HorizontalAlignment = HorizontalAlignment.Left;
            icon.VerticalAlignment = VerticalAlignment.Center; icon.Margin = new Thickness(0, 0, 9, 0);
            row.Children.Add(icon);
            row.Children.Add(new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeights.Bold,
                Foreground = Brush("#334155"), VerticalAlignment = VerticalAlignment.Center });
            return row;
        }

        internal static DockPanel FeatureHeader(Window window, string glyph, string title)
        {
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 11) };
            StyleHeader(header);
            var close = ToolCloseButton(window); close.Margin = new Thickness(0, 4, 6, 4);
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(FeatureHeading(glyph, title)); EnableDrag(window, header); return header;
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

        // 아이콘만 있는 버튼은 자동화 트리에서 이름이 빈 칸이 된다. 화면 낭독기와 자동화가
        // 무슨 버튼인지 알 수 없다. 도구 설명을 그대로 이름으로 삼는다. 이미 이름이 있으면 두 번 덮지 않는다.
        internal static void NameFromToolTip(FrameworkElement element)
        {
            if (element == null) return;
            var text = element.ToolTip as string;
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(element))) return;
            System.Windows.Automation.AutomationProperties.SetName(element, text);
        }

        internal static Button ActionButton(string text, double width)
        {
            var button = Button(text, width, ActionSurfaceColor, ActionTextColor);
            button.FontWeight = FontWeights.Bold; button.BorderBrush = Brush(ActionSurfaceColor);
            return button;
        }

        // 메인 창과 팝업이 함께 쓰는 테두리 크기 조절. 판정 기준은 모서리 18px, 변 10px이다.
        // 2026-09-03: 메인 창이 들고 있던 같은 판정 사본을 지우고 이쪽 하나로 합쳤다.
        internal static void EnableResize(Window window, FrameworkElement surface)
        {
            if (window == null || surface == null) return;
            surface.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
            {
                surface.Cursor = ResizeCursor(ResizeEdgeAt(e.GetPosition(surface), surface));
            };
            surface.MouseLeave += delegate { surface.Cursor = Cursors.Arrow; };
            surface.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                var edge = ResizeEdgeAt(e.GetPosition(surface), surface);
                if (edge == 0) return;
                DesktopLayer.BeginResize(window, edge); e.Handled = true;
            };
        }

        internal static int ResizeEdgeAt(Point point, FrameworkElement surface)
        {
            const double corner = 18, edge = 10;
            var leftCorner = point.X <= corner; var rightCorner = point.X >= surface.ActualWidth - corner;
            var topCorner = point.Y <= corner; var bottomCorner = point.Y >= surface.ActualHeight - corner;
            if (leftCorner && topCorner) return 1;
            if (rightCorner && topCorner) return 2;
            if (leftCorner && bottomCorner) return 3;
            if (rightCorner && bottomCorner) return 4;
            if (point.X <= edge) return 5;
            if (point.X >= surface.ActualWidth - edge) return 6;
            if (point.Y <= edge) return 7;
            if (point.Y >= surface.ActualHeight - edge) return 8;
            return 0;
        }

        internal static Cursor ResizeCursor(int edge)
        {
            return edge == 1 || edge == 4 ? UiCursor.ResizeNwSe : edge == 2 || edge == 3 ? UiCursor.ResizeNeSw
                : edge == 5 || edge == 6 ? UiCursor.ResizeHorizontal : edge == 7 || edge == 8 ? UiCursor.ResizeVertical : Cursors.Arrow;
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

        // 메인 로고와 같은 온하루 브랜드 그라데이션. 각 창의 대표 실행 버튼 하나에만 사용한다.
        // 흰 글자 대비 4.5:1을 지키려고 시작색은 브랜드 스카이(#0EA5E9) 대신 한 단계 짙은 #0369A1을 쓴다.
        // 선택된 라디오의 Tag를 찾는다. 패널의 직접 자식만 훑으면 배치를 한 단계라도 감쌌을 때
        // 결과가 조용히 비고 `First`가 예외를 던져 저장 도중 창이 닫힌다.
        // 2026-09-03 설정 저장 크래시가 정확히 그 경로였다. 자식을 재귀로 훑고,
        // 선택된 항목이 없으면 예외 대신 대체값을 돌려준다. 설정창과 일정 등록·수정 창이 함께 쓴다.
        internal static string CheckedRadioTag(DependencyObject root, string fallback)
        {
            return FindCheckedRadioTag(root) ?? fallback;
        }

        internal static RadioButton CheckedRadio(DependencyObject root)
        {
            var panel = root as Panel;
            if (panel == null) return null;
            foreach (var child in panel.Children.OfType<DependencyObject>())
            {
                var radio = child as RadioButton;
                if (radio != null) { if (radio.IsChecked == true) return radio; continue; }
                var nested = CheckedRadio(child);
                if (nested != null) return nested;
            }
            return null;
        }

        internal static RadioButton RadioByTag(DependencyObject root, string tag)
        {
            var panel = root as Panel;
            if (panel == null) return null;
            foreach (var child in panel.Children.OfType<DependencyObject>())
            {
                var radio = child as RadioButton;
                if (radio != null) { if (string.Equals(Convert.ToString(radio.Tag), tag)) return radio; continue; }
                var nested = RadioByTag(child, tag);
                if (nested != null) return nested;
            }
            return null;
        }

        static string FindCheckedRadioTag(DependencyObject root)
        {
            var radio = CheckedRadio(root);
            return radio == null ? null : Convert.ToString(radio.Tag);
        }

        // 비활성 표현을 한 방식으로 통일한다. IsEnabled만 끄면 WPF 기본 회색이 옅어
        // 컨트롤이 아직 활성인 것처럼 보인다. 중첩해서 부르면 불투명도가 곱해지므로 한 층에서만 쓴다.
        internal static void SetOptionsEnabled(bool enabled, params UIElement[] targets)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;
                target.IsEnabled = enabled;
                target.Opacity = enabled ? 1 : .45;
            }
        }

        internal static Brush BrandGradientBrush()
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(BrandGradientStartColor), 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(BrandGradientEndColor), 1));
            brush.Freeze(); return brush;
        }

        internal static Border Shell(UIElement content)
        {
            var inner = new Border { Background = Brush(SurfaceColor), CornerRadius = new CornerRadius(16), Child = content };
            Action clip = delegate
            {
                if (inner.ActualWidth > 0 && inner.ActualHeight > 0)
                    inner.Clip = new RectangleGeometry(new Rect(0, 0, inner.ActualWidth, inner.ActualHeight), 16, 16);
            };
            inner.Loaded += delegate { clip(); };
            inner.SizeChanged += delegate { clip(); };
            var shell = UiRound.EmphasizePopup(new Border { Background = Brush(SurfaceColor), BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(18), Child = inner });
            shell.Loaded += delegate { EnableTopDrag(Window.GetWindow(shell)); };
            return shell;
        }

        internal static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
