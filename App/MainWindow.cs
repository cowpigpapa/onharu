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
using System.Windows.Automation;
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
        readonly HashSet<DateTime> diaryDates = new HashSet<DateTime>();
        bool diaryDatesLoaded;
        DateTime shownMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime selectedDate = DateTime.Today;
        bool positionLocked;
        bool publishPending;
        object lastDesktopClickTarget;
        Button lockButton;
        TextBlock positionStatus;
        FrameworkElement resizeSurface;
        Grid mainFrame;
        Canvas floatingOverlay;
        Border sidebarPanel;
        ColumnDefinition sidebarColumn;
        Button sidebarButton;
        Button collapseSidebarButton;
        Button googleButton;
        Border googleAccountCard;
        Button timetableButton;
        Button diaryButton;
        Button sportsButton;
        Button previousPeriodButton;
        Button nextPeriodButton;
        Button periodViewButton;
        Button monthViewButton;
        bool temporaryMonthView;
        DateTime periodViewAnchor;
        TextBlock googleStatus;
        StackPanel googleFilterPanel;
        Button selectedDayButton;
        Button thisWeekButton;
        Button nextWeekButton;
        Button dateColorButton;
        FrameworkElement dateColorPalette;
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
        DispatcherTimer googleStatusTimer;
        DispatcherTimer fixedOpacityPreviewTimer;
        double pendingFixedOpacity;
        int fixedOpacityVisualTick;
        readonly HashSet<string> shownReminders = new HashSet<string>();
        string syncProblem;
        bool googleSyncing;
        bool googleConnecting;
        int googleAccountVisualVersion;
        bool applicationExitRequested;
        int visibleEventLanes = 3;
        Forms.NotifyIcon trayIcon;
        Forms.ToolStripMenuItem trayVisibilityItem;
        Forms.ToolStripMenuItem trayPositionItem;
        bool calendarMinimized;
        readonly PlannerSettings settings;
        readonly ExplorerFramePublisher explorerFrame = new ExplorerFramePublisher();
        readonly DesktopActionWindow desktopActions = new DesktopActionWindow();

        static readonly Dictionary<string, string> Colors = new Dictionary<string, string>
        { { "업무일정", "#5B7CFA" }, { "개인일정", "#F08CA6" }, { "야구", "#16A085" }, { "기념일", "#A78BFA" }, { "국경일", "#EF4444" } };

        public MainWindow()
        {
            desktopActions.Received += HandleDesktopAction;
            settings = Store.LoadSettings();
            RestoreConnectedGoogleAccount();
            Store.SetAccount(settings.ActiveGoogleAccountId);
            items = Store.Load();
            RepairLoadedData();
            ConfigureInitialWindow();
            Content = BuildLayout();
            monthTitle.Click += OpenMonthJump;
            RenderAll();
            AttachWindowLifecycle();
            AttachDpiPlacement();
        }

    }
}
