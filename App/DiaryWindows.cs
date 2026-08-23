using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class DiaryEditorWindow : Window
    {
        readonly DateTime originalDate;
        DateTime diaryDate;
        readonly TextBox title;
        readonly TextBox body;
        readonly TextBox dateText;
        readonly System.Windows.Controls.Calendar calendar = new System.Windows.Controls.Calendar();
        readonly TextBlock dateLabel = new TextBlock();
        readonly TextBlock validation = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)), FontSize = 11, Margin = new Thickness(2, 4, 0, 0) };
        public DiaryEntry Result;

        public DiaryEditorWindow(DateTime date, DiaryEntry existing)
        {
            originalDate = diaryDate = date.Date;
            Title = "온하루 일기"; Width = 650; Height = 540; MinWidth = 520; MinHeight = 430;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Malgun Gothic");

            var root = new Grid { Margin = new Thickness(24, 18, 20, 20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            dateText = Input(diaryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture), 36); dateText.MaxLength = 8; dateText.FontSize = 14;
            dateText.VerticalContentAlignment = VerticalAlignment.Center; dateText.ToolTip = "YYYYMMDD 8자리";
            dateText.PreviewTextInput += delegate(object sender, TextCompositionEventArgs e) { e.Handled = e.Text.Any(x => !char.IsDigit(x)); };
            calendar.SelectedDate = diaryDate; calendar.DisplayDate = diaryDate; OnharuCalendarStyle.Apply(calendar);
            var dateButton = OnharuPopupChrome.Button("▦", 27, "#EEF2FF", "#4F46E5"); dateButton.Height = 23; dateButton.FontSize = 13;
            dateButton.Margin = new Thickness(7, 0, 0, 0); dateButton.ToolTip = "일기 날짜 변경";
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Background = Brushes.Transparent };
            var close = OnharuPopupChrome.CloseButton(this); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            titlePanel.Children.Add(DiaryIcon("✎"));
            var heading = new StackPanel();
            heading.Children.Add(new TextBlock { Text = "오늘의 일기", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brush("#1E293B") });
            var dateLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            dateLabel.Text = diaryDate.ToString("yyyy년 M월 d일 dddd"); dateLabel.FontSize = 12; dateLabel.Foreground = Brush("#6366F1"); dateLabel.VerticalAlignment = VerticalAlignment.Center;
            dateLine.Children.Add(dateLabel); dateLine.Children.Add(dateButton); heading.Children.Add(dateLine);
            titlePanel.Children.Add(heading); header.Children.Add(titlePanel);
            OnharuPopupChrome.EnableDrag(this, header); root.Children.Add(header);

            var popupPanel = new StackPanel();
            popupPanel.Children.Add(new TextBlock { Text = "일기 날짜", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brush("#475569"), Margin = new Thickness(3, 1, 0, 6) });
            var dateInputRow = new DockPanel { LastChildFill = true, Margin = new Thickness(2, 0, 2, 7) };
            var applyDate = OnharuPopupChrome.Button("적용", 54, "#4F46E5", "#FFFFFF"); applyDate.Height = 36; applyDate.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(applyDate, Dock.Right); dateInputRow.Children.Add(applyDate); dateInputRow.Children.Add(dateText); popupPanel.Children.Add(dateInputRow); popupPanel.Children.Add(calendar);
            var datePopup = new Popup { PlacementTarget = dateButton, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 5,
                Child = new Border { Background = Brushes.White, BorderBrush = Brush("#818CF8"), BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12), Padding = new Thickness(9), Child = popupPanel } };
            dateButton.Click += delegate
            {
                datePopup.IsOpen = !datePopup.IsOpen;
                if (!datePopup.IsOpen) return;
                dateText.Text = diaryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                calendar.SelectedDate = diaryDate; calendar.DisplayDate = diaryDate;
                dateText.Focus(); dateText.SelectAll();
            };
            applyDate.Click += delegate { if (ParseDate(true)) datePopup.IsOpen = false; };
            datePopup.Closed += delegate
            {
                dateText.Text = diaryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                calendar.SelectedDate = diaryDate; calendar.DisplayDate = diaryDate; validation.Text = "";
            };
            calendar.SelectedDatesChanged += delegate
            {
                if (!calendar.SelectedDate.HasValue || !datePopup.IsOpen) return;
                dateText.Text = calendar.SelectedDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture); validation.Text = "";
            };
            calendar.PreviewMouseLeftButtonUp += delegate
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate
                {
                    Mouse.Capture(null); applyDate.Focus();
                }));
            };

            title = Input(existing == null ? "" : existing.Title, 42);
            title.FontSize = 15; title.MaxLength = 80; title.VerticalContentAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(0, 0, 0, 10); title.ToolTip = "일기 제목";
            Grid.SetRow(title, 1); root.Children.Add(title);

            body = Input(existing == null ? "" : existing.Content, double.NaN);
            body.AcceptsReturn = true; body.AcceptsTab = true; body.TextWrapping = TextWrapping.Wrap;
            body.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; body.VerticalContentAlignment = VerticalAlignment.Top;
            body.Padding = new Thickness(15, 13, 15, 13); body.FontSize = 14; body.ToolTip = "오늘의 이야기를 적어보세요.";
            Grid.SetRow(body, 2); root.Children.Add(body);

            var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
            var save = OnharuPopupChrome.PrimaryButton("✓  일기 저장", 112);
            save.FontWeight = FontWeights.Bold; save.Background = SaveGradient(); DockPanel.SetDock(save, Dock.Right); footer.Children.Add(save);
            var localOnly = new TextBlock { Text = "이 PC에만 안전하게 저장됩니다.", Foreground = Brush("#94A3B8"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(localOnly, Dock.Left); footer.Children.Add(localOnly);
            save.Click += delegate
            {
                if (!ParseDate(true)) return;
                if (string.IsNullOrWhiteSpace(title.Text) && string.IsNullOrWhiteSpace(body.Text))
                {
                    body.Focus(); return;
                }
                if (diaryDate != originalDate && DiaryStore.Load().Any(x => x.Date.Date == diaryDate))
                {
                    validation.Text = "선택한 날짜에는 이미 다른 일기가 있습니다."; dateText.SelectAll(); dateText.Focus(); return;
                }
                Result = new DiaryEntry { Date = diaryDate, Title = title.Text.Trim(), Content = body.Text.Trim(), UpdatedAt = DateTime.Now };
                DialogResult = true;
            };
            var footerPanel = new StackPanel(); footerPanel.Children.Add(validation); footerPanel.Children.Add(footer);
            Grid.SetRow(footerPanel, 3); root.Children.Add(footerPanel);
            Content = OnharuPopupChrome.Shell(root);
            Loaded += delegate { (string.IsNullOrWhiteSpace(title.Text) ? title : body).Focus(); };
        }

        void SetDate(DateTime value)
        {
            diaryDate = value.Date; dateText.Text = diaryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            dateLabel.Text = diaryDate.ToString("yyyy년 M월 d일 dddd"); validation.Text = "";
            calendar.SelectedDate = diaryDate; calendar.DisplayDate = diaryDate;
        }

        bool ParseDate(bool focusOnError)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(dateText.Text.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) || parsed.Year < 1900)
            {
                validation.Text = "날짜를 YYYYMMDD 8자리로 입력해 주세요. 예: 20260822";
                if (focusOnError) { dateText.SelectAll(); dateText.Focus(); } return false;
            }
            SetDate(parsed); return true;
        }

        static TextBox Input(string value, double height)
        {
            var box = new TextBox { Text = value ?? "", Height = height, Background = Brushes.White, Foreground = Brush("#334155"),
                BorderBrush = Brush("#C7D2FE"), BorderThickness = new Thickness(1), Padding = new Thickness(13, 8, 13, 8),
                SelectionBrush = Brush("#C7D2FE"), Cursor = Cursors.IBeam };
            UiRound.StyleTextBox(box, 11); return box;
        }

        static Border DiaryIcon(string glyph)
        {
            return new Border { Width = 38, Height = 38, Margin = new Thickness(0, 0, 11, 0), Background = Brush("#EEF2FF"), BorderBrush = Brush("#C7D2FE"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = new TextBlock { Text = glyph, FontSize = 20, FontFamily = new FontFamily("Segoe UI Symbol"),
                    Foreground = Brush("#4F46E5"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        }

        static LinearGradientBrush SaveGradient()
        {
            var value = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
            value.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3977E8"), 0));
            value.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C5CE5"), 1)); return value;
        }

        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    public sealed class DiaryReaderWindow : Window
    {
        public event Action Changed;
        readonly DateTime initialDate;
        readonly Grid bodyGrid = new Grid();
        readonly StackPanel entryList = new StackPanel();
        readonly TextBlock readingDate = new TextBlock();
        readonly TextBlock readingTitle = new TextBlock();
        readonly TextBlock readingBody = new TextBlock();
        readonly TextBox search = new TextBox();
        readonly OnharuSegmentedSwitch modeSwitch;
        readonly OnharuSegmentedSwitch sortSwitch;
        readonly Button previousButton;
        readonly Button nextButton;
        List<DiaryEntry> entries = new List<DiaryEntry>();
        DiaryEntry current;
        bool pageMode;
        bool oldestFirst;

        public DiaryReaderWindow(DateTime selectedDate)
        {
            initialDate = selectedDate.Date;
            Title = "온하루 일기장"; Width = 920; Height = 640; MinWidth = 700; MinHeight = 500;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Malgun Gothic");

            var root = new Grid { Margin = new Thickness(24, 18, 20, 20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition());
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Background = Brushes.Transparent };
            var close = OnharuPopupChrome.CloseButton(this); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var add = OnharuPopupChrome.PrimaryButton("＋ 새 일기", 92); add.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(add, Dock.Right); header.Children.Add(add);
            modeSwitch = new OnharuSegmentedSwitch(new[] { "목록 보기", "한 장 보기" }, new[] { 72.0, 72.0 }, 0, delegate(int index) { SetPageMode(index == 1); });
            modeSwitch.Margin = new Thickness(0, 0, 8, 0); DockPanel.SetDock(modeSwitch, Dock.Right); header.Children.Add(modeSwitch);
            sortSwitch = new OnharuSegmentedSwitch(new[] { "최신순", "오래된순" }, new[] { 58.0, 68.0 }, 0, delegate(int index) { oldestFirst = index == 1; SortEntries(); RefreshList(); });
            sortSwitch.Margin = new Thickness(0, 0, 8, 0); DockPanel.SetDock(sortSwitch, Dock.Right); header.Children.Add(sortSwitch);
            header.Children.Add(OnharuPopupChrome.FeatureTitle("✎", "나의 일기장"));
            OnharuPopupChrome.EnableDrag(this, header); root.Children.Add(header);

            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) }); bodyGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var left = new Grid { Margin = new Thickness(0, 0, 12, 0) };
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition());
            search.Height = 36; search.Padding = new Thickness(11, 7, 34, 6); search.BorderBrush = Brush("#C7D2FE"); search.BorderThickness = new Thickness(1); search.ToolTip = "제목이나 내용 검색";
            UiRound.StyleTextBox(search, 10);
            var searchField = new Grid(); searchField.Children.Add(search);
            searchField.Children.Add(new TextBlock { Text = "\uE721", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14,
                Foreground = Brush("#6366F1"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 11, 0), IsHitTestVisible = false, ToolTip = "일기 검색" });
            left.Children.Add(searchField);
            var listScroll = new ScrollViewer { Content = entryList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 9, 0, 0) };
            listScroll.Loaded += delegate { UiRound.SoftenScrollBars(listScroll); }; Grid.SetRow(listScroll, 1); left.Children.Add(listScroll); bodyGrid.Children.Add(left);

            var page = new Grid(); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition()); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var pageHeader = new StackPanel { Margin = new Thickness(18, 14, 18, 10) };
            readingDate.Foreground = Brush("#6366F1"); readingDate.FontSize = 12; readingTitle.Foreground = Brush("#1E293B"); readingTitle.FontSize = 22; readingTitle.FontWeight = FontWeights.Bold; readingTitle.Margin = new Thickness(0, 5, 0, 0);
            pageHeader.Children.Add(readingDate); pageHeader.Children.Add(readingTitle); page.Children.Add(pageHeader);
            readingBody.TextWrapping = TextWrapping.Wrap; readingBody.FontSize = 14; readingBody.LineHeight = 24; readingBody.Foreground = Brush("#334155");
            var contentScroll = new ScrollViewer { Content = readingBody, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(18, 5, 18, 12) };
            contentScroll.Loaded += delegate { UiRound.SoftenScrollBars(contentScroll); }; Grid.SetRow(contentScroll, 1); page.Children.Add(contentScroll);
            var navigation = new DockPanel { Margin = new Thickness(18, 8, 18, 14), LastChildFill = false };
            var edit = OnharuPopupChrome.Button("✎  수정", 76, "#EEF2FF", "#4338CA"); DockPanel.SetDock(edit, Dock.Right); navigation.Children.Add(edit);
            previousButton = OnharuPopupChrome.Button("←  이전", 80, "#EEF2FF", "#4338CA");
            nextButton = OnharuPopupChrome.Button("다음  →", 80, "#EEF2FF", "#4338CA");
            previousButton.FontWeight = FontWeights.SemiBold; nextButton.FontWeight = FontWeights.SemiBold;
            previousButton.Margin = new Thickness(0, 0, 7, 0); DockPanel.SetDock(previousButton, Dock.Left); DockPanel.SetDock(nextButton, Dock.Left);
            navigation.Children.Add(previousButton); navigation.Children.Add(nextButton); Grid.SetRow(navigation, 2); page.Children.Add(navigation);
            var pageShell = new Border { Background = Brushes.White, BorderBrush = Brush("#E0E7FF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Child = page };
            Grid.SetColumn(pageShell, 1); bodyGrid.Children.Add(pageShell); Grid.SetRow(bodyGrid, 1); root.Children.Add(bodyGrid);

            add.Click += delegate { Edit(initialDate, entries.FirstOrDefault(x => x.Date.Date == initialDate)); };
            edit.Click += delegate { if (current != null) Edit(current.Date, current); };
            previousButton.Click += delegate { Move(false); }; nextButton.Click += delegate { Move(true); };
            search.TextChanged += delegate { RefreshList(); };
            listPanel = left;
            Content = OnharuPopupChrome.Shell(root); Loaded += delegate { Reload(initialDate); };
        }

        void Reload(DateTime preferred)
        {
            entries = DiaryStore.Load(); SortEntries(); RefreshList();
            ShowEntry(entries.FirstOrDefault(x => x.Date.Date == preferred.Date) ?? entries.FirstOrDefault());
        }

        void RefreshList()
        {
            entryList.Children.Clear(); var query = search.Text == null ? "" : search.Text.Trim();
            var filtered = entries.Where(x => query.Length == 0 || (x.Title ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || (x.Content ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || x.Date.ToString("yyyy-MM-dd").Contains(query));
            foreach (var entry in filtered)
            {
                var captured = entry; var text = new Grid { Width = 230 };
                text.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(102) }); text.ColumnDefinitions.Add(new ColumnDefinition());
                text.Children.Add(new TextBlock { Text = entry.Date.ToString("yyyy.MM.dd"), FontSize = 11, Foreground = Brush("#6366F1"), VerticalAlignment = VerticalAlignment.Center });
                var cardTitle = new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Title) ? "제목 없는 일기" : entry.Title, FontWeight = FontWeights.SemiBold, Foreground = Brush("#334155"), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(cardTitle, 1); text.Children.Add(cardTitle);
                var button = OnharuPopupChrome.Button("", 258, current != null && current.Date.Date == entry.Date.Date ? "#E0E7FF" : "#F8FAFC", "#334155");
                button.Height = 42; button.Content = text; button.Margin = new Thickness(0, 0, 0, 7); button.Padding = new Thickness(10, 4, 8, 4);
                button.Click += delegate { ShowEntry(captured); RefreshList(); }; button.MouseDoubleClick += delegate { Edit(captured.Date, captured); };
                entryList.Children.Add(button);
            }
            if (entryList.Children.Count == 0) entryList.Children.Add(new TextBlock { Text = query.Length == 0 ? "아직 작성한 일기가 없습니다." : "검색 결과가 없습니다.", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(8, 28, 8, 8) });
        }

        void ShowEntry(DiaryEntry entry)
        {
            current = entry;
            readingDate.Text = entry == null ? "" : entry.Date.ToString("yyyy년 M월 d일 dddd");
            readingTitle.Text = entry == null ? "일기를 써보세요" : string.IsNullOrWhiteSpace(entry.Title) ? "제목 없는 일기" : entry.Title;
            readingBody.Text = entry == null ? "달력의 날짜를 더블클릭하거나 ‘새 일기’를 눌러 오늘의 이야기를 남길 수 있습니다." : entry.Content;
            UpdateNavigationButtons();
        }

        void UpdateNavigationButtons()
        {
            var chronological = entries.OrderBy(x => x.Date).ToList();
            var index = current == null ? -1 : chronological.FindIndex(x => x.Date.Date == current.Date.Date);
            StyleNavigation(previousButton, index > 0); StyleNavigation(nextButton, index >= 0 && index < chronological.Count - 1);
        }

        static void StyleNavigation(Button button, bool enabled)
        {
            button.IsEnabled = enabled; button.Background = Brush(enabled ? "#EEF2FF" : "#F1F5F9");
            button.Foreground = Brush(enabled ? "#4338CA" : "#CBD5E1"); button.Cursor = enabled ? Cursors.Hand : Cursors.Arrow;
        }

        void SortEntries()
        {
            entries = oldestFirst ? entries.OrderBy(x => x.Date).ToList() : entries.OrderByDescending(x => x.Date).ToList();
        }

        FrameworkElement listPanel;

        void SetPageMode(bool singlePage)
        {
            pageMode = singlePage; bodyGrid.ColumnDefinitions[0].Width = pageMode ? new GridLength(0) : new GridLength(270);
            if (listPanel != null) listPanel.Visibility = pageMode ? Visibility.Collapsed : Visibility.Visible;
        }

        void Move(bool newer)
        {
            if (entries.Count == 0) return; var chronological = entries.OrderBy(x => x.Date).ToList();
            var index = current == null ? 0 : chronological.FindIndex(x => x.Date.Date == current.Date.Date);
            index = Math.Max(0, Math.Min(chronological.Count - 1, index + (newer ? 1 : -1))); ShowEntry(chronological[index]); RefreshList();
        }

        void Edit(DateTime date, DiaryEntry entry)
        {
            var window = new DiaryEditorWindow(date, entry) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (window.ShowDialog() == true && window.Result != null) { DiaryStore.Upsert(window.Result, entry == null ? (DateTime?)null : entry.Date); Reload(window.Result.Date); if (Changed != null) Changed(); }
        }

        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
