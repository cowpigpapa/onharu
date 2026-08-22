using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class LocalItemsOfferWindow : Window
    {
        public bool ReviewItems;

        public LocalItemsOfferWindow(int count)
        {
            Title = "로컬 일정 확인"; Width = 430; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(26, 20, 26, 18) };
            panel.Children.Add(new TextBlock { Text = "↻  로그아웃 상태 일정", FontSize = 20,
                FontWeight = FontWeights.Bold, Foreground = Brush("#4338CA") });
            panel.Children.Add(new TextBlock { Text = "로그아웃 상태에서 등록한 로컬 일정 " + count + "개가 있습니다.\n현재 Google 계정으로 가져올까요?",
                TextWrapping = TextWrapping.Wrap, Foreground = Brush("#475569"), FontSize = 13,
                Margin = new Thickness(0, 11, 0, 14) });

            var buttons = new Grid();
            buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var later = new Button { Content = "지금은 안 함", Height = 40, Margin = new Thickness(0, 0, 5, 0),
                Background = Brush("#E2E8F0"), Foreground = Brush("#475569"), BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            var review = new Button { Content = "일정 선택", Height = 40, Margin = new Thickness(5, 0, 0, 0),
                Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            UiRound.Apply(later, 11); UiRound.Apply(review, 11);
            later.Click += delegate { DialogResult = false; };
            review.Click += delegate { ReviewItems = true; DialogResult = true; };
            buttons.Children.Add(later); Grid.SetColumn(review, 1); buttons.Children.Add(review); panel.Children.Add(buttons);
            Content = UiRound.EmphasizePopup(new Border { Background = Brush("#FFFAFCFF"), CornerRadius = new CornerRadius(18), Child = panel });
        }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
