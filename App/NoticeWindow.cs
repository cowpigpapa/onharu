using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public class NoticeWindow : Window
    {
        public NoticeWindow(string message, bool warning, string heading = null)
        {
            Title = "온하루"; Width = 400; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(26, 20, 26, 18) };
            panel.Children.Add(OnharuPopupChrome.Header(this, warning ? "!  " + (string.IsNullOrWhiteSpace(heading) ? "확인이 필요합니다" : heading) : "✓  " + (string.IsNullOrWhiteSpace(heading) ? "안내" : heading), warning ? "#DC2626" : "#4338CA"));
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("#475569"), FontSize = 13, Margin = new Thickness(0, 11, 0, 14) });
            var ok = OnharuPopupChrome.FooterButton("확인", warning ? "#FFF1F2" : "#4338CA", warning ? "#BE123C" : "#FFFFFF");
            if (warning) ok.BorderBrush = Brush("#FECDD3");
            ok.Click += delegate { DialogResult = true; }; panel.Children.Add(ok);
            Content = OnharuPopupChrome.Shell(panel);
        }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
