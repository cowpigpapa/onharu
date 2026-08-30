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
            var root = new Grid { Margin = new Thickness(12, 12, 12, 5), Background = Brushes.Transparent };
            root.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!positionLocked && e.GetPosition(root).Y <= 72 && !HasInteractiveParent(e.OriginalSource as DependencyObject))
                { DragMove(); }
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
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
                Width = 83, Height = 18, Margin = new Thickness(-6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Arrow, ToolTip = "달력 투명도", Foreground = Brush(OnharuStateColors.OpacityControl(settings.ThemeId)), RenderTransformOrigin = new Point(.5, .5),
                RenderTransform = new TransformGroup { Children = new TransformCollection { new ScaleTransform(1, .74), new TranslateTransform(0, 5) } } };
            opacitySlider.Template = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type Slider}'><Grid Height='18' Background='Transparent'><Border Width='2' Height='14' Background='{TemplateBinding Foreground}' HorizontalAlignment='Left' VerticalAlignment='Center' CornerRadius='1'/><Track x:Name='PART_Track' Orientation='Horizontal'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static Slider.DecreaseLarge}' Focusable='False' Foreground='{TemplateBinding Foreground}'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Height='4' Background='{TemplateBinding Foreground}' CornerRadius='2' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='11' Height='15' Foreground='{TemplateBinding Foreground}'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Ellipse Fill='White' Stroke='{TemplateBinding Foreground}' StrokeThickness='2'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static Slider.IncreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Height='4' Background='#CBD5E1' CornerRadius='2' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>");
            opacitySlider.ValueChanged += delegate
            {
                settings.Opacity = opacitySlider.Value;
                if (positionLocked) QueueFixedOpacityPreview(settings.Opacity);
                else Opacity = settings.Opacity;
            };
            var previousButton = IconButton("‹", null, 20); previousButton.Height = 26; previousButton.FontSize = 12.5; previousButton.Padding = new Thickness(0); previousButton.Tag = "calendar_previous";
            var nextButton = IconButton("›", null, 20); nextButton.Height = 26; nextButton.FontSize = 12.5; nextButton.Padding = new Thickness(0); nextButton.Tag = "calendar_next";
            previousButton.Margin = new Thickness(0); nextButton.Margin = new Thickness(0);
            BindCalendarNavigation(previousButton, -1); BindCalendarNavigation(nextButton, 1);
            todayButton = Button("오늘", delegate { GoToday(); }, 42); todayButton.Height = 26; todayButton.FontSize = 12.5;
            todayButton.Padding = new Thickness(0);
            todayButton.FontWeight = FontWeights.SemiBold; todayButton.Margin = new Thickness(3, 0, 3, 0);
            UpdateTodayButtonStyle();
            var brandLine = new Grid { Width = 306, VerticalAlignment = VerticalAlignment.Center };
            brandLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            brandLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            brandLine.Children.Add(new TextBlock { Text = "온하루 · ONHARU", FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = BrandBrush(), VerticalAlignment = VerticalAlignment.Bottom });
            var brandNavigation = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(brandNavigation, 1); brandLine.Children.Add(brandNavigation);
            brandLine.Margin = new Thickness(0); titleStack.Children.Add(brandLine);
            monthTitle.Width = 306; monthTitle.HorizontalContentAlignment = HorizontalAlignment.Left;
            titleStack.Children.Add(monthTitle); titleRow.Children.Add(titleStack); header.Children.Add(titleRow);
            var upperActions = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var featureArea = new Grid { Width = 202, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 114, 0) };
            var featureActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center };
            featureArea.Children.Add(featureActions);
            var windowActions = new Grid { Width = 92, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0) };
            windowActions.ColumnDefinitions.Add(new ColumnDefinition()); windowActions.ColumnDefinitions.Add(new ColumnDefinition()); windowActions.ColumnDefinitions.Add(new ColumnDefinition());
            var lowerActions = new Grid { Width = 384, HorizontalAlignment = HorizontalAlignment.Right, ClipToBounds = false,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), RenderTransform = new TranslateTransform(0, 5) };
            var lowerSwitches = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center };
            lowerActions.Children.Add(lowerSwitches);
            var periodNavigation = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-8, 0, 0, 0) };
            periodNavigation.Children.Add(previousButton); periodNavigation.Children.Add(todayButton); periodNavigation.Children.Add(nextButton);
            lowerActions.Children.Add(periodNavigation);
            searchButton = IconButton("⌕", OpenSearch, 34); searchButton.Height = 28; searchButton.FontSize = 16; searchButton.ToolTip = "일정 검색";
            searchButton.Margin = new Thickness(0);
            searchButton.Visibility = settings.ShowSearchIcon ? Visibility.Visible : Visibility.Collapsed; featureActions.Children.Add(searchButton);
            timetableButton = IconButton("▦", OpenTimetable, 32); timetableButton.Height = 28; timetableButton.FontSize = 15;
            timetableButton.FontFamily = new FontFamily("Segoe UI Symbol");
            timetableButton.ToolTip = "시간표 보기 · 편집"; timetableButton.Visibility = settings.UseTimetable ? Visibility.Visible : Visibility.Collapsed; timetableButton.Margin = new Thickness(4, 0, 0, 0);
            featureActions.Children.Add(timetableButton);
            diaryButton = IconButton("✎", OpenDiaryReader, 32); diaryButton.Height = 28; diaryButton.FontSize = 15;
            diaryButton.FontFamily = new FontFamily("Segoe UI Symbol"); diaryButton.ToolTip = "일기장 보기 · 날짜 더블클릭으로 작성";
            diaryButton.Visibility = settings.UseDiary ? Visibility.Visible : Visibility.Collapsed; diaryButton.Margin = new Thickness(4, 0, 0, 0); featureActions.Children.Add(diaryButton);
            sportsButton = IconButton("⚾", OpenProBaseball, 32);
            sportsButton.Height = 28; sportsButton.FontSize = 14; sportsButton.ToolTip = "프로야구 일정";
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
            ApplyNeutralSwitchPalette(calendarRangeSwitch);
            calendarRangeSwitch.Clicked += delegate(int index, bool wasSelected)
            {
                if (index == 0 && wasSelected) OpenWeekCountPopup();
                else CloseWeekCountOverlay();
            };
            calendarRangeSwitch.Margin = new Thickness(0); calendarRangeSwitch.VerticalAlignment = VerticalAlignment.Center;
            calendarRangeSwitch.Visibility = settings.ShowRangeSwitch ? Visibility.Visible : Visibility.Collapsed;
            lowerSwitches.Children.Add(calendarRangeSwitch);
            TemporarySegmentPaletteTool.Attach(calendarRangeSwitch);
            themeQuickSwitch = new OnharuSegmentedSwitch(new[] { "파스텔", "블랙" }, new[] { 45.0, 38.0 }, settings.ThemeId == "dark" ? 1 : 0,
                delegate(int index)
                {
                    CloseWeekCountOverlay();
                    ApplyTheme(index == 1 ? "dark" : "classic");
                    Store.SaveSettings(settings);
                });
            themeQuickSwitch.Margin = new Thickness(4.5, 0, 0, 0); themeQuickSwitch.VerticalAlignment = VerticalAlignment.Center;
            themeQuickSwitch.Visibility = settings.ShowThemeSwitch ? Visibility.Visible : Visibility.Collapsed;
            lowerSwitches.Children.Add(themeQuickSwitch);
            TemporarySegmentPaletteTool.Attach(themeQuickSwitch);
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
            positionModeSwitch.Margin = new Thickness(4.5, 0, 0, 0); positionModeSwitch.Width = 92; positionModeSwitch.Height = 26;
            positionModeSwitch.VerticalAlignment = VerticalAlignment.Center;
            positionModeSwitch.Visibility = settings.ShowPositionSwitch ? Visibility.Visible : Visibility.Collapsed;
            lowerSwitches.Children.Add(positionModeSwitch);
            TemporarySegmentPaletteTool.Attach(positionModeSwitch);
            googleButton = Button("G 연결", GoogleClick, 74); googleButton.Height = 28; googleButton.FontSize = 11;
            googleButton.Foreground = Brush("#2563EB"); googleButton.Visibility = Visibility.Collapsed;
            settingsButton = IconButton("", OpenSettings, 34); settingsButton.Content = SettingsGlyph(T("Icon")); settingsButton.Height = 28; settingsButton.ToolTip = "색상 및 설정";
            settingsButton.Margin = new Thickness(4, 0, 0, 0); featureActions.Children.Add(settingsButton);
            StyleLightHeaderActionButton(searchButton, "⌕"); StyleLightHeaderActionButton(timetableButton, "▦");
            StyleLightHeaderActionButton(diaryButton, "✎"); StyleLightHeaderActionButton(sportsButton, "⚾"); StyleLightHeaderActionButton(settingsButton, "settings");
            var minimizeButton = IconButton("window_minimize", delegate { MinimizeToTray(); }, 26); minimizeButton.Height = 26; minimizeButton.ToolTip = "최소화";
            StyleWindowControl(minimizeButton, "window_minimize", new Thickness(0)); minimizeButton.HorizontalAlignment = HorizontalAlignment.Left; windowActions.Children.Add(minimizeButton);
            windowMaximizeButton = IconButton("window_maximize", delegate { if (!positionLocked) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }, 26);
            windowMaximizeButton.Height = 26; windowMaximizeButton.ToolTip = "최대화 · 고정 상태에서는 사용할 수 없음"; windowMaximizeButton.IsEnabled = !positionLocked; windowMaximizeButton.Opacity = positionLocked ? .4 : 1;
            StyleWindowControl(windowMaximizeButton, "window_maximize", new Thickness(0)); windowMaximizeButton.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(windowMaximizeButton, 1); windowActions.Children.Add(windowMaximizeButton);
            var closeWindowButton = IconButton("window_close", delegate { RequestExit(); }, 26); closeWindowButton.Height = 26; closeWindowButton.ToolTip = "끝내기";
            StyleWindowControl(closeWindowButton, "window_close", new Thickness(0)); closeWindowButton.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(closeWindowButton, 2); windowActions.Children.Add(closeWindowButton);
            upperActions.Children.Add(featureArea); upperActions.Children.Add(windowActions);
            var actionArea = new Grid { Width = 390, Height = 59, VerticalAlignment = VerticalAlignment.Bottom, ClipToBounds = false };
            actionArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            actionArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
            actionArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            actionArea.Children.Add(upperActions);
            opacitySlider.HorizontalAlignment = HorizontalAlignment.Left;
            TemporarySegmentPaletteTool.Attach(opacitySlider);
            Grid.SetRow(opacitySlider, 0); actionArea.Children.Add(opacitySlider);
            Grid.SetRow(lowerActions, 2); actionArea.Children.Add(lowerActions);
            googleStatus = new TextBlock { Text = "동기화 완료", Foreground = Brush("#16A34A"),
                FontSize = 10.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 1, 1), Visibility = Visibility.Collapsed };
            Grid.SetColumn(actionArea, 1); header.Children.Add(actionArea);
            root.Children.Add(header);

            var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition());
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
            TemporarySegmentPaletteTool.Attach(googleLoginButton);
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
            var localTitle = new TextBlock { Text = "온하루 등록", Foreground = T("Muted"), FontSize = Ui(11),
                VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            localHeader.Children.Add(localTitle);
            var localIndicator = new TextBlock { Text = "▲", Foreground = T("Muted"), FontSize = Ui(9),
                Margin = new Thickness(3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            localHeader.Children.Add(localIndicator);
            localAllFilter = HeaderAllFilter("온하루 등록 전체 선택/해제", SetLocalFilters);
            localHeader.Children.Add(localAllFilter);
            var specialHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
            var specialTitle = new TextBlock { Text = "Special Day Card", Foreground = T("Muted"), FontSize = Ui(11),
                VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
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
                localIndicator.Text = visible ? "▲" : "▼";
                localHeader.Margin = new Thickness(0, 0, 0, visible ? 7 : 0);
                specialHeader.Margin = new Thickness(0, 0, 0, visible ? 7 : 0);
                filterGroups.Margin = new Thickness(0, 0, 0, visible ? 14 : 6);
            };
            localIndicator.MouseLeftButtonUp += delegate { localTitle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent }); };
            specialTitle.MouseLeftButtonUp += delegate { localTitle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent }); };
            var googleHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
            var googleTitle = new TextBlock { Text = "Google", Foreground = T("Muted"), FontSize = Ui(11),
                VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            googleHeader.Children.Add(googleTitle);
            var googleIndicator = new TextBlock { Text = "▲", Foreground = T("Muted"), FontSize = Ui(9),
                Margin = new Thickness(3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
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
                googleIndicator.Text = visible ? "▲" : "▼";
                googleHeader.Margin = new Thickness(0, 0, 0, visible ? 7 : 10);
            };
            googleIndicator.MouseLeftButtonUp += delegate { googleTitle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent }); };
            UpdateGroupFilterChecks();
            detailPeriodSwitch = new OnharuSegmentedSwitch(new[] { "선택 날짜", "이번 주", "다음 주" }, new[] { 88.0, 88.0, 88.0 }, 0,
                delegate(int index) { detailMode = index == 1 ? "this_week" : index == 2 ? "next_week" : "selected"; RenderDetail(); });
            ((Control)detailPeriodSwitch.SegmentTarget(0)).MouseDoubleClick += delegate(object sender, MouseButtonEventArgs e) { GoToday(); e.Handled = true; };
            detailPeriodSwitch.Margin = new Thickness(0, 0, 0, 6); ApplyDetailSwitchPalette(detailPeriodSwitch);
            TemporarySegmentPaletteTool.Attach(detailPeriodSwitch); sideStack.Children.Add(detailPeriodSwitch);
            detailOrderSwitchHost = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            sideStack.Children.Add(detailOrderSwitchHost); BuildDetailOrderSwitch();
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
            detailAddButton.ToolTip = "이 날짜에 일정 추가";
            System.Windows.Automation.AutomationProperties.SetName(detailAddButton, "이 날짜에 일정 추가");
            System.Windows.Automation.AutomationProperties.SetAutomationId(detailAddButton, "OnharuDetailAdd");
            detailAddButton.Margin = new Thickness(8, 0, 13, 0); StyleDetailHeaderActionButtons();
            DockPanel.SetDock(detailAddButton, Dock.Right); detailHeader.Children.Add(detailAddButton);
            var selectedTitleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            dateColorButton.Margin = new Thickness(0, 0, 5, 0);
            selectedTitleRow.Children.Add(dateColorButton); selectedTitleRow.Children.Add(selectedTitle);
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
            var credit = new TextBlock { Text = "MADE BY JUAN.HJLEE · ONHARU (ver. 2.2.6 test)", FontSize = 10,
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
                var edge = ResizeEdgeAt(e.GetPosition(shell), shell);
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
            if (detailOrderSwitchHost == null) return;
            detailOrderSwitchHost.Children.Clear();
            var labels = settings.ShowIncompleteTodoButton ? new[] { "카테고리", "시간순", "미완료 일정" } : new[] { "카테고리", "시간순" };
            var widths = settings.ShowIncompleteTodoButton ? new[] { 88.0, 88.0, 88.0 } : new[] { 132.0, 132.0 };
            var selected = detailIncompleteMode && settings.ShowIncompleteTodoButton ? 2 : settings.DetailOrderMode == "time" ? 1 : 0;
            detailOrderSwitch = new OnharuSegmentedSwitch(labels, widths, selected, delegate(int index)
            {
                CloseWeekCountOverlay();
                if (index == 2)
                {
                    detailIncompleteMode = true;
                }
                else
                {
                    detailIncompleteMode = false;
                    settings.DetailOrderMode = index == 1 ? "time" : "category";
                    Store.SaveSettings(settings);
                }
                RenderDetail();
            });
            ApplyDetailSwitchPalette(detailOrderSwitch); TemporarySegmentPaletteTool.Attach(detailOrderSwitch);
            detailOrderSwitchHost.Children.Add(detailOrderSwitch);
        }

        void ToggleSidebar(object sender, RoutedEventArgs e)
        {
            settings.SidebarVisible = !settings.SidebarVisible;
            sidebarPanel.Visibility = settings.SidebarVisible ? Visibility.Visible : Visibility.Collapsed;
            sidebarColumn.Width = settings.SidebarVisible ? new GridLength(310) : new GridLength(0);
            collapseSidebarButton.ToolTip = settings.SidebarVisible ? "일정 패널 접기" : "일정 패널 펼치기";
            UpdateSidebarFloatButton(collapseSidebarButton.IsMouseOver);
            Store.SaveSettings(settings);
        }

        void UpdateSidebarFloatButton(bool expanded)
        {
            if (collapseSidebarButton == null) return;
            var width = expanded ? 32.0 : 14.0;
            collapseSidebarButton.Width = width;
            collapseSidebarButton.Margin = new Thickness(-width, 8, 0, 0);
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
            button.Content = HeaderGlyph(glyph, T("Icon"));
            button.Foreground = T("Icon"); button.Background = T("Button"); button.BorderBrush = T("Grid");
            button.Padding = new Thickness(0); button.Margin = margin;
        }

        static FrameworkElement HeaderGlyph(string glyph, Brush foreground)
        {
            string geometry = null;
            if (glyph == "‹" || glyph == "❮") geometry = "M11.5,3.5 L6.5,9 L11.5,14.5";
            else if (glyph == "›" || glyph == "❯") geometry = "M6.5,3.5 L11.5,9 L6.5,14.5";
            else if (glyph == "«") geometry = "M9,3.5 L4,9 L9,14.5 M14,3.5 L9,9 L14,14.5";
            else if (glyph == "»") geometry = "M4,3.5 L9,9 L4,14.5 M9,3.5 L14,9 L9,14.5";
            else if (glyph == "⌕") geometry = "M8,3 A5,5 0 1 0 8,13 A5,5 0 1 0 8,3 M11.7,11.7 L16,16";
            else if (glyph == "window_minimize") geometry = "M4,11.5 L14,11.5";
            else if (glyph == "window_maximize") geometry = "M4.5,4.5 L13.5,4.5 L13.5,13.5 L4.5,13.5 Z";
            else if (glyph == "window_close") geometry = "M5,5 L13,13 M13,5 L5,13";
            else if (glyph == "add") geometry = "M9,5 L9,13 M5,9 L13,9";
            else if (glyph == "range") geometry = "M3,2 L15,9 L3,16 Z";
            else if (glyph == "important_day") geometry = "M9,1.8 L11.1,6.4 L16.1,7 L12.4,10.4 L13.4,15.3 L9,12.8 L4.6,15.3 L5.6,10.4 L1.9,7 L6.9,6.4 Z";
            if (geometry != null)
            {
                var path = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse(geometry), Stroke = foreground, StrokeThickness = glyph == "range" ? 1.2 : 1.8,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round, Fill = glyph == "important_day" ? foreground : Brushes.Transparent,
                    Width = 18, Height = 18, Stretch = Stretch.None
                };
                var glyphSize = glyph == "range" ? 19 : 17;
                return new Viewbox { Width = glyphSize, Height = glyphSize, Stretch = Stretch.Uniform, Child = path,
                    RenderTransform = glyph == "range" ? new TranslateTransform(0, 1) : null,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }

            var featureIcon = glyph == "▦" || glyph == "✎" || glyph == "⚾";
            return new Viewbox
            {
                Width = featureIcon ? 21 : 17, Height = featureIcon ? 21 : 17, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = glyph, Foreground = foreground, FontFamily = new FontFamily("Segoe UI Symbol"),
                    FontWeight = FontWeights.SemiBold, Padding = new Thickness(0), Margin = new Thickness(0),
                    TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center }
            };
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

        static FrameworkElement SettingsGlyph(Brush foreground)
        {
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M19.43,12.98 C19.47,12.66 19.5,12.34 19.5,12 C19.5,11.66 19.47,11.34 19.42,11.02 L21.54,9.37 L19.54,5.91 L17.05,6.91 C16.54,6.5 15.98,6.17 15.35,5.92 L14.96,3.27 L10.96,3.27 L10.57,5.92 C9.96,6.17 9.39,6.5 8.88,6.91 L6.39,5.91 L4.39,9.37 L6.51,11.02 C6.46,11.34 6.42,11.67 6.42,12 C6.42,12.33 6.46,12.66 6.51,12.98 L4.39,14.63 L6.39,18.09 L8.88,17.09 C9.39,17.5 9.96,17.83 10.57,18.08 L10.96,20.73 L14.96,20.73 L15.35,18.08 C15.98,17.83 16.54,17.5 17.05,17.09 L19.54,18.09 L21.54,14.63 Z M12.96,15.5 C11.03,15.5 9.46,13.93 9.46,12 C9.46,10.07 11.03,8.5 12.96,8.5 C14.89,8.5 16.46,10.07 16.46,12 C16.46,13.93 14.89,15.5 12.96,15.5 Z"),
                Fill = foreground, Stretch = Stretch.Uniform
            };
            return new Viewbox { Width = 17, Height = 17, Stretch = Stretch.Uniform, Child = path,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
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
