using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FamilyPlanner
{
    static class AlarmCenter
    {
        internal sealed class Entry { internal Guid Id; internal DateTime Due; internal string Label; internal DispatcherTimer Timer; }
        static readonly List<Entry> entries = new List<Entry>();
        internal static event Action Changed;
        internal static IList<Entry> Entries { get { return entries.OrderBy(x => x.Due).ToList(); } }

        internal static void Add(DateTime due, string label)
        {
            var entry = new Entry { Id = Guid.NewGuid(), Due = due, Label = string.IsNullOrWhiteSpace(label) ? "알람" : label.Trim() };
            entry.Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            entry.Timer.Tick += delegate
            {
                if (DateTime.Now < entry.Due) return;
                entry.Timer.Stop(); entries.Remove(entry); SystemSounds.Exclamation.Play();
                new NoticeWindow(entry.Label, false, "온하루 알람").ShowDialog();
                if (Changed != null) Changed();
            };
            entries.Add(entry); entry.Timer.Start(); if (Changed != null) Changed();
        }

        internal static void Remove(Guid id)
        {
            var entry = entries.FirstOrDefault(x => x.Id == id); if (entry == null) return;
            entry.Timer.Stop(); entries.Remove(entry); if (Changed != null) Changed();
        }
    }

    public sealed class AlarmWindow : Window
    {
        readonly StackPanel list = new StackPanel();
        readonly TextBox timerMinutes = new TextBox { Text = "10", Width = 72, Height = 30, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        readonly TextBox alarmTime = new TextBox { Text = DateTime.Now.AddHours(1).ToString("HH:mm"), Width = 88, Height = 30, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        readonly TextBox label = new TextBox { Text = "알림", Width = 180, Height = 30, VerticalContentAlignment = VerticalAlignment.Center };
        readonly DispatcherTimer countdownRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        readonly StackPanel timerGroup = new StackPanel();
        readonly StackPanel alarmGroup = new StackPanel();
        Button startButton;
        int alarmMode;

        public AlarmWindow()
        {
            // 창 크기는 팝업 본체 500×420에 그림자 여백 12px을 사방으로 더한 값이다.
            // 여백이 없으면 Shell의 DropShadow가 창 경계에서 잘려 네 모서리에 검은 자국으로 남는다.
            Title = "온하루 알람"; Width = 524; Height = 444; MinWidth = 484; MinHeight = 384;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent;
            // 크기 조절은 메인 창·검색창·시간표와 같은 네이티브 방식을 쓴다. 아래 EnableResize가 테두리를 잡는다.
            ResizeMode = ResizeMode.NoResize;
            // 실행 중 목록이 창 크기를 따라가야 하므로 바깥은 Grid로 두고 목록을 '*' 행에 넣는다.
            // StackPanel이면 ScrollViewer가 무한 높이를 받아 스크롤되지 않고 창 밖으로 넘친다.
            var root = new Grid();
            foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto })
                root.RowDefinitions.Add(new RowDefinition { Height = height });
            root.RowDefinitions.Add(new RowDefinition());
            // 왼쪽 여백 15는 의도한 값이다. FeatureHeading의 글리프가 24px 상자에 가운데 정렬돼 있어
            // `◴`의 획이 상자보다 약 5px 안쪽에서 시작한다. 상자가 아니라 획을 20px 기준선에 세운다.
            var headerRow = OnharuPopupChrome.FeatureHeader(this, "◴", "알람 · 타이머");
            headerRow.Margin = new Thickness(15, 9, 14, 9);
            Grid.SetRow(headerRow, 0); root.Children.Add(headerRow);
            var card = new Border { Background = OnharuPopupChrome.Brush(OnharuPopupChrome.ContentSurfaceColor), BorderBrush = OnharuPopupChrome.Brush("#D6DCE8"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Margin = new Thickness(12, 0, 12, 12) };
            var controls = new StackPanel();
            StyleInput(label); StyleInput(timerMinutes); StyleInput(alarmTime);
            controls.Children.Add(new TextBlock { Text = "알림 이름", Foreground = OnharuPopupChrome.Brush("#64748B"), Margin = new Thickness(0, 0, 0, 5) }); controls.Children.Add(label);
            // 타이머와 시각 알람은 동등한 두 방식이다. 슬라이딩 버튼으로 하나를 고르게 해서
            // 실행 버튼을 하나로 만든다. 그래야 대표 버튼 하나에만 브랜드 그라데이션을 쓰는 규칙이 성립하고,
            // 어느 입력칸이 어느 버튼에 연결되는지도 분명해진다.
            var row = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
            var modeSwitch = new OnharuSegmentedSwitch(new[] { "타이머", "시각 알람" }, new[] { 84.0, 84.0 }, 0,
                delegate(int index) { SetMode(index); });
            modeSwitch.SetPalette(OnharuPopupChrome.Brush("#EDE9FE"), OnharuPopupChrome.Brush("#6D28D9"),
                OnharuPopupChrome.Brush("#F8FAFC"), OnharuPopupChrome.Brush("#64748B"), OnharuPopupChrome.Brush("#C4B5FD"));
            modeSwitch.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(modeSwitch, Dock.Left); row.Children.Add(modeSwitch);
            timerGroup.Orientation = Orientation.Horizontal; timerGroup.VerticalAlignment = VerticalAlignment.Center;
            timerGroup.Margin = new Thickness(14, 0, 0, 0);
            timerGroup.Children.Add(timerMinutes);
            timerGroup.Children.Add(new TextBlock { Text = "분 뒤", FontSize = 12.5, Foreground = OnharuPopupChrome.Brush("#64748B"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) });
            DockPanel.SetDock(timerGroup, Dock.Left); row.Children.Add(timerGroup);
            alarmGroup.Orientation = Orientation.Horizontal; alarmGroup.VerticalAlignment = VerticalAlignment.Center;
            alarmGroup.Margin = new Thickness(14, 0, 0, 0); alarmGroup.Visibility = Visibility.Collapsed;
            alarmGroup.Children.Add(alarmTime);
            alarmGroup.Children.Add(new TextBlock { Text = "에", FontSize = 12.5, Foreground = OnharuPopupChrome.Brush("#64748B"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) });
            DockPanel.SetDock(alarmGroup, Dock.Left); row.Children.Add(alarmGroup);
            startButton = OnharuPopupChrome.Button("시작", 96, "#4F46E5", "#FFFFFF");
            startButton.Background = OnharuPopupChrome.BrandGradientBrush(); startButton.Foreground = Brushes.White;
            startButton.BorderBrush = Brushes.Transparent; startButton.Height = 30; startButton.FontWeight = FontWeights.Bold;
            startButton.VerticalAlignment = VerticalAlignment.Center; UiRound.Apply(startButton, 8);
            startButton.Click += delegate { if (alarmMode == 0) StartTimer(); else SetAlarm(); };
            DockPanel.SetDock(startButton, Dock.Right); row.Children.Add(startButton);
            controls.Children.Add(row); card.Child = controls;
            Grid.SetRow(card, 1); root.Children.Add(card);
            var listTitle = new TextBlock { Text = "실행 중", FontWeight = FontWeights.Bold, Foreground = OnharuPopupChrome.Brush("#334155"), Margin = new Thickness(20, 0, 20, 8) };
            Grid.SetRow(listTitle, 2); root.Children.Add(listTitle);
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list };
            scroll.Resources["OnharuScrollThumb"] = OnharuPopupChrome.Brush("#B7ACE8"); scroll.Resources["OnharuScrollTrack"] = OnharuPopupChrome.Brush("#F1F5F9");
            var listShell = new Border { Margin = new Thickness(12, 0, 12, 12), Background = OnharuPopupChrome.Brush(OnharuPopupChrome.SupportSurfaceColor),
                BorderBrush = OnharuPopupChrome.Brush("#D6DCE8"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(7, 10, 7, 10), Child = scroll };
            Grid.SetRow(listShell, 3); root.Children.Add(listShell);
            var shell = OnharuPopupChrome.Shell(root);
            shell.Margin = new Thickness(12);
            OnharuPopupChrome.EnableResize(this, shell);
            Content = shell; Loaded += delegate { UiRound.SoftenScrollBars(scroll); };
            OnharuTimeInput.Attach(alarmTime, new TimeSpan(7, 0, 0));
            countdownRefresh.Tick += delegate { Refresh(); }; countdownRefresh.Start();
            AlarmCenter.Changed += Refresh; Closed += delegate { countdownRefresh.Stop(); AlarmCenter.Changed -= Refresh; }; Refresh();
        }

        // 울릴 시각 표기. 이전에는 `yy/MM`이라 연·월을 찍어 정작 필요한 날짜가 보이지 않았다.
        // 알람은 대부분 오늘 아니면 내일이라 날짜를 숫자로 읽게 하지 않고 말로 적는다.
        static string DueText(DateTime due)
        {
            var days = (due.Date - DateTime.Today).Days;
            if (days == 0) return "오늘 " + due.ToString("HH:mm");
            if (days == 1) return "내일 " + due.ToString("HH:mm");
            return due.ToString("MM/dd HH:mm");
        }

        void SetMode(int index)
        {
            alarmMode = index;
            timerGroup.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            alarmGroup.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
            startButton.Content = index == 0 ? "시작" : "설정";
        }

        static void StyleInput(TextBox box)
        {
            box.Background = Brushes.White; box.BorderBrush = OnharuPopupChrome.Brush("#CBD5E1"); box.BorderThickness = new Thickness(1);
            box.Padding = new Thickness(8, 3, 8, 3); UiRound.StyleTextBox(box, 9);
        }

        void StartTimer()
        {
            double minutes; if (!double.TryParse(timerMinutes.Text, out minutes) || minutes <= 0 || minutes > 1440) { new NoticeWindow("1~1440분 사이로 입력해 주세요.", true, "타이머 입력 확인") { Owner = this }.ShowDialog(); return; }
            AlarmCenter.Add(DateTime.Now.AddMinutes(minutes), label.Text); Refresh();
        }

        void SetAlarm()
        {
            TimeSpan time; if (!OnharuTimeInput.TryParse(alarmTime.Text, out time)) { new NoticeWindow("시간을 24시간 형식으로 입력해 주세요. 900, 0900, 9:00 모두 됩니다.", true, "알람 입력 확인") { Owner = this }.ShowDialog(); return; }
            alarmTime.Text = OnharuTimeInput.Format(time);
            var due = DateTime.Today.Add(time); if (due <= DateTime.Now) due = due.AddDays(1); AlarmCenter.Add(due, label.Text); Refresh();
        }

        void Refresh()
        {
            list.Children.Clear();
            foreach (var entry in AlarmCenter.Entries)
            {
                var captured = entry; var row = new DockPanel();
                var remove = OnharuPopupChrome.Button("취소", 56, "#FFF1F2", "#BE123C"); remove.Height = 28; remove.BorderBrush = OnharuPopupChrome.Brush("#FECDD3");
                remove.Click += delegate { AlarmCenter.Remove(captured.Id); }; DockPanel.SetDock(remove, Dock.Right); row.Children.Add(remove);
                var remaining = captured.Due - DateTime.Now; if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                var countdown = remaining.TotalHours >= 1 ? string.Format("{0:00}:{1:00}:{2:00}", (int)remaining.TotalHours, remaining.Minutes, remaining.Seconds)
                    : string.Format("{0:00}:{1:00}", remaining.Minutes, remaining.Seconds);
                row.Children.Add(new TextBlock { Text = DueText(captured.Due) + "  ·  남은 시간 " + countdown + "  ·  " + captured.Label,
                    Foreground = OnharuPopupChrome.Brush("#334155"), VerticalAlignment = VerticalAlignment.Center });
                list.Children.Add(new Border { Background = OnharuPopupChrome.Brush(OnharuPopupChrome.ContentSurfaceColor), BorderBrush = OnharuPopupChrome.Brush("#D6DCE8"),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 7, 8, 7), Margin = new Thickness(0, 0, 0, 7), Child = row });
            }
            if (list.Children.Count == 0) list.Children.Add(new TextBlock { Text = "설정된 알람이 없습니다.", Foreground = OnharuPopupChrome.Brush("#94A3B8"), Margin = new Thickness(2, 8, 2, 8) });
        }
    }

    public partial class MainWindow
    {
        AlarmWindow alarmWindow;

        // 알람은 POPUP_POLICY의 독립 도구 창이다. 시간표·KBO와 같은 방식으로 연다.
        // 2026-09-03: `Owner = this`를 쓰고 있어 고정 상태에서 창이 뜨지 않았다. 고정 상태에서는
        // 메인 WPF 창이 cloak되는데 WPF는 소유된 창을 소유자와 함께 숨긴다. 이동으로 바꾸면
        // 소유자가 다시 보이면서 그제야 알람 창이 나타났다. Owner를 떼고 배치·활성화를 맞춘다.
        void OpenAlarm(object sender, RoutedEventArgs e)
        {
            if (alarmWindow != null && alarmWindow.IsLoaded)
            {
                if (alarmWindow.WindowState == WindowState.Minimized) alarmWindow.WindowState = WindowState.Normal;
                alarmWindow.Activate(); return;
            }
            alarmWindow = new AlarmWindow();
            PlaceCalendarDialog(alarmWindow);
            alarmWindow.Closed += delegate { alarmWindow = null; };
            alarmWindow.Show(); alarmWindow.Activate();
        }
    }
}
