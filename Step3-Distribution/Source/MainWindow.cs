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
    public class MainWindow : Window
    {
        readonly List<PlannerItem> items;
        readonly Grid calendar = new Grid();
        readonly StackPanel detail = new StackPanel();
        readonly TextBlock monthTitle = new TextBlock { FontSize = 25, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock selectedTitle = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold };
        readonly TextBlock accountStatus = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        readonly Canvas accountStatusViewport = new Canvas { ClipToBounds = true, Height = 18 };
        readonly TranslateTransform accountStatusShift = new TranslateTransform();
        readonly Dictionary<string, CheckBox> filters = new Dictionary<string, CheckBox>();
        DateTime shownMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime selectedDate = DateTime.Today;
        bool positionLocked;
        Button lockButton;
        Border resizeHandle;
        Border oppositeResizeHandle;
        Border sidebarPanel;
        ColumnDefinition sidebarColumn;
        Button sidebarButton;
        Button collapseSidebarButton;
        Button googleButton;
        TextBlock googleStatus;
        StackPanel googleFilterPanel;
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
        bool resizing;
        Point resizeStart;
        double resizeWidth;
        double resizeHeight;
        double resizeLeft;
        double resizeTop;
        Forms.NotifyIcon trayIcon;
        readonly PlannerSettings settings;

        static readonly Dictionary<string, string> Colors = new Dictionary<string, string>
        { { "업무일정", "#5B7CFA" }, { "개인일정", "#F08CA6" }, { "국경일", "#EF4444" } };

        public MainWindow()
        {
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
            Title = "온하루"; Width = settings.Width >= 820 ? settings.Width : 1120;
            Height = settings.Height >= 560 ? settings.Height : 700; MinWidth = 820; MinHeight = 560;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            FontSize = settings.FontSize > 0 ? settings.FontSize : 12;
            monthTitle.FontSize = Ui(24); monthTitle.Foreground = Brush("#4338CA"); selectedTitle.FontSize = Ui(18);
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
            RenderAll();
            SourceInitialized += delegate
            {
                DesktopLayer.Attach(this);
            };
            Activated += delegate { Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { DesktopLayer.Lower(this); })); };
            Loaded += async delegate
            {
                CreateTrayIcon(); UpdateModeButtons(); UpdateGoogleButton();
                if (GoogleCalendar.IsConnected) await SyncGoogle(false);
                StartAutoSync();
            };
            Closing += delegate
            {
                Store.Save(items); SaveWindowSettings();
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
            };
            new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Normal, delegate { Rollover(); }, Dispatcher);
            reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            reminderTimer.Tick += delegate { CheckReminders(); }; reminderTimer.Start();
            syncRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            syncRetryTimer.Tick += async delegate { if (GoogleCalendar.IsConnected && (syncProblem != null || items.Any(x => x.PendingGoogleSync))) await SyncGoogle(false); };
            syncRetryTimer.Start();
        }

        UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(12), Background = Brushes.Transparent };
            root.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!positionLocked && e.GetPosition(root).Y <= 72 && !HasInteractiveParent(e.OriginalSource as DependencyObject))
                { DragMove(); DesktopLayer.Lower(this); }
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            var header = new Grid { Margin = new Thickness(16, 10, 48, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
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
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -3, 0, 0) };
            actions.Children.Add(Button("◀", delegate { shownMonth = shownMonth.AddMonths(-1); RenderAll(); }, 42));
            actions.Children.Add(Button("오늘", delegate { shownMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); selectedDate = DateTime.Today; RenderAll(); }, 62));
            actions.Children.Add(Button("▶", delegate { shownMonth = shownMonth.AddMonths(1); RenderAll(); }, 42));
            lockButton = Button("📌 고정됨", null, 112);
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
            };
            actions.Children.Add(lockButton);
            actions.Children.Add(new TextBlock { Text = "투명도", VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#64748B"), Margin = new Thickness(12, 0, 5, 0), FontSize = 11 });
            var opacitySlider = new Slider { Minimum = .45, Maximum = .98, Value = settings.Opacity,
                Width = 72, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Arrow };
            opacitySlider.ValueChanged += delegate { Opacity = opacitySlider.Value; settings.Opacity = opacitySlider.Value; };
            actions.Children.Add(opacitySlider);
            googleButton = Button("G 연결", GoogleClick, 92); googleButton.Foreground = Brush("#2563EB");
            googleButton.ToolTip = "개인일정을 Google 기본 캘린더와 동기화"; actions.Children.Add(googleButton);
            var searchButton = Button("⌕", OpenSearch, 38); searchButton.FontSize = 20; searchButton.ToolTip = "일정 검색"; actions.Children.Add(searchButton);
            var settingsButton = Button("⚙", OpenSettings, 38); settingsButton.FontSize = 17; settingsButton.ToolTip = "색상 및 설정";
            actions.Children.Add(settingsButton);
            var actionArea = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            actionArea.Children.Add(actions);
            googleStatus = new TextBlock { Text = "동기화가 완료되었습니다", Foreground = Brush("#DC2626"),
                FontSize = 11, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 1, 44, 0), Visibility = Visibility.Hidden };
            actionArea.Children.Add(googleStatus);
            Grid.SetColumn(actionArea, 1); header.Children.Add(actionArea);
            root.Children.Add(header);

            var close = Button("×", delegate { Close(); }, 32); close.Height = 32;
            close.Foreground = Brush("#DC2626"); close.FontSize = 17; close.ToolTip = "종료";
            close.Background = Brush("#FEE2E2"); close.BorderBrush = Brushes.Transparent;
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top;
            close.Margin = new Thickness(0, 8, 8, 0); Panel.SetZIndex(close, 30);

            resizeHandle = new Border { Width = 28, Height = 28, Background = Brush("#D9E0F2FE"),
                CornerRadius = new CornerRadius(9), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE, ToolTip = "창 크기 조절" };
            resizeHandle.Child = new TextBlock { Text = "◢", Foreground = Brush("#64748B"), FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            resizeHandle.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (positionLocked) return;
                resizing = true; resizeStart = PointToScreen(e.GetPosition(this)); resizeWidth = Width; resizeHeight = Height;
                resizeHandle.CaptureMouse(); e.Handled = true;
            };
            resizeHandle.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!resizing) return;
                var point = PointToScreen(e.GetPosition(this)); Width = Math.Max(MinWidth, resizeWidth + point.X - resizeStart.X);
                Height = Math.Max(MinHeight, resizeHeight + point.Y - resizeStart.Y);
            };
            resizeHandle.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            { resizing = false; resizeHandle.ReleaseMouseCapture(); e.Handled = true; };
            resizeHandle.Visibility = positionLocked ? Visibility.Collapsed : Visibility.Visible;
            resizeHandle.Margin = new Thickness(0, 0, 3, 16); Grid.SetRow(resizeHandle, 1);
            Panel.SetZIndex(resizeHandle, 20); root.Children.Add(resizeHandle);

            oppositeResizeHandle = new Border { Width = 24, Height = 24, Background = Brush("#D9E0F2FE"),
                CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.SizeNWSE, ToolTip = "창 크기 조절" };
            oppositeResizeHandle.Child = new TextBlock { Text = "◤", Foreground = Brush("#64748B"), FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            oppositeResizeHandle.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (positionLocked) return;
                resizing = true; resizeStart = PointToScreen(e.GetPosition(this)); resizeWidth = Width; resizeHeight = Height;
                resizeLeft = Left; resizeTop = Top; oppositeResizeHandle.CaptureMouse(); e.Handled = true;
            };
            oppositeResizeHandle.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!resizing || !oppositeResizeHandle.IsMouseCaptured) return;
                var point = PointToScreen(e.GetPosition(this));
                Width = Math.Max(MinWidth, resizeWidth - point.X + resizeStart.X); Left = resizeLeft + resizeWidth - Width;
                Height = Math.Max(MinHeight, resizeHeight - point.Y + resizeStart.Y); Top = resizeTop + resizeHeight - Height;
            };
            oppositeResizeHandle.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            { resizing = false; oppositeResizeHandle.ReleaseMouseCapture(); e.Handled = true; };
            oppositeResizeHandle.Visibility = positionLocked ? Visibility.Collapsed : Visibility.Visible;
            oppositeResizeHandle.Margin = new Thickness(3, -12, 0, 0); Grid.SetRow(oppositeResizeHandle, 1);
            Panel.SetZIndex(oppositeResizeHandle, 20); root.Children.Add(oppositeResizeHandle);

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
                ToolTip = "Google 계정 및 동기화 대기 일정 보기" };
            accountCard.MouseLeftButtonDown += OpenPendingSync; categoryHeader.Children.Add(accountCard);
            UpdateAccountStatus();
            sideStack.Children.Add(categoryHeader);
            sideStack.Children.Add(new TextBlock { Text = "온하루 등록", Foreground = Brush("#64748B"), FontSize = Ui(11), Margin = new Thickness(0, 0, 0, 7) });
            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            foreach (var category in new[] { "업무일정", "개인일정" })
            {
                var visible = category == "업무일정" ? settings.BusinessVisible : settings.PersonalVisible;
                var box = new CheckBox { Content = category, IsChecked = visible, Foreground = Brush(Colors[category]), Margin = new Thickness(0, 0, 14, 0) };
                box.Click += delegate { SaveWindowSettings(); RenderAll(); }; filters[category] = box; filterRow.Children.Add(box);
            }
            sideStack.Children.Add(filterRow);
            sideStack.Children.Add(new TextBlock { Text = "Google", Foreground = Brush("#64748B"), FontSize = Ui(11), Margin = new Thickness(0, 0, 0, 7) });
            googleFilterPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            sideStack.Children.Add(googleFilterPanel); BuildGoogleFilters(); sideStack.Children.Add(selectedTitle);
            sideStack.Children.Add(new Border { Height = 1, Background = Brush("#E2E8F0"), Margin = new Thickness(0, 12, 0, 12) });
            sideStack.Children.Add(detail); sidebarPanel.Child = sideStack; Grid.SetColumn(sidebarPanel, 1); body.Children.Add(sidebarPanel);
            sidebarButton = Button("❮", ToggleSidebar, 28);
            sidebarButton.Height = 32; sidebarButton.FontSize = 20; sidebarButton.ToolTip = settings.SidebarVisible ? "일정 패널 접기" : "일정 패널 펼치기";
            sidebarButton.HorizontalAlignment = HorizontalAlignment.Right; sidebarButton.VerticalAlignment = VerticalAlignment.Top;
            sidebarButton.Margin = new Thickness(0, 8, 3, 0); sidebarButton.Visibility = settings.SidebarVisible ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetColumn(sidebarButton, 1); Panel.SetZIndex(sidebarButton, 30); body.Children.Add(sidebarButton);
            Grid.SetRow(body, 1); body.Margin = new Thickness(12, 0, 12, 30); root.Children.Add(body);
            var credit = new TextBlock { Text = "MADE BY JUAN.HJLEE · ONHARU (ver. 1.0.0)", FontSize = 10,
                FontWeight = FontWeights.SemiBold, Foreground = Brush("#475569"),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 50, 5) };
            Grid.SetRow(credit, 1); Panel.SetZIndex(credit, 25); root.Children.Add(credit);
            var shell = new Border { CornerRadius = new CornerRadius(18), Background = Brush("#BFF1F5F9"),
                BorderBrush = Brush("#99FFFFFF"), BorderThickness = new Thickness(1), Child = root };
            var frame = new Grid(); frame.Children.Add(shell); frame.Children.Add(close); return frame;
        }

        void RenderAll()
        {
            monthTitle.Text = shownMonth.ToString("yyyy년 M월");
            calendar.Children.Clear(); calendar.RowDefinitions.Clear(); calendar.ColumnDefinitions.Clear();
            var weekOffset = settings.ShowWeekNumbers ? 1 : 0;
            if (settings.ShowWeekNumbers) calendar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            for (var c = 0; c < 7; c++) calendar.ColumnDefinitions.Add(new ColumnDefinition());
            calendar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            for (var r = 0; r < 6; r++) calendar.RowDefinitions.Add(new RowDefinition());
            var mondayFirst = settings.WeekNumberRule == "iso";
            var weekdays = mondayFirst ? new[] { "월", "화", "수", "목", "금", "토", "일" } : new[] { "일", "월", "화", "수", "목", "금", "토" };
            if (settings.ShowWeekNumbers)
            {
                var weekHeader = new TextBlock { Text = "주", HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#94A3B8"), FontSize = Ui(10), FontWeight = FontWeights.Bold };
                calendar.Children.Add(weekHeader);
            }
            for (var c = 0; c < 7; c++)
            {
                var day = new TextBlock { Text = weekdays[c], HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold, FontSize = Ui(13),
                    Foreground = weekdays[c] == "일" ? Brush("#DC2626") : weekdays[c] == "토" ? Brush("#2563EB") : Brush("#0F766E") };
                Grid.SetColumn(day, c + weekOffset); calendar.Children.Add(day);
            }
            var firstOffset = mondayFirst ? ((int)shownMonth.DayOfWeek + 6) % 7 : (int)shownMonth.DayOfWeek;
            var first = shownMonth.AddDays(-firstOffset);
            if (settings.ShowWeekNumbers)
                for (var r = 0; r < 6; r++)
                {
                    var week = new TextBlock { Text = "W" + GetWeekNumber(first.AddDays(r * 7)).ToString("00"),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brush("#64748B"), FontSize = Ui(10), FontWeight = FontWeights.SemiBold };
                    Grid.SetRow(week, r + 1); calendar.Children.Add(week);
                }
            for (var i = 0; i < 42; i++) AddDayCell(first.AddDays(i), i / 7 + 1, i % 7 + weekOffset);
            for (var r = 0; r < 6; r++) AddWeekEventBars(first.AddDays(r * 7), r + 1, weekOffset);
            RenderDetail();
        }

        void AddDayCell(DateTime date, int row, int col)
        {
            var stack = new StackPanel();
            var dateItems = VisibleItems(date).ToList();
            var isHoliday = dateItems.Any(x => x.Category == "국경일");
            var dateHeader = new StackPanel { Orientation = Orientation.Horizontal };
            var number = new TextBlock { Text = date.Day.ToString(), FontSize = Ui(13), FontWeight = date == DateTime.Today ? FontWeights.Bold : FontWeights.Normal,
                Foreground = date.Month != shownMonth.Month ? Brush("#CBD5E1") : isHoliday || date.DayOfWeek == DayOfWeek.Sunday ? Brush("#EF4444") : date.DayOfWeek == DayOfWeek.Saturday ? Brush("#3B82F6") : Brush("#0F172A"),
                Margin = new Thickness(5, 3, 2, 4) };
            dateHeader.Children.Add(number);
            if (settings.ShowLunar)
                dateHeader.Children.Add(new TextBlock { Text = Lunar(date), Foreground = Brush("#8B5CF6"), FontSize = Ui(9),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 1, 1, 2) });
            var holidays = string.Join(", ", dateItems.Where(x => x.Category == "국경일").Select(x => x.Title).ToArray());
            if (date == DateTime.Today)
                dateHeader.Children.Add(new TextBlock { Text = "오늘", Foreground = Brush("#2563EB"), FontSize = Ui(10),
                    FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 1, 0, 2) });
            if (!string.IsNullOrWhiteSpace(holidays))
                dateHeader.Children.Add(new TextBlock { Text = (date == DateTime.Today ? ". " : "") + holidays, Foreground = Brush("#EF4444"), FontSize = Ui(10),
                    FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(3, 1, 2, 2), TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(dateHeader);
            var border = new Border { Child = stack, BorderBrush = Brush("#99CBD5E1"), BorderThickness = new Thickness(.5),
                Background = date == DateTime.Today ? Brush("#CCFCE7F3") : date == selectedDate ? Brush("#CCDBEAFE") : Brush("#99FFFFFF"), Cursor = Cursors.Hand };
            border.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                selectedDate = date;
                if (e.ClickCount == 2) AddItem(sender, e); else RenderAll();
            };
            Grid.SetRow(border, row); Grid.SetColumn(border, col); calendar.Children.Add(border);
        }

        void AddWeekEventBars(DateTime weekStart, int row, int weekOffset)
        {
            var weekEnd = weekStart.AddDays(6);
            var weekItems = items.Where(x => x.Category != "국경일" && IsItemVisible(x) &&
                x.Start.Date <= weekEnd && (x.End > x.Start ? x.End.AddTicks(-1).Date : x.Start.Date) >= weekStart);
            if (settings.CalendarOrderMode == "time")
                weekItems = settings.MultiDayFirst
                    ? weekItems.OrderByDescending(IsMultiDay).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title)
                    : weekItems.OrderBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title);
            else
                weekItems = settings.MultiDayFirst
                    ? weekItems.OrderByDescending(IsMultiDay).ThenBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start)
                    : weekItems.OrderBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start);
            var laneEnds = new List<DateTime>();
            foreach (var item in weekItems)
            {
                var itemEnd = item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date;
                var segmentStart = item.Start.Date < weekStart ? weekStart : item.Start.Date;
                var segmentEnd = itemEnd > weekEnd ? weekEnd : itemEnd;
                var lane = laneEnds.FindIndex(x => x < segmentStart);
                if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(segmentEnd); } else laneEnds[lane] = segmentEnd;
                if (lane >= 6) continue;
                var spansDays = item.Start.Date != itemEnd;
                var prefix = item.IsTodo ? (item.Completed ? "✓ " : "□ ") : "";
                if (!item.AllDay && !spansDays) prefix += TimeText(item.Start) + " ";
                var text = new TextBlock { Text = prefix + (item.Important ? "★ " : "") + item.Title,
                    FontSize = Ui(11), Foreground = item.Important ? Brush("#F20D7A") : settings.PastelEventStyle ? Brush("#1F2937") : Brushes.White,
                    FontWeight = item.Important ? FontWeights.Bold : FontWeights.Normal, Padding = new Thickness(5, 1, 4, 1),
                    TextTrimming = TextTrimming.CharacterEllipsis, TextDecorations = item.Completed ? TextDecorations.Strikethrough : null };
                var bar = new Border { Child = text, Height = Ui(19), CornerRadius = new CornerRadius(4),
                    Background = item.Important ? Brush("#FFF1F7") : settings.PastelEventStyle ? PastelBrush(ItemColor(item), .72) : Brush(ItemColor(item)),
                    Margin = new Thickness(2, Ui(29 + lane * 20), 2, 0), VerticalAlignment = VerticalAlignment.Top,
                    Cursor = Cursors.Hand, ToolTip = "클릭하여 날짜 선택 · 더블클릭하여 수정" };
                bar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    var days = (segmentEnd - segmentStart).Days + 1;
                    var clickedDay = bar.ActualWidth <= 0 ? 0 : Math.Min(days - 1, Math.Max(0, (int)(e.GetPosition(bar).X / bar.ActualWidth * days)));
                    selectedDate = segmentStart.AddDays(clickedDay);
                    if (e.ClickCount == 2) OpenEdit(item); else RenderAll();
                    e.Handled = true;
                };
                Grid.SetRow(bar, row); Grid.SetColumn(bar, (segmentStart - weekStart).Days + weekOffset);
                Grid.SetColumnSpan(bar, (segmentEnd - segmentStart).Days + 1); Panel.SetZIndex(bar, 5); calendar.Children.Add(bar);
            }
        }

        void RenderDetail()
        {
            selectedTitle.Text = selectedDate.ToString("M월 d일 dddd", new CultureInfo("ko-KR")); detail.Children.Clear();
            var dayItems = VisibleItems(selectedDate).ToList();
            if (dayItems.Count == 0) detail.Children.Add(new TextBlock { Text = "일정이 없습니다.", Foreground = Brush("#94A3B8"), Margin = new Thickness(0, 8, 0, 0) });
            foreach (var sourceGroup in dayItems.GroupBy(DisplayGroup).OrderBy(x => GroupOrder(x.First())))
            {
                var categoryItems = sourceGroup.ToList();
                var groupColor = ItemColor(categoryItems[0]);
                var group = new StackPanel();
                group.Children.Add(new TextBlock { Text = "●  " + sourceGroup.Key, Foreground = Brush(groupColor),
                    FontWeight = FontWeights.Bold, FontSize = Ui(12), Margin = new Thickness(0, 0, 0, 7) });
                foreach (var item in categoryItems)
                {
                    var row = new DockPanel { Margin = new Thickness(0, 3, 0, 8) };
                    if (item.IsTodo)
                    {
                        var check = new CheckBox { IsChecked = item.Completed,
                            ToolTip = item.GoogleTaskEvent ? "완료 상태는 온하루에 저장됩니다." : null,
                            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 8, 0) };
                        check.Click += async delegate
                        {
                            item.Completed = check.IsChecked == true; Store.Save(items); RenderAll();
                            if (item.Category == "개인일정" && !item.GoogleTaskEvent && !item.GoogleReadOnly && GoogleCalendar.IsConnected) await SaveGoogleItem(item);
                        };
                        DockPanel.SetDock(check, Dock.Left); row.Children.Add(check);
                    }
                    var text = new StackPanel();
                    text.Children.Add(new TextBlock { Text = (item.Important ? "★ " : "") + (item.AllDay ? "" : TimeText(item.Start) + " ") + item.Title,
                        FontWeight = item.Important ? FontWeights.Bold : FontWeights.SemiBold,
                        Foreground = item.Important ? Brush("#F20D7A") : item.Category == "국경일" ? Brush("#EF4444") : Brush("#1E293B"),
                        TextDecorations = item.Completed ? TextDecorations.Strikethrough : null });
                    if (item.AllDay || IsMultiDay(item))
                        text.Children.Add(new TextBlock { Text = DetailTimeText(item, selectedDate),
                            FontSize = Ui(11), Foreground = Brush(ItemColor(item)) });
                    if (!string.IsNullOrWhiteSpace(item.Notes))
                        text.Children.Add(new TextBlock { Text = item.Notes, FontSize = Ui(11), Foreground = Brush("#64748B"),
                            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                    text.Cursor = Cursors.Hand; text.ToolTip = "더블클릭하여 수정";
                    text.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                    { if (e.ClickCount == 2) { OpenEdit(item); e.Handled = true; } };
                    row.Children.Add(text);
                    if (itemNoticeId == item.Id) row.Margin = new Thickness(0, 3, 0, 2);
                    group.Children.Add(row);
                    if (itemNoticeId == item.Id)
                        group.Children.Add(new TextBlock { Text = itemNoticeText, Foreground = Brush("#DC2626"), FontSize = Ui(11),
                            FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(item.IsTodo ? 24 : 0, 0, 0, 8) });
                }
                detail.Children.Add(new Border { Background = PastelBrush(groupColor, .86),
                    BorderBrush = PastelBrush(groupColor, .62), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(11), Padding = new Thickness(12), Margin = new Thickness(0, 5, 0, 8), Child = group });
            }
            var add = Button("+ 이 날짜에 추가", AddItem, 150); add.Margin = new Thickness(0, 14, 0, 0); detail.Children.Add(add);
        }

        IEnumerable<PlannerItem> VisibleItems(DateTime date)
        {
            var day = items.Where(x => OccursOnDate(x, date) && IsItemVisible(x));
            if (settings.CalendarOrderMode == "time")
                return day.OrderBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title);
            return day.OrderBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start);
        }

        internal static bool OccursOnDate(PlannerItem item, DateTime date)
        {
            var last = item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date;
            return date.Date >= item.Start.Date && date.Date <= last;
        }

        static bool IsMultiDay(PlannerItem item)
        {
            return item.Start.Date != (item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date);
        }

        string TimeText(DateTime value)
        {
            return settings.Use24HourTime ? value.ToString("HH:mm") : value.ToString("tt h:mm", new CultureInfo("ko-KR"));
        }

        string DetailTimeText(PlannerItem item, DateTime date)
        {
            if (item.AllDay) return "하루 종일";
            if (!IsMultiDay(item)) return TimeText(item.Start);
            return item.Start.ToString("M/d ") + TimeText(item.Start) + " – " + item.End.ToString("M/d ") + TimeText(item.End);
        }

        bool IsItemVisible(PlannerItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.GoogleCalendarId))
            {
                if (settings.GoogleCalendars == null || !settings.GoogleCalendars.Any(x => x.Id == item.GoogleCalendarId)) return false;
                var key = "google:" + item.GoogleCalendarId;
                return !filters.ContainsKey(key) || filters[key].IsChecked == true;
            }
            return !filters.ContainsKey(item.Category) || filters[item.Category].IsChecked == true;
        }

        string ItemColor(PlannerItem item)
        {
            if (item.Category == "국경일") return Colors["국경일"];
            return !string.IsNullOrWhiteSpace(item.GoogleCalendarColor) ? item.GoogleCalendarColor : Colors.ContainsKey(item.Category) ? Colors[item.Category] : Colors["개인일정"];
        }

        string DisplayGroup(PlannerItem item)
        {
            if (string.IsNullOrWhiteSpace(item.GoogleCalendarId)) return item.Category;
            var source = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Id == item.GoogleCalendarId);
            var name = !string.IsNullOrWhiteSpace(item.GoogleCalendarName) ? item.GoogleCalendarName : source == null ? "Google" : source.Name;
            return source != null && source.Primary ? "내 캘린더 · " + name : name;
        }

        int GroupOrder(PlannerItem item)
        {
            var key = item.Category == "업무일정" ? "local:business" : string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "local:personal" : "google:" + item.GoogleCalendarId;
            var index = settings.CategoryOrder == null ? -1 : settings.CategoryOrder.IndexOf(key);
            return index < 0 ? 999 : index;
        }

        int GetWeekNumber(DateTime date)
        {
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date,
                settings.WeekNumberRule == "iso" ? CalendarWeekRule.FirstFourDayWeek : CalendarWeekRule.FirstDay,
                settings.WeekNumberRule == "iso" ? DayOfWeek.Monday : DayOfWeek.Sunday);
        }

        static string Lunar(DateTime date)
        {
            try { var c = new KoreanLunisolarCalendar(); return "음 " + c.GetMonth(date) + "/" + c.GetDayOfMonth(date); }
            catch { return ""; }
        }

        void BuildGoogleFilters()
        {
            if (googleFilterPanel == null) return;
            UpdateAccountStatus();
            foreach (var key in filters.Keys.Where(x => x.StartsWith("google:")).ToList()) filters.Remove(key);
            googleFilterPanel.Children.Clear();
            if (settings.GoogleCalendars == null || settings.GoogleCalendars.Count == 0)
            {
                googleFilterPanel.Children.Add(new TextBlock { Text = "G 연결 후 목록이 표시됩니다.", Foreground = Brush("#94A3B8"), FontSize = Ui(11) });
                return;
            }
            foreach (var source in settings.GoogleCalendars.OrderBy(x => CategoryRank("google:" + x.Id)).ThenBy(x => x.Name))
            {
                var key = "google:" + source.Id;
                var color = string.IsNullOrWhiteSpace(source.Color) ? Colors["개인일정"] : source.Color;
                var box = new CheckBox { Content = (source.Primary ? "내 캘린더 · " : "") + source.Name,
                    IsChecked = source.Visible, Foreground = Brush(color), Margin = new Thickness(0, 0, 0, 6) };
                box.Click += delegate { source.Visible = box.IsChecked == true; Store.SaveSettings(settings); RenderAll(); };
                filters[key] = box; googleFilterPanel.Children.Add(box);
            }
        }

        static bool IsHolidayCalendar(GoogleCalendarSetting source)
        {
            return (source.Name ?? "").Contains("휴일") || (source.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        int CategoryRank(string key)
        {
            var index = settings.CategoryOrder == null ? -1 : settings.CategoryOrder.IndexOf(key);
            return index < 0 ? 999 : index;
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

        async void OpenSettings(object sender, RoutedEventArgs e)
        {
            var allLocalItems = Store.LoadLocal();
            var localItems = allLocalItems.Where(x => !items.Any(y => y.Id == x.Id)).ToList();
            if (localItems.Count != allLocalItems.Count) Store.SaveLocal(localItems);
            var persistedSettings = Store.LoadSettings();
            settings.CustomPalette = persistedSettings.CustomPalette;
            settings.CustomPalettePastelStyle = persistedSettings.CustomPalettePastelStyle;
            settings.PaletteNames = persistedSettings.PaletteNames;
            settings.SavedPalettes = persistedSettings.SavedPalettes;
            var window = new SettingsWindow(Colors["업무일정"], Colors["개인일정"], settings.FontSize,
                settings.CalendarOrderMode, settings.MultiDayFirst, settings.Use24HourTime, settings.ShowWeekNumbers, settings.WeekNumberRule,
                settings.PastelEventStyle, settings.AutoSyncMinutes, settings.GoogleCalendars,
                GoogleCalendar.IsConnected, localItems.Count, settings.ShowLunar, Store.Backups().Length, settings.CategoryOrder,
                settings.CustomPalette, settings.CustomPalettePastelStyle, settings.PaletteNames, settings.SavedPalettes) { Owner = this };
            if (window.ShowDialog() != true) return;
            Colors["업무일정"] = window.BusinessColor; Colors["개인일정"] = window.PersonalColor;
            settings.BusinessColor = window.BusinessColor; settings.PersonalColor = window.PersonalColor;
            settings.FontSize = window.SelectedFontSize; settings.CalendarOrderMode = window.OrderMode;
            settings.MultiDayFirst = window.MultiDayFirst;
            settings.Use24HourTime = window.Use24HourTime;
            settings.CategoryOrder = window.CategoryOrder;
            settings.CustomPalette = window.CustomPalette;
            settings.CustomPalettePastelStyle = window.CustomPalettePastelStyle;
            settings.PaletteNames = window.PaletteNames;
            settings.SavedPalettes = window.SavedPalettes;
            settings.ShowWeekNumbers = window.ShowWeekNumbers; settings.WeekNumberRule = window.WeekRule;
            settings.ShowLunar = window.ShowLunar;
            settings.PastelEventStyle = window.PastelEventStyle;
            settings.AutoSyncMinutes = window.AutoSyncMinutes;
            FontSize = settings.FontSize;
            monthTitle.FontSize = Ui(24); selectedTitle.FontSize = Ui(18);
            Store.SaveSettings(settings);
            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId)))
            {
                var source = settings.GoogleCalendars.FirstOrDefault(x => x.Id == item.GoogleCalendarId);
                if (source != null) { item.GoogleCalendarColor = source.Color; item.GoogleReadOnly = !source.Editable; }
            }
            Store.Save(items); BuildGoogleFilters(); StartAutoSync();
            foreach (var category in filters.Keys.Where(Colors.ContainsKey)) filters[category].Foreground = Brush(Colors[category]);
            RenderAll();
            if (window.ImportLocalItems)
            {
                var importWindow = new LocalImportWindow(localItems) { Owner = this };
                if (importWindow.ShowDialog() == true)
                {
                    foreach (var item in importWindow.SelectedItems)
                        if (!items.Any(x => x.Id == item.Id)) items.Add(item);
                    localItems.RemoveAll(x => importWindow.SelectedItems.Any(y => y.Id == x.Id));
                    Store.Save(items); Store.SaveLocal(localItems); RenderAll();
                }
            }
            if (window.RestoreBackup)
            {
                var backup = new BackupWindow(Store.Backups()) { Owner = this };
                if (backup.ShowDialog() == true)
                { items.Clear(); items.AddRange(Store.Restore(backup.SelectedPath)); RenderAll(); }
            }
            if (window.ExportItems)
                new ExportWindow(items.OrderBy(x => x.Start).ToList()) { Owner = this }.ShowDialog();
            if (window.ChangeGoogleAccount || window.LogoutGoogleAccount)
            {
                GoogleCalendar.Disconnect();
                Store.SetAccount(null);
                items.Clear();
                settings.ActiveGoogleAccountId = null;
                settings.GoogleCalendars.Clear();
                if (window.LogoutGoogleAccount) items.AddRange(Store.LoadLocal());
                Store.SaveSettings(settings); BuildGoogleFilters(); RenderAll(); UpdateGoogleButton();
                if (window.LogoutGoogleAccount) { StartAutoSync(); return; }
                if (await ConnectGoogle(false)) await SyncGoogle(true);
                StartAutoSync();
            }
        }

        void OpenSearch(object sender, RoutedEventArgs e)
        {
            var window = new SearchWindow(items.Where(IsItemVisible).ToList()) { Owner = this };
            if (window.ShowDialog() == true && window.SelectedItem != null)
            { selectedDate = window.SelectedItem.Start.Date; shownMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1); RenderAll(); OpenEdit(window.SelectedItem); }
        }

        void SaveWindowSettings()
        {
            settings.HasPosition = true; settings.Left = Left; settings.Top = Top;
            settings.Width = ActualWidth; settings.Height = ActualHeight; settings.PositionLocked = positionLocked;
            settings.FontSize = FontSize; settings.Opacity = Opacity;
            if (filters.ContainsKey("업무일정")) settings.BusinessVisible = filters["업무일정"].IsChecked == true;
            if (filters.ContainsKey("개인일정")) settings.PersonalVisible = filters["개인일정"].IsChecked == true;
            if (filters.ContainsKey("국경일")) settings.HolidayVisible = filters["국경일"].IsChecked == true;
            Store.SaveSettings(settings);
        }

        async void AddItem(object sender, RoutedEventArgs e)
        {
            var window = new AddItemWindow(selectedDate, null, settings.GoogleCalendars, GoogleCalendar.IsConnected);
            PlaceCalendarDialog(window);
            if (window.ShowDialog() == true)
            {
                items.Add(window.Result);
                if (string.IsNullOrWhiteSpace(window.Result.GoogleCalendarId)) ExpandLocalRecurrence(window.Result);
                Store.Save(items); RenderAll();
                if (!string.IsNullOrWhiteSpace(window.Result.GoogleCalendarId) && GoogleCalendar.IsConnected)
                { await SaveGoogleItem(window.Result); if (!string.IsNullOrWhiteSpace(window.Result.RecurrenceFrequency)) await SyncGoogle(false); }
            }
        }

        void ExpandLocalRecurrence(PlannerItem master)
        {
            if (string.IsNullOrWhiteSpace(master.RecurrenceFrequency) || master.RecurrenceUntil <= master.Start.Date) return;
            master.SeriesId = string.IsNullOrWhiteSpace(master.SeriesId) ? Guid.NewGuid().ToString() : master.SeriesId;
            var start = master.Start; var duration = master.End - master.Start; var count = 0;
            while (count++ < 500)
            {
                start = RecurrenceService.NextOccurrence(master, start);
                if (start.Date > master.RecurrenceUntil.Date) break;
                items.Add(new PlannerItem { Id = Guid.NewGuid().ToString(), Title = master.Title, Start = start, End = start.Add(duration), AllDay = master.AllDay,
                    IsTodo = master.IsTodo, Category = master.Category, Notes = master.Notes, CreatedInOnharu = true, RolloverMode = master.RolloverMode,
                    AutoRollover = master.AutoRollover, Important = master.Important, ReminderMinutes = master.ReminderMinutes, ReminderConfigured = master.ReminderConfigured,
                    RecurrenceFrequency = master.RecurrenceFrequency, RecurrenceMode = master.RecurrenceMode, RecurrenceDays = master.RecurrenceDays,
                    RecurrenceUntil = master.RecurrenceUntil, SeriesId = master.SeriesId });
            }
        }





        async void OpenEdit(PlannerItem item)
        {
            if (item.GoogleReadOnly)
            {
                ShowItemNotice(item, item.Category == "국경일" ? "읽기 전용 일정입니다." : "설정에서 수정 가능을 선택해주세요.");
                return;
            }
            var oldRecurrence = item.RecurrenceFrequency; var oldMode = item.RecurrenceMode; var oldDays = item.RecurrenceDays; var oldUntil = item.RecurrenceUntil;
            var originalSeriesStart = string.IsNullOrWhiteSpace(item.SeriesId) ? item.Start : items.Where(x => x.SeriesId == item.SeriesId).Min(x => x.Start);
            var window = new AddItemWindow(item.Start.Date, item, settings.GoogleCalendars, GoogleCalendar.IsConnected);
            PlaceCalendarDialog(window);
            if (window.ShowDialog() != true) return;
            if (window.DeleteRequested)
            {
                var recurring = !string.IsNullOrWhiteSpace(item.SeriesId) || !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId) || !string.IsNullOrWhiteSpace(item.RecurrenceFrequency);
                var deleteScope = "single";
                if (recurring)
                {
                    var deleteWindow = new RepeatDeleteWindow(item) { Owner = this };
                    if (deleteWindow.ShowDialog() != true) return;
                    deleteScope = deleteWindow.Scope;
                }
                var isGoogle = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) || !string.IsNullOrWhiteSpace(item.GoogleEventId);
                if (isGoogle && GoogleCalendar.IsConnected)
                {
                    try
                    {
                        if (deleteScope == "future") await GoogleCalendar.TrimSeriesBeforeAsync(item);
                        else await GoogleCalendar.DeleteAsync(item, deleteScope == "all");
                    }
                    catch (Exception ex) { ErrorLog.Write("Delete Google event", ex); ShowItemNotice(item, "Google에서 삭제하지 못했습니다 · 일정은 유지됩니다."); return; }
                }
                if (deleteScope == "all" && !string.IsNullOrWhiteSpace(item.SeriesId)) items.RemoveAll(x => x.SeriesId == item.SeriesId);
                else if (deleteScope == "all" && !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId)) items.RemoveAll(x => x.GoogleRecurringEventId == item.GoogleRecurringEventId);
                else if (deleteScope == "future" && !string.IsNullOrWhiteSpace(item.SeriesId)) items.RemoveAll(x => x.SeriesId == item.SeriesId && x.Start >= item.Start);
                else if (deleteScope == "future" && !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId)) items.RemoveAll(x => x.GoogleRecurringEventId == item.GoogleRecurringEventId && x.Start >= item.Start);
                else items.RemoveAll(x => x.Id == item.Id);
            }
            else
            {
                var oldGoogle = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) || !string.IsNullOrWhiteSpace(item.GoogleEventId);
                var newGoogle = !string.IsNullOrWhiteSpace(window.Result.GoogleCalendarId);
                if (!window.ApplyToSeries && !string.IsNullOrWhiteSpace(item.SeriesId) && !oldGoogle)
                { window.Result.SeriesId = null; window.Result.RecurrenceFrequency = null; window.Result.RecurrenceMode = null; window.Result.RecurrenceDays = null; window.Result.RecurrenceUntil = window.Result.Start.Date; }
                var movedCalendar = oldGoogle && (!newGoogle || item.GoogleCalendarId != window.Result.GoogleCalendarId);
                if (movedCalendar && GoogleCalendar.IsConnected)
                    try { await GoogleCalendar.DeleteAsync(item, window.ApplyToSeries); } catch (Exception ex) { ErrorLog.Write("Delete Google recurrence", ex); ShowItemNotice(item, "Google 캘린더를 변경하지 못했습니다."); return; }
                var index = items.FindIndex(x => x.Id == item.Id);
                if (index >= 0) items[index] = window.Result;
                if (window.ApplyToSeries && !string.IsNullOrWhiteSpace(item.SeriesId) && string.IsNullOrWhiteSpace(item.GoogleCalendarId) &&
                    (oldRecurrence != window.Result.RecurrenceFrequency || oldMode != window.Result.RecurrenceMode || oldDays != window.Result.RecurrenceDays || oldUntil.Date != window.Result.RecurrenceUntil.Date))
                    RebuildLocalSeries(window.Result, originalSeriesStart);
                if (window.ApplyToSeries && !string.IsNullOrWhiteSpace(item.SeriesId))
                {
                    var duration = window.Result.End - window.Result.Start;
                    foreach (var sibling in items.Where(x => x.SeriesId == item.SeriesId && x.Id != window.Result.Id))
                    {
                        sibling.Title = window.Result.Title; sibling.Notes = window.Result.Notes; sibling.Category = window.Result.Category;
                        sibling.Important = window.Result.Important; sibling.ReminderMinutes = window.Result.ReminderMinutes; sibling.ReminderConfigured = true;
                        sibling.RecurrenceFrequency = window.Result.RecurrenceFrequency; sibling.RecurrenceMode = window.Result.RecurrenceMode;
                        sibling.RecurrenceDays = window.Result.RecurrenceDays; sibling.RecurrenceUntil = window.Result.RecurrenceUntil;
                        sibling.Start = sibling.Start.Date.Add(window.Result.Start.TimeOfDay); sibling.End = sibling.Start.Add(duration);
                    }
                }
                if (newGoogle && GoogleCalendar.IsConnected) await SaveGoogleItem(window.Result, window.ApplyToSeries);
            }
            Store.Save(items); RenderAll();
        }

        void RebuildLocalSeries(PlannerItem edited, DateTime originalStart)
        {
            var seriesId = edited.SeriesId; var duration = edited.End - edited.Start; items.RemoveAll(x => x.SeriesId == seriesId && x.Id != edited.Id);
            edited.Start = originalStart.Date.Add(edited.Start.TimeOfDay); edited.End = edited.Start.Add(duration);
            if (!string.IsNullOrWhiteSpace(edited.RecurrenceFrequency)) ExpandLocalRecurrence(edited);
        }

        async void GoogleClick(object sender, RoutedEventArgs e)
        {
            if (googleConnecting)
            {
                GoogleCalendar.CancelConnect(); googleConnecting = false; UpdateGoogleButton();
                ShowGoogleStatus("Google 로그인을 취소했습니다.", 1500); return;
            }
            if (!GoogleCalendar.IsConnected)
            {
                if (!await ConnectGoogle(true)) return;
                items.Clear();
            }
            await SyncGoogle(true);
        }

        async Task<bool> ConnectGoogle(bool saveLocal)
        {
            try
            {
                if (saveLocal) Store.SaveLocal(items);
                googleConnecting = true; googleButton.IsEnabled = true; googleButton.Content = "로그인 취소";
                await GoogleCalendar.ConnectAsync(); return true;
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Connect Google account", ex);
                ShowGoogleStatus("Google 로그인 실패 또는 취소", UiRound.ErrorNoticeMilliseconds); return false;
            }
            finally { googleConnecting = false; UpdateGoogleButton(); }
        }

        void ShowGoogleStatus(string message, int milliseconds)
        {
            googleStatus.Text = message; googleStatus.Visibility = Visibility.Visible;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); googleStatus.Visibility = Visibility.Hidden; googleStatus.Text = "동기화가 완료되었습니다"; };
            timer.Start();
        }

        async Task SyncGoogle(bool showSuccess)
        {
            if (googleSyncing || !GoogleCalendar.IsConnected) return;
            googleSyncing = true;
            try
            {
                if (showSuccess) { googleButton.IsEnabled = false; googleButton.Content = "동기화 중…"; }
                settings.GoogleCalendars = await GoogleCalendar.SyncAsync(items, settings.GoogleCalendars);
                syncProblem = null;
                var primary = settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
                if (primary != null)
                {
                    settings.ActiveGoogleAccountId = primary.Id;
                    GoogleCalendar.RememberAccount(primary.Id);
                    Store.SetAccount(primary.Id);
                }
                var allowedCalendars = new HashSet<string>(settings.GoogleCalendars.Select(x => x.Id));
                items.RemoveAll(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId) && !allowedCalendars.Contains(x.GoogleCalendarId) && !x.PendingGoogleSync);
                settings.CategoryOrder = (settings.CategoryOrder ?? new List<string>()).Where(x => !x.StartsWith("google:") || allowedCalendars.Contains(x.Substring(7))).ToList();
                Store.Save(items); Store.SaveSettings(settings); BuildGoogleFilters(); RenderAll();
                if (showSuccess)
                {
                    googleStatus.Visibility = Visibility.Visible;
                    await Task.Delay(1000);
                    googleStatus.Visibility = Visibility.Hidden;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Synchronize Google Calendar", ex);
                syncProblem = IsOffline(ex) ? "오프라인" : "Google 오류"; UpdateAccountStatus();
                if (showSuccess)
                {
                    googleStatus.Text = syncProblem + " · " + ShortGoogleError(ex.Message); googleStatus.Visibility = Visibility.Visible;
                    var hideStatus = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(UiRound.ErrorNoticeMilliseconds) };
                    hideStatus.Tick += delegate { hideStatus.Stop(); googleStatus.Visibility = Visibility.Hidden; googleStatus.Text = "동기화가 완료되었습니다"; };
                    hideStatus.Start();
                }
            }
            finally { googleSyncing = false; if (showSuccess) { googleButton.IsEnabled = true; UpdateGoogleButton(); } }
        }

        void StartAutoSync()
        {
            if (autoSyncTimer != null) autoSyncTimer.Stop();
            if (settings.AutoSyncMinutes <= 0 || !GoogleCalendar.IsConnected) return;
            autoSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(settings.AutoSyncMinutes) };
            autoSyncTimer.Tick += async delegate { await SyncGoogle(false); };
            autoSyncTimer.Start();
        }

        async Task SaveGoogleItem(PlannerItem item, bool wholeSeries = false)
        {
            try { if (wholeSeries) await GoogleCalendar.UpsertSeriesAsync(item); else await GoogleCalendar.UpsertAsync(item); item.PendingGoogleSync = false; syncProblem = null; AttachPrimaryCalendar(item); Store.Save(items); RenderAll(); UpdateAccountStatus(); }
            catch (Exception ex)
            {
                ErrorLog.Write("Save Google event", ex);
                item.PendingGoogleSync = true; syncProblem = IsOffline(ex) ? "오프라인" : "Google 오류"; Store.Save(items); UpdateAccountStatus();
                ShowItemNotice(item, "로컬 저장됨 · " + ShortGoogleError(ex.Message));
            }
        }

        static string ShortGoogleError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "다시 동기화해 주세요";
            if (message.IndexOf("Bad Request", StringComparison.OrdinalIgnoreCase) >= 0) return "반복 일정 형식을 확인해 주세요";
            if (message.IndexOf("time zone", StringComparison.OrdinalIgnoreCase) >= 0) return "일정 시간대를 확인해 주세요";
            if (message.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0) return "수정 권한을 확인해 주세요";
            if (message.IndexOf("Unauthorized", StringComparison.OrdinalIgnoreCase) >= 0) return "Google 계정을 다시 연결해 주세요";
            return "Google 요청을 처리하지 못했습니다";
        }

        static bool IsOffline(Exception ex) { return ex is HttpRequestException || ex is TaskCanceledException; }

        void AttachPrimaryCalendar(PlannerItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.GoogleCalendarId)) return;
            var primary = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
            if (primary == null) return;
            item.GoogleCalendarId = primary.Id; item.GoogleCalendarName = primary.Name;
            item.GoogleCalendarColor = primary.Color; item.GoogleReadOnly = false;
        }

        void UpdateGoogleButton()
        {
            if (googleButton == null) return;
            googleButton.Content = GoogleCalendar.IsConnected ? "G 동기화" : "G 연결";
            googleButton.Background = GoogleCalendar.IsConnected ? Brush("#DBEAFE") : Brushes.White;
            UpdateAccountStatus();
        }

        void UpdateAccountStatus()
        {
            if (accountStatus == null) return;
            var primary = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
            if (GoogleCalendar.IsConnected)
            {
                var pending = items.Count(x => x.PendingGoogleSync && !string.IsNullOrWhiteSpace(x.GoogleCalendarId));
                var state = syncProblem != null
                    ? " · " + syncProblem + (pending > 0 ? " (동기화 대기 " + pending + "건)" : "")
                    : pending > 0 ? " · 동기화 대기 " + pending + "건" : " · Gmail";
                accountStatus.Text = "G  " + (primary == null ? "Google 계정" : primary.Name) + state;
                accountStatus.Foreground = syncProblem != null || pending > 0 ? Brush("#DB2777") : Brush("#4338CA");
            }
            else
            {
                accountStatus.Text = "●  로그아웃됨 · 로컬 저장";
                accountStatus.Foreground = Brush("#7C3AED");
            }
            accountStatus.ToolTip = accountStatus.Text;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(StartAccountMarquee));
        }

        void StartAccountMarquee()
        {
            accountStatusShift.BeginAnimation(TranslateTransform.XProperty, null); accountStatusShift.X = 0;
            var overflow = accountStatus.ActualWidth - accountStatusViewport.ActualWidth;
            if (overflow <= 2) return;
            var animation = new System.Windows.Media.Animation.DoubleAnimation(0, -overflow,
                TimeSpan.FromSeconds(Math.Max(3, overflow / 18))) { AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(1) };
            accountStatusShift.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        void OpenPendingSync(object sender, MouseButtonEventArgs e)
        {
            var pending = items.Where(x => x.PendingGoogleSync && !string.IsNullOrWhiteSpace(x.GoogleCalendarId)).OrderBy(x => x.Start).ToList();
            if (pending.Count == 0) { ShowGoogleStatus("모든 일정이 동기화되었습니다", 1200); return; }
            new PendingSyncWindow(pending) { Owner = this }.ShowDialog();
        }

        async void ShowItemNotice(PlannerItem item, string message)
        {
            itemNoticeId = item.Id; itemNoticeText = message; var version = ++itemNoticeVersion;
            selectedDate = item.Start.Date; RenderDetail();
            await Task.Delay(UiRound.ErrorNoticeMilliseconds);
            if (version != itemNoticeVersion) return;
            itemNoticeId = null; itemNoticeText = null; RenderDetail();
        }

        async void Rollover()
        {
            var changed = items.Where(x => x.IsTodo && !string.IsNullOrWhiteSpace(x.RolloverMode) && !x.GoogleTaskEvent && !x.Completed && x.Start.Date < DateTime.Today).ToList();
            foreach (var item in changed)
            {
                var duration = item.End - item.Start;
                var nextDate = NextRolloverDate(item.Start.Date, item.RolloverMode);
                item.Start = nextDate.Add(item.AllDay ? TimeSpan.Zero : item.Start.TimeOfDay);
                item.End = item.Start.Add(item.AllDay ? TimeSpan.FromDays(1) : duration);
            }
            if (changed.Count == 0) return;
            Store.Save(items); RenderAll();
            if (GoogleCalendar.IsConnected)
                foreach (var item in changed.Where(x => x.Category == "개인일정" && !string.IsNullOrWhiteSpace(x.GoogleEventId)))
                    try { await GoogleCalendar.UpsertAsync(item); } catch (Exception ex) { ErrorLog.Write("Rollover Google event", ex); }
            Store.Save(items);
        }

        void CheckReminders()
        {
            var now = DateTime.Now; var due = new List<PlannerItem>(); var keys = new Dictionary<string, string>();
            foreach (var item in items.Where(x => x.ReminderConfigured && x.ReminderMinutes >= 0 && !x.Completed))
            {
                var key = item.Id + "|" + item.Start.ToString("o") + "|" + item.ReminderMinutes;
                if (item.ReminderDismissedKey == key) continue;
                var target = item.SnoozeUntil > now.AddMinutes(-2) ? item.SnoozeUntil : (item.AllDay ? item.Start.Date.AddHours(9) : item.Start).AddMinutes(-item.ReminderMinutes);
                if (now >= target && now < target.AddMinutes(2) && shownReminders.Add(key)) { due.Add(item); keys[item.Id] = key; }
            }
            if (due.Count > 0) new ReminderWindow(due, delegate(int? snooze)
            {
                foreach (var item in due)
                {
                    var key = keys[item.Id];
                    if (snooze.HasValue) { item.SnoozeUntil = DateTime.Now.AddMinutes(snooze.Value); shownReminders.Remove(key); }
                    else { item.ReminderDismissedKey = key; item.SnoozeUntil = DateTime.MinValue; }
                }
                Store.Save(items);
            }) { Owner = null }.Show();
        }

        static DateTime NextRolloverDate(DateTime date, string mode)
        {
            do
            {
                date = mode == "next_week" ? date.AddDays(7) : date.AddDays(1);
                if (mode == "next_weekday")
                    while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) date = date.AddDays(1);
            }
            while (date < DateTime.Today);
            return date;
        }

        void UpdateModeButtons()
        {
            if (lockButton == null) return;
            lockButton.Content = positionLocked ? "📌 고정됨" : "📍 이동 가능";
            lockButton.ToolTip = positionLocked ? "클릭하면 위치 잠금 해제" : "클릭하면 현재 위치에 고정";
            lockButton.Background = positionLocked ? Brush("#DCFCE7") : Brushes.White;
            lockButton.Foreground = positionLocked ? Brush("#15803D") : Brush("#475569");
            if (resizeHandle != null) resizeHandle.Visibility = positionLocked ? Visibility.Collapsed : Visibility.Visible;
            if (oppositeResizeHandle != null) oppositeResizeHandle.Visibility = positionLocked ? Visibility.Collapsed : Visibility.Visible;
        }

        void PlaceCalendarDialog(Window window)
        {
            window.Owner = null;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = Left + Math.Max(20, (ActualWidth - window.Width) / 2);
            window.Top = Top + 36;
        }

        void CreateTrayIcon()
        {
            var appIcon = Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            trayIcon = new Forms.NotifyIcon { Icon = appIcon ?? Drawing.SystemIcons.Application, Text = "온하루", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("편집 모드 열기", null, delegate
            {
                Show(); Activate();
            });
            menu.Items.Add("종료", null, delegate { Close(); });
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate
            {
                Show(); Activate();
            };
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
