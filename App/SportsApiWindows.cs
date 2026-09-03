using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class SportsApiSetupWindow : Window
    {
        readonly PasswordBox keyBox = new PasswordBox { Height = 34, Padding = new Thickness(9, 6, 9, 5), FontSize = 13, BorderThickness = new Thickness(0), Background = Brushes.White };
        readonly TextBlock status = new TextBlock { FontSize = 11.5, Foreground = OnharuPopupChrome.Brush("#64748B"), Margin = new Thickness(0, 7, 0, 0), TextWrapping = TextWrapping.Wrap };

        public SportsApiSetupWindow()
        {
            Title = "프로야구 API 설정"; Width = 480; Height = 350; ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel { Margin = new Thickness(24, 18, 24, 20) };
            panel.Children.Add(OnharuPopupChrome.Header(this, "⚾  프로야구 API 설정", "#4338CA"));
            panel.Children.Add(new TextBlock { Text = "Parse.bot KBO Schedule API의 개인 키로 2026 KBO 일정을 불러옵니다.", FontSize = 13, Foreground = OnharuPopupChrome.Brush("#334155"), TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = "키는 이 PC의 Windows 계정에 암호화하여 저장하며 ONHARU 서버로 보내지 않습니다.", FontSize = 11.5, Foreground = OnharuPopupChrome.Brush("#64748B"), Margin = new Thickness(0, 5, 0, 13), TextWrapping = TextWrapping.Wrap });
            var guide = OnharuPopupChrome.Button("?  API 발급 방법 보기", double.NaN, "#EEF2FF", "#4338CA");
            guide.HorizontalAlignment = HorizontalAlignment.Stretch; guide.Click += delegate { new SportsApiGuideWindow { Owner = this }.ShowDialog(); }; panel.Children.Add(guide);
            panel.Children.Add(new TextBlock { Text = "API 키", FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush("#475569"), Margin = new Thickness(0, 13, 0, 5) });
            panel.Children.Add(new Border { Background = Brushes.White, BorderBrush = OnharuPopupChrome.Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Child = keyBox });
            status.Text = SportsApiKeyStore.HasKey ? "✓ 이 PC에 Parse.bot API 키가 연결되어 있습니다. 변경할 때만 새 키를 입력하세요." : "무료 계정은 200크레딧이며 월 일정 조회 1회에 1크레딧을 사용합니다."; panel.Children.Add(status);
            var footer = new Grid { Margin = new Thickness(0, 15, 0, 0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition());
            var remove = OnharuPopupChrome.FooterButton("API 키 삭제", "#FFF1F2", "#BE123C"); remove.Margin = new Thickness(0, 0, 5, 0); remove.IsEnabled = SportsApiKeyStore.HasKey;
            remove.Click += delegate { SportsApiKeyStore.Delete(); keyBox.Clear(); remove.IsEnabled = false; status.Text = "API 키를 삭제했습니다."; }; footer.Children.Add(remove);
            var connect = OnharuPopupChrome.ActionButton(SportsApiKeyStore.HasKey ? "새 API 키 연결" : "API 키 연결", double.NaN); connect.Margin = new Thickness(5, 0, 0, 0);
            connect.Click += async delegate
            {
                var key = keyBox.Password.Trim();
                if (key.Length < 12) { status.Text = "API 키를 정확히 입력해 주세요."; status.Foreground = OnharuPopupChrome.Brush("#DC2626"); return; }
                connect.IsEnabled = false; status.Text = "연결을 확인하는 중입니다…"; status.Foreground = OnharuPopupChrome.Brush("#64748B");
                try
                {
                    var error = await SportsApi.ValidateKey(key);
                    if (error != null) { status.Text = error; status.Foreground = OnharuPopupChrome.Brush("#DC2626"); return; }
                    SportsApiKeyStore.Save(key); status.Text = "✓ API 키가 안전하게 저장되었습니다."; status.Foreground = OnharuPopupChrome.Brush("#059669"); remove.IsEnabled = true; DialogResult = true;
                }
                catch (Exception ex) { ErrorLog.Write("Validate sports API key", ex); status.Text = "연결을 확인하지 못했습니다. 인터넷 연결을 확인해 주세요."; status.Foreground = OnharuPopupChrome.Brush("#DC2626"); }
                finally { connect.IsEnabled = true; }
            };
            Grid.SetColumn(connect, 1); footer.Children.Add(connect); panel.Children.Add(footer); Content = OnharuPopupChrome.Shell(panel);
        }
    }

    public sealed class SportsApiGuideWindow : Window
    {
        public SportsApiGuideWindow()
        {
            Title = "API 발급 방법"; Width = 500; Height = 450; ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel { Margin = new Thickness(24, 18, 24, 20) };
            panel.Children.Add(OnharuPopupChrome.Header(this, "API 발급 방법", "#4338CA"));
            panel.Children.Add(new TextBlock { Text = "Parse.bot 무료 API 키 받기", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = OnharuPopupChrome.Brush("#0F172A"), Margin = new Thickness(0, 0, 0, 10) });
            foreach (var line in new[] { "1. https://parse.bot/ 에 회원 가입합니다.", "2. KBO Schedule API 페이지에서 무료 API를 확인합니다.", "3. Parse.bot Settings에서 API Key를 복사합니다.", "4. ONHARU의 API 키 입력창에 붙여넣고 연결합니다." })
                panel.Children.Add(new TextBlock { Text = line, FontSize = 13, Foreground = OnharuPopupChrome.Brush("#334155"), Margin = new Thickness(0, 0, 0, 9), TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new Border { Background = OnharuPopupChrome.Brush("#FFFBEB"), BorderBrush = OnharuPopupChrome.Brush("#FDE68A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 4, 0, 10), Child = new TextBlock { Text = "월별 첫 조회와 새로고침에서만 크레딧을 사용하고 이후에는 PC 캐시를 사용합니다. 이 서비스는 KBO의 공식 개발자 API가 아닌 공개 일정의 관리형 연동 서비스입니다.", Foreground = OnharuPopupChrome.Brush("#92400E"), TextWrapping = TextWrapping.Wrap, FontSize = 11.5 } });
            panel.Children.Add(new TextBlock { Text = "사이트 주소", FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush("#475569"), Margin = new Thickness(0, 0, 0, 4) });
            var open = OnharuPopupChrome.ActionButton("https://parse.bot/marketplace · KBO Schedule API  ↗", double.NaN);
            open.Click += delegate { Process.Start(new ProcessStartInfo("https://parse.bot/marketplace/94785380-1559-45df-a2b8-58bad46be68a/koreabaseball-com-api") { UseShellExecute = true }); }; panel.Children.Add(open);
            Content = OnharuPopupChrome.Shell(panel);
        }
    }

    public partial class MainWindow
    {
        void OpenProBaseball(object sender, RoutedEventArgs e)
        {
            if (sportsWindow != null)
            {
                if (sportsWindow.WindowState == WindowState.Minimized) sportsWindow.WindowState = WindowState.Normal;
                sportsWindow.Activate(); return;
            }
            if (!SportsApiKeyStore.HasKey)
            {
                var setup = new SportsApiSetupWindow(); PlaceCalendarDialog(setup); ShowBlockingDialog(setup);
                if (!SportsApiKeyStore.HasKey) { ShowNotice("프로야구 일정을 사용하려면 API 키를 연결해 주세요.", false, "프로야구 일정"); return; }
            }
            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.SportsGameId))) item.SportsGameId = SportsApi.RegistrationId(item);
            CollapseDuplicateSportsItems();
            sportsWindow = new SportsCalendarWindow(items.Where(x => !string.IsNullOrWhiteSpace(x.SportsGameId)).Select(x => x.SportsGameId),
                settings.FavoriteBaseballTeam, settings.SportsCalendarScale, settings.GoogleCalendars, GoogleCalendar.IsConnected);
            PlaceCalendarDialog(sportsWindow);
            sportsWindow.ViewScaleChanged += delegate(double scale)
            {
                settings.SportsCalendarScale = scale;
                Store.SaveSettings(settings);
            };
            sportsWindow.RegistrationRequested += async delegate(List<PlannerItem> selectedItems)
            {
                foreach (var item in selectedItems) if (!items.Any(x => x.SportsGameId == item.SportsGameId)) items.Add(item);
                CollapseDuplicateSportsItems();
                Store.Save(items); RenderAll();
                var googleItems = selectedItems.Where(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId)).ToList();
                foreach (var item in googleItems) await SaveGoogleItem(item);
                var pending = googleItems.Count(x => x.PendingGoogleSync);
                return pending > 0 ? "PC 저장 완료 · Google 동기화 대기 " + pending + "건" :
                    googleItems.Count > 0 ? "선택한 경기 " + selectedItems.Count + "개를 Google Calendar에 등록했습니다." :
                    "선택한 경기 " + selectedItems.Count + "개를 PC에 등록했습니다.";
            };
            sportsWindow.Closed += delegate
            {
                if (!string.Equals(settings.FavoriteBaseballTeam ?? "", sportsWindow.FavoriteTeam ?? "", StringComparison.Ordinal))
                { settings.FavoriteBaseballTeam = sportsWindow.FavoriteTeam; Store.SaveSettings(settings); }
                sportsWindow = null;
            };
            sportsWindow.Show(); sportsWindow.Activate();
        }

        bool CollapseDuplicateSportsItems()
        {
            var changed = false;
            foreach (var item in items)
            {
                var stableId = SportsApi.RegistrationId(item);
                if (string.IsNullOrWhiteSpace(stableId) || item.SportsGameId == stableId) continue;
                item.SportsGameId = stableId; changed = true;
            }
            foreach (var group in items.Where(x => !string.IsNullOrWhiteSpace(x.SportsGameId)).GroupBy(x => x.SportsGameId).Where(x => x.Count() > 1).ToList())
            {
                var keep = group.OrderByDescending(x => !string.IsNullOrWhiteSpace(x.GoogleEventId)).ThenByDescending(x => x.CreatedInOnharu).First();
                changed |= items.RemoveAll(x => x != keep && x.SportsGameId == group.Key) > 0;
            }
            return changed;
        }
    }
}
