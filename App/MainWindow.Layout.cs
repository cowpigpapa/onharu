using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        UIElement BuildLayout()
        {
            filters.Clear(); dayCells.Clear();
            var root = new Grid { Margin = new Thickness(12, 8, 12, 5), Background = Brushes.Transparent };
            root.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (dateColorPalette != null && dateColorPalette.Visibility == Visibility.Visible &&
                    !IsInside(e.OriginalSource as DependencyObject, dateColorButton))
                    dateColorPalette.Visibility = Visibility.Collapsed;
                if (!positionLocked && e.GetPosition(root).Y <= 72 && !HasInteractiveParent(e.OriginalSource as DependencyObject))
                { DragMove(); }
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            var header = new Grid { Margin = new Thickness(0, 0, 0, 8), RenderTransform = new TranslateTransform(0, -3) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
            var logo = new Border { Width = 44, Height = 44, Background = T("Button"), BorderBrush = T("AccentBorder"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(7),
                Cursor = Cursors.Arrow, ToolTip = "온하루" };
            var logoTiles = new UniformGrid { Rows = 3, Columns = 3 };
            foreach (var color in new[] { "#38BDF8", "#60A5FA", "#818CF8", "#34D399", "#22C55E", "#A3E635", "#FBBF24", "#FB923C", "#F472B6" })
                logoTiles.Children.Add(new Border { Background = Brush(color), CornerRadius = new CornerRadius(2), Margin = new Thickness(1) });
            logo.Child = logoTiles;
            titleRow.Children.Add(logo);
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            opacitySlider = new Slider { Minimum = .10, Maximum = 1.0, Value = Math.Max(.10, Math.Min(1.0, settings.Opacity)),
                Width = 78, Height = 18, Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Arrow, ToolTip = "달력 투명도", Foreground = OpacitySliderBrush(), RenderTransformOrigin = new Point(.5, .5),
                RenderTransform = new ScaleTransform(1, .74) };
            opacitySlider.Template = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type Slider}'><Grid Height='18' Background='Transparent'><Border Width='2' Height='14' Background='{TemplateBinding Foreground}' HorizontalAlignment='Left' VerticalAlignment='Center' CornerRadius='1'/><Track x:Name='PART_Track' Orientation='Horizontal'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static Slider.DecreaseLarge}' Focusable='False' Foreground='{TemplateBinding Foreground}'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Height='4' Background='{TemplateBinding Foreground}' CornerRadius='2' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='11' Height='15' Foreground='{TemplateBinding Foreground}'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Ellipse Fill='White' Stroke='{TemplateBinding Foreground}' StrokeThickness='2'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static Slider.IncreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Height='4' Background='#CBD5E1' CornerRadius='2' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>");
            opacitySlider.ValueChanged += delegate
            {
                settings.Opacity = opacitySlider.Value;
                if (positionLocked) QueueFixedOpacityPreview(settings.Opacity);
                else Opacity = settings.Opacity;
            };
            const double compactHeaderWidth = 245;
            var brandLine = new Grid { Width = compactHeaderWidth, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center };
            brandLine.ColumnDefinitions.Add(new ColumnDefinition());
            var brandTitle = new TextBlock { Text = "온하루 · ONHARU", FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = BrandBrush(), VerticalAlignment = VerticalAlignment.Bottom };
            brandLine.Children.Add(brandTitle);
            // 2026-09-03: 투명도 슬라이더를 브랜드 줄에서 아래 슬라이딩 버튼 줄로 옮겼다.
            // 브랜드 표기 영역에 조작 컨트롤이 섞여 있었고, 기간·스킨·이동/고정과 한 줄에 모아야
            // 조작 층위가 분명해진다. 그 줄은 폭 384 중 스위치가 288을 써서 왼쪽 96이 비어 있다.
            brandLine.Margin = new Thickness(0); titleStack.Width = compactHeaderWidth; titleStack.Children.Add(brandLine);
            // 2026-09-03: 연·월 제목 옆의 `‹ • ›` 이동 버튼 셋을 사용자 요청으로 없앴다.
            // 제목을 누르면 열리는 날짜 선택기(OpenMonthJump)로 어떤 날짜로든 갈 수 있고,
            // 오늘로 이동은 오른쪽 상세의 `선택 날짜` 탭 더블클릭이 담당한다.
            var monthNavigation = new Grid { Width = compactHeaderWidth };
            monthNavigation.ColumnDefinitions.Add(new ColumnDefinition());
            monthTitle.HorizontalContentAlignment = HorizontalAlignment.Left; monthTitle.Margin = new Thickness(0); monthTitle.ToolTip = "클릭하여 날짜 선택";
            monthNavigation.Children.Add(monthTitle);
            titleStack.Children.Add(monthNavigation); titleRow.Children.Add(titleStack); header.Children.Add(titleRow);
            var upperActions = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var featureArea = new Grid { Width = 202, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 111, 0) };
            var featureActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center };
            featureArea.Children.Add(featureActions);
            featureIconArea = featureArea; featureIconRow = featureActions;
            var windowActions = new Grid { Width = 89, Height = 25, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0) };
            windowActions.ColumnDefinitions.Add(new ColumnDefinition()); windowActions.ColumnDefinitions.Add(new ColumnDefinition()); windowActions.ColumnDefinitions.Add(new ColumnDefinition());
            var lowerActions = new Grid { Width = 384, HorizontalAlignment = HorizontalAlignment.Right, ClipToBounds = false,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), RenderTransform = new TranslateTransform(0, 4) };
            var lowerSwitches = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center };
            lowerActions.Children.Add(lowerSwitches);
            opacitySlider.HorizontalAlignment = HorizontalAlignment.Left;
            opacitySlider.Margin = new Thickness(2, 0, 0, 0);
            lowerActions.Children.Add(opacitySlider);
            searchButton = IconButton("⌕", OpenSearch, 34); searchButton.Height = 28; searchButton.ToolTip = "일정 검색";
            searchButton.Margin = new Thickness(0);
            searchButton.Visibility = settings.ShowSearchIcon ? Visibility.Visible : Visibility.Collapsed; featureActions.Children.Add(searchButton);
            timetableButton = IconButton("▦", OpenTimetable, 34); timetableButton.Height = 28;
            timetableButton.ToolTip = "시간표 보기 · 편집"; timetableButton.Visibility = settings.UseTimetable ? Visibility.Visible : Visibility.Collapsed; timetableButton.Margin = new Thickness(4, 0, 0, 0);
            featureActions.Children.Add(timetableButton);
            diaryButton = IconButton("◴", OpenAlarm, 34); diaryButton.Height = 28; diaryButton.ToolTip = "알람 · 타이머";
            diaryButton.Visibility = settings.UseDiary ? Visibility.Visible : Visibility.Collapsed; diaryButton.Margin = new Thickness(4, 0, 0, 0); featureActions.Children.Add(diaryButton);
            sportsButton = IconButton("⚾", OpenProBaseball, 34);
            sportsButton.Height = 28; sportsButton.ToolTip = "프로야구 일정";
            sportsButton.Visibility = settings.UseProBaseball ? Visibility.Visible : Visibility.Collapsed; sportsButton.Margin = new Thickness(4, 0, 0, 0);
            featureActions.Children.Add(sportsButton);
            collapseSidebarButton = IconButton(settings.SidebarVisible ? "›" : "‹", ToggleSidebar, 14);
            collapseSidebarButton.Tag = "toggle_sidebar";
            collapseSidebarButton.Height = 34; collapseSidebarButton.Margin = new Thickness(0); collapseSidebarButton.Background = T("Calendar");
            collapseSidebarButton.BorderBrush = T("Grid"); collapseSidebarButton.BorderThickness = new Thickness(1);
            collapseSidebarButton.ToolTip = settings.SidebarVisible ? "일정 패널 접기" : "일정 패널 펼치기";
            collapseSidebarButton.MouseEnter += delegate { UpdateSidebarFloatButton(true); };
            collapseSidebarButton.MouseLeave += delegate { UpdateSidebarFloatButton(false); };
            calendarRangeSwitch = new OnharuSegmentedSwitch(
                new[] { Math.Max(1, Math.Min(6, settings.VisibleWeekCount)) + "주", "월 전체" }, new[] { 41.0, 55.0 }, temporaryMonthView ? 1 : 0,
                delegate(int index) { SetTemporaryMonthView(index == 1); });
            calendarRangeSwitch.Height = 24; calendarRangeSwitch.Padding = new Thickness(0);
            ApplyNeutralSwitchPalette(calendarRangeSwitch);
            calendarRangeSwitch.Clicked += delegate(int index, bool wasSelected)
            {
                if (index == 0 && wasSelected) OpenWeekCountPopup();
                else CloseWeekCountOverlay();
            };
            calendarRangeSwitch.Margin = new Thickness(0); calendarRangeSwitch.VerticalAlignment = VerticalAlignment.Center;
            calendarRangeSwitch.Visibility = settings.ShowRangeSwitch ? Visibility.Visible : Visibility.Collapsed;
            lowerSwitches.Children.Add(calendarRangeSwitch);
            themeQuickSwitch = new OnharuSegmentedSwitch(new[] { "파스텔", "블랙" }, new[] { 45.0, 38.0 }, settings.ThemeId == "dark" ? 1 : 0,
                delegate(int index)
                {
                    CloseWeekCountOverlay();
                    ApplyTheme(index == 1 ? "dark" : "classic");
                    Store.SaveSettings(settings);
                });
            themeQuickSwitch.Margin = new Thickness(4.5, 0, 0, 0); themeQuickSwitch.VerticalAlignment = VerticalAlignment.Center;
            themeQuickSwitch.Height = 24; themeQuickSwitch.Padding = new Thickness(0);
            themeQuickSwitch.Visibility = settings.ShowThemeSwitch ? Visibility.Visible : Visibility.Collapsed;
            lowerSwitches.Children.Add(themeQuickSwitch);
            UpdateThemeQuickSwitchStyle();
            UpdatePeriodNavigationButtons();
            positionModeSwitch = new OnharuSegmentedSwitch(new[] { "이동", "고정" }, new[] { 45.0, 45.0 }, positionLocked ? 1 : 0,
                async delegate(int index)
                {
                    CloseWeekCountOverlay();
                    if (index == 0) { EnterEditMode(); return; }
                    await System.Threading.Tasks.Task.Delay(140);
                    if (!positionLocked) LockCurrentPlacement();
                });
            AutomationProperties.SetAutomationId(positionModeSwitch, "OnharuPositionMode");
            positionModeSwitch.Tag = "position_mode";
            positionModeSwitch.Margin = new Thickness(4.5, 0, 0, 0); positionModeSwitch.Width = 92; positionModeSwitch.Height = 24;
            positionModeSwitch.Padding = new Thickness(0);
            positionModeSwitch.VerticalAlignment = VerticalAlignment.Center;
            positionModeSwitch.Visibility = settings.ShowPositionSwitch ? Visibility.Visible : Visibility.Collapsed;
            lowerSwitches.Children.Add(positionModeSwitch);
            googleButton = Button("G 연결", GoogleClick, 74); googleButton.Height = 28; googleButton.FontSize = 11;
            googleButton.Foreground = Brush("#2563EB"); googleButton.Visibility = Visibility.Collapsed;
            settingsButton = IconButton("", OpenSettings, 34); settingsButton.Content = HeaderGlyph("settings", T("Icon")); settingsButton.Height = 28; settingsButton.ToolTip = "색상 및 설정";
            settingsButton.Margin = new Thickness(4, 0, 0, 0); featureActions.Children.Add(settingsButton);
            StyleLightHeaderActionButton(searchButton, "⌕"); StyleLightHeaderActionButton(timetableButton, "▦");
            StyleLightHeaderActionButton(diaryButton, "◴"); StyleLightHeaderActionButton(sportsButton, "⚾"); StyleLightHeaderActionButton(settingsButton, "settings");
            var minimizeButton = IconButton("window_minimize", delegate { MinimizeToTray(); }, 25); minimizeButton.Height = 25; minimizeButton.ToolTip = "최소화";
            StyleWindowControl(minimizeButton, "window_minimize", new Thickness(0)); minimizeButton.HorizontalAlignment = HorizontalAlignment.Left;
            minimizeButton.RenderTransform = new TranslateTransform(2, 0); windowActions.Children.Add(minimizeButton);
            windowMaximizeButton = IconButton("window_maximize", delegate { if (!positionLocked) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }, 25);
            windowMaximizeButton.Height = 25; windowMaximizeButton.ToolTip = "최대화 · 고정 상태에서는 사용할 수 없음"; windowMaximizeButton.IsEnabled = !positionLocked; windowMaximizeButton.Opacity = positionLocked ? .4 : 1;
            StyleWindowControl(windowMaximizeButton, "window_maximize", new Thickness(0)); windowMaximizeButton.HorizontalAlignment = HorizontalAlignment.Center;
            windowMaximizeButton.RenderTransform = new TranslateTransform(1, 0); Grid.SetColumn(windowMaximizeButton, 1); windowActions.Children.Add(windowMaximizeButton);
            var closeWindowButton = IconButton("window_close", delegate { RequestExit(); }, 25); closeWindowButton.Height = 25; closeWindowButton.ToolTip = "끝내기";
            StyleWindowControl(closeWindowButton, "window_close", new Thickness(0)); closeWindowButton.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(closeWindowButton, 2); windowActions.Children.Add(closeWindowButton);
            upperActions.Children.Add(featureArea); upperActions.Children.Add(windowActions);
            var actionArea = new Grid { Width = 390, Height = 55, VerticalAlignment = VerticalAlignment.Bottom, ClipToBounds = false };
            actionArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            actionArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            actionArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            headerActionArea = actionArea;
            actionArea.Children.Add(upperActions);
            Grid.SetRow(lowerActions, 2); actionArea.Children.Add(lowerActions);
            // 검색 아이콘의 획이 기간 스위치의 왼쪽 테두리와 같은 선에 서게 맞춘다.
            featureActions.LayoutUpdated += delegate { AlignFeatureIconsToRangeSwitch(); };
            googleStatus = new TextBlock { Text = "동기화 완료", Foreground = Brush("#16A34A"),
                FontSize = 10.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 1, 1), Visibility = Visibility.Collapsed };
            Grid.SetColumn(actionArea, 1); header.Children.Add(actionArea);
            root.Children.Add(header);

            var body = new Grid(); bodyGrid = body; body.ColumnDefinitions.Add(new ColumnDefinition());
            sidebarColumn = new ColumnDefinition { Width = settings.SidebarVisible ? new GridLength(310) : new GridLength(0) };
            body.ColumnDefinitions.Add(sidebarColumn);
            var calendarCard = new Border { Background = T("Calendar"), CornerRadius = new CornerRadius(14),
                BorderBrush = T("CardBorder"), BorderThickness = new Thickness(1), Padding = new Thickness(5), Child = calendar };
            EnableCalendarDrop();
            body.Children.Add(calendarCard);
            sidebarPanel = new Border { Background = T("Sidebar"), BorderBrush = T("CardBorder"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14), Margin = new Thickness(12, 0, 0, 0), Padding = new Thickness(9),
                Visibility = settings.SidebarVisible ? Visibility.Visible : Visibility.Collapsed };
            var sideStack = new StackPanel();
            var categoryHeader = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            accountStatus.RenderTransform = accountStatusShift; Canvas.SetTop(accountStatus, 1); accountStatusViewport.Children.Add(accountStatus);
            accountStatusViewport.SizeChanged += delegate { StartAccountMarquee(); };
            var googleCardContent = new Grid();
            googleCardContent.Children.Add(accountStatusViewport);
            googleLoginButton = Button("Login", ToggleGoogleConnection, 53); googleLoginButton.Height = 23; googleLoginButton.FontSize = 10;
            System.Windows.Automation.AutomationProperties.SetAutomationId(googleLoginButton, "OnharuButton:google-login");
            googleLoginButton.Tag = "palette_independent_surface";
            googleLoginButton.Padding = new Thickness(2, 0, 2, 0); googleLoginButton.Margin = new Thickness(2, 1, 0, 1);
            googleLoginButton.FontWeight = FontWeights.Bold; googleLoginButton.Foreground = Brush(OnharuStateColors.GoogleButtonText(settings.ThemeId));
            googleLoginButton.Background = Brush(OnharuStateColors.GoogleButtonSurface(settings.ThemeId)); googleLoginButton.BorderBrush = googleLoginButton.Background;
            googleAccountCard = new Border { Background = OnharuStateColors.GoogleSurfaceBrush(settings.ThemeId), CornerRadius = new CornerRadius(9), Height = 27,
                Padding = new Thickness(10, 0, 58, 0), Child = googleCardContent, Cursor = Cursors.Hand,
                ToolTip = "클릭하여 Google Calendar 동기화", Tag = "google_sync" };
            googleAccountCard.MouseLeftButtonDown += GoogleClick;
            categoryHeader.Children.Add(googleAccountCard);
            googleLoginButton.HorizontalAlignment = HorizontalAlignment.Right;
            googleLoginButton.VerticalAlignment = VerticalAlignment.Top;
            googleLoginButton.Margin = new Thickness(0, 2, 3, 0);
            categoryHeader.Children.Add(googleLoginButton);
            UpdateAccountStatus();
            sideStack.Children.Add(categoryHeader);
            var filterGroups = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            filterGroups.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            filterGroups.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(17) });
            filterGroups.ColumnDefinitions.Add(new ColumnDefinition());
            filterGroups.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            filterGroups.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var localHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
            // Tag `toggle_section`은 고정 상태 전용 표식이다. 고정 화면의 적중 지도는 Button·CheckBox·Slider와
            // 몇몇 태그만 담는데, 이 머리글들은 TextBlock이라 담기지 않아 눌러도 아무 일이 없었다.
            var localTitle = new TextBlock { Text = "온하루 일정", Foreground = T("Muted"), FontSize = Ui(11),
                Tag = "toggle_section", VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            localHeader.Children.Add(localTitle);
            // Border여야 투명 배경이 그려지고 상자 전체가 눌린다. ContentControl은 기본 템플릿에
            // 테두리가 없어 배경이 칠해지지 않고, 결국 1.6px 획만 눌려 화살표를 잡기 어려웠다
            // (2026-09-03 사용자 보고). 그림은 12로 두고 누를 상자만 20으로 넓힌다.
            var localIndicator = SectionToggleIndicator(true);
            localHeader.Children.Add(localIndicator);
            localAllFilter = HeaderAllFilter("온하루 일정 전체 선택/해제", SetLocalFilters);
            localHeader.Children.Add(localAllFilter);
            var specialHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
            var specialTitle = new TextBlock { Text = "Special Day", Foreground = T("Muted"), FontSize = Ui(11),
                Tag = "toggle_section", VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            specialHeader.Children.Add(specialTitle);
            specialAllFilter = HeaderAllFilter("Special Day Card 전체 선택/해제", SetSpecialFilters);
            specialHeader.Children.Add(specialAllFilter);
            Grid.SetColumn(specialHeader, 2); filterGroups.Children.Add(localHeader); filterGroups.Children.Add(specialHeader);
            var divider = new Border { Width = 1, Background = T("Grid"), Margin = new Thickness(8, 1, 8, 2) };
            Grid.SetColumn(divider, 1); Grid.SetRowSpan(divider, 2); filterGroups.Children.Add(divider);
            localFilterRow = new UniformGrid { Columns = 2 };
            specialFilterRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetRow(localFilterRow, 1); Grid.SetRow(specialFilterRow, 1); Grid.SetColumn(specialFilterRow, 2);
            var filterCategories = new[] { "업무일정", "개인일정", "야구", "D-Day", "기념일" }
                .OrderBy(category => CategoryOrderPolicy.Rank(settings.CategoryOrder,
                    category == "업무일정" ? "local:business" : category == "개인일정" ? "local:personal" :
                    category == "야구" ? "local:baseball" : category == "D-Day" ? "special:dday" : "special:anniversary"));
            foreach (var category in filterCategories)
            {
                var enabled = category == "업무일정" ? settings.LocalBusinessEnabled : category == "개인일정" ? settings.LocalPersonalEnabled :
                    category == "야구" ? settings.LocalBaseballEnabled : category == "D-Day" ? settings.DdayEnabled : settings.AnniversaryEnabled;
                var visible = category == "업무일정" ? settings.BusinessVisible : category == "개인일정" ? settings.PersonalVisible :
                    category == "야구" ? settings.BaseballVisible : category == "기념일" ? settings.AnniversaryVisible : settings.DdayPanelVisible;
                var displayCategory = category == "업무일정" ? "업무" : category == "개인일정" ? "개인" : category;
                var box = new CheckBox { Content = displayCategory, IsChecked = visible,
                    Foreground = Brush(Colors[category]), Background = Brush(Colors[category]), Tag = Colors[category],
                    Margin = new Thickness(0, 0, 7, 4), VerticalAlignment = VerticalAlignment.Top,
                    Visibility = enabled ? Visibility.Visible : Visibility.Collapsed,
                    ToolTip = category == "D-Day" ? "오른쪽 D-Day 카드 표시" : null };
                box.Click += delegate { SaveWindowSettings(); UpdateGroupFilterChecks(); if (category == "D-Day") RenderDetail(); else RenderAll(); };
                filters[category] = box;
                (category == "D-Day" || category == "기념일" ? (Panel)specialFilterRow : localFilterRow).Children.Add(box);
            }
            NormalizeSpecialFilterSpacing();
            filterGroups.Children.Add(localFilterRow); filterGroups.Children.Add(specialFilterRow); sideStack.Children.Add(filterGroups);
            localTitle.MouseLeftButtonUp += delegate
            {
                var visible = localFilterRow.Visibility != Visibility.Visible;
                localFilterRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                specialFilterRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                SetSectionToggleIndicator(localIndicator, visible);
                localHeader.Margin = new Thickness(0, 0, 0, visible ? 7 : 0);
                specialHeader.Margin = new Thickness(0, 0, 0, visible ? 7 : 0);
                filterGroups.Margin = new Thickness(0, 0, 0, visible ? 14 : 6);
            };
            localIndicator.MouseLeftButtonUp += delegate { localTitle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent }); };
            specialTitle.MouseLeftButtonUp += delegate { localTitle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent }); };
            var googleHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
            var googleTitle = new TextBlock { Text = "Google", Foreground = T("Muted"), FontSize = Ui(11),
                Tag = "toggle_section", VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            googleHeader.Children.Add(googleTitle);
            var googleIndicator = SectionToggleIndicator(true);
            googleHeader.Children.Add(googleIndicator);
            googleAllFilter = HeaderAllFilter("Google 일정 전체 선택/해제", SetGoogleFilters);
            googleHeader.Children.Add(googleAllFilter);
            sideStack.Children.Add(googleHeader);
            googleFilterPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            sideStack.Children.Add(googleFilterPanel); BuildGoogleFilters();
            googleTitle.MouseLeftButtonUp += delegate
            {
                var visible = googleFilterPanel.Visibility != Visibility.Visible;
                googleFilterPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                SetSectionToggleIndicator(googleIndicator, visible);
                googleHeader.Margin = new Thickness(0, 0, 0, visible ? 7 : 10);
            };
            googleIndicator.MouseLeftButtonUp += delegate { googleTitle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent }); };
            UpdateGroupFilterChecks();
            detailPeriodSwitch = new OnharuSegmentedSwitch(new[] { "선택 날짜", "이번 주", "다음 주" }, new[] { 92.0, 92.0, 92.0 }, 0,
                delegate(int index) { detailMode = index == 1 ? "this_week" : index == 2 ? "next_week" : "selected"; RenderDetail(); });
            detailPeriodSwitch.Width = 278; detailPeriodSwitch.Height = 24;
            detailPeriodSwitch.Padding = new Thickness(0); detailPeriodSwitch.HorizontalAlignment = HorizontalAlignment.Left;
            detailPeriodSwitch.Tag = "fixed_detail_period_palette";
            detailPeriodSwitch.SegmentTarget(0).AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(delegate(object sender, MouseButtonEventArgs e)
                {
                    if (e.ClickCount < 2) return; GoToday(); e.Handled = true;
                }), true);
            detailPeriodSwitch.Margin = new Thickness(0, 0, 0, 6); ApplyDetailSwitchPalette(detailPeriodSwitch);
            detailPeriodSwitch.LayoutUpdated += delegate { AlignSidebarToggleToDetailSwitch(); };
            sideStack.Children.Add(detailPeriodSwitch);
            var detailHeader = new DockPanel();
            dateColorButton = IconButton("", null, 23); dateColorButton.Width = 23; dateColorButton.MinWidth = 23; dateColorButton.Height = 23;
            dateColorButton.VerticalAlignment = VerticalAlignment.Center;
            dateColorButton.Margin = new Thickness(5, 0, 0, 0); dateColorButton.Padding = new Thickness(0);
            dateColorButton.Background = Brushes.Transparent; dateColorButton.Foreground = Brush("#64748B");
            dateColorButton.BorderBrush = Brushes.Transparent; dateColorButton.BorderThickness = new Thickness(0);
            dateColorButton.Click += delegate
            {
                var opening = dateColorPalette.Visibility != Visibility.Visible;
                dateColorPalette.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
                if (opening) PositionDateColorPalette();
                if (positionLocked) SchedulePublish();
            };
            dateColorButton.ToolTip = "중요한 날";
            detailAddButton = IconButton("add", AddItem, 26); detailAddButton.Width = 26; detailAddButton.MinWidth = 26; detailAddButton.Height = 26;
            detailAddButton.FontSize = 17; detailAddButton.FontWeight = FontWeights.SemiBold;
            detailAddButton.Padding = new Thickness(0); detailAddButton.HorizontalContentAlignment = HorizontalAlignment.Center;
            detailAddButton.VerticalContentAlignment = VerticalAlignment.Center; detailAddButton.VerticalAlignment = VerticalAlignment.Center;
            detailAddButton.RenderTransform = new TranslateTransform(0, 1);
            detailAddButton.ToolTip = "이 날짜에 일정 추가";
            System.Windows.Automation.AutomationProperties.SetName(detailAddButton, "이 날짜에 일정 추가");
            System.Windows.Automation.AutomationProperties.SetAutomationId(detailAddButton, "OnharuDetailAdd");
            detailAddButton.Margin = new Thickness(5, 0, 0, 0); StyleDetailHeaderActionButtons();
            DockPanel.SetDock(detailAddButton, Dock.Right); detailHeader.Children.Add(detailAddButton);
            var detailTools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = new TranslateTransform(0, 1) };
            BuildDetailOrderSwitch();
            detailTools.Children.Add(detailIncompleteButton); detailTools.Children.Add(detailCategoryButton); detailTools.Children.Add(detailTimeButton);
            DockPanel.SetDock(detailTools, Dock.Right); detailHeader.Children.Add(detailTools);
            var selectedTitleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            dateColorButton.Margin = new Thickness(5, 0, 0, 0);
            selectedTitleRow.Children.Add(selectedTitle); selectedTitleRow.Children.Add(dateColorButton);
            detailHeader.Children.Add(selectedTitleRow); sideStack.Children.Add(detailHeader);
            dateColorPalette = BuildInlineDateColorPalette();
            sideStack.Children.Add(new Border { Height = 8, Background = Brushes.Transparent });
            detailScroll = new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Tag = "detail_scroll" };
            detailScroll.Loaded += delegate
            {
                StyleDetailScrollBar();
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(detailScroll, "OnharuDetailScroll");
            EnableDetailCardOrderSurface();
            var sideLayout = new Grid();
            sideLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sideLayout.RowDefinitions.Add(new RowDefinition());
            sideLayout.Children.Add(sideStack); Grid.SetRow(detailScroll, 1); sideLayout.Children.Add(detailScroll);
            sidebarPanel.Child = sideLayout; Grid.SetColumn(sidebarPanel, 1); body.Children.Add(sidebarPanel);
            collapseSidebarButton.HorizontalAlignment = HorizontalAlignment.Left; collapseSidebarButton.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(collapseSidebarButton, 1); UpdateSidebarFloatButton(false);
            Panel.SetZIndex(collapseSidebarButton, 60); body.Children.Add(collapseSidebarButton);
            Grid.SetRow(body, 1); body.Margin = new Thickness(0, 0, 0, 18); root.Children.Add(body);
            var credit = new TextBlock { Text = "MADE BY JUAN.HJLEE · ONHARU (ver. 2.2.5)", FontSize = 10,
                FontWeight = FontWeights.SemiBold, Foreground = T("Heading"),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(12, 0, 0, 1) };
            Grid.SetRow(credit, 1); Panel.SetZIndex(credit, 25); root.Children.Add(credit);
            Grid.SetRow(googleStatus, 1); Panel.SetZIndex(googleStatus, 25);
            googleStatus.HorizontalAlignment = HorizontalAlignment.Right; googleStatus.VerticalAlignment = VerticalAlignment.Bottom;
            googleStatus.Margin = new Thickness(0, 0, 12, 1); root.Children.Add(googleStatus);
            var shell = new Border { CornerRadius = new CornerRadius(18), Background = T("Shell"),
                BorderBrush = T("CardBorder"), BorderThickness = new Thickness(1), Child = root };
            resizeSurface = shell;
            shell.PreviewMouseMove += ResizeSurfaceMouseMove;
            shell.MouseLeave += delegate { if (!positionLocked) shell.Cursor = Cursors.Arrow; };
            shell.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (positionLocked) return;
                var edge = OnharuPopupChrome.ResizeEdgeAt(e.GetPosition(shell), shell);
                if (edge == 0) return;
                BeginResize(shell, edge); e.Handled = true;
            };
            mainFrame = new Grid(); mainFrame.Children.Add(shell);
            floatingOverlay = new Canvas { ClipToBounds = false };
            Panel.SetZIndex(floatingOverlay, 100); mainFrame.Children.Add(floatingOverlay);
            floatingOverlay.Children.Add(dateColorPalette); return mainFrame;
        }

        void BuildDetailOrderSwitch()
        {
            if (detailCategoryButton != null)
            {
                detailIncompleteButton.Visibility = settings.ShowIncompleteTodoButton ? Visibility.Visible : Visibility.Collapsed;
                StyleDetailHeaderActionButtons(); return;
            }
            detailCategoryButton = DetailToolButton("detail_category", "카테고리순", delegate { SetDetailOrder("category"); });
            detailTimeButton = DetailToolButton("detail_time", "시간순", delegate { SetDetailOrder("time"); });
            detailIncompleteButton = DetailToolButton("detail_incomplete", "미완료 일정", delegate
            { CloseWeekCountOverlay(); detailIncompleteMode = true; RenderDetail(); });
            detailIncompleteButton.Visibility = settings.ShowIncompleteTodoButton ? Visibility.Visible : Visibility.Collapsed;
            StyleDetailHeaderActionButtons();
        }

        Button DetailToolButton(string glyph, string name, Action action)
        {
            var button = IconButton(glyph, delegate { action(); }, 26);
            button.Width = button.MinWidth = button.Height = 18; button.Margin = new Thickness(0, 0, 4, 0);
            button.ToolTip = name; System.Windows.Automation.AutomationProperties.SetName(button, name);
            return button;
        }

        void SetDetailOrder(string mode)
        {
            CloseWeekCountOverlay(); detailIncompleteMode = false; settings.DetailOrderMode = mode;
            Store.SaveSettings(settings); RenderDetail();
        }

        // 그룹 접힘 화살표. 그림은 12px 그대로 두고 누를 상자만 20px로 넓힌다.
        // 투명 배경을 칠해야 상자 안쪽 빈 곳도 눌린다.
        Border SectionToggleIndicator(bool expanded)
        {
            var indicator = new Border { Width = 20, Height = 20, Background = Brushes.Transparent,
                Tag = "toggle_section", Margin = new Thickness(1, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            SetSectionToggleIndicator(indicator, expanded);
            return indicator;
        }

        void SetSectionToggleIndicator(Border indicator, bool expanded)
        {
            if (indicator == null) return;
            var glyph = OnharuIcons.Draw(expanded ? "chevron_up" : "chevron_down", T("Muted"), 12);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            indicator.Child = glyph;
        }

        void ToggleSidebar(object sender, RoutedEventArgs e)
        {
            settings.SidebarVisible = !settings.SidebarVisible;
            sidebarPanel.Visibility = settings.SidebarVisible ? Visibility.Visible : Visibility.Collapsed;
            sidebarColumn.Width = settings.SidebarVisible ? new GridLength(310) : new GridLength(0);
            collapseSidebarButton.ToolTip = settings.SidebarVisible ? "일정 패널 접기" : "일정 패널 펼치기";
            UpdateSidebarFloatButton(collapseSidebarButton.IsMouseOver);
            AlignSidebarToggleToDetailSwitch();
            Store.SaveSettings(settings);
        }

        // 접힘·펼침 버튼은 세부 일정의 기간 스위치와 세로 중앙을 맞춘다. 시작 직후에는
        // sidebarToggleTop이 초기값이라 버튼이 위쪽 옛 자리에 서 있고, 스위치가 실제로
        // 배치된 뒤에야 제자리로 내려온다. 배치 전 좌표(0)를 받아들이면 그 옛 자리가 굳는다.
        // 고정 상태에서는 화면이 캐시된 프레임이라 자리를 옮기고 다시 그리지 않으면
        // 옛 자리가 그대로 남는다. 실제로 그 증상이 보고돼 갱신마다 다시 그리게 했다.
        void AlignSidebarToggleToDetailSwitch() { AlignSidebarToggleToDetailSwitch(false); }

        void AlignSidebarToggleToDetailSwitch(bool measuringWhileHidden)
        {
            if (collapseSidebarButton == null || detailPeriodSwitch == null || bodyGrid == null) return;
            if (!measuringWhileHidden && (!settings.SidebarVisible || !detailPeriodSwitch.IsVisible)) return;
            if (detailPeriodSwitch.ActualHeight <= 0 || bodyGrid.ActualHeight <= 0) return;
            var point = detailPeriodSwitch.TranslatePoint(new Point(0, 0), bodyGrid);
            var top = point.Y + (detailPeriodSwitch.ActualHeight - collapseSidebarButton.Height) / 2;
            if (top <= 0 || Math.Abs(sidebarToggleTop - top) < .5) return;
            sidebarToggleTop = top; UpdateSidebarFloatButton(collapseSidebarButton.IsMouseOver);
            if (positionLocked) SchedulePublish();
        }

        // 접힌 상태로 시작하면 기간 스위치가 배치된 적이 없어 버튼이 초기값 자리에 선다.
        // 그 뒤 한 번 펼치면 제자리로 내려오고 다시 접어도 그 자리를 지키므로, 같은 접힘
        // 상태인데 자리가 두 가지가 된다. 사용자가 본 증상이 이것이다.
        // 첫 배치 직후 사이드바를 잠깐 펼쳐 좌표만 재고 곧바로 되돌린다. 같은 디스패처
        // 작업 안에서 끝나므로 화면에는 펼쳐진 모습이 나타나지 않는다.
        void EnsureSidebarTogglePlacement()
        {
            if (settings.SidebarVisible || sidebarPanel == null || sidebarColumn == null || bodyGrid == null) return;
            sidebarPanel.Visibility = Visibility.Visible; sidebarColumn.Width = new GridLength(310);
            bodyGrid.UpdateLayout();
            AlignSidebarToggleToDetailSwitch(true);
            sidebarPanel.Visibility = Visibility.Collapsed; sidebarColumn.Width = new GridLength(0);
            bodyGrid.UpdateLayout();
            UpdateSidebarFloatButton(false);
        }

        void UpdateSidebarFloatButton(bool expanded)
        {
            if (collapseSidebarButton == null) return;
            var width = expanded ? 32.0 : 14.0;
            collapseSidebarButton.Width = width;
            collapseSidebarButton.Margin = settings.SidebarVisible ? new Thickness(4, sidebarToggleTop, 0, 0) : new Thickness(-width, sidebarToggleTop, 0, 0);
            var glyph = HeaderGlyph(settings.SidebarVisible ? "›" : "‹", T("Icon"));
            glyph.Width = expanded ? 17 : 9; glyph.Height = expanded ? 17 : 15;
            collapseSidebarButton.Content = glyph;
        }

        Button Button(string text, RoutedEventHandler click, double width)
        {
            var button = new Button { Content = text, Width = width, Height = 34, Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0),
                Background = T("Button"), Foreground = T("Text"), BorderBrush = T("Grid"), Cursor = Cursors.Hand };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.AppendChild(content);
            var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = border };
            var pressed = new Trigger { Property = System.Windows.Controls.Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, .62));
            template.Triggers.Add(pressed);
            button.Template = template;
            if (click != null) button.Click += click; return button;
        }


        Button IconButton(string glyph, RoutedEventHandler click, double width)
        {
            FrameworkElement icon = HeaderGlyph(glyph, T("Icon"));
            var button = Button("", click, width);
            button.Content = icon;
            button.Foreground = T("Icon");
            button.Padding = new Thickness(0);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(0));
            border.AppendChild(content);
            var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = border };
            var pressed = new Trigger { Property = System.Windows.Controls.Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, .62));
            template.Triggers.Add(pressed);
            button.Template = template;
            return button;
        }

        void StyleWindowControl(Button button, string glyph, Thickness margin)
        {
            OnharuPopupChrome.NameFromToolTip(button);
            var icon = HeaderGlyph(glyph, T("Icon")); icon.RenderTransform = new TranslateTransform(0, 1);
            button.Content = icon;
            button.Foreground = T("Icon"); button.Background = T("Button"); button.BorderBrush = T("Grid");
            button.Padding = new Thickness(0); button.Margin = margin;
        }

        // 아이콘 도형은 OnharuIcons가 단일 기준이다. 메인 헤더와 팝업 제목이 같은 도형을 쓴다.
        // 부가기능 아이콘은 ▦ ✎ ◴ ⚾ 이며 메인 기준 크기 21px, 나머지는 17px이다.

        // 기능 아이콘 줄은 창 조작 버튼 왼쪽에 붙어 오른쪽부터 채운다. 설정에서 하나씩 켤 때마다
        // 왼쪽으로 늘어나고, 전부 켰을 때 맨 왼쪽 아이콘의 테두리가 기간 스위치의 왼쪽 테두리와
        // 같은 선에 선다. 이것이 사용자가 정한 기준이다.
        //
        // 2026-09-03: `보이는 첫 아이콘`을 스위치에 맞추게 했더니 아이콘을 하나만 켜도 줄 전체가
        // 왼쪽으로 끌려갔다. 기준은 현재 보이는 줄이 아니라 **전부 켰을 때의 줄 너비**다.
        // 그 너비는 버튼의 선언 크기와 여백에서 나오므로 숨김 여부와 무관하다.
        // 기능 아이콘 한 칸(34)과 사이 여백(4)을 합친 폭. 보정 허용치의 기준이다.
        const double MaxFeatureRowShift = 38;

        double FullFeatureRowWidth()
        {
            if (featureIconRow == null) return 0;
            double total = 0;
            foreach (UIElement child in featureIconRow.Children)
            {
                var element = child as FrameworkElement;
                if (element == null || double.IsNaN(element.Width)) continue;
                total += element.Width + element.Margin.Left + element.Margin.Right;
            }
            return total;
        }

        void AlignFeatureIconsToRangeSwitch()
        {
            if (featureIconArea == null || featureIconRow == null || calendarRangeSwitch == null || headerActionArea == null) return;
            if (featureIconArea.ActualWidth <= 0 || calendarRangeSwitch.ActualWidth <= 0) return;
            if (!calendarRangeSwitch.IsVisible) return;
            var fullWidth = FullFeatureRowWidth();
            if (fullWidth <= 0) return;
            double areaRight, switchLeft;
            try
            {
                areaRight = featureIconArea.TransformToVisual(headerActionArea).Transform(new Point(featureIconArea.ActualWidth, 0)).X;
                switchLeft = calendarRangeSwitch.TransformToVisual(headerActionArea).Transform(new Point(0, 0)).X;
            }
            catch (InvalidOperationException) { return; }
            var delta = areaRight - fullWidth - switchLeft;
            // 보정은 미세 조정이다. 아래 줄에서 스위치가 빠지면 기준선이 아이콘 몇 개 폭만큼
            // 오른쪽으로 밀리는데, 그때까지 따라가면 아이콘이 창 조작 버튼 위로 올라탄다.
            // 실제로 그렇게 겹쳤다(2026-09-03). 한 칸 폭을 넘는 요구는 무시하고 고정 위치를 지킨다.
            if (Math.Abs(delta) < .5 || Math.Abs(delta) > MaxFeatureRowShift) return;
            var margin = featureIconArea.Margin;
            var target = margin.Right + delta;
            if (target < 0 || target > 400) return;
            featureIconArea.Margin = new Thickness(margin.Left, margin.Top, target, margin.Bottom);
        }

        static FrameworkElement HeaderGlyph(string glyph, Brush foreground)
        {
            return OnharuIcons.Draw(glyph, foreground);
        }

        static FrameworkElement ImportantDayStar(Brush outline, Brush fill, double strokeThickness)
        {
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M9,1.8 L11.1,6.4 L16.1,7 L12.4,10.4 L13.4,15.3 L9,12.8 L4.6,15.3 L5.6,10.4 L1.9,7 L6.9,6.4 Z"),
                Stroke = outline, StrokeThickness = strokeThickness, StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Fill = fill, Width = 18, Height = 18, Stretch = Stretch.None
            };
            return new Viewbox { Width = 19, Height = 19, Stretch = Stretch.Uniform, Child = path,
                Margin = new Thickness(0), RenderTransform = new TranslateTransform(0, 1), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center };
        }

        FrameworkElement BuildInlineDateColorPalette()
        {
            var colors = new[] { "#FFF1F2", "#FEF3C7", "#DCFCE7", "#DBEAFE", "#EDE9FE", "#F1F5F9" };
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (var hex in colors)
            {
                var color = hex;
                var swatch = IconButton("", null, 25); swatch.Height = 25; swatch.Margin = new Thickness(3, 0, 0, 0);
                swatch.Background = Brush(color); swatch.BorderBrush = Brush("#CBD5E1");
                swatch.Click += delegate
                {
                    settings.DateBackgroundColors[DateKey(selectedDate)] = color;
                    Store.SaveSettings(settings); dateColorPalette.Visibility = Visibility.Collapsed; RenderAll();
                };
                row.Children.Add(swatch);
            }
            var clear = IconButton("×", null, 25); clear.Height = 25; clear.FontSize = 13;
            clear.Margin = new Thickness(3, 0, 0, 0); clear.Foreground = Brush("#DC2626"); clear.ToolTip = "날짜 배경색 지우기";
            clear.Click += delegate
            {
                settings.DateBackgroundColors.Remove(DateKey(selectedDate));
                Store.SaveSettings(settings); dateColorPalette.Visibility = Visibility.Collapsed; RenderAll();
            };
            row.Children.Add(clear);
            return new Border { Visibility = Visibility.Collapsed,
                Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(5), Child = row };
        }

        void PositionDateColorPalette()
        {
            if (mainFrame == null || floatingOverlay == null || dateColorButton == null || dateColorPalette == null) return;
            dateColorPalette.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Point point;
            try { point = dateColorButton.TranslatePoint(new Point(dateColorButton.ActualWidth, dateColorButton.ActualHeight), mainFrame); }
            catch { return; }
            var width = dateColorPalette.DesiredSize.Width;
            Canvas.SetLeft(dateColorPalette, Math.Max(8, point.X - width));
            Canvas.SetTop(dateColorPalette, point.Y + 5);
        }

        void UpdateCompactHeaderTypography()
        {
            monthTitle.FontSize = 17;
        }

        static Brush BrandBrush()
        {
            return OnharuStateColors.BrandGradient();
        }

        static bool HasInteractiveParent(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button || source is Slider || source is CheckBox) return true;
                var element = source as FrameworkElement;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
    }
}
