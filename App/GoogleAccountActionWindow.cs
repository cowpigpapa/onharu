using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class GoogleAccountActionWindow : Window
    {
        public string SelectedAction { get; private set; }

        public GoogleAccountActionWindow(string accountName = null)
        {
            Title = "Google 계정"; Width = 410; SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new StackPanel { Margin = new Thickness(22, 16, 22, 20) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var close = OnharuPopupChrome.CloseButton(this); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(new TextBlock { Text = "G  Google 계정", FontSize = 19, FontWeight = FontWeights.Bold,
                Foreground = B("#4338CA"), VerticalAlignment = VerticalAlignment.Center });
            OnharuPopupChrome.EnableDrag(this, header); root.Children.Add(header);

            root.Children.Add(new Border { Background = OnharuStateColors.GoogleSurfaceBrush("classic"), BorderBrush = B("#D9CCF6"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 13),
                Child = new TextBlock { Text = "현재 연결 · " + (string.IsNullOrWhiteSpace(accountName) ? "Google 계정" : accountName),
                    Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis } });
            root.Children.Add(new TextBlock { Text = "원하는 작업을 선택해 주세요.", Foreground = B("#64748B"),
                Margin = new Thickness(1, 0, 0, 10) });
            var actions = new Grid(); actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition());
            var change = OnharuPopupChrome.Button("G  계정 변경", 158, "#EEF2FF", "#4338CA");
            change.Background = B(OnharuStateColors.GoogleButtonSurface("classic")); change.Foreground = Brushes.White;
            change.Height = 40; change.Margin = new Thickness(0, 0, 5, 0);
            change.Click += delegate { SelectedAction = "change"; DialogResult = true; };
            actions.Children.Add(change);
            var logout = OnharuPopupChrome.Button("로그아웃", 158, "#FEF2F2", "#DC2626");
            logout.Background = B("#FDECEF"); logout.Foreground = B("#BE3658");
            logout.Height = 40; logout.Margin = new Thickness(5, 0, 0, 0);
            logout.Click += delegate { SelectedAction = "logout"; DialogResult = true; };
            Grid.SetColumn(logout, 1); actions.Children.Add(logout); root.Children.Add(actions);
            Content = OnharuPopupChrome.Shell(root);
        }

        static Brush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    public sealed class GoogleLogoutConfirmWindow : Window
    {
        public GoogleLogoutConfirmWindow()
        {
            Title = "Google 로그아웃 확인"; Width = 390; SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new StackPanel { Margin = new Thickness(22, 16, 22, 20) };
            root.Children.Add(OnharuPopupChrome.Header(this, "G  Google 계정 로그아웃", "#4338CA"));
            root.Children.Add(new TextBlock { Text = "Google 계정에서 로그아웃하시겠습니까?", FontSize = 15,
                FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush("#111827"), Margin = new Thickness(1, 4, 0, 5) });
            root.Children.Add(new TextBlock { Text = "동기화된 Google 일정은 화면에서 사라지고, 온하루 로컬 일정은 그대로 유지됩니다.",
                TextWrapping = TextWrapping.Wrap, Foreground = OnharuPopupChrome.Brush("#64748B"), Margin = new Thickness(1, 0, 0, 14) });

            var actions = new Grid(); actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = OnharuPopupChrome.Button("취소", 158, "#F1F5F9", "#475569");
            cancel.Height = 38; cancel.Margin = new Thickness(0, 0, 5, 0); cancel.Click += delegate { DialogResult = false; };
            actions.Children.Add(cancel);
            var logout = OnharuPopupChrome.Button("로그아웃", 158, "#DC2626", "#FFFFFF");
            logout.Background = OnharuPopupChrome.Brush("#D94B68");
            logout.Height = 38; logout.Margin = new Thickness(5, 0, 0, 0); logout.Click += delegate { DialogResult = true; };
            Grid.SetColumn(logout, 1); actions.Children.Add(logout); root.Children.Add(actions);
            Content = OnharuPopupChrome.Shell(root);
        }
    }
}
