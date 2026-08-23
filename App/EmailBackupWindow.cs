using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class EmailBackupWindow : Window
    {
        public string Recipient { get; private set; }

        public EmailBackupWindow(string connectedGoogleAddress, string format, int itemCount, bool includesGoogle)
        {
            Recipient = connectedGoogleAddress;
            Title = "메일로 보내기"; Width = 430; SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new StackPanel { Margin = new Thickness(22, 16, 22, 20) };
            root.Children.Add(OnharuPopupChrome.Header(this, "✉  메일로 보내기", "#4338CA"));
            root.Children.Add(new TextBlock { Text = "연결된 Google 계정", FontWeight = FontWeights.SemiBold, Foreground = B("#334155"), Margin = new Thickness(1, 1, 0, 6) });
            var account = new Border { CornerRadius = new CornerRadius(10), Background = B("#EEF2FF"), BorderBrush = B("#C7D2FE"),
                BorderThickness = new Thickness(1), Padding = new Thickness(12, 9, 12, 9) };
            account.Child = new TextBlock { Text = "G  " + connectedGoogleAddress, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = B("#4338CA") };
            root.Children.Add(account);
            root.Children.Add(new TextBlock { Text = "백업은 현재 연결된 Google 계정으로만 보낼 수 있습니다.\n다른 이메일 주소로는 전송할 수 없습니다.",
                Foreground = B("#64748B"), Margin = new Thickness(1, 7, 0, 0), LineHeight = 18 });
            root.Children.Add(new TextBlock { Text = format + " · " + (includesGoogle ? "Google 포함 전체 일정 " : "로컬 일정 ") + itemCount + "개\n" +
                    (includesGoogle ? "CSV에는 현재 표시된 Google 일정도 포함됩니다." : "Google 원본 일정은 첨부하지 않습니다."),
                Foreground = B("#64748B"), Margin = new Thickness(1, 9, 0, 12), LineHeight = 18 });
            var send = OnharuPopupChrome.Button("메일 보내기", 350, "#4F46E5", "#FFFFFF");
            send.Height = 40; send.Click += delegate { DialogResult = true; };
            root.Children.Add(send); Content = OnharuPopupChrome.Shell(root);
        }

        static Brush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
