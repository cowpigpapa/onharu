using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FamilyPlanner
{
    public sealed class SportsCalendarWindow : Window
    {
        readonly Grid calendar = new Grid();
        readonly Grid calendarHost = new Grid();
        readonly Border loadingOverlay;
        readonly TextBlock monthTitle = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = OnharuPopupChrome.Brush("#4338CA"), VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock status = new TextBlock { FontSize = 11.5, Foreground = OnharuPopupChrome.Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center };
        readonly Button favoritePicker;
        readonly Popup favoritePopup;
        Button previousPeriodButton, nextPeriodButton;
        readonly OnharuSegmentedSwitch gameFilterSwitch, rangeSwitch, scaleSwitch;
        int visibleWeeks = 4;
        readonly List<string> favoriteTeams = new List<string>();
        readonly Dictionary<string, CheckBox> favoriteChecks = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
        bool favoriteOnly;
        readonly Button registerButton;
        readonly HashSet<string> existingIds;
        readonly Dictionary<string, SportsGame> selected = new Dictionary<string, SportsGame>();
        double uiScale = 1.0;
        List<SportsGame> games = new List<SportsGame>();
        DateTime month = DateTime.Today;
        internal List<PlannerItem> SelectedItems { get; private set; }
        internal string FavoriteTeam { get { return string.Join("|", favoriteTeams); } }
        internal event Action<List<PlannerItem>> RegistrationRequested;
        internal event Action<double> ViewScaleChanged;

        internal SportsCalendarWindow(IEnumerable<string> existingSportsIds, string favoriteTeam, double initialScale)
        {
            uiScale = initialScale < .95 ? .90 : initialScale > 1.05 ? 1.15 : 1.0;
            existingIds = new HashSet<string>(existingSportsIds ?? Enumerable.Empty<string>()); favoriteTeams.AddRange((favoriteTeam ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(2));
            monthTitle.FontSize = 19 * uiScale; status.FontSize = 12.5 * uiScale;
            Title = "KBO 경기 일정"; Width = Math.Min(SystemParameters.WorkArea.Width - 24, 1100 * uiScale); Height = Math.Min(SystemParameters.WorkArea.Height - 24, 944 * uiScale); MinWidth = Math.Min(940, Width); MinHeight = Math.Min(780, Height); WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new DockPanel { Margin = new Thickness(20, 16, 20, 18) };
            var controls = new Grid { Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent }; controls.ColumnDefinitions.Add(new ColumnDefinition()); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); controls.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); controls.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var navigation = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; previousPeriodButton = OnharuPopupChrome.Button("«", 23, "#E0E7FF", "#4338CA"); var previous = OnharuPopupChrome.Button("‹", 23, "#EEF2FF", "#4338CA"); var today = OnharuPopupChrome.Button("오늘", 46, "#EEF2FF", "#4338CA"); var next = OnharuPopupChrome.Button("›", 23, "#EEF2FF", "#4338CA"); nextPeriodButton = OnharuPopupChrome.Button("»", 23, "#E0E7FF", "#4338CA");
            foreach (var button in new[] { previousPeriodButton, previous, today, next, nextPeriodButton }) { button.Height = 27; button.Padding = new Thickness(0); }
            previousPeriodButton.FontSize = nextPeriodButton.FontSize = 15; previous.FontSize = next.FontSize = 16; today.FontSize = 12.5; previousPeriodButton.FontWeight = nextPeriodButton.FontWeight = today.FontWeight = FontWeights.SemiBold;
            previousPeriodButton.Margin = new Thickness(0, 0, 3, 0); previous.Margin = new Thickness(0, 0, 5, 0); next.Margin = new Thickness(5, 0, 0, 0); nextPeriodButton.Margin = new Thickness(3, 0, 0, 0); today.Margin = new Thickness(6, 0, 0, 0);
            previousPeriodButton.Click += delegate { if (visibleWeeks != 0) Navigate(-4); }; nextPeriodButton.Click += delegate { if (visibleWeeks != 0) Navigate(4); };
            previous.Click += delegate { Navigate(-1); }; today.Click += delegate { month = visibleWeeks == 0 ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : DateTime.Today; LoadGames(false); }; next.Click += delegate { Navigate(1); };
            navigation.Children.Add(previousPeriodButton); navigation.Children.Add(previous); monthTitle.Margin = new Thickness(14, 0, 12, 0); navigation.Children.Add(monthTitle); navigation.Children.Add(next); navigation.Children.Add(nextPeriodButton); navigation.Children.Add(today); Grid.SetRow(navigation, 1);
            gameFilterSwitch = new OnharuSegmentedSwitch(new[] { "전체 경기", "응원팀 경기" }, new[] { 68.0, 84.0 }, 0, delegate(int index) { SetGameFilter(index == 1); });
            rangeSwitch = new OnharuSegmentedSwitch(new[] { "4주", "월" }, new[] { 48.0, 42.0 }, 0, delegate(int index) { SetRange(index == 0 ? 4 : 0); });
            scaleSwitch = new OnharuSegmentedSwitch(new[] { "작게", "중간", "크게" }, new[] { 46.0, 46.0, 46.0 },
                uiScale < .95 ? 0 : uiScale > 1.05 ? 2 : 1, delegate(int index) { ApplyViewScale(index == 0 ? .90 : index == 1 ? 1.0 : 1.15); });
            gameFilterSwitch.Margin = rangeSwitch.Margin = scaleSwitch.Margin = new Thickness(0, 0, 7, 0);
            var titleActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 7) }; titleActions.Children.Add(OnharuPopupChrome.FeatureTitle("⚾", "KBO 경기 일정")); var refreshTop = OnharuPopupChrome.Button("↻ 새로고침", 84, "#ECFDF5", "#047857"); refreshTop.Height = 27; refreshTop.Margin = new Thickness(10, 0, 0, 0); refreshTop.Click += delegate { LoadGames(true); }; titleActions.Children.Add(refreshTop); controls.Children.Add(titleActions);
            var viewOptions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 7) }; viewOptions.Children.Add(gameFilterSwitch); viewOptions.Children.Add(rangeSwitch); viewOptions.Children.Add(scaleSwitch); var closeTop = OnharuPopupChrome.CloseButton(this); closeTop.Width = closeTop.Height = 27; closeTop.Margin = new Thickness(1, 0, 0, 0); viewOptions.Children.Add(closeTop); Grid.SetColumn(viewOptions, 1); controls.Children.Add(viewOptions); controls.Children.Add(navigation);
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; right.Children.Add(new TextBlock { Text = "응원팀", FontSize = 12 * uiScale, FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush("#475569"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) }); favoritePicker = OnharuPopupChrome.Button("응원팀 선택  ▾", 150, "#FFFFFF", "#334155"); favoritePicker.Height = 30; favoritePicker.VerticalContentAlignment = VerticalAlignment.Center; favoritePicker.BorderBrush = OnharuPopupChrome.Brush("#C7D2FE"); favoritePicker.BorderThickness = new Thickness(1); favoritePicker.ToolTip = "최대 2팀까지 선택"; favoritePopup = new Popup { Placement = PlacementMode.Bottom, PlacementTarget = favoritePicker, AllowsTransparency = true, StaysOpen = false }; favoritePicker.PreviewMouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (!favoritePopup.IsOpen) return; favoritePopup.IsOpen = false; e.Handled = true; }; favoritePicker.Click += delegate { BuildFavoritePopup(); favoritePopup.IsOpen = true; }; right.Children.Add(favoritePicker); UpdateFavoritePicker();
            AddButton(right, "응원팀 경기 선택", 100, "#FFF7ED", "#C2410C", delegate { SelectFavoriteGames(); });
            AddButton(right, "전체 선택 취소", 92, "#EEF2FF", "#4338CA", delegate { selected.Clear(); RenderCalendar(); UpdateRegisterButton(); status.Text = "경기 선택을 모두 취소했습니다."; });
            Grid.SetColumn(right, 1); Grid.SetRow(right, 1); controls.Children.Add(right); OnharuPopupChrome.EnableDrag(this, controls); DockPanel.SetDock(controls, Dock.Top); root.Children.Add(controls);
            var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            var footerLeft = new StackPanel { Orientation = Orientation.Horizontal }; var apiButton = OnharuPopupChrome.Button("API 설정", 72, "#F1F5F9", "#475569"); apiButton.Margin = new Thickness(0, 0, 10, 0); apiButton.Click += delegate { new SportsApiSetupWindow { Owner = this }.ShowDialog(); }; footerLeft.Children.Add(apiButton); footerLeft.Children.Add(status); footer.Children.Add(footerLeft);
            registerButton = OnharuPopupChrome.PrimaryButton("선택 경기 ONHARU 등록", double.NaN); registerButton.Height = 40; registerButton.IsEnabled = false; registerButton.Click += delegate { SelectedItems = selected.Values.Where(x => !existingIds.Contains("parse-kbo:" + x.Id)).Select(ToPlannerItem).ToList(); if (SelectedItems.Count == 0) { status.Text = "새로 등록할 경기를 선택해 주세요."; return; } if (RegistrationRequested != null) RegistrationRequested(SelectedItems); foreach (var item in SelectedItems) existingIds.Add(item.SportsGameId); selected.Clear(); RenderCalendar(); UpdateRegisterButton(); status.Text = "선택한 경기 " + SelectedItems.Count + "개를 ONHARU에 등록했습니다."; }; Grid.SetColumn(registerButton, 1); footer.Children.Add(registerButton); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            calendarHost.Children.Add(calendar); loadingOverlay = new Border { Background = OnharuPopupChrome.Brush("#F4F7FB"), Visibility = Visibility.Collapsed, Child = new Border { Background = Brushes.White, BorderBrush = OnharuPopupChrome.Brush("#C7D2FE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(22, 15, 22, 15), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = new StackPanel { Children = { new TextBlock { Text = "⚾", FontSize = 25, HorizontalAlignment = HorizontalAlignment.Center }, new TextBlock { Text = "경기 일정을 준비하고 있어요…", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush("#4338CA"), Margin = new Thickness(0, 7, 0, 0) } } } } }; Panel.SetZIndex(loadingOverlay, 10); calendarHost.Children.Add(loadingOverlay);
            root.Children.Add(new Border { Background = Brushes.White, BorderBrush = OnharuPopupChrome.Brush("#DDE4EE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), ClipToBounds = true, Child = calendarHost }); Content = OnharuPopupChrome.Shell(root); Loaded += delegate { LoadGames(false); };
        }

        static Button AddButton(Panel panel, string text, double width, string background, string foreground, RoutedEventHandler click) { var button = OnharuPopupChrome.Button(text, width, background, foreground); button.Margin = new Thickness(5, 0, 0, 0); if (click != null) button.Click += click; panel.Children.Add(button); return button; }
        void ShowLoading(bool show, string message = null) { if (message != null) status.Text = message; loadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed; }

        void SetGameFilter(bool favorites)
        {
            if (favorites && favoriteTeams.Count == 0) { status.Text = "먼저 응원팀을 선택해 주세요."; return; }
            favoriteOnly = favorites; gameFilterSwitch.SetSelected(favorites ? 1 : 0, false); RenderCalendar();
            status.Text = favorites ? string.Join(" · ", favoriteTeams) + " 경기만 표시합니다." : "전체 경기를 표시합니다.";
        }

        void ApplyViewScale(double scale)
        {
            var currentWidth = ActualWidth > 0 ? ActualWidth : Width; var currentHeight = ActualHeight > 0 ? ActualHeight : Height;
            BeginAnimation(WidthProperty, null); BeginAnimation(HeightProperty, null); Width = currentWidth; Height = currentHeight;
            var work = SystemParameters.WorkArea; var targetWidth = Math.Min(Math.Max(940, work.Right - Left - 12), 1100 * scale); var targetHeight = Math.Min(Math.Max(780, work.Bottom - Top - 12), 944 * scale);
            uiScale = scale;
            monthTitle.FontSize = 19 * uiScale; status.FontSize = 12.5 * uiScale; favoritePicker.FontSize = 12 * uiScale;
            scaleSwitch.SetSelected(scale < .95 ? 0 : scale > 1.05 ? 2 : 1, false);
            if (ViewScaleChanged != null) ViewScaleChanged(uiScale);
            if (IsLoaded) { PopulateTeams(); RenderCalendar(); }
            var duration = new Duration(TimeSpan.FromMilliseconds(150)); var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(WidthProperty, new DoubleAnimation(currentWidth, targetWidth, duration) { EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd }); BeginAnimation(HeightProperty, new DoubleAnimation(currentHeight, targetHeight, duration) { EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd });
        }

        void SetRange(int weeks)
        {
            if (visibleWeeks == weeks) return; visibleWeeks = weeks;
            if (weeks == 4 && month.Year == DateTime.Today.Year && month.Month == DateTime.Today.Month) month = DateTime.Today;
            rangeSwitch.SetSelected(weeks == 0 ? 1 : 0, false);
            previousPeriodButton.IsEnabled = nextPeriodButton.IsEnabled = weeks != 0; previousPeriodButton.Opacity = nextPeriodButton.Opacity = weeks == 0 ? .38 : 1.0;
            if (IsLoaded) LoadGames(false);
        }

        async void Navigate(int direction)
        {
            var target = visibleWeeks == 0 ? month.AddMonths(direction) : month.AddDays(direction * 7);
            var targetMonths = RequestedMonths(target); ShowLoading(true, "경기 일정을 확인하는 중입니다…");
            try
            {
                var targetGames = new List<SportsGame>(); foreach (var requested in targetMonths) targetGames.AddRange(await SportsApi.KboGames(requested.Year, requested.Month, false));
                targetGames = targetGames.GroupBy(x => x.Id).Select(x => x.First()).ToList(); var first = DisplayFirst(target); var last = first.AddDays((visibleWeeks == 0 ? 5 : visibleWeeks) * 7 - 1);
                if (!targetGames.Any(x => x.LocalStart.Date >= first.Date && x.LocalStart.Date <= last.Date)) { status.Text = direction < 0 ? "이전 경기 일정이 없습니다." : "다음 경기 일정이 없습니다."; return; }
                month = target; games = targetGames; PopulateTeams(); RenderCalendar(); status.Text = monthTitle.Text + " · " + games.Count + "경기";
            }
            catch (Exception ex) { ErrorLog.Write("Navigate KBO games", ex); status.Text = ex.Message; }
            finally { ShowLoading(false); }
        }

        async void LoadGames(bool refresh)
        {
            monthTitle.Text = month.ToString("yyyy년 M월"); ShowLoading(true, refresh ? "변경된 KBO 일정을 확인하는 중입니다…" : "KBO 일정을 불러오는 중입니다…"); registerButton.IsEnabled = false;
            var requestedMonths = RequestedMonths(); var before = games.ToList();
            try
            {
                games = new List<SportsGame>(); foreach (var requested in requestedMonths) games.AddRange(await SportsApi.KboGames(requested.Year, requested.Month, refresh)); games = games.GroupBy(x => x.Id).Select(x => x.First()).ToList(); await SportsTeamLogoStore.EnsureDownloaded(); if (IsLoaded) { PopulateTeams(); RenderCalendar(); } var count = games.Count;
                if (refresh && before.Count > 0) { var oldMap = before.GroupBy(x => x.MatchKey).ToDictionary(x => x.Key, x => x.First()); var newMap = games.GroupBy(x => x.MatchKey).ToDictionary(x => x.Key, x => x.First()); status.Text = "새 일정 " + newMap.Keys.Count(x => !oldMap.ContainsKey(x)) + " · 변경 " + newMap.Keys.Count(x => oldMap.ContainsKey(x) && oldMap[x].Fingerprint != newMap[x].Fingerprint) + " · 취소 " + oldMap.Keys.Count(x => !newMap.ContainsKey(x)) + "  (총 " + count + "경기)"; }
                else status.Text = month.ToString("M월") + " · " + count + "경기";
            }
            catch (Exception ex) { ErrorLog.Write("Load KBO games", ex); games = new List<SportsGame>(); RenderCalendar(); status.Text = ex.Message; }
            finally { ShowLoading(false); }
        }

        DateTime DisplayFirst(DateTime anchor)
        {
            var weekStart = anchor.AddDays(-(int)anchor.DayOfWeek);
            return visibleWeeks == 0 ? new DateTime(anchor.Year, anchor.Month, 1).AddDays(-(int)new DateTime(anchor.Year, anchor.Month, 1).DayOfWeek) : weekStart.AddDays(-7);
        }

        List<DateTime> RequestedMonths() { return RequestedMonths(month); }
        List<DateTime> RequestedMonths(DateTime anchor)
        {
            var first = DisplayFirst(anchor); var shownWeeks = visibleWeeks == 0 ? 5 : visibleWeeks; var last = first.AddDays(shownWeeks * 7 - 1); var result = new List<DateTime>();
            for (var cursor = new DateTime(first.Year, first.Month, 1); cursor <= new DateTime(last.Year, last.Month, 1); cursor = cursor.AddMonths(1)) result.Add(cursor);
            return result;
        }

        void PopulateTeams()
        {
            UpdateFavoritePicker(); BuildFavoritePopup();
        }
        void UpdateFavoritePicker()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (favoriteTeams.Count == 0) row.Children.Add(new TextBlock { Text = "응원팀 선택", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            else foreach (var team in favoriteTeams) { row.Children.Add(TeamSymbol(team, 18)); row.Children.Add(new TextBlock { Text = team, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush("#334155"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 6, 0) }); }
            row.Children.Add(new TextBlock { Text = "▾", FontSize = 10, Foreground = OnharuPopupChrome.Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center }); favoritePicker.Content = row;
        }
        void BuildFavoritePopup()
        {
            if (favoritePopup == null) return; favoriteChecks.Clear(); var panel = new StackPanel { Margin = new Thickness(10, 9, 10, 9) };
            panel.Children.Add(new TextBlock { Text = "응원팀 선택 · 최대 2팀", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush("#4338CA"), Margin = new Thickness(4, 1, 4, 7) });
            foreach (var team in SportsTeamLogoStore.Names.OrderBy(x => x))
            {
                var selectedTeam = favoriteTeams.Contains(team); var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Height = 24, Margin = new Thickness(7, 0, 0, 0) }; row.Children.Add(TeamSymbol(team, 22)); row.Children.Add(new TextBlock { Text = team, FontSize = 12, Foreground = OnharuPopupChrome.Brush("#0F172A"), Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                var check = new CheckBox { Content = row, IsChecked = selectedTeam, IsEnabled = selectedTeam || favoriteTeams.Count < 2, Cursor = System.Windows.Input.Cursors.Hand, Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(1), Height = 30, VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
                check.Checked += delegate { if (favoriteTeams.Contains(team)) return; if (favoriteTeams.Count >= 2) { check.IsChecked = false; status.Text = "응원팀은 최대 2팀까지 선택할 수 있습니다."; return; } favoriteTeams.Add(team); FavoriteSelectionChanged(); };
                check.Unchecked += delegate { if (!favoriteTeams.Remove(team)) return; FavoriteSelectionChanged(); };
                favoriteChecks[team] = check; panel.Children.Add(check);
            }
            var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition()); var clear = OnharuPopupChrome.Button("전체 해제", 88, "#F1F5F9", "#64748B"); clear.Margin = new Thickness(0, 0, 4, 0); clear.Click += delegate { favoriteTeams.Clear(); FavoriteSelectionChanged(); }; var done = OnharuPopupChrome.Button("완료", 88, "#4F46E5", "#FFFFFF"); done.Margin = new Thickness(4, 0, 0, 0); done.Click += delegate { favoritePopup.IsOpen = false; }; footer.Children.Add(clear); Grid.SetColumn(done, 1); footer.Children.Add(done); panel.Children.Add(footer);
            favoritePopup.Child = new Border { Width = 224, Background = Brushes.White, BorderBrush = OnharuPopupChrome.Brush("#C7D2FE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(4), Child = panel };
        }
        void UpdateFavoriteChecks() { foreach (var pair in favoriteChecks) { var selectedTeam = favoriteTeams.Contains(pair.Key); pair.Value.IsEnabled = selectedTeam || favoriteTeams.Count < 2; if (pair.Value.IsChecked != selectedTeam) pair.Value.IsChecked = selectedTeam; } }
        void FavoriteSelectionChanged() { UpdateFavoritePicker(); UpdateFavoriteChecks(); if (favoriteOnly && favoriteTeams.Count == 0) SetGameFilter(false); else if (IsLoaded) RenderCalendar(); }
        void SelectFavoriteGames() { if (favoriteTeams.Count == 0) { status.Text = "먼저 응원팀을 선택해 주세요."; return; } foreach (var game in games.Where(x => favoriteTeams.Contains(x.AwayTeam) || favoriteTeams.Contains(x.HomeTeam))) if (!existingIds.Contains("parse-kbo:" + game.Id)) selected[game.Id] = game; RenderCalendar(); UpdateRegisterButton(); status.Text = string.Join(" · ", favoriteTeams) + " 경기 " + selected.Count + "개를 선택했습니다."; }

        void RenderCalendar()
        {
            calendar.Children.Clear(); calendar.RowDefinitions.Clear(); calendar.ColumnDefinitions.Clear(); for (var i = 0; i < 7; i++) calendar.ColumnDefinitions.Add(new ColumnDefinition());
            var weekCount = visibleWeeks == 0 ? 5 : visibleWeeks; calendar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); for (var i = 0; i < weekCount; i++) calendar.RowDefinitions.Add(new RowDefinition());
            var names = new[] { "일", "월", "화", "수", "목", "금", "토" }; for (var col = 0; col < 7; col++) { var day = new TextBlock { Text = names[col], FontSize = 12 * uiScale, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush(col == 0 ? "#DC2626" : col == 6 ? "#2563EB" : "#0F766E") }; Grid.SetColumn(day, col); calendar.Children.Add(day); }
            var first = DisplayFirst(month); var displayedGames = (favoriteOnly ? games.Where(x => favoriteTeams.Contains(x.AwayTeam) || favoriteTeams.Contains(x.HomeTeam)) : games).ToList(); monthTitle.Text = visibleWeeks > 0 ? first.ToString("M월 d일") + " – " + first.AddDays(weekCount * 7 - 1).ToString("M월 d일") : month.ToString("yyyy년 M월"); for (var index = 0; index < weekCount * 7; index++)
            {
                var date = first.AddDays(index); var isToday = date.Date == DateTime.Today; var dayGames = displayedGames.Where(x => x.LocalStart.Date == date.Date).ToList(); var panel = new StackPanel { Margin = new Thickness(4, 2, 4, 2) };
                var dateHeader = new StackPanel { Orientation = Orientation.Horizontal, Height = 20 * uiScale, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "이 날짜의 경기를 크게 보기" };
                var dateText = new TextBlock { Text = date.Day + (isToday ? "  오늘" : ""), FontSize = 13.5 * uiScale, FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal, Foreground = OnharuPopupChrome.Brush(isToday ? "#047857" : DateColor(date)), VerticalAlignment = VerticalAlignment.Center };
                dateHeader.Children.Add(dateText); if (visibleWeeks == 0 && dayGames.Count > 5) dateHeader.Children.Add(new TextBlock { Text = "  +" + (dayGames.Count - 5) + "경기", FontSize = 10.5 * uiScale, FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush("#4F46E5"), VerticalAlignment = VerticalAlignment.Center });
                dateHeader.MouseLeftButtonUp += delegate { ShowDayGames(date, dayGames); }; panel.Children.Add(dateHeader);
                foreach (var game in dayGames.Take(5)) panel.Children.Add(GameChoice(game));
                if (visibleWeeks != 0 && dayGames.Count > 5) { var more = new TextBlock { Text = "+" + (dayGames.Count - 5) + "경기 더보기", FontSize = 9.5 * uiScale, FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush("#4F46E5"), Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(3, 1, 0, 0) }; more.MouseLeftButtonUp += delegate { ShowDayGames(date, dayGames); }; panel.Children.Add(more); }
                var cell = new Border { Background = OnharuPopupChrome.Brush(isToday ? "#DCFCE7" : "#FFFFFF"), BorderBrush = OnharuPopupChrome.Brush(isToday ? "#10B981" : "#E2E8F0"), BorderThickness = isToday ? new Thickness(2) : new Thickness(index % 7 == 0 ? 0 : 1, index < 7 ? 1 : 0, 0, 0), Child = panel }; Grid.SetRow(cell, index / 7 + 1); Grid.SetColumn(cell, index % 7); calendar.Children.Add(cell);
            }
        }
        string DateColor(DateTime date) { return date.Month != month.Month ? "#CBD5E1" : date.DayOfWeek == DayOfWeek.Sunday ? "#EF4444" : date.DayOfWeek == DayOfWeek.Saturday ? "#3B82F6" : "#0F172A"; }

        CheckBox GameChoice(SportsGame game)
        {
            var verticalScale = Math.Min(1.0, uiScale); var rowHeight = 21 * verticalScale; var id = "parse-kbo:" + game.Id; var content = new StackPanel { Orientation = Orientation.Horizontal, Height = rowHeight };
            var timeText = game.IsCancelled ? "취소" : game.LocalStart.ToString("HH:mm"); content.Children.Add(new TextBlock { Text = timeText, Width = 42 * uiScale, FontSize = 11.5 * uiScale, Foreground = OnharuPopupChrome.Brush(game.IsCancelled ? "#EF4444" : "#64748B"), VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(TeamSymbol(game.AwayTeam, 24 * verticalScale)); content.Children.Add(new TextBlock { Text = game.HasScore ? "  " + game.AwayScore + " : " + game.HomeScore + "  " : "  vs  ", FontSize = (game.HasScore ? 11.5 : 10) * uiScale, FontWeight = game.HasScore ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center }); content.Children.Add(TeamSymbol(game.HomeTeam, 24 * verticalScale));
            if (game.IsCancelled) foreach (var child in content.Children.OfType<TextBlock>()) child.TextDecorations = TextDecorations.Strikethrough;
            var isFavorite = favoriteTeams.Contains(game.AwayTeam) || favoriteTeams.Contains(game.HomeTeam); var gameContent = new Border { Background = isFavorite ? OnharuPopupChrome.Brush("#FEF3C7") : Brushes.Transparent, BorderBrush = isFavorite ? OnharuPopupChrome.Brush("#F59E0B") : Brushes.Transparent, BorderThickness = new Thickness(isFavorite ? 1 : 0), CornerRadius = new CornerRadius(5), Padding = new Thickness(2, 0, 3, 0), Child = content };
            var box = new CheckBox { Content = gameContent, Height = rowHeight, MinHeight = 0, Padding = new Thickness(0), IsChecked = selected.ContainsKey(game.Id), IsEnabled = !existingIds.Contains(id), Foreground = OnharuPopupChrome.Brush(game.IsCancelled ? "#94A3B8" : existingIds.Contains(id) ? "#94A3B8" : "#0F766E"), ToolTip = (existingIds.Contains(id) ? "이미 ONHARU에 등록됨 · " : "") + game.Title + (string.IsNullOrWhiteSpace(game.Stadium) ? "" : " · " + game.Stadium), Margin = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center }; box.Checked += delegate { selected[game.Id] = game; UpdateRegisterButton(); }; box.Unchecked += delegate { selected.Remove(game.Id); UpdateRegisterButton(); }; return box;
        }
        static FrameworkElement TeamSymbol(string team, double size)
        {
            var logo = SportsTeamLogoStore.Image(team);
            if (logo != null) return new Image { Source = logo, Width = size, Height = size, Stretch = Stretch.Uniform, SnapsToDevicePixels = true };
            return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = OnharuPopupChrome.Brush(TeamColor(team)), Child = new TextBlock { Text = TeamMark(team), FontSize = size <= 19 ? 8 : 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        }
        static string TeamMark(string team) { var name = TeamShort(team); return name == "두산" ? "D" : name == "삼성" ? "S" : name == "롯데" ? "L" : name == "한화" ? "H" : name == "키움" ? "K" : name; }

        void ShowDayGames(DateTime date, List<SportsGame> dayGames)
        {
            if (dayGames.Count == 0) { status.Text = date.ToString("M월 d일") + "에는 경기가 없습니다."; return; }
            var window = new Window { Title = date.ToString("M월 d일 경기"), Width = 540 * uiScale, Height = Math.Min(700, (170 + dayGames.Count * 56) * uiScale), MinHeight = 300, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this };
            var panel = new StackPanel { Margin = new Thickness(22, 16, 22, 20) }; panel.Children.Add(OnharuPopupChrome.Header(window, "⚾  " + date.ToString("M월 d일 dddd") + " · " + dayGames.Count + "경기", "#0F766E"));
            foreach (var game in dayGames)
            {
                var id = "parse-kbo:" + game.Id; var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(TeamSymbol(game.AwayTeam, 34)); row.Children.Add(new TextBlock { Text = game.AwayTeam, Width = 82, Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold }); row.Children.Add(new TextBlock { Text = game.HasScore ? game.AwayScore + "  :  " + game.HomeScore : game.IsCancelled ? "취소" : game.LocalStart.ToString("HH:mm"), Width = 70, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = OnharuPopupChrome.Brush(game.IsCancelled ? "#EF4444" : "#475569"), FontWeight = FontWeights.Bold }); row.Children.Add(TeamSymbol(game.HomeTeam, 34)); row.Children.Add(new TextBlock { Text = game.HomeTeam, Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold });
                var box = new CheckBox { Content = row, IsChecked = selected.ContainsKey(game.Id), IsEnabled = !existingIds.Contains(id), Height = 48, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 5), ToolTip = game.Stadium }; box.Checked += delegate { selected[game.Id] = game; }; box.Unchecked += delegate { selected.Remove(game.Id); }; panel.Children.Add(box);
            }
            var close = OnharuPopupChrome.FooterButton("선택 적용", "#4F46E5", "#FFFFFF"); close.Margin = new Thickness(0, 8, 0, 0); close.Click += delegate { window.Close(); }; panel.Children.Add(close); window.Content = OnharuPopupChrome.Shell(panel); window.Closed += delegate { RenderCalendar(); UpdateRegisterButton(); }; window.ShowDialog();
        }
        static string TeamShort(string team) { foreach (var name in new[] { "KIA", "SSG", "LG", "KT", "NC", "두산", "삼성", "롯데", "한화", "키움" }) if ((team ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return name; return string.IsNullOrWhiteSpace(team) ? "?" : team.Substring(0, Math.Min(2, team.Length)); }
        static string TeamColor(string team) { var name = TeamShort(team); if (name == "KIA" || name == "SSG") return "#CE0E2D"; if (name == "LG") return "#C30452"; if (name == "KT") return "#111827"; if (name == "NC") return "#315288"; if (name == "두산") return "#131230"; if (name == "삼성") return "#074CA1"; if (name == "롯데") return "#041E42"; if (name == "한화") return "#F37321"; if (name == "키움") return "#570514"; return "#64748B"; }
        void UpdateRegisterButton() { registerButton.IsEnabled = selected.Count > 0; registerButton.Content = selected.Count == 0 ? "선택 경기 ONHARU 등록" : "선택 경기 " + selected.Count + "개 등록"; }
        static PlannerItem ToPlannerItem(SportsGame game) { var start = game.LocalStart; return new PlannerItem { Id = Guid.NewGuid().ToString("N"), SportsGameId = "parse-kbo:" + game.Id, Title = "⚾ " + game.Title, Start = start, End = start.AddHours(3), AllDay = false, Category = "야구", CreatedInOnharu = true, Notes = "KBO 경기 일정" + (string.IsNullOrWhiteSpace(game.Stadium) ? "" : " · " + game.Stadium) }; }
    }
}
