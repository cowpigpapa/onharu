using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class GoogleTasksWarningWindow : Window
    {
        public GoogleTasksWarningWindow()
        {
            Title = "Google Tasks 사용 전 확인";
            Width = 470;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;

            var root = new StackPanel { Margin = new Thickness(24, 18, 24, 20) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var close = OnharuPopupChrome.CloseButton(this);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            header.Children.Add(new TextBlock { Text = "!  Google Tasks 사용 전 확인", FontSize = 20,
                FontWeight = FontWeights.Bold, Foreground = OnharuPopupChrome.Brush("#C2410C"),
                VerticalAlignment = VerticalAlignment.Center });
            OnharuPopupChrome.EnableDrag(this, header);
            root.Children.Add(header);

            root.Children.Add(new Border { Background = OnharuPopupChrome.Brush("#FFF7ED"),
                BorderBrush = OnharuPopupChrome.Brush("#FED7AA"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 11, 14, 11),
                Child = new TextBlock { Text = "Google Tasks는 간단한 할 일 확인용으로만 권장합니다.",
                    Foreground = OnharuPopupChrome.Brush("#9A3412"), FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap } });

            var details = new TextBlock { Margin = new Thickness(2, 13, 2, 16), FontSize = 13,
                Foreground = OnharuPopupChrome.Brush("#475569"), TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Text = "• Google 공개 API는 반복 규칙과 미래 반복 회차를 온전히 제공하지 않습니다.\n" +
                    "• 마감 시간은 제공되지 않아 09:00 Task도 온하루에서는 하루 종일로 표시됩니다.\n" +
                    "• 온하루에서 만든 Google Task는 반복·시간·알림·여러 날 일정을 지원하지 않습니다.\n" +
                    "• Google에서 만든 Task는 반복 여부를 판별할 수 없어 완료 상태만 변경할 수 있습니다.\n\n" +
                    "시간이나 반복이 중요한 일정은 Google Calendar 또는 온하루 로컬 일정으로 등록해 주세요." };
            root.Children.Add(details);

            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = OnharuPopupChrome.Button("취소", 0, "#F1F5F9", "#475569");
            cancel.Width = double.NaN; cancel.Height = 40; cancel.Margin = new Thickness(0, 0, 5, 0);
            cancel.Click += delegate { DialogResult = false; };
            actions.Children.Add(cancel);
            var accept = OnharuPopupChrome.Button("내용을 이해하고 사용", 0, "#4F46E5", "#FFFFFF");
            accept.Width = double.NaN; accept.Height = 40; accept.Margin = new Thickness(5, 0, 0, 0);
            accept.FontWeight = FontWeights.Bold;
            accept.Click += delegate { DialogResult = true; };
            Grid.SetColumn(accept, 1); actions.Children.Add(accept);
            root.Children.Add(actions);
            Content = OnharuPopupChrome.Shell(root);
        }
    }
}
