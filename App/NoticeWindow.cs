using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public class NoticeWindow : Window
    {
        public NoticeWindow(string message, bool warning)
        {
            Title = "온하루"; Width = 400; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(26, 20, 26, 18) };
            panel.Children.Add(new TextBlock { Text = warning ? "!  확인이 필요합니다" : "✓  일정 가져오기",
                FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brush(warning ? "#C2410C" : "#4338CA") });
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("#475569"), FontSize = 13, Margin = new Thickness(0, 11, 0, 14) });
            var ok = new Button { Content = "확인", Height = 38, Background = Brush(warning ? "#FFF7ED" : "#4F46E5"),
                Foreground = warning ? Brush("#C2410C") : Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            UiRound.Apply(ok, 11); ok.Click += delegate { DialogResult = true; }; panel.Children.Add(ok);
            Content = UiRound.EmphasizePopup(new Border { Background = Brush("#FFFAFCFF"), CornerRadius = new CornerRadius(18), Child = panel });
        }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
