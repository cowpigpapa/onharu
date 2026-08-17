using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace FamilyPlanner
{
    public partial class MainWindow : Window
    {
        readonly List<PlannerItem> items;
        readonly Grid calendar = new Grid();
        readonly StackPanel detail = new StackPanel();
        readonly Button monthTitle = new Button { FontSize = 25, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Cursor = Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = "연·월 바로 이동" };
        readonly TextBlock selectedTitle = new TextBlock { FontSize = 16, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        readonly TextBlock accountStatus = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        readonly Canvas accountStatusViewport = new Canvas { ClipToBounds = true, Height = 18 };
        readonly TranslateTransform accountStatusShift = new TranslateTransform();
        readonly Dictionary<string, CheckBox> filters = new Dictionary<string, CheckBox>();
        readonly Dictionary<DateTime, Border> dayCells = new Dictionary<DateTime, Border>();
        DateTime shownMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime selectedDate = DateTime.Today;
        bool positionLocked;
        bool publishPending;
        object lastDesktopClickTarget;
        Button lockButton;
        TextBlock positionStatus;
        FrameworkElement resizeSurface;
        Border sidebarPanel;
        ColumnDefinition sidebarColumn;
        Button sidebarButton;
        Button collapseSidebarButton;
        Button googleButton;
        TextBlock googleStatus;
        StackPanel googleFilterPanel;
        Button selectedDayButton;
        Button thisWeekButton;
        Button nextWeekButton;
        Button dateColorButton;
        Popup transientPopup;
        Slider opacitySlider;
        ScrollViewer detailScroll;
        string detailMode = "selected";
        bool anniversaryCardsExpanded;
        bool anniversarySectionCollapsed;
        bool ddayCardsExpanded;
        bool ddaySectionCollapsed;
        string itemNoticeId;
        string itemNoticeText;
        int itemNoticeVersion;
        DispatcherTimer autoSyncTimer;
        DispatcherTimer reminderTimer;
        DispatcherTimer syncRetryTimer;
        readonly HashSet<string> shownReminders = new HashSet<string>();
        string syncProblem;
        bool googleSyncing;
        bool googleConnecting;
        int visibleEventLanes = 3;
        Forms.NotifyIcon trayIcon;
        Forms.ToolStripMenuItem trayVisibilityItem;
        Forms.ToolStripMenuItem trayPositionItem;
        bool calendarMinimized;
        readonly PlannerSettings settings;
        readonly ExplorerFramePublisher explorerFrame = new ExplorerFramePublisher();
        readonly DesktopActionWindow desktopActions = new DesktopActionWindow();

        static readonly Dictionary<string, string> Colors = new Dictionary<string, string>
        { { "업무일정", "#5B7CFA" }, { "개인일정", "#F08CA6" }, { "기념일", "#A78BFA" }, { "국경일", "#EF4444" } };

        public MainWindow()
        {
            desktopActions.Received += HandleDesktopAction;
            settings = Store.LoadSettings();
            if (!GoogleCalendar.IsConnected) settings.ActiveGoogleAccountId = null;
            var connectedAccount = GoogleCalendar.ConnectedAccountId;
            if (GoogleCalendar.IsConnected && !string.IsNullOrWhiteSpace(connectedAccount)) settings.ActiveGoogleAccountId = connectedAccount;
            else if (GoogleCalendar.IsConnected && string.IsNullOrWhiteSpace(settings.ActiveGoogleAccountId) && settings.GoogleCalendars != null)
            {
                var savedPrimary = settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
                if (savedPrimary != null) settings.ActiveGoogleAccountId = savedPrimary.Id;
            }
            Store.SetAccount(settings.ActiveGoogleAccountId);
            items = Store.Load();
            if (!settings.AnniversarySeparationComplete)
            {
                foreach (var legacy in items.Where(x => x.ShowDday && string.IsNullOrWhiteSpace(x.AnniversaryType)))
                { legacy.AnniversaryType = InferAnniversaryType(legacy.Title); legacy.Category = "기념일"; }
                settings.AnniversarySeparationComplete = true; Store.SaveSettings(settings); Store.Save(items);
            }
            var correctedAnniversaryCategory = false;
            foreach (var anniversary in items.Where(x => !string.IsNullOrWhiteSpace(x.AnniversaryType) && x.Category != "기념일"))
            { anniversary.Category = "기념일"; correctedAnniversaryCategory = true; }
            foreach (var anniversary in items.Where(x => !string.IsNullOrWhiteSpace(x.AnniversaryType) && x.CreatedInOnharu &&
                (!string.IsNullOrWhiteSpace(x.GoogleCalendarId) || !string.IsNullOrWhiteSpace(x.GoogleEventId))))
            {
                anniversary.GoogleCalendarId = null; anniversary.GoogleCalendarName = null; anniversary.GoogleCalendarColor = null;
                anniversary.GoogleEventId = null; anniversary.GoogleEventType = null; anniversary.GoogleRecurringEventId = null;
                anniversary.GoogleReadOnly = false; anniversary.OnharuManaged = false; anniversary.PendingGoogleSync = false;
                correctedAnniversaryCategory = true;
            }
            if (correctedAnniversaryCategory) Store.Save(items);
            var clearedOrphanPending = false;
            foreach (var orphan in items.Where(x => x.PendingGoogleSync && string.IsNullOrWhiteSpace(x.GoogleCalendarId)))
            { orphan.PendingGoogleSync = false; clearedOrphanPending = true; }
            if (clearedOrphanPending) Store.Save(items);
            var orphanSeries = items.Where(x => string.IsNullOrWhiteSpace(x.GoogleCalendarId) && !string.IsNullOrWhiteSpace(x.RecurrenceFrequency) && string.IsNullOrWhiteSpace(x.SeriesId)).ToList();
            foreach (var master in orphanSeries) ExpandLocalRecurrence(master);
            if (orphanSeries.Count > 0) Store.Save(items);
            if (!string.IsNullOrWhiteSpace(settings.BusinessColor)) Colors["업무일정"] = settings.BusinessColor;
            if (!string.IsNullOrWhiteSpace(settings.PersonalColor)) Colors["개인일정"] = settings.PersonalColor;
            positionLocked = settings.HasPosition ? settings.PositionLocked : true;
            calendarMinimized = false;
            if (settings.StartupPositionMode == "locked") positionLocked = true;
            else if (settings.StartupPositionMode == "editable") positionLocked = false;
            var startDate = settings.StartViewMode == "last" && settings.LastShownDate.Year >= 1900 ? settings.LastShownDate : DateTime.Today;
            shownMonth = settings.CalendarRangeMode == "weeks" ? startDate : new DateTime(startDate.Year, startDate.Month, 1);
            selectedDate = startDate.Date;
            Title = "온하루"; Width = settings.Width >= 820 ? settings.Width : 1120;
            Height = settings.Height >= 560 ? settings.Height : 700; MinWidth = 820; MinHeight = 560;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            FontSize = settings.FontSize > 0 ? settings.FontSize : 12;
            monthTitle.FontSize = Ui(24); monthTitle.Foreground = Brush("#4338CA"); selectedTitle.FontSize = Ui(16);
            Opacity = settings.Opacity > 0 ? settings.Opacity : .95;
            if (settings.HasPosition)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.Left; Top = settings.Top;
            }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = false; ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Content = BuildLayout();
            monthTitle.Click += OpenMonthJump;
            RenderAll();
            Loaded += async delegate
            {
                explorerFrame.SetActionSink(desktopActions.WindowHandle);
                EnsureWindowOnScreen(false);
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
                CreateTrayIcon(); UpdateModeButtons(); UpdateGoogleButton();
                UpdateLayout(); RenderAll(); UpdateLayout();
                if (positionLocked) SchedulePublish();
                else ShowPositionEditor();
                if (GoogleCalendar.IsConnected) await SyncGoogle(false);
                StartAutoSync();
            };
            Closing += delegate
            {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
                Store.Save(items); SaveWindowSettings();
                explorerFrame.Dispose(); desktopActions.Dispose();
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
            };
            new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Normal, delegate { Rollover(); }, Dispatcher);
            reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            reminderTimer.Tick += delegate { SafeCheckReminders(); }; reminderTimer.Start();
            syncRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            syncRetryTimer.Tick += async delegate { if (GoogleCalendar.IsConnected && (syncProblem != null || items.Any(x => x.PendingGoogleSync))) await SyncGoogle(false); };
            syncRetryTimer.Start();
        }

        UIElement BuildLayout()
        {
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
            var logo = new Border { Width = 44, Height = 44, Background = Brushes.White, BorderBrush = Brush("#BAE6FD"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 14, 0), Padding = new Thickness(7) };
            var logoTiles = new UniformGrid { Rows = 3, Columns = 3 };
            foreach (var color in new[] { "#38BDF8", "#60A5FA", "#818CF8", "#34D399", "#22C55E", "#A3E635", "#FBBF24", "#FB923C", "#F472B6" })
                logoTiles.Children.Add(new Border { Background = Brush(color), CornerRadius = new CornerRadius(2), Margin = new Thickness(1) });
            logo.Child = logoTiles;
            titleRow.Children.Add(logo);
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameBrush = new LinearGradientBrush(); nameBrush.StartPoint = new Point(0, .5); nameBrush.EndPoint = new Point(1, .5);
            nameBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0EA5E9"), 0));
            nameBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C3AED"), 1));
            titleStack.Children.Add(new TextBlock { Text = "온하루 · ONHARU", FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = nameBrush });
            titleStack.Children.Add(monthTitle); titleRow.Children.Add(titleStack); header.Children.Add(titleRow);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 5, 0, 0) };
            actions.Children.Add(Button("◀", delegate { MoveCalendar(-1); }, 42));
            actions.Children.Add(Button("오늘", delegate { GoToday(); }, 62));
            actions.Children.Add(Button("▶", delegate { MoveCalendar(1); }, 42));
            lockButton = Button("↔  위치 조정", null, 132);
            lockButton.Click += delegate
            {
                positionLocked = !positionLocked;
                if (positionLocked)
                {
                    settings.HasPosition = true; settings.Left = Left; settings.Top = Top;
                    settings.Width = ActualWidth; settings.Height = ActualHeight;
                }
                settings.PositionLocked = positionLocked; Store.SaveSettings(settings);
                UpdateModeButtons();
                if (positionLocked) { Topmost = false; ShowInTaskbar = false; SchedulePublish(); }
                else ShowPositionEditor();
            };
            positionStatus = new TextBlock { Text = "● 고정됨", FontSize = 9, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -15, 0, 0) };
            var positionControl = new Grid { Width = 132, Height = 34, Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom, ClipToBounds = false };
            lockButton.Margin = new Thickness(0); lockButton.Width = 132; lockButton.Height = 34;
            lockButton.VerticalAlignment = VerticalAlignment.Bottom;
            positionControl.Children.Add(lockButton); positionControl.Children.Add(positionStatus); actions.Children.Add(positionControl);
            actions.Children.Add(new TextBlock { Text = "투명도", VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#64748B"), Margin = new Thickness(12, 0, 5, 0), FontSize = 11 });
            opacitySlider = new Slider { Minimum = .45, Maximum = .98, Value = settings.Opacity,
                Width = 72, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Arrow };
            opacitySlider.ValueChanged += delegate
            {
                Opacity = opacitySlider.Value; settings.Opacity = opacitySlider.Value;
                if (positionLocked) SchedulePublish();
            };
            actions.Children.Add(opacitySlider);
            googleButton = Button("G 연결", GoogleClick, 92); googleButton.Foreground = Brush("#2563EB");
            googleButton.ToolTip = "개인일정을 Google 기본 캘린더와 동기화"; actions.Children.Add(googleButton);
            var searchButton = Button("⌕", OpenSearch, 38); searchButton.FontSize = 20; searchButton.ToolTip = "일정 검색"; actions.Children.Add(searchButton);
            var settingsButton = Button("⚙", OpenSettings, 38); settingsButton.FontSize = 17; settingsButton.ToolTip = "색상 및 설정";
            actions.Children.Add(settingsButton);
            var close = Button("×", delegate { ExecuteCloseButtonAction(); }, 38); close.FontSize = 17; close.ToolTip = "왼쪽 클릭: 기본 동작 · 오른쪽 클릭: 닫기 메뉴";
            close.Tag = "close_button"; close.ContextMenu = CreateCloseContextMenu();
            close.Height = settingsButton.Height; close.Margin = new Thickness(43, 0, 0, 0);
            close.Foreground = Brush("#DC2626"); close.Background = Brush("#FEE2E2"); close.BorderBrush = Brushes.Transparent;
            actions.Children.Add(close);
            var actionArea = new Grid { Height = 39, VerticalAlignment = VerticalAlignment.Bottom, ClipToBounds = false };
            actionArea.Children.Add(actions);
            googleStatus = new TextBlock { Text = "동기화가 완료되었습니다", Foreground = Brush("#DC2626"),
                FontSize = 11, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 39, 44, 0), Visibility = Visibility.Collapsed };
            actionArea.Children.Add(googleStatus);
            Grid.SetColumn(actionArea, 1); header.Children.Add(actionArea);
            root.Children.Add(header);

            var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition());
            sidebarColumn = new ColumnDefinition { Width = settings.SidebarVisible ? new GridLength(310) : new GridLength(34) };
            body.ColumnDefinitions.Add(sidebarColumn);
            var calendarCard = new Border { Background = Brush("#D9FFFFFF"), CornerRadius = new CornerRadius(14),
                BorderBrush = Brush("#80FFFFFF"), BorderThickness = new Thickness(1), Padding = new Thickness(5), Child = calendar };
            body.Children.Add(calendarCard);
            sidebarPanel = new Border { Background = Brush("#E6FFFFFF"), CornerRadius = new CornerRadius(14), Margin = new Thickness(12, 0, 0, 0), Padding = new Thickness(18),
                Visibility = settings.SidebarVisible ? Visibility.Visible : Visibility.Collapsed };
            var sideStack = new StackPanel();
            var categoryHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            collapseSidebarButton = Button("❯", ToggleSidebar, 28); collapseSidebarButton.Height = 28; collapseSidebarButton.FontSize = 15;
            collapseSidebarButton.VerticalAlignment = VerticalAlignment.Center; collapseSidebarButton.Margin = new Thickness(-8, 0, 7, 0);
            collapseSidebarButton.ToolTip = "일정 패널 접기"; DockPanel.SetDock(collapseSidebarButton, Dock.Left); categoryHeader.Children.Add(collapseSidebarButton);
            accountStatus.RenderTransform = accountStatusShift; Canvas.SetTop(accountStatus, 1); accountStatusViewport.Children.Add(accountStatus);
            accountStatusViewport.SizeChanged += delegate { StartAccountMarquee(); };
            var accountCard = new Border { Background = Brush("#EEF2FF"), CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 6, 10, 6), Child = accountStatusViewport, Cursor = Cursors.Hand,
                ToolTip = "Google 계정 및 동기화 대기 일정 보기", Tag = "open_pending_sync" };
            accountCard.MouseLeftButtonDown += OpenPendingSync; categoryHeader.Children.Add(accountCard);
            UpdateAccountStatus();
            sideStack.Children.Add(categoryHeader);
            sideStack.Children.Add(new TextBlock { Text = "온하루 등록", Foreground = Brush("#64748B"), FontSize = Ui(11), Margin = new Thickness(0, 0, 0, 7) });
            var filterRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            foreach (var category in new[] { "업무일정", "개인일정", "D-Day", "기념일" })
            {
                var visible = category == "업무일정" ? settings.BusinessVisible : category == "개인일정" ? settings.PersonalVisible :
                    category == "기념일" ? settings.AnniversaryVisible : settings.DdayPanelVisible;
                var box = new CheckBox { Content = category, IsChecked = visible,
                    Foreground = Brush(category == "D-Day" ? "#0369A1" : Colors[category]), Margin = new Thickness(0, 0, 9, 4),
                    ToolTip = category == "D-Day" ? "오른쪽 D-Day 카드 표시" : null };
                box.Click += delegate { SaveWindowSettings(); if (category == "D-Day") RenderDetail(); else RenderAll(); };
                filters[category] = box; filterRow.Children.Add(box);
            }
            sideStack.Children.Add(filterRow);
            sideStack.Children.Add(new TextBlock { Text = "Google", Foreground = Brush("#64748B"), FontSize = Ui(11), Margin = new Thickness(0, 0, 0, 7) });
            googleFilterPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            sideStack.Children.Add(googleFilterPanel); BuildGoogleFilters();
            var detailTabs = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            for (var i = 0; i < 3; i++) detailTabs.ColumnDefinitions.Add(new ColumnDefinition());
            selectedDayButton = DetailTab("선택일", "selected"); thisWeekButton = DetailTab("이번 주", "this_week"); nextWeekButton = DetailTab("다음 주", "next_week");
            detailTabs.Children.Add(selectedDayButton); Grid.SetColumn(thisWeekButton, 1); detailTabs.Children.Add(thisWeekButton); Grid.SetColumn(nextWeekButton, 2); detailTabs.Children.Add(nextWeekButton);
            sideStack.Children.Add(detailTabs);
            var detailHeader = new DockPanel();
            dateColorButton = Button("★ 중요한 날", null, 82); dateColorButton.Height = 28; dateColorButton.FontSize = 10.5;
            dateColorButton.Background = Brushes.White; dateColorButton.Foreground = Brush("#64748B");
            dateColorButton.BorderBrush = Brush("#CBD5E1");
            dateColorButton.Click += delegate { OpenDateColorPopup(dateColorButton); };
            dateColorButton.ToolTip = "중요한 날 배경색 선택"; DockPanel.SetDock(dateColorButton, Dock.Right); detailHeader.Children.Add(dateColorButton);
            detailHeader.Children.Add(selectedTitle); sideStack.Children.Add(detailHeader);
            sideStack.Children.Add(new Border { Height = 1, Background = Brush("#E2E8F0"), Margin = new Thickness(0, 12, 0, 12) });
            detailScroll = new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Tag = "detail_scroll" };
            detailScroll.Loaded += delegate { UiRound.SoftenScrollBars(detailScroll); };
            var sideLayout = new Grid();
            sideLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sideLayout.RowDefinitions.Add(new RowDefinition());
            sideLayout.Children.Add(sideStack); Grid.SetRow(detailScroll, 1); sideLayout.Children.Add(detailScroll);
            sidebarPanel.Child = sideLayout; Grid.SetColumn(sidebarPanel, 1); body.Children.Add(sidebarPanel);
            sidebarButton = Button("❮", ToggleSidebar, 28);
            sidebarButton.Height = 32; sidebarButton.FontSize = 20; sidebarButton.ToolTip = settings.SidebarVisible ? "일정 패널 접기" : "일정 패널 펼치기";
            sidebarButton.HorizontalAlignment = HorizontalAlignment.Right; sidebarButton.VerticalAlignment = VerticalAlignment.Top;
            sidebarButton.Margin = new Thickness(0); sidebarButton.Visibility = settings.SidebarVisible ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetColumn(sidebarButton, 1); Panel.SetZIndex(sidebarButton, 30); body.Children.Add(sidebarButton);
            Grid.SetRow(body, 1); body.Margin = new Thickness(0, 0, 0, 18); root.Children.Add(body);
            var credit = new TextBlock { Text = "MADE BY JUAN.HJLEE · ONHARU (ver. 2.1.0)", FontSize = 10,
                FontWeight = FontWeights.SemiBold, Foreground = Brush("#475569"),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(12, 0, 0, 1) };
            Grid.SetRow(credit, 1); Panel.SetZIndex(credit, 25); root.Children.Add(credit);
            var shell = new Border { CornerRadius = new CornerRadius(18), Background = Brush("#BFF1F5F9"),
                BorderBrush = Brush("#99FFFFFF"), BorderThickness = new Thickness(1), Child = root };
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
            var frame = new Grid(); frame.Children.Add(shell); return frame;
        }

        void ToggleSidebar(object sender, RoutedEventArgs e)
        {
            settings.SidebarVisible = !settings.SidebarVisible;
            sidebarPanel.Visibility = settings.SidebarVisible ? Visibility.Visible : Visibility.Collapsed;
            sidebarColumn.Width = settings.SidebarVisible ? new GridLength(310) : new GridLength(34);
            sidebarButton.Visibility = settings.SidebarVisible ? Visibility.Collapsed : Visibility.Visible;
            sidebarButton.ToolTip = "일정 패널 펼치기";
            Store.SaveSettings(settings);
        }

        void UpdateModeButtons()
        {
            if (lockButton == null) return;
            lockButton.Content = positionLocked ? "↔  위치 조정" : "✓  이 위치로 고정";
            lockButton.ToolTip = positionLocked
                ? "달력의 위치와 크기를 조정합니다"
                : "현재 위치와 크기를 저장하고 바탕화면에 고정합니다";
            lockButton.Background = positionLocked ? Brush("#EEF2FF") : Brush("#4F46E5");
            lockButton.Foreground = positionLocked ? Brush("#4338CA") : Brushes.White;
            lockButton.BorderBrush = positionLocked ? Brush("#C7D2FE") : Brush("#4338CA");
            if (positionStatus != null)
            {
                positionStatus.Text = positionLocked ? "● 고정됨" : "● 이동 가능";
                positionStatus.Foreground = positionLocked ? Brush("#16A34A") : Brush("#D97706");
            }
            if (resizeSurface != null && positionLocked) resizeSurface.Cursor = Cursors.Arrow;
            if (trayPositionItem != null) trayPositionItem.Text = positionLocked ? "위치·크기 조정" : "이 위치·크기로 고정";
        }

        void PlaceCalendarDialog(Window window)
        {
            window.Owner = null;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            var width = double.IsNaN(window.Width) || window.Width <= 0 ? 420 : window.Width;
            var height = double.IsNaN(window.Height) || window.Height <= 0 ? 0 : window.Height;
            if (height <= 0)
            {
                var content = window.Content as FrameworkElement;
                if (content != null) { content.Measure(new Size(width, double.PositiveInfinity)); height = content.DesiredSize.Height; }
                if (height <= 0) height = 420;
            }
            var left = Left + (ActualWidth - width) / 2;
            var top = Top + (ActualHeight - height) / 2;
            var center = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
            var area = Forms.Screen.FromPoint(new Drawing.Point((int)center.X, (int)center.Y)).WorkingArea;
            var source = PresentationSource.FromVisual(this);
            var fromDevice = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            var areaTopLeft = fromDevice.Transform(new Point(area.Left, area.Top));
            var areaBottomRight = fromDevice.Transform(new Point(area.Right, area.Bottom));
            window.Left = Math.Max(areaTopLeft.X, Math.Min(left, areaBottomRight.X - width));
            window.Top = Math.Max(areaTopLeft.Y, Math.Min(top, areaBottomRight.Y - height));
        }

        void ShowNotice(string message, bool warning)
        {
            var window = new NoticeWindow(message, warning); PlaceCalendarDialog(window); window.ShowDialog();
        }

        static Button Button(string text, RoutedEventHandler click, double width)
        {
            var button = new Button { Content = text, Width = width, Height = 34, Margin = new Thickness(5, 0, 0, 0),
                Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), Cursor = Cursors.Hand };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(8, 0, 8, 0));
            border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = border };
            if (click != null) button.Click += click; return button;
        }
        static bool HasInteractiveParent(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button || source is Slider || source is CheckBox) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        static Brush PastelBrush(string hex, double whiteRatio)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var r = (byte)(color.R + (255 - color.R) * whiteRatio);
            var g = (byte)(color.G + (255 - color.G) * whiteRatio);
            var b = (byte)(color.B + (255 - color.B) * whiteRatio);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
        double Ui(double baseSize) { return baseSize * (settings.FontSize > 0 ? settings.FontSize / 12.0 : 1); }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }
}
