using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FamilyPlanner
{
    sealed class UpdateAvailableWindow : Window
    {
        readonly UpdateInfo update;
        readonly TextBlock status;
        readonly Button install;

        public bool InstallerStarted { get; private set; }

        public UpdateAvailableWindow(UpdateInfo info)
        {
            update = info; Title = "ONHARU 업데이트"; Width = 470; SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new StackPanel { Margin = new Thickness(24, 17, 24, 21) };
            root.Children.Add(OnharuPopupChrome.Header(this, "↻  새 버전이 준비됐어요", "#4338CA"));
            root.Children.Add(new TextBlock { Text = "ONHARU " + info.VersionText + " 업데이트를 설치하시겠습니까?",
                FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = B("#1E293B"), Margin = new Thickness(2, 2, 2, 9) });
            root.Children.Add(new Border { Background = B("#F8FAFC"), BorderBrush = B("#E2E8F0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11), Padding = new Thickness(13, 10, 13, 10), MaxHeight = 150,
                Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new TextBlock { Text = ShortNotes(info.Notes), TextWrapping = TextWrapping.Wrap,
                        FontSize = 12, Foreground = B("#64748B"), LineHeight = 18 } } });
            status = new TextBlock { Foreground = B("#64748B"), FontSize = 11.5, Margin = new Thickness(2, 9, 2, 0) };
            root.Children.Add(status);
            var buttons = new Grid { Margin = new Thickness(0, 13, 0, 0) };
            buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var later = OnharuPopupChrome.FooterButton("나중에", "#E2E8F0", "#475569"); later.Margin = new Thickness(0, 0, 5, 0); later.Click += delegate { DialogResult = false; };
            install = OnharuPopupChrome.FooterButton("다운로드 후 설치", "#4F46E5", "#FFFFFF"); install.Margin = new Thickness(5, 0, 0, 0); install.Click += Install;
            buttons.Children.Add(later); Grid.SetColumn(install, 1); buttons.Children.Add(install); root.Children.Add(buttons);
            Content = OnharuPopupChrome.Shell(root);
        }

        async void Install(object sender, RoutedEventArgs e)
        {
            install.IsEnabled = false; status.Text = "설치 파일을 안전하게 확인하고 있어요…";
            try
            {
                var path = await UpdateService.DownloadVerifiedInstallerAsync(update);
                status.Text = "검증 완료 · 설치를 시작합니다."; UpdateService.LaunchInstaller(path);
                InstallerStarted = true; DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Download update", ex); status.Text = "자동 다운로드에 실패했습니다. 릴리스 페이지를 엽니다.";
                if (!string.IsNullOrWhiteSpace(update.PageUrl)) Process.Start(update.PageUrl);
                install.IsEnabled = true;
            }
        }

        static string ShortNotes(string notes)
        {
            notes = (notes ?? "새 기능과 안정성 개선이 포함되어 있습니다.").Trim();
            return notes.Length <= 900 ? notes : notes.Substring(0, 900) + "…";
        }

        static Brush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
