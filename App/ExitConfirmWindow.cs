using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public class ExitConfirmWindow : Window
    {
        public string Choice = "cancel";

        public ExitConfirmWindow()
        {
            Title = "온하루 종료"; Width = 440; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(26, 22, 26, 22) };
            panel.Children.Add(new TextBlock { Text = "온하루를 종료하시겠습니까?", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brush("#334155") });
            panel.Children.Add(new TextBlock { Text = "종료하면 알림과 자동 동기화도 함께 중지됩니다.", TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("#64748B"), FontSize = 12, Margin = new Thickness(0, 10, 0, 18) });
            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = ActionButton("취소", "#F1F5F9", "#475569"); cancel.Margin = new Thickness(0, 0, 5, 0);
            cancel.Click += delegate { Choice = "cancel"; Close(); }; actions.Children.Add(cancel);
            var exit = ActionButton("종료", "#FEE2E2", "#DC2626"); exit.Click += delegate { Choice = "exit"; Close(); };
            exit.Margin = new Thickness(5, 0, 0, 0); Grid.SetColumn(exit, 1); actions.Children.Add(exit); panel.Children.Add(actions);
            Content = UiRound.EmphasizePopup(new Border { Background = Brush("#FFFAFCFF"), CornerRadius = new CornerRadius(18), Child = panel });
        }

        static Button ActionButton(string text, string background, string foreground)
        {
            var button = new Button { Content = text, Height = 40, Background = Brush(background), Foreground = Brush(foreground),
                BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
            UiRound.Apply(button, 11); return button;
        }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
