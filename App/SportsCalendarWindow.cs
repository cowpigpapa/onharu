using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading.Tasks;

namespace FamilyPlanner
{
    public sealed class SportsCalendarWindow : Window
    {
        readonly Grid calendar = new Grid();
        readonly Grid calendarHost = new Grid();
        readonly Border loadingOverlay;
        readonly TextBlock monthTitle = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = OnharuPopupChrome.Brush("#334155"), VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock status = new TextBlock { FontSize = 11.5, Foreground = OnharuPopupChrome.Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center };
        readonly Button favoritePicker;
        readonly Popup favoritePopup;
        readonly OnharuSegmentedSwitch gameFilterSwitch, rangeSwitch, scaleSwitch;
        int visibleWeeks = 4;
        readonly List<string> favoriteTeams = new List<string>();
        readonly Dictionary<string, CheckBox> favoriteChecks = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
        bool favoriteOnly;
        readonly Button registerButton;
        readonly ComboBox registrationTarget;
        readonly HashSet<string> existingIds;
        readonly Dictionary<string, SportsGame> selected = new Dictionary<string, SportsGame>();
        double uiScale = 1.0;
        Popup viewOptionsPopup;
        DateTime navigationOrigin;
        int navigationVersion;
        List<SportsGame> games = new List<SportsGame>();
        DateTime month = DateTime.Today;
        internal List<PlannerItem> SelectedItems { get; private set; }
        internal string FavoriteTeam { get { return string.Join("|", favoriteTeams); } }
        internal event Func<List<PlannerItem>, Task<string>> RegistrationRequested;
        internal event Action<double> ViewScaleChanged;

