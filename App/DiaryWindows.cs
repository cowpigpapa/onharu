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
        readonly TextBox lead;
        readonly TextBox body;
        readonly ComboBox mood;
        readonly TextBox dateText;
        readonly System.Windows.Controls.Calendar calendar = new System.Windows.Controls.Calendar();
        readonly TextBlock dateLabel = new TextBlock();
        readonly TextBlock validation = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)), FontSize = 11, Margin = new Thickness(2, 4, 0, 0) };
        public DiaryEntry Result;

        public DiaryEditorWindow(DateTime date, DiaryEntry existing)
        {
            originalDate = diaryDate = date.Date;
            Title = "온하루 일기"; Width = 720; Height = 700; MinWidth = 600; MinHeight = 590;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Malgun Gothic");

            var root = new Grid { Margin = new Thickness(30, 26, 30, 24) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            dateText = Input(diaryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 42); dateText.MaxLength = 10; dateText.FontSize = 13; dateText.FontWeight = FontWeights.SemiBold;
            dateText.VerticalContentAlignment = VerticalAlignment.Center; dateText.ToolTip = "YYYY-MM-DD";
            dateText.PreviewTextInput += delegate(object sender, TextCompositionEventArgs e) { e.Handled = e.Text.Any(x => !char.IsDigit(x) && x != '-'); };
            calendar.SelectedDate = diaryDate; calendar.DisplayDate = diaryDate; OnharuCalendarStyle.Apply(calendar);
            var dateButton = OnharuPopupChrome.Button("▦", 34, "#FFFFFF", "#171717"); dateButton.Height = 38; dateButton.FontSize = 13; dateButton.BorderBrush = Brushes.Transparent; dateButton.ToolTip = "일기 날짜 변경";
            var header = new TextBlock { Text = existing == null ? "오늘 일기 쓰기" : "일기 수정", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brush("#171717"), Margin = new Thickness(0, 0, 0, 25) };
            OnharuPopupChrome.EnableDrag(this, header); root.Children.Add(header);

            var popupPanel = new StackPanel();
            var applyDate = OnharuPopupChrome.Button("적용", double.NaN, "#171717", "#FFFFFF"); applyDate.Height = 34; applyDate.Margin = new Thickness(4, 7, 4, 3);
            popupPanel.Children.Add(calendar); popupPanel.Children.Add(applyDate);
            var datePopup = new Popup { PlacementTarget = dateButton, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 5,
                Child = OnharuCalendarStyle.PopupHost(popupPanel, 9) };
            dateButton.Click += delegate
            {
                datePopup.IsOpen = !datePopup.IsOpen;
                if (!datePopup.IsOpen) return;
                dateText.Text = diaryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                calendar.SelectedDate = diaryDate; calendar.DisplayDate = diaryDate;
                dateText.Focus(); dateText.SelectAll();
            };
            applyDate.Click += delegate { if (ParseDate(true)) datePopup.IsOpen = false; };
            datePopup.Closed += delegate
            {
                dateText.Text = diaryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                calendar.SelectedDate = diaryDate; calendar.DisplayDate = diaryDate; validation.Text = "";
            };
            calendar.SelectedDatesChanged += delegate
            {
                if (!calendar.SelectedDate.HasValue || !datePopup.IsOpen) return;
                dateText.Text = calendar.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); validation.Text = "";
            };
            calendar.PreviewMouseLeftButtonUp += delegate
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate
                {
                    Mouse.Capture(null); applyDate.Focus();
                }));
            };

            var dateMood = new Grid { Margin = new Thickness(0, 0, 0, 18) }; dateMood.ColumnDefinitions.Add(new ColumnDefinition()); dateMood.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
            var dateShell = new Grid(); dateShell.Children.Add(dateText); dateButton.HorizontalAlignment = HorizontalAlignment.Right; dateButton.Margin = new Thickness(0, 2, 5, 2); dateShell.Children.Add(dateButton);
            dateMood.Children.Add(Labeled("날짜", dateShell));
            mood = new ComboBox { Height = 42, Background = Brushes.White, BorderBrush = Brush("#E8E4E0"), BorderThickness = new Thickness(1), Padding = new Thickness(12, 7, 8, 7), Margin = new Thickness(14, 0, 0, 0) };
            foreach (var item in new[] { "보통", "느긋함", "개운함", "차분함", "반가움", "설렘" }) mood.Items.Add(item);
            mood.SelectedItem = existing == null || string.IsNullOrWhiteSpace(existing.Mood) ? "보통" : existing.Mood; SettingsWindow.StyleComboBox(mood);
            var moodField = Labeled("기분", mood); Grid.SetColumn(moodField, 1); dateMood.Children.Add(moodField); Grid.SetRow(dateMood, 1); root.Children.Add(dateMood);

            title = Input(existing == null ? "" : existing.Title, 42); title.FontSize = 14; title.MaxLength = 80; title.Margin = new Thickness(0, 0, 0, 18);
            var titleField = Labeled("제목", title); Grid.SetRow(titleField, 2); root.Children.Add(titleField);
            lead = Input(existing == null ? "" : existing.Lead, 88); lead.AcceptsReturn = true; lead.TextWrapping = TextWrapping.Wrap; lead.VerticalContentAlignment = VerticalAlignment.Top; lead.Padding = new Thickness(13, 11, 13, 11);
            var leadField = Labeled("한 줄 요약", lead); leadField.Margin = new Thickness(0, 0, 0, 18); Grid.SetRow(leadField, 3); root.Children.Add(leadField);
            body = Input(existing == null ? "" : existing.Content, double.NaN); body.AcceptsReturn = true; body.AcceptsTab = true; body.TextWrapping = TextWrapping.Wrap; body.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; body.VerticalContentAlignment = VerticalAlignment.Top; body.Padding = new Thickness(13, 11, 13, 11); body.FontSize = 14;
            var bodyField = Labeled("본문", body); Grid.SetRow(bodyField, 4); root.Children.Add(bodyField);

            var footer = new DockPanel { Margin = new Thickness(0, 18, 0, 0), Height = 36, LastChildFill = false };
            var save = OnharuPopupChrome.Button("저장", 54, "#4338CA", "#FFFFFF"); save.Height = 36; save.FontWeight = FontWeights.Bold; DockPanel.SetDock(save, Dock.Right); footer.Children.Add(save);
            var cancel = OnharuPopupChrome.Button("취소", 54, "#FFFFFF", "#171717"); cancel.Height = 36; cancel.BorderBrush = Brush("#E8E4E0"); cancel.Margin = new Thickness(0, 0, 8, 0); cancel.Click += delegate { Close(); }; DockPanel.SetDock(cancel, Dock.Right); footer.Children.Add(cancel);
            save.Click += delegate
            {
                if (!ParseDate(true)) return;
                if (diaryDate != originalDate && DiaryStore.Load().Any(x => x.Date.Date == diaryDate))
                {
                    validation.Text = "선택한 날짜에는 이미 다른 일기가 있습니다."; dateText.SelectAll(); dateText.Focus(); return;
                }
                Result = new DiaryEntry { Date = diaryDate, Title = title.Text.Trim(), Mood = mood.SelectedItem as string, Lead = lead.Text.Trim(), Content = body.Text.Trim(), UpdatedAt = DateTime.Now };
                DialogResult = true;
            };
            var footerPanel = new StackPanel(); footerPanel.Children.Add(validation); footerPanel.Children.Add(footer);
            Grid.SetRow(footerPanel, 5); root.Children.Add(footerPanel);
            var shell = OnharuPopupChrome.Shell(root); shell.Background = Brush("#FFFEFD"); shell.ClipToBounds = true;
            Action clip = delegate { if (shell.ActualWidth > 0 && shell.ActualHeight > 0) shell.Clip = new RectangleGeometry(new Rect(0, 0, shell.ActualWidth, shell.ActualHeight), 22, 22); };
            shell.Loaded += delegate { clip(); }; shell.SizeChanged += delegate { clip(); }; Content = shell;
            Loaded += delegate { (string.IsNullOrWhiteSpace(title.Text) ? title : body).Focus(); };
        }

        void SetDate(DateTime value)
        {
            diaryDate = value.Date; dateText.Text = diaryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            dateLabel.Text = diaryDate.ToString("yyyy년 M월 d일 dddd"); validation.Text = "";
            calendar.SelectedDate = diaryDate; calendar.DisplayDate = diaryDate;
        }

        bool ParseDate(bool focusOnError)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(dateText.Text.Trim(), new[] { "yyyy-MM-dd", "yyyyMMdd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) || parsed.Year < 1900)
            {
                validation.Text = "날짜를 YYYY-MM-DD 형식으로 입력해 주세요. 예: 2026-08-31";
                if (focusOnError) { dateText.SelectAll(); dateText.Focus(); } return false;
            }
            SetDate(parsed); return true;
        }

        static TextBox Input(string value, double height)
        {
            var box = new TextBox { Text = value ?? "", Height = height, Background = Brushes.White, Foreground = Brush("#334155"),
                BorderBrush = Brush("#D5D8DE"), BorderThickness = new Thickness(1), Padding = new Thickness(13, 8, 13, 8),
                SelectionBrush = Brush("#C7D2FE"), Cursor = Cursors.IBeam };
            UiRound.StyleTextBox(box, 11); return box;
        }

        static StackPanel Labeled(string label, FrameworkElement content)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = label, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brush("#77716D"), Margin = new Thickness(0, 0, 0, 7) });
            panel.Children.Add(content); return panel;
        }

        static Border DiaryIcon(string glyph)
        {
            return new Border { Width = 38, Height = 38, Margin = new Thickness(0, 0, 11, 0), Background = Brushes.White, BorderBrush = Brush("#D5D8DE"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = new TextBlock { Text = glyph, FontSize = 20, FontFamily = new FontFamily("Segoe UI Symbol"),
                    Foreground = Brush("#1F2937"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
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
        readonly TextBlock readingLead = new TextBlock();
        readonly TextBlock readingBody = new TextBlock();
        readonly TextBlock entryCount = new TextBlock();
        readonly TextBox search = new TextBox();
        readonly Button viewButton;
        readonly Button sortButton;
        readonly Button selectionButton;
        readonly Button selectAllButton;
        readonly Button deleteSelectedButton;
        readonly Button previousButton;
        readonly Button nextButton;
        readonly HashSet<DateTime> selectedDates = new HashSet<DateTime>();
        List<DiaryEntry> entries = new List<DiaryEntry>();
        DiaryEntry current;
        bool pageMode;
        bool oldestFirst;
        bool selectionMode;

        public DiaryReaderWindow(DateTime selectedDate)
        {
            initialDate = selectedDate.Date;
            Title = "온하루 일기장"; Width = 1120; Height = 720; MinWidth = 820; MinHeight = 560;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Malgun Gothic");
            var root = new Grid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); bodyGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var left = new Grid { Background = Brushes.White, Margin = new Thickness(0, 0, 0, 0) };
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition());
            left.Children.Add(new StackPanel { Margin = new Thickness(24, 25, 20, 14), Children = {
                new TextBlock { Text = "온하루 일기장", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brush("#171717") },
                new TextBlock { Text = "평범한 하루에서 오래 남기고 싶은 것들을 기록합니다.", FontSize = 11.5, Foreground = Brush("#77716D"), TextWrapping = TextWrapping.Wrap, LineHeight = 19, Margin = new Thickness(0, 7, 0, 0) } } });
            var diaryBar = new DockPanel { Margin = new Thickness(24, 4, 20, 10) };
            entryCount.Foreground = Brush("#171717"); entryCount.FontSize = 10.5; entryCount.FontWeight = FontWeights.Bold; entryCount.VerticalAlignment = VerticalAlignment.Center;
            var countChip = new Border { Background = Brush("#DFF3EA"), CornerRadius = new CornerRadius(14), Padding = new Thickness(9, 5, 9, 5), Child = entryCount };
            DockPanel.SetDock(countChip, Dock.Right); diaryBar.Children.Add(countChip); diaryBar.Children.Add(new TextBlock { Text = "D I A R Y", FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetRow(diaryBar, 1); left.Children.Add(diaryBar);
            var tools = new WrapPanel { Margin = new Thickness(24, 0, 20, 12) };
            sortButton = PaperButton("최신순 ↓", 76); sortButton.Click += delegate { oldestFirst = !oldestFirst; SetSortIcon(); SortEntries(); RefreshList(); };
            selectionButton = PaperButton("선택", 48); selectionButton.Margin = new Thickness(6, 0, 0, 0); selectionButton.Click += delegate { SetSelectionMode(!selectionMode); };
            selectAllButton = PaperButton("전체", 48); selectAllButton.Margin = new Thickness(6, 0, 0, 0); selectAllButton.Visibility = Visibility.Collapsed; selectAllButton.Click += delegate { ToggleSelectAll(); };
            deleteSelectedButton = PaperButton("삭제", 48); deleteSelectedButton.Foreground = Brush("#A44343"); deleteSelectedButton.Margin = new Thickness(6, 0, 0, 0); deleteSelectedButton.Visibility = Visibility.Collapsed; deleteSelectedButton.Click += delegate { DeleteSelected(); };
            tools.Children.Add(sortButton); tools.Children.Add(selectionButton); tools.Children.Add(selectAllButton); tools.Children.Add(deleteSelectedButton); Grid.SetRow(tools, 2); left.Children.Add(tools);
            search.Visibility = Visibility.Collapsed;
            var listScroll = new ScrollViewer { Content = entryList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(16, 0, 12, 18) };
            listScroll.Resources["OnharuScrollThumb"] = Brush("#D8D3CE"); listScroll.Resources["OnharuScrollTrack"] = Brushes.Transparent; listScroll.Loaded += delegate { UiRound.SoftenScrollBars(listScroll); };
            Grid.SetRow(listScroll, 3); left.Children.Add(listScroll); bodyGrid.Children.Add(left);
            var divider = new Border { Width = 1, Background = Brush("#E8E4E0"), HorizontalAlignment = HorizontalAlignment.Right }; left.Children.Add(divider);

            var page = new Grid { Background = Brush("#FFFEFD"), Margin = new Thickness(0) }; page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition());
            var navigation = new DockPanel { Margin = new Thickness(68, 34, 48, 0), Height = 32, LastChildFill = false };
            var close = OnharuPopupChrome.ToolCloseButton(this); close.Width = 32; close.Height = 32; close.Margin = new Thickness(7, 0, 0, 0); UiRound.Apply(close, 10); DockPanel.SetDock(close, Dock.Right); navigation.Children.Add(close);
            var add = InkButton("오늘 일기 +", 82); add.Margin = new Thickness(7, 0, 0, 0); add.Click += delegate { Edit(initialDate, entries.FirstOrDefault(x => x.Date.Date == initialDate)); }; DockPanel.SetDock(add, Dock.Right); navigation.Children.Add(add);
            var edit = PaperButton("수정", 50); edit.Click += delegate { if (current != null) Edit(current.Date, current); }; DockPanel.SetDock(edit, Dock.Right); navigation.Children.Add(edit);
            previousButton = PaperButton("← 이전", 62); nextButton = PaperButton("다음 →", 62); previousButton.Margin = new Thickness(0, 0, 7, 0); DockPanel.SetDock(previousButton, Dock.Left); DockPanel.SetDock(nextButton, Dock.Left); navigation.Children.Add(previousButton); navigation.Children.Add(nextButton); Grid.SetRow(navigation, 0); page.Children.Add(navigation);
            var article = new StackPanel { Margin = new Thickness(68, 38, 48, 50), MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Stretch };
            readingDate.Foreground = Brush("#77716D"); readingDate.FontSize = 12; readingDate.Margin = new Thickness(0, 0, 0, 28);
            readingTitle.Foreground = Brush("#171717"); readingTitle.FontSize = 44; readingTitle.FontWeight = FontWeights.Bold; readingTitle.Margin = new Thickness(0, 0, 0, 30); readingTitle.TextWrapping = TextWrapping.Wrap;
            readingLead.Foreground = Brush("#171717"); readingLead.FontSize = 16; readingLead.LineHeight = 29; readingLead.TextWrapping = TextWrapping.Wrap;
            var leadBox = new Border { Background = Brush("#FFF8F3"), BorderBrush = Brush("#E97855"), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(24, 20, 20, 20), Margin = new Thickness(0, 0, 0, 38), Child = readingLead };
            readingBody.TextWrapping = TextWrapping.Wrap; readingBody.FontFamily = new FontFamily("Batang"); readingBody.FontSize = 16; readingBody.LineHeight = 32; readingBody.Foreground = Brush("#3D3936");
            article.Children.Add(readingDate); article.Children.Add(readingTitle); article.Children.Add(leadBox); article.Children.Add(readingBody);
            var contentScroll = new ScrollViewer { Content = article, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; contentScroll.Resources["OnharuScrollThumb"] = Brush("#D8D3CE"); contentScroll.Resources["OnharuScrollTrack"] = Brushes.Transparent; contentScroll.Loaded += delegate { UiRound.SoftenScrollBars(contentScroll); }; Grid.SetRow(contentScroll, 1); page.Children.Add(contentScroll);
            Grid.SetColumn(page, 1); bodyGrid.Children.Add(page); root.Children.Add(bodyGrid);
            viewButton = OnharuPopupChrome.Button("", 36, "#FFFFFF", "#171717"); viewButton.Height = 36; viewButton.BorderBrush = Brush("#E8E4E0");
            viewButton.HorizontalAlignment = HorizontalAlignment.Left; viewButton.VerticalAlignment = VerticalAlignment.Top; viewButton.Margin = new Thickness(282, 24, 0, 0);
            UiRound.Apply(viewButton, 18); SetListToggleIcon(); viewButton.Click += delegate { SetPageMode(!pageMode); }; root.Children.Add(viewButton);
            OnharuPopupChrome.EnableDrag(this, navigation);
            previousButton.Click += delegate { Move(false); }; nextButton.Click += delegate { Move(true); };
            listPanel = left;
            var shell = OnharuPopupChrome.Shell(root); shell.Background = Brushes.White; shell.ClipToBounds = true;
            Action clipShell = delegate { if (shell.ActualWidth > 0 && shell.ActualHeight > 0) shell.Clip = new RectangleGeometry(new Rect(0, 0, shell.ActualWidth, shell.ActualHeight), 18, 18); };
            shell.Loaded += delegate { clipShell(); }; shell.SizeChanged += delegate { clipShell(); }; Content = shell; Loaded += delegate { Reload(initialDate); };
        }

        void Reload(DateTime preferred)
        {
            entries = DiaryStore.Load(); SortEntries(); RefreshList();
            ShowEntry(entries.FirstOrDefault(x => x.Date.Date == preferred.Date) ?? entries.FirstOrDefault());
        }

        void RefreshList()
        {
            entryList.Children.Clear(); var query = search.Text == null ? "" : search.Text.Trim();
            var filtered = FilteredEntries();
            entryCount.Text = entries.Count + " NOTES";
            for (var itemIndex = 0; itemIndex < filtered.Count; itemIndex++)
            {
                var entry = filtered[itemIndex]; var captured = entry; var selected = current != null && current.Date.Date == entry.Date.Date;
                var text = new Grid(); if (selectionMode) text.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); text.ColumnDefinitions.Add(new ColumnDefinition());
                if (selectionMode)
                {
                    var check = new CheckBox { Width = 17, Height = 17, IsChecked = selectedDates.Contains(entry.Date.Date), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 0), Tag = entry.Date.Date };
                    check.Click += delegate(object sender, RoutedEventArgs e) { if (check.IsChecked == true) selectedDates.Add(captured.Date.Date); else selectedDates.Remove(captured.Date.Date); UpdateSelectionControls(); e.Handled = true; };
                    text.Children.Add(check);
                }
                var words = new StackPanel();
                words.Children.Add(new TextBlock { Text = entry.Date.ToString("yyyy. MM. dd"), FontSize = 11, Foreground = Brush("#77716D"), Margin = new Thickness(0, 0, 0, 7) });
                var titleLine = new StackPanel { Orientation = Orientation.Horizontal };
                var dots = new[] { "#F9E4D7", "#DFF3EA", "#E9E5F7", "#F8EFC9" };
                titleLine.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = Brush(dots[itemIndex % dots.Length]), Margin = new Thickness(0, 7, 8, 0), VerticalAlignment = VerticalAlignment.Top });
                titleLine.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Title) ? "제목 없는 일기" : entry.Title, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brush("#171717"), TextTrimming = TextTrimming.CharacterEllipsis });
                words.Children.Add(titleLine);
                words.Children.Add(new TextBlock { Text = Excerpt(string.IsNullOrWhiteSpace(entry.Lead) ? entry.Content : entry.Lead), FontSize = 11, Foreground = Brush("#77716D"), Margin = new Thickness(0, 8, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap });
                Grid.SetColumn(words, selectionMode ? 1 : 0); text.Children.Add(words);
                var button = OnharuPopupChrome.Button("", double.NaN, selected ? "#FAF9F7" : "#FFFFFF", "#171717");
                button.BorderBrush = Brush(selected ? "#DED9D4" : "#FFFFFF"); button.BorderThickness = new Thickness(1);
                button.Height = 92; button.Content = text; button.Margin = new Thickness(0, 0, 0, 7); button.Padding = new Thickness(selectionMode ? 12 : 14, 13, 12, 13);
                button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Top;
                button.Click += delegate
                {
                    if (selectionMode) { if (!selectedDates.Add(captured.Date.Date)) selectedDates.Remove(captured.Date.Date); UpdateSelectionControls(); RefreshList(); }
                    else { ShowEntry(captured); RefreshList(); }
                };
                button.MouseDoubleClick += delegate { if (!selectionMode) Edit(captured.Date, captured); };
                entryList.Children.Add(button);
            }
            if (entryList.Children.Count == 0) entryList.Children.Add(new TextBlock { Text = query.Length == 0 ? "아직 작성한 일기가 없습니다." : "검색 결과가 없습니다.", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(8, 28, 8, 8) });
            UpdateSelectionControls();
        }

        List<DiaryEntry> FilteredEntries()
        {
            var query = search.Text == null ? "" : search.Text.Trim();
            return entries.Where(x => query.Length == 0 || (x.Title ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                (x.Content ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || x.Date.ToString("yyyy-MM-dd").Contains(query)).ToList();
        }

        void SetSelectionMode(bool enabled)
        {
            selectionMode = enabled;
            if (!enabled) selectedDates.Clear();
            selectionButton.Content = enabled ? "취소" : "선택";
            selectAllButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            deleteSelectedButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            RefreshList();
        }

        void UpdateSelectionControls()
        {
            if (!selectionMode) return;
            var visible = FilteredEntries();
            var allSelected = visible.Count > 0 && visible.All(x => selectedDates.Contains(x.Date.Date));
            selectAllButton.Content = allSelected ? "해제" : "전체";
            selectAllButton.IsEnabled = visible.Count > 0;
            deleteSelectedButton.IsEnabled = selectedDates.Count > 0;
            deleteSelectedButton.ToolTip = selectedDates.Count + "개 일기 삭제";
        }

        void ToggleSelectAll()
        {
            var visible = FilteredEntries();
            var allSelected = visible.Count > 0 && visible.All(x => selectedDates.Contains(x.Date.Date));
            foreach (var entry in visible)
            {
                if (allSelected) selectedDates.Remove(entry.Date.Date);
                else selectedDates.Add(entry.Date.Date);
            }
            RefreshList();
        }

        void DeleteSelected()
        {
            if (selectedDates.Count == 0) return;
            var dates = selectedDates.ToList();
            var confirm = new LocalDeleteConfirmWindow("선택한 " + dates.Count + "개 일기를 삭제할까요?", "삭제 전 일기 백업은 오늘 날짜 파일로 유지됩니다.")
            { Owner = this };
            if (confirm.ShowDialog() != true) return;
            DiaryStore.Delete(dates);
            selectedDates.Clear();
            selectionMode = false;
            selectionButton.Content = "선택";
            selectAllButton.Visibility = Visibility.Collapsed;
            deleteSelectedButton.Visibility = Visibility.Collapsed;
            var preferred = current != null && !dates.Contains(current.Date.Date) ? current.Date : initialDate;
            Reload(preferred);
            if (Changed != null) Changed();
        }

        void ShowEntry(DiaryEntry entry)
        {
            current = entry;
            readingDate.Text = entry == null ? "" : entry.Date.ToString("yyyy년 M월 d일") + "  ·  " + (string.IsNullOrWhiteSpace(entry.Mood) ? "기록" : entry.Mood);
            readingTitle.Text = entry == null ? "일기를 써보세요" : string.IsNullOrWhiteSpace(entry.Title) ? "제목 없는 일기" : entry.Title;
            var paragraphs = Paragraphs(entry == null ? "" : entry.Content);
            readingLead.Text = entry == null ? "오늘 일기 + 버튼을 눌러 첫 기록을 남겨보세요." : !string.IsNullOrWhiteSpace(entry.Lead) ? entry.Lead : paragraphs.FirstOrDefault() ?? "조용히 남겨 둔 하루의 기록입니다.";
            readingBody.Text = entry == null ? "" : !string.IsNullOrWhiteSpace(entry.Lead) ? entry.Content : string.Join(Environment.NewLine + Environment.NewLine, paragraphs.Skip(1));
            UpdateNavigationButtons();
        }

        void UpdateNavigationButtons()
        {
            var index = current == null ? -1 : entries.FindIndex(x => x.Date.Date == current.Date.Date);
            StyleNavigation(previousButton, index > 0); StyleNavigation(nextButton, index >= 0 && index < entries.Count - 1);
        }

        static void StyleNavigation(Button button, bool enabled)
        {
            button.IsEnabled = enabled; button.Background = Brushes.White;
            button.Foreground = Brush(enabled ? "#171717" : "#B9B5B1"); button.BorderBrush = Brush(enabled ? "#E8E4E0" : "#F0EDEA"); button.Cursor = enabled ? Cursors.Hand : Cursors.Arrow;
        }

        void SortEntries()
        {
            entries = oldestFirst ? entries.OrderBy(x => x.Date).ToList() : entries.OrderByDescending(x => x.Date).ToList();
        }

        FrameworkElement listPanel;

        void SetSortIcon()
        {
            sortButton.Content = oldestFirst ? "오래된순 ↑" : "최신순 ↓";
            sortButton.ToolTip = oldestFirst ? "오래된순 · 최신순으로 변경" : "최신순 · 오래된순으로 변경";
        }

        void SetListToggleIcon()
        {
            var data = Geometry.Parse(pageMode ? "M6,4 L11,9 L6,14" : "M11,4 L6,9 L11,14");
            viewButton.Content = new System.Windows.Shapes.Path { Data = data, Stroke = Brush("#2B2D42"), StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 18, Height = 18, Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            viewButton.ToolTip = pageMode ? "일기 목록 펼치기" : "일기 목록 숨기기";
        }

        void SetPageMode(bool singlePage)
        {
            pageMode = singlePage; bodyGrid.ColumnDefinitions[0].Width = pageMode ? new GridLength(0) : new GridLength(300);
            if (listPanel != null) listPanel.Visibility = pageMode ? Visibility.Collapsed : Visibility.Visible;
            viewButton.Margin = pageMode ? new Thickness(12, 24, 0, 0) : new Thickness(282, 24, 0, 0);
            SetListToggleIcon();
        }

        void Move(bool forward)
        {
            if (entries.Count == 0) return;
            var index = current == null ? 0 : entries.FindIndex(x => x.Date.Date == current.Date.Date);
            index = Math.Max(0, Math.Min(entries.Count - 1, index + (forward ? 1 : -1)));
            ShowEntry(entries[index]); RefreshList();
        }

        void Edit(DateTime date, DiaryEntry entry)
        {
            var window = new DiaryEditorWindow(date, entry) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (window.ShowDialog() == true && window.Result != null) { DiaryStore.Upsert(window.Result, entry == null ? (DateTime?)null : entry.Date); Reload(window.Result.Date); if (Changed != null) Changed(); }
        }

        static Button PaperButton(string text, double width)
        {
            var button = OnharuPopupChrome.Button(text, width, "#FFFFFF", "#171717");
            button.Height = 32; button.BorderBrush = Brush("#E8E4E0"); button.FontSize = 11; button.FontWeight = FontWeights.SemiBold;
            return button;
        }

        static Button InkButton(string text, double width)
        {
            var button = OnharuPopupChrome.Button(text, width, "#171717", "#FFFFFF");
            button.Height = 32; button.BorderBrush = Brush("#171717"); button.FontSize = 11; button.FontWeight = FontWeights.Bold;
            return button;
        }

        static List<string> Paragraphs(string value)
        {
            return (value ?? "").Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        }

        static string Excerpt(string value)
        {
            var text = string.Join(" ", Paragraphs(value));
            return text.Length <= 46 ? text : text.Substring(0, 46) + "…";
        }

        static FrameworkElement DiaryTitle()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new Border { Width = 34, Height = 34, Margin = new Thickness(0, 0, 10, 0), Background = Brushes.White,
                BorderBrush = Brush("#D5D8DE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Child = new TextBlock { Text = "✎", FontSize = 17, FontFamily = new FontFamily("Segoe UI Symbol"), FontWeight = FontWeights.Bold,
                    Foreground = Brush("#2B2D42"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            row.Children.Add(new TextBlock { Text = "나의 일기장", FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = Brush("#2B2D42"), VerticalAlignment = VerticalAlignment.Center });
            return row;
        }

        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
