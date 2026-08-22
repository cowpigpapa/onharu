using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class DataFormatChoiceWindow : Window
    {
        public string SelectedFormat { get; private set; }

        public DataFormatChoiceWindow(string title, bool email)
        {
            Title = title; Width = 430; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new StackPanel { Margin = new Thickness(22, 16, 22, 20) };
            root.Children.Add(OnharuPopupChrome.Header(this, title, email ? "#4338CA" : "#047857"));
            root.Children.Add(new TextBlock { Text = email ? "메일로 보낼 파일 형식을 선택하세요." : "사용할 파일 형식을 선택하세요.",
                Foreground = B("#64748B"), Margin = new Thickness(1, 0, 0, 9) });
            root.Children.Add(Choice("ONHARU JSON", "로컬 일정 · ONHARU 정보 보존", "json", "#EEF2FF", "#4338CA"));
            root.Children.Add(Choice("표준 달력 ICS", "로컬 일정 · 다른 달력과 호환", "ics", "#ECFEFF", "#0F766E"));
            root.Children.Add(Choice("Excel CSV", "Google 포함 전체 일정 · 조회 및 보고", "csv", "#F0FDF4", "#15803D"));
            Content = OnharuPopupChrome.Shell(root);
        }

        Button Choice(string title, string detail, string format, string background, string foreground)
        {
            var text = new StackPanel { Margin = new Thickness(3, 0, 3, 0) };
            text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 13 });
            text.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = B("#64748B"), Margin = new Thickness(0, 2, 0, 0) });
            var button = OnharuPopupChrome.Button("", 350, background, foreground); button.Content = text;
            button.Height = 52; button.Margin = new Thickness(0, 4, 0, 0); button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Click += delegate { SelectedFormat = format; DialogResult = true; }; return button;
        }

        static Brush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    public sealed class RecoveryChoiceWindow : Window
    {
        public string SelectedAction { get; private set; }

        public RecoveryChoiceWindow(int backupCount, int localCount)
        {
            Title = "백업·로컬 일정"; Width = 430; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new StackPanel { Margin = new Thickness(22, 16, 22, 20) };
            root.Children.Add(OnharuPopupChrome.Header(this, "↶  백업·로컬 일정", "#4338CA"));
            root.Children.Add(new TextBlock { Text = "원하는 작업을 선택해 주세요.", Foreground = B("#64748B"), Margin = new Thickness(1, 0, 0, 9) });
            root.Children.Add(Choice("자동 백업 복원", backupCount > 0 ? backupCount + "개 백업에서 복원" : "사용 가능한 백업 없음", "backup", backupCount > 0));
            root.Children.Add(Choice("로그아웃 상태 일정 가져오기", localCount > 0 ? localCount + "개 로컬 일정 선택" : "가져올 로컬 일정 없음", "local", localCount > 0));
            Content = OnharuPopupChrome.Shell(root);
        }

        Button Choice(string title, string detail, string action, bool enabled)
        {
            var text = new StackPanel { Margin = new Thickness(3, 0, 3, 0) };
            text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 13 });
            text.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = B("#64748B"), Margin = new Thickness(0, 2, 0, 0) });
            var button = OnharuPopupChrome.Button("", 350, "#EEF2FF", "#4338CA"); button.Content = text;
            button.Height = 52; button.Margin = new Thickness(0, 4, 0, 0); button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.IsEnabled = enabled; button.Opacity = enabled ? 1 : .45;
            button.Click += delegate { SelectedAction = action; DialogResult = true; }; return button;
        }

        static Brush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