        internal SportsCalendarWindow(IEnumerable<string> existingSportsIds, string favoriteTeam, double initialScale,
            IEnumerable<GoogleCalendarSetting> googleCalendars, bool googleConnected)
        {
            uiScale = initialScale < .95 ? .90 : initialScale > 1.05 ? 1.15 : 1.0;
            existingIds = new HashSet<string>(existingSportsIds ?? Enumerable.Empty<string>()); favoriteTeams.AddRange((favoriteTeam ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(2));
            monthTitle.FontSize = 19 * uiScale; status.FontSize = 12.5 * uiScale;
            // 창 크기에 그림자 여백 12px을 사방으로 더한다. 없으면 Shell의 DropShadow가 창 경계에서 잘려
            // 네 모서리에 검은 자국으로 남는다. 크기 조절은 아래 EnableResize가 테두리로 처리한다.
            Title = "KBO 경기 일정"; Width = Math.Min(SystemParameters.WorkArea.Width - 24, 1100 * uiScale + 24); Height = Math.Min(SystemParameters.WorkArea.Height - 24, 944 * uiScale + 24); MinWidth = Math.Min(964, Width); MinHeight = Math.Min(804, Height); WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new DockPanel { Margin = new Thickness(20, 16, 20, 18) };
            // 제목줄은 한 줄이다. 정체성·날짜 이동·오늘·응원팀·보기 설정·닫기만 두고
            // 나머지 조작은 톱니 버튼의 플로팅 팝오버로 접는다. 달력에 세로 공간을 최대한 넘긴다.
            var controls = new DockPanel { Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent, LastChildFill = false };
            OnharuPopupChrome.StyleHeader(controls);
            // 이동 버튼은 좌우 하나씩만 둔다. 4주 이동은 별도 버튼 대신 더블클릭으로 처리한다.
            var previous = VectorNavigationButton(true, false, "이전 · 더블클릭은 4주"); var next = VectorNavigationButton(false, false, "다음 · 더블클릭은 4주");
            var today = OnharuPopupChrome.Button("오늘", 46, OnharuPopupChrome.TodaySurfaceColor, OnharuPopupChrome.TodayTextColor);
            today.BorderBrush = OnharuPopupChrome.Brush(OnharuPopupChrome.TodayBorderColor);
            today.Height = 27; today.Padding = new Thickness(0); today.FontSize = 12.5; today.FontWeight = FontWeights.SemiBold; today.Margin = new Thickness(8, 0, 0, 0);
            BindNavigation(previous, -1); BindNavigation(next, 1);
            today.Click += delegate { month = visibleWeeks == 0 ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : DateTime.Today; LoadGames(false); };

            // 칸 폭은 팝오버 안쪽 폭을 꽉 채우도록 계산한다. 팝오버 214 - 테두리 2 - 안여백 24 = 188,
            // 여기서 스위치 자신의 테두리 1x2와 안여백 1x2를 빼면 184가 칸에 쓸 수 있는 폭이다.
            // 아래 도구 버튼들이 폭을 꽉 채우므로 스위치만 좁으면 오른쪽에 빈 자리가 남아 보인다.
            gameFilterSwitch = new OnharuSegmentedSwitch(new[] { "전체 경기", "응원팀" }, new[] { 92.0, 92.0 }, 0, delegate(int index) { SetGameFilter(index == 1); });
            rangeSwitch = new OnharuSegmentedSwitch(new[] { "4주", "월 전체" }, new[] { 92.0, 92.0 }, 0, delegate(int index) { SetRange(index == 0 ? 4 : 0); });
            scaleSwitch = new OnharuSegmentedSwitch(new[] { "작게", "중간", "크게" }, new[] { 61.34, 61.33, 61.33 },
                uiScale < .95 ? 0 : uiScale > 1.05 ? 2 : 1, delegate(int index) { ApplyViewScale(index == 0 ? .90 : index == 1 ? 1.0 : 1.15); });
            foreach (var segment in new[] { gameFilterSwitch, rangeSwitch, scaleSwitch })
            {
                segment.Height = 26; segment.Padding = new Thickness(1); segment.Margin = new Thickness(0, 0, 0, 8);
                // 선택색은 검색창 범위 버튼·알람 모드 버튼과 같은 값을 쓴다. 창마다 다른 선택색을 만들지 않는다.
                segment.SetPalette(OnharuPopupChrome.Brush("#EDE9FE"), OnharuPopupChrome.Brush("#6D28D9"),
                    OnharuPopupChrome.Brush("#F8FAFC"), OnharuPopupChrome.Brush("#64748B"), OnharuPopupChrome.Brush("#C4B5FD"));
            }

            var closeTop = OnharuPopupChrome.ToolCloseButton(this); closeTop.Margin = new Thickness(7, 0, 0, 0); closeTop.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(closeTop, Dock.Right); controls.Children.Add(closeTop);
            // 톱니는 글꼴 문자가 아니라 OnharuIcons 도형이다. 메인 헤더·설정창과 같은 그림을 쓴다.
            var optionsButton = OnharuPopupChrome.Button("", 30, "#FFFFFF", "#334155");
            optionsButton.Content = OnharuIcons.Draw("settings", OnharuPopupChrome.Brush("#334155"), 21);
            optionsButton.Padding = new Thickness(0);
            optionsButton.Height = 27; optionsButton.BorderBrush = OnharuPopupChrome.Brush("#CBD5E1");
            optionsButton.VerticalAlignment = VerticalAlignment.Center; optionsButton.ToolTip = "보기 설정과 도구"; UiRound.Apply(optionsButton, 8);
            DockPanel.SetDock(optionsButton, Dock.Right); controls.Children.Add(optionsButton);
            favoritePicker = OnharuPopupChrome.Button("응원팀 선택  ▾", 150, "#FFFFFF", "#334155");
            favoritePicker.Height = 27; favoritePicker.Margin = new Thickness(0, 0, 7, 0); favoritePicker.VerticalAlignment = VerticalAlignment.Center;
            favoritePicker.VerticalContentAlignment = VerticalAlignment.Center; favoritePicker.BorderBrush = OnharuPopupChrome.Brush("#C7D2FE");
            favoritePicker.BorderThickness = new Thickness(1); favoritePicker.ToolTip = "최대 2팀까지 선택";
            favoritePopup = new Popup { Placement = PlacementMode.Bottom, PlacementTarget = favoritePicker, AllowsTransparency = true, StaysOpen = false };
            favoritePicker.PreviewMouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (!favoritePopup.IsOpen) return; favoritePopup.IsOpen = false; e.Handled = true; };
            favoritePicker.Click += delegate { BuildFavoritePopup(); favoritePopup.IsOpen = true; };
            DockPanel.SetDock(favoritePicker, Dock.Right); controls.Children.Add(favoritePicker); UpdateFavoritePicker();

            var titleGroup = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleGroup.Children.Add(OnharuPopupChrome.FeatureHeading("⚾", "KBO 경기 일정"));
            previous.Margin = new Thickness(22, 0, 0, 0); titleGroup.Children.Add(previous);
            monthTitle.Margin = new Thickness(8, 0, 8, 0); monthTitle.VerticalAlignment = VerticalAlignment.Center; titleGroup.Children.Add(monthTitle);
            titleGroup.Children.Add(next); titleGroup.Children.Add(today);
            DockPanel.SetDock(titleGroup, Dock.Left); controls.Children.Add(titleGroup);

            // 보기 설정과 도구는 톱니 옆에 뜨는 플로팅 팝오버에 모은다.
            // 바깥을 눌러도 닫히지 않는다. 달력을 보면서 설정을 계속 열어 둘 수 있어야 하므로
            // StaysOpen 을 켜고 팝오버 안의 X 로만 닫는다. 제목 줄을 끌어 위치도 옮길 수 있다.
            viewOptionsPopup = new Popup { Placement = PlacementMode.Bottom, PlacementTarget = optionsButton, AllowsTransparency = true, StaysOpen = true, HorizontalOffset = -184 };
            var options = new StackPanel { Margin = new Thickness(12) };
            var optionsHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeAll };
            var optionsClose = OnharuPopupChrome.Button("\u2715", 24, "#FFFFFF", "#111827");
            optionsClose.Height = 24; optionsClose.FontSize = 11; optionsClose.BorderBrush = OnharuPopupChrome.Brush("#D6DCE8");
            optionsClose.ToolTip = "설정 닫기"; UiRound.Apply(optionsClose, 7);
            optionsClose.Click += delegate { viewOptionsPopup.IsOpen = false; };
            DockPanel.SetDock(optionsClose, Dock.Right); optionsHeader.Children.Add(optionsClose);
            optionsHeader.Children.Add(new TextBlock { Text = "보기 설정", FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = OnharuPopupChrome.Brush("#334155"), VerticalAlignment = VerticalAlignment.Center });
            EnablePopupDrag(optionsHeader);
            options.Children.Add(optionsHeader);
            options.Children.Add(OptionLabel("보기"));
            options.Children.Add(gameFilterSwitch); options.Children.Add(rangeSwitch); options.Children.Add(scaleSwitch);
            options.Children.Add(OptionLabel("도구"));
            // 선택을 더하는 동작이므로 선택 의미색(피치)을 쓴다. 아래 선택 해제는 중립색으로 짝을 맞춘다.
            options.Children.Add(OptionButton("응원팀 경기 선택", OnharuPopupChrome.SelectionSurfaceColor, OnharuPopupChrome.SelectionTextColor,
                delegate { SelectFavoriteGames(); }));
            options.Children.Add(OptionButton("선택 해제", "#FFFFFF", "#475569",
                delegate { selected.Clear(); RenderCalendar(); UpdateRegisterButton(); status.Text = "경기 선택을 모두 취소했습니다."; }));
            // 새로고침은 조회 동작이다. 로즈 계열은 삭제·경고에 배정돼 있어(design-onharu 3.4) 중립색을 쓴다.
            options.Children.Add(OptionButton("↻ 새로고침", "#F1F5F9", "#475569",
                delegate { LoadGames(true); }));
            options.Children.Add(OptionButton("API 설정", "#F1F5F9", "#475569",
                delegate { viewOptionsPopup.IsOpen = false; new SportsApiSetupWindow { Owner = this }.ShowDialog(); }));
            viewOptionsPopup.Child = new Border { Width = 214, Background = OnharuPopupChrome.Brush(OnharuPopupChrome.ContentSurfaceColor),
                BorderBrush = OnharuPopupChrome.Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 5, 0, 0), Child = options,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = System.Windows.Media.Color.FromRgb(30, 41, 59), BlurRadius = 16, ShadowDepth = 4, Opacity = .24 } };
            optionsButton.PreviewMouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (!viewOptionsPopup.IsOpen) return; viewOptionsPopup.IsOpen = false; e.Handled = true; };
            optionsButton.Click += delegate { viewOptionsPopup.IsOpen = true; };

            OnharuPopupChrome.EnableDrag(this, controls); DockPanel.SetDock(controls, Dock.Top); root.Children.Add(controls);
            var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // API 설정은 톱니 팝오버로 옮겼다. 하단에는 상태 문구만 남긴다.
            var footerLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; footerLeft.Children.Add(status); footer.Children.Add(footerLeft);
            var registration = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            registrationTarget = new ComboBox { Width = 210, Height = 40, Margin = new Thickness(10, 0, 8, 0), Padding = new Thickness(9, 0, 5, 0), Background = Brushes.White, BorderBrush = OnharuPopupChrome.Brush("#CBD5E1"), VerticalContentAlignment = VerticalAlignment.Center };
            SettingsWindow.StyleComboBox(registrationTarget);
            registrationTarget.Items.Add(new ComboBoxItem { Content = "ONHARU 로컬 일정", Tag = null, IsSelected = true });
            if (googleConnected)
                foreach (var source in (googleCalendars ?? Enumerable.Empty<GoogleCalendarSetting>()).Where(x => x.Editable && !GoogleTasks.IsSource(x.Id) && (x.AccessRole == "owner" || x.AccessRole == "writer")).OrderBy(x => x.Primary ? 0 : 1).ThenBy(x => x.Name))
                    registrationTarget.Items.Add(new ComboBoxItem { Content = "Google · " + source.Name, Tag = source });
            registration.Children.Add(registrationTarget);
            // 창의 대표 실행 버튼 하나에만 브랜드 그라데이션을 쓴다. 검색·시간표·알람과 같은 규격이다.
            registerButton = OnharuPopupChrome.Button("선택 경기 등록", 190, "#4F46E5", "#FFFFFF");
            registerButton.Background = OnharuPopupChrome.BrandGradientBrush(); registerButton.Foreground = Brushes.White;
            registerButton.BorderBrush = Brushes.Transparent; registerButton.Height = 40; registerButton.FontWeight = FontWeights.Bold;
            UiRound.Apply(registerButton, 8); registerButton.IsEnabled = false;
            registerButton.Click += async delegate
            {
                SelectedItems = selected.Values.Where(x => !existingIds.Contains(SportsApi.RegistrationId(x))).Select(ToPlannerItem).ToList();
                if (SelectedItems.Count == 0) { status.Text = "새로 등록할 경기를 선택해 주세요."; return; }
                var target = (registrationTarget.SelectedItem as ComboBoxItem == null ? null : (registrationTarget.SelectedItem as ComboBoxItem).Tag) as GoogleCalendarSetting;
                if (target != null)
                    foreach (var item in SelectedItems) { item.GoogleCalendarId = target.Id; item.GoogleCalendarName = target.Name; item.GoogleCalendarColor = target.Color; item.PendingGoogleSync = true; }
                registerButton.IsEnabled = false;
                var message = RegistrationRequested == null ? null : await RegistrationRequested(SelectedItems);
                foreach (var item in SelectedItems) existingIds.Add(item.SportsGameId);
                selected.Clear(); RenderCalendar(); UpdateRegisterButton();
                status.Text = string.IsNullOrWhiteSpace(message) ? "선택한 경기 " + SelectedItems.Count + "개를 등록했습니다." : message;
            };
            registration.Children.Add(registerButton); Grid.SetColumn(registration, 1); footer.Children.Add(registration); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            calendarHost.Children.Add(calendar); loadingOverlay = new Border { Background = OnharuPopupChrome.Brush("#F4F7FB"), Visibility = Visibility.Collapsed, Child = new Border { Background = Brushes.White, BorderBrush = OnharuPopupChrome.Brush(OnharuPopupChrome.PrimaryBorderColor), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(22, 15, 22, 15), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = new StackPanel { Children = { new TextBlock { Text = "⚾", FontSize = 25, HorizontalAlignment = HorizontalAlignment.Center }, new TextBlock { Text = "경기 일정을 준비하고 있어요…", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush(OnharuPopupChrome.PrimaryTextColor), Margin = new Thickness(0, 7, 0, 0) } } } } }; Panel.SetZIndex(loadingOverlay, 10); calendarHost.Children.Add(loadingOverlay);
            root.Children.Add(new Border { Background = Brushes.White, BorderBrush = OnharuPopupChrome.Brush("#DDE4EE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), ClipToBounds = true, Child = calendarHost });
            var shell = OnharuPopupChrome.Shell(root);
            shell.Margin = new Thickness(12);
            OnharuPopupChrome.EnableResize(this, shell);
            Content = shell; Loaded += delegate { LoadGames(false); };
            // 팝오버는 StaysOpen 이라 창을 닫아도 남을 수 있다. 창 수명에 묶어 둔다.
            Closed += delegate { if (viewOptionsPopup != null) viewOptionsPopup.IsOpen = false; if (favoritePopup != null) favoritePopup.IsOpen = false; };
        }

        // 팝오버 제목 줄을 잡아 옮긴다. Popup 은 창이 아니라서 DragMove 를 쓸 수 없으므로
        // 화면 좌표의 이동량을 그대로 Offset 에 더한다.
        void EnablePopupDrag(FrameworkElement handle)
        {
            var dragging = false;
            var startPoint = new Point();
            var startHorizontal = 0.0;
            var startVertical = 0.0;
            handle.MouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                dragging = true; startPoint = handle.PointToScreen(e.GetPosition(handle));
                startHorizontal = viewOptionsPopup.HorizontalOffset; startVertical = viewOptionsPopup.VerticalOffset;
                handle.CaptureMouse(); e.Handled = true;
            };
            handle.MouseMove += delegate(object sender, System.Windows.Input.MouseEventArgs e)
            {
                if (!dragging) return;
                var current = handle.PointToScreen(e.GetPosition(handle));
                viewOptionsPopup.HorizontalOffset = startHorizontal + (current.X - startPoint.X);
                viewOptionsPopup.VerticalOffset = startVertical + (current.Y - startPoint.Y);
            };
            handle.MouseLeftButtonUp += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                if (!dragging) return;
                dragging = false; handle.ReleaseMouseCapture(); e.Handled = true;
            };
        }

        static TextBlock OptionLabel(string text)
        {
            return new TextBlock { Text = text, FontSize = 11, Foreground = OnharuPopupChrome.Brush("#64748B"),
                Margin = new Thickness(2, 0, 0, 6) };
        }

        static Button OptionButton(string text, string background, string foreground, RoutedEventHandler click)
        {
            var button = OnharuPopupChrome.Button(text, double.NaN, background, foreground);
            button.Height = 30; button.Margin = new Thickness(0, 0, 0, 6);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            if (background == "#FFFFFF" || background == "#F1F5F9") button.BorderBrush = OnharuPopupChrome.Brush("#CBD5E1");
            if (click != null) button.Click += click;
            return button;
        }

        static Button AddButton(Panel panel, string text, double width, string background, string foreground, RoutedEventHandler click) { var button = OnharuPopupChrome.Button(text, width, background, foreground); button.Margin = new Thickness(5, 0, 0, 0); if (click != null) button.Click += click; panel.Children.Add(button); return button; }
        static Button VectorNavigationButton(bool left, bool doubleArrow, string toolTip)
        {
            var canvas = new Canvas { Width = doubleArrow ? 22 : 16, Height = 20, ClipToBounds = false };
            var paths = doubleArrow ? new[] { 2.0, 9.0 } : new[] { 4.0 };
            foreach (var x in paths)
            {
                var geometry = left
                    ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "M{0},4.5 L{1},10 L{0},15.5", x + 7, x + 1)
                    : string.Format(System.Globalization.CultureInfo.InvariantCulture, "M{0},4.5 L{1},10 L{0},15.5", x + 1, x + 7);
                canvas.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse(geometry), Stroke = OnharuPopupChrome.Brush("#3B82F6"),
                    StrokeThickness = 2.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round, Fill = Brushes.Transparent });
            }
            // 템플릿 뿌리를 투명 Border로 둔다. ContentPresenter만 두면 배경이 없어 화살표 획 자체만
            // 히트테스트되고, 선을 정확히 짚어야 눌린다. 메인 달력의 일·토 옆 버튼과 같은 방식으로
            // 버튼 면 전체에서 잡히게 한다.
            var surface = new FrameworkElementFactory(typeof(Border));
            surface.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            surface.AppendChild(presenter);
            return new Button { Content = canvas, Width = 30, Height = 30, Padding = new Thickness(0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = toolTip,
                Template = new ControlTemplate(typeof(Button)) { VisualTree = surface } };
        }
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
            if (IsLoaded) LoadGames(false);
        }

        // 메인 달력과 같은 이동 규칙을 쓴다. 한 번 클릭은 한 단계, 더블클릭은 한 기간이다.
        // 4주 보기: 클릭 1주, 더블클릭 4주. 월 보기: 클릭 한 달, 더블클릭은 첫 클릭이 이미 옮겼으므로 추가 이동 없음.
        // 메인과 달리 이 창의 이동은 네트워크 조회를 동반한다. 첫 클릭의 조회가 끝나기 전에 두 번째
        // 클릭이 들어오므로, 두 클릭 모두 같은 원점에서 계산하고 최신 요청만 결과를 반영한다.
        void BindNavigation(Button button, int direction)
        {
            button.PreviewMouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (e.ClickCount > 1)
                {
                    if (visibleWeeks == 0) return;
                    NavigateFrom(navigationOrigin, direction * visibleWeeks);
                    return;
                }
                navigationOrigin = month;
                NavigateFrom(navigationOrigin, direction);
            };
        }

        async void NavigateFrom(DateTime origin, int direction)
        {
            var request = ++navigationVersion;
            var target = visibleWeeks == 0 ? origin.AddMonths(direction) : origin.AddDays(direction * 7);
            var targetMonths = RequestedMonths(target); ShowLoading(true, "경기 일정을 확인하는 중입니다…");
            try
            {
                var targetGames = new List<SportsGame>(); foreach (var requested in targetMonths) targetGames.AddRange(await SportsApi.KboGames(requested.Year, requested.Month, false));
                if (request != navigationVersion) return;
                targetGames = targetGames.GroupBy(x => x.Id).Select(x => x.First()).ToList(); var first = DisplayFirst(target); var last = first.AddDays((visibleWeeks == 0 ? 5 : visibleWeeks) * 7 - 1);
                if (!targetGames.Any(x => x.LocalStart.Date >= first.Date && x.LocalStart.Date <= last.Date)) { status.Text = direction < 0 ? "이전 경기 일정이 없습니다." : "다음 경기 일정이 없습니다."; return; }
                month = target; games = targetGames; PopulateTeams(); RenderCalendar(); status.Text = monthTitle.Text + " · " + games.Count + "경기";
            }
            catch (Exception ex) { ErrorLog.Write("Navigate KBO games", ex); status.Text = ex.Message; }
            finally { if (request == navigationVersion) ShowLoading(false); }
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
            panel.Children.Add(new TextBlock { Text = "응원팀 선택 · 최대 2팀", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = OnharuPopupChrome.Brush(OnharuPopupChrome.PrimaryTextColor), Margin = new Thickness(4, 1, 4, 7) });
            foreach (var team in SportsTeamLogoStore.Names.OrderBy(x => x))
            {
                var selectedTeam = favoriteTeams.Contains(team); var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Height = 24, Margin = new Thickness(7, 0, 0, 0) }; row.Children.Add(TeamSymbol(team, 22)); row.Children.Add(new TextBlock { Text = team, FontSize = 12, Foreground = OnharuPopupChrome.Brush("#0F172A"), Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                var check = new CheckBox { Content = row, IsChecked = selectedTeam, IsEnabled = selectedTeam || favoriteTeams.Count < 2, Cursor = System.Windows.Input.Cursors.Hand, Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(1), Height = 30, VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
                check.Checked += delegate { if (favoriteTeams.Contains(team)) return; if (favoriteTeams.Count >= 2) { check.IsChecked = false; status.Text = "응원팀은 최대 2팀까지 선택할 수 있습니다."; return; } favoriteTeams.Add(team); FavoriteSelectionChanged(); };
                check.Unchecked += delegate { if (!favoriteTeams.Remove(team)) return; FavoriteSelectionChanged(); };
                favoriteChecks[team] = check; panel.Children.Add(check);
            }
            var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition()); var clear = OnharuPopupChrome.Button("전체 해제", 88, "#FFFFFF", "#64748B"); clear.Margin = new Thickness(0, 0, 4, 0); clear.Click += delegate { favoriteTeams.Clear(); FavoriteSelectionChanged(); }; var done = OnharuPopupChrome.ActionButton("완료", 88); done.FontWeight = FontWeights.Bold; done.Margin = new Thickness(4, 0, 0, 0); done.Click += delegate { favoritePopup.IsOpen = false; }; footer.Children.Add(clear); Grid.SetColumn(done, 1); footer.Children.Add(done); panel.Children.Add(footer);
            favoritePopup.Child = new Border { Width = 224, Background = Brushes.White, BorderBrush = OnharuPopupChrome.Brush("#D6DCE8"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(4), Child = panel };
        }
        void UpdateFavoriteChecks() { foreach (var pair in favoriteChecks) { var selectedTeam = favoriteTeams.Contains(pair.Key); pair.Value.IsEnabled = selectedTeam || favoriteTeams.Count < 2; if (pair.Value.IsChecked != selectedTeam) pair.Value.IsChecked = selectedTeam; } }
        void FavoriteSelectionChanged() { UpdateFavoritePicker(); UpdateFavoriteChecks(); if (favoriteOnly && favoriteTeams.Count == 0) SetGameFilter(false); else if (IsLoaded) RenderCalendar(); }
        void SelectFavoriteGames() { if (favoriteTeams.Count == 0) { status.Text = "먼저 응원팀을 선택해 주세요."; return; } foreach (var game in games.Where(x => favoriteTeams.Contains(x.AwayTeam) || favoriteTeams.Contains(x.HomeTeam))) if (!existingIds.Contains(SportsApi.RegistrationId(game))) selected[game.Id] = game; RenderCalendar(); UpdateRegisterButton(); status.Text = string.Join(" · ", favoriteTeams) + " 경기 " + selected.Count + "개를 선택했습니다."; }

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
                var cell = new Border { Background = OnharuPopupChrome.Brush(isToday ? OnharuPopupChrome.PrimarySurfaceColor : "#FFFFFF"), BorderBrush = OnharuPopupChrome.Brush(isToday ? OnharuPopupChrome.PrimaryBorderColor : "#E2E8F0"), BorderThickness = isToday ? new Thickness(2) : new Thickness(index % 7 == 0 ? 0 : 1, index < 7 ? 1 : 0, 0, 0), Child = panel }; Grid.SetRow(cell, index / 7 + 1); Grid.SetColumn(cell, index % 7); calendar.Children.Add(cell);
            }
        }
        string DateColor(DateTime date) { return date.Month != month.Month ? "#CBD5E1" : date.DayOfWeek == DayOfWeek.Sunday ? "#EF4444" : date.DayOfWeek == DayOfWeek.Saturday ? "#3B82F6" : "#0F172A"; }

        CheckBox GameChoice(SportsGame game)
        {
            var verticalScale = Math.Min(1.0, uiScale); var rowHeight = 21 * verticalScale; var id = SportsApi.RegistrationId(game); var content = new StackPanel { Orientation = Orientation.Horizontal, Height = rowHeight };
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
                var id = SportsApi.RegistrationId(game); var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(TeamSymbol(game.AwayTeam, 34)); row.Children.Add(new TextBlock { Text = game.AwayTeam, Width = 82, Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold }); row.Children.Add(new TextBlock { Text = game.HasScore ? game.AwayScore + "  :  " + game.HomeScore : game.IsCancelled ? "취소" : game.LocalStart.ToString("HH:mm"), Width = 70, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = OnharuPopupChrome.Brush(game.IsCancelled ? "#EF4444" : "#475569"), FontWeight = FontWeights.Bold }); row.Children.Add(TeamSymbol(game.HomeTeam, 34)); row.Children.Add(new TextBlock { Text = game.HomeTeam, Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold });
                var box = new CheckBox { Content = row, IsChecked = selected.ContainsKey(game.Id), IsEnabled = !existingIds.Contains(id), Height = 48, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 5), ToolTip = game.Stadium }; box.Checked += delegate { selected[game.Id] = game; }; box.Unchecked += delegate { selected.Remove(game.Id); }; panel.Children.Add(box);
            }
            var close = OnharuPopupChrome.ActionButton("선택 적용", double.NaN); close.Margin = new Thickness(0, 8, 0, 0); close.Click += delegate { window.Close(); }; panel.Children.Add(close); window.Content = OnharuPopupChrome.Shell(panel); window.Closed += delegate { RenderCalendar(); UpdateRegisterButton(); }; window.ShowDialog();
        }
        static string TeamShort(string team) { foreach (var name in new[] { "KIA", "SSG", "LG", "KT", "NC", "두산", "삼성", "롯데", "한화", "키움" }) if ((team ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return name; return string.IsNullOrWhiteSpace(team) ? "?" : team.Substring(0, Math.Min(2, team.Length)); }
        static string TeamColor(string team) { var name = TeamShort(team); if (name == "KIA" || name == "SSG") return "#CE0E2D"; if (name == "LG") return "#C30452"; if (name == "KT") return "#111827"; if (name == "NC") return "#315288"; if (name == "두산") return "#131230"; if (name == "삼성") return "#074CA1"; if (name == "롯데") return "#041E42"; if (name == "한화") return "#F37321"; if (name == "키움") return "#570514"; return "#64748B"; }
        void UpdateRegisterButton() { registerButton.IsEnabled = selected.Count > 0; registerButton.Content = selected.Count == 0 ? "선택 경기 ONHARU 등록" : "선택 경기 " + selected.Count + "개 등록"; }
        static PlannerItem ToPlannerItem(SportsGame game) { var start = game.LocalStart; return new PlannerItem { Id = Guid.NewGuid().ToString("N"), SportsGameId = SportsApi.RegistrationId(game), Title = "⚾ " + game.Title, Start = start, End = start.AddHours(3), AllDay = false, Category = "야구", CreatedInOnharu = true, Notes = "KBO 경기 일정" + (string.IsNullOrWhiteSpace(game.Stadium) ? "" : " · " + game.Stadium) }; }
    }
}
