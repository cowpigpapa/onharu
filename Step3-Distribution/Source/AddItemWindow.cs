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
    public class AddItemWindow : Window
    {
        readonly TextBox title = new TextBox { Margin = new Thickness(0, 6, 0, 4), Height = 46,
            FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) };
        readonly TextBlock validationMessage = new TextBlock { Text = "제목을 입력해 주세요.", Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            FontSize = 12, FontWeight = FontWeights.SemiBold, Height = 18, Margin = new Thickness(4, 0, 0, 6), Visibility = Visibility.Collapsed };
        DateTime selectedDate;
        DateTime endDateInclusive;
        readonly RadioButton allDay = new RadioButton { Content = "하루 종일", GroupName = "TimeMode", IsChecked = true, Margin = new Thickness(0, 0, 18, 0) };
        readonly RadioButton morning = new RadioButton { Content = "오전", GroupName = "TimeMode", Margin = new Thickness(0, 0, 18, 0) };
        readonly RadioButton afternoon = new RadioButton { Content = "오후", GroupName = "TimeMode" };
        readonly UniformGrid hourGrid = new UniformGrid { Columns = 6, Margin = new Thickness(0, 8, 0, 12), IsEnabled = false };
        readonly UniformGrid minuteGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 5, 0, 10), IsEnabled = false };
        readonly CheckBox multiDay = new CheckBox { Content = "여러 날 일정", Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly Button endDateButton = new Button { Height = 32, Width = 118, IsEnabled = false, Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
        readonly RadioButton noRollover = new RadioButton { Content = "이월 안 함", GroupName = "Rollover", IsChecked = true, Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
        readonly RadioButton nextDayRollover = new RadioButton { Content = "다음 날", Tag = "next_day", GroupName = "Rollover", Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
        readonly RadioButton nextWeekRollover = new RadioButton { Content = "다음 주 같은 요일", Tag = "next_week", GroupName = "Rollover", Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
        readonly RadioButton nextWeekdayRollover = new RadioButton { Content = "다음 평일", Tag = "next_weekday", GroupName = "Rollover", FontSize = 12 };
        readonly WrapPanel rolloverOptions = new WrapPanel { Margin = new Thickness(0, 5, 0, 8), IsEnabled = false };
        readonly StackPanel categories = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };
        readonly List<RadioButton> categoryOptions = new List<RadioButton>();
        readonly WrapPanel reminderOptions = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
        readonly CheckBox important = new CheckBox { Content = "★ 중요 일정", Foreground = new SolidColorBrush(Color.FromRgb(242, 13, 122)), VerticalAlignment = VerticalAlignment.Center };
        readonly WrapPanel recurrenceOptions = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
        readonly Border recurrenceAdvancedCard = new Border { Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 8, 10, 4), Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
        readonly StackPanel recurrenceAdvanced = new StackPanel();
        readonly RadioButton dailyEvery = new RadioButton { Content = "매일", GroupName = "DailyMode", IsChecked = true, Margin = new Thickness(0, 0, 18, 4) };
        readonly RadioButton dailyWeekdays = new RadioButton { Content = "평일만 · 월~금", GroupName = "DailyMode", Margin = new Thickness(0, 0, 0, 4) };
        readonly List<CheckBox> weeklyDays = new List<CheckBox>();
        readonly RadioButton monthlyDate = new RadioButton { GroupName = "MonthlyMode", IsChecked = true, Margin = new Thickness(0, 0, 14, 4) };
        readonly RadioButton monthlyNth = new RadioButton { GroupName = "MonthlyMode", Margin = new Thickness(0, 0, 14, 4) };
        readonly RadioButton monthlyLast = new RadioButton { Content = "매월 마지막 날", GroupName = "MonthlyMode", Margin = new Thickness(0, 0, 0, 4) };
        readonly Button recurrenceUntilButton = new Button { Height = 34, IsEnabled = false, Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
        DateTime recurrenceUntilDate;
        readonly List<GoogleCalendarSetting> googleSources;
        readonly TextBox notes = new TextBox { Margin = new Thickness(0, 4, 0, 14), Height = 72,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) };
        readonly PlannerItem editingItem;
        public PlannerItem Result;
        public bool DeleteRequested;
        public bool ApplyToSeries;
        readonly CheckBox editSingleOccurrence = new CheckBox { Content = "이번 일정만 변경", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        bool editingSeries;

        public AddItemWindow(DateTime selected, PlannerItem existing = null, List<GoogleCalendarSetting> sources = null, bool googleConnected = true)
        {
            editingItem = existing;
            googleSources = sources ?? new List<GoogleCalendarSetting>();
            selectedDate = selected.Date;
            endDateInclusive = existing != null && existing.AllDay && existing.End > existing.Start ? existing.End.AddTicks(-1).Date : selectedDate;
            editingSeries = existing != null && (!string.IsNullOrWhiteSpace(existing.SeriesId) || !string.IsNullOrWhiteSpace(existing.GoogleRecurringEventId) || !string.IsNullOrWhiteSpace(existing.RecurrenceFrequency));
            recurrenceUntilDate = selectedDate.AddYears(1);
            Title = existing == null ? "새 일정" : "일정 수정";
            Width = 460; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.GetPosition(this).Y < 70 && !HasButtonParent(e.OriginalSource as DependencyObject) && Mouse.LeftButton == MouseButtonState.Pressed)
                    DragMove();
            };
            for (var h = 1; h <= 12; h++)
                hourGrid.Children.Add(new RadioButton { Content = h + "시", Tag = h, GroupName = "Hour", IsChecked = h == 9, Margin = new Thickness(2, 4, 2, 4) });
            foreach (var minute in new[] { 0, 15, 30, 45 })
                minuteGrid.Children.Add(new RadioButton { Content = minute + "분", Tag = minute, GroupName = "Minute",
                    IsChecked = minute == 0, Margin = new Thickness(2, 4, 2, 4) });
            allDay.Checked += delegate { hourGrid.IsEnabled = false; minuteGrid.IsEnabled = false; rolloverOptions.IsEnabled = false; multiDay.IsEnabled = true; endDateButton.IsEnabled = multiDay.IsChecked == true; };
            morning.Checked += delegate { hourGrid.IsEnabled = true; minuteGrid.IsEnabled = true; rolloverOptions.IsEnabled = true; multiDay.IsEnabled = false; endDateButton.IsEnabled = false; };
            afternoon.Checked += delegate { hourGrid.IsEnabled = true; minuteGrid.IsEnabled = true; rolloverOptions.IsEnabled = true; multiDay.IsEnabled = false; endDateButton.IsEnabled = false; };
            categories.Children.Add(new TextBlock { Text = "온하루 · 로컬 전용", Foreground = Brush("#64748B"), FontSize = 11, Margin = new Thickness(0, 0, 0, 5) });
            var localChoices = new WrapPanel();
            AddCategoryChoice(localChoices, "업무일정", "local:business", true, true);
            AddCategoryChoice(localChoices, "개인일정", "local:personal", false, true); categories.Children.Add(localChoices);
            if (googleSources.Count > 0)
            {
                categories.Children.Add(new TextBlock { Text = "Google · 동기화", Foreground = Brush("#64748B"), FontSize = 11, Margin = new Thickness(0, 9, 0, 5) });
                var googleChoices = new WrapPanel();
                foreach (var source in OrderedSources(googleSources))
                    AddCategoryChoice(googleChoices, (source.Primary ? "내 캘린더 · " : "") + source.Name,
                        "google:" + source.Id, false, source.Editable);
                categories.Children.Add(googleChoices);
            }

            StyleInput(title); StyleInput(notes);
            var panel = new StackPanel { Margin = new Thickness(26, 12, 18, 20) };
            var header = new DockPanel { Margin = new Thickness(26, 14, 12, 12) };
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"),
                Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            Round(close, 10);
            close.Click += delegate { DialogResult = false; };
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var headerTitle = new StackPanel { Orientation = Orientation.Horizontal };
            headerTitle.Children.Add(new TextBlock { Text = existing == null ? "✦  새 일정" : "✎  일정 수정", FontSize = 22, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center });
            if (existing != null)
                headerTitle.Children.Add(new Border { Background = string.IsNullOrWhiteSpace(existing.GoogleCalendarId) ? Brush("#EEF2FF") : Brush("#F0FDF4"),
                    CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(12, 2, 0, 0),
                    Child = new TextBlock { Text = string.IsNullOrWhiteSpace(existing.GoogleCalendarId) ? "온하루 등록" : "Google Calendar",
                        Foreground = string.IsNullOrWhiteSpace(existing.GoogleCalendarId) ? Brush("#4338CA") : Brush("#15803D"), FontSize = 11, FontWeight = FontWeights.SemiBold } });
            header.Children.Add(headerTitle);
            var dateCard = new Border { Background = Brush("#EFF6FF"), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 18) };
            Popup datePopup = null;
            System.Windows.Controls.Calendar inlineCalendar = null;
            TextBlock editableDateText = null;
            Button changeDateButton = null;
            var pendingDate = selectedDate;
            if (existing == null)
                dateCard.Child = new TextBlock { Text = "날짜  ·  " + selectedDate.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")),
                    Foreground = Brush("#1D4ED8"), FontWeight = FontWeights.SemiBold, FontSize = 14 };
            else
            {
                var dateRow = new Grid(); dateRow.ColumnDefinitions.Add(new ColumnDefinition());
                dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
                editableDateText = new TextBlock { Text = selectedDate.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")),
                    Foreground = Brush("#1D4ED8"), FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
                dateRow.Children.Add(editableDateText);
                changeDateButton = new Button { Content = "📅 날짜 변경", Height = 34, Background = Brushes.White,
                    Foreground = Brush("#2563EB"), BorderBrush = Brush("#BFDBFE"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                Round(changeDateButton, 9);
                inlineCalendar = new System.Windows.Controls.Calendar { SelectedDate = selectedDate, DisplayDate = selectedDate,
                    SelectionMode = CalendarSelectionMode.SingleDate, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center,
                    LayoutTransform = new ScaleTransform(1.20, 1.20) };
                StyleCalendar(inlineCalendar);
                datePopup = new Popup { PlacementTarget = changeDateButton, Placement = PlacementMode.Bottom,
                    AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.Fade,
                    VerticalOffset = 6 };
                datePopup.Child = inlineCalendar;
                inlineCalendar.SelectedDatesChanged += delegate
                { if (inlineCalendar.SelectedDate.HasValue) pendingDate = inlineCalendar.SelectedDate.Value.Date; };
                inlineCalendar.PreviewMouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
                {
                    if (HasDayButtonParent(e.OriginalSource as DependencyObject))
                    { selectedDate = pendingDate; NormalizeEndDate(); editableDateText.Text = FormatDate(selectedDate); UpdateRecurrenceOptions(); datePopup.IsOpen = false; e.Handled = true; }
                };
                changeDateButton.Click += delegate
                {
                    if (!datePopup.IsOpen)
                    {
                        inlineCalendar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        datePopup.HorizontalOffset = changeDateButton.ActualWidth - inlineCalendar.DesiredSize.Width + 120;
                    }
                    datePopup.IsOpen = !datePopup.IsOpen;
                };
                datePopup.Closed += delegate
                { selectedDate = pendingDate; NormalizeEndDate(); editableDateText.Text = FormatDate(selectedDate); UpdateRecurrenceOptions(); };
                Grid.SetColumn(changeDateButton, 1); dateRow.Children.Add(changeDateButton); dateCard.Child = dateRow;
            }
            panel.Children.Add(dateCard);
            var titleLabelRow = new Grid(); titleLabelRow.ColumnDefinitions.Add(new ColumnDefinition()); titleLabelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleLabelRow.Children.Add(Label("제목")); Grid.SetColumn(important, 1); titleLabelRow.Children.Add(important);
            panel.Children.Add(titleLabelRow); panel.Children.Add(title); panel.Children.Add(validationMessage);
            var timeCardContent = new StackPanel();
            timeCardContent.Children.Add(new TextBlock { Text = "시간을 지정하면 완료 체크 항목으로 등록됩니다.", FontSize = 11,
                Foreground = Brush("#64748B"), Margin = new Thickness(0, 0, 0, 8) });
            var timeModes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 2) };
            timeModes.Children.Add(allDay); timeModes.Children.Add(morning); timeModes.Children.Add(afternoon);
            timeCardContent.Children.Add(timeModes);
            var durationRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 2) };
            endDateButton.Content = endDateInclusive.ToString("yyyy.MM.dd"); Round(endDateButton, 9);
            multiDay.IsEnabled = true; multiDay.IsChecked = endDateInclusive > selectedDate; endDateButton.IsEnabled = multiDay.IsChecked == true;
            multiDay.Checked += delegate { if (endDateInclusive <= selectedDate) endDateInclusive = selectedDate.AddDays(1); UpdateEndDateButton(); endDateButton.IsEnabled = true; UpdateRecurrenceAvailability(); };
            multiDay.Unchecked += delegate { endDateInclusive = selectedDate; UpdateEndDateButton(); endDateButton.IsEnabled = false; UpdateRecurrenceAvailability(); };
            durationRow.Children.Add(multiDay); durationRow.Children.Add(new TextBlock { Text = "종료 날짜", FontSize = 11, Foreground = Brush("#64748B"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) }); durationRow.Children.Add(endDateButton);
            timeCardContent.Children.Add(durationRow); timeCardContent.Children.Add(hourGrid);
            timeCardContent.Children.Add(new TextBlock { Text = "분", FontSize = 11, Foreground = Brush("#64748B") });
            timeCardContent.Children.Add(minuteGrid);
            rolloverOptions.Children.Add(noRollover); rolloverOptions.Children.Add(nextDayRollover);
            rolloverOptions.Children.Add(nextWeekRollover); rolloverOptions.Children.Add(nextWeekdayRollover);
            timeCardContent.Children.Add(new TextBlock { Text = "자동 이월", FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(0, 2, 0, 0) });
            timeCardContent.Children.Add(rolloverOptions);
            panel.Children.Add(new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12, 14, 6),
                Margin = new Thickness(0, 2, 0, 12), Child = timeCardContent });
            var endCalendar = new System.Windows.Controls.Calendar { SelectedDate = endDateInclusive, DisplayDate = endDateInclusive,
                SelectionMode = CalendarSelectionMode.SingleDate, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center,
                LayoutTransform = new ScaleTransform(1.20, 1.20) };
            StyleCalendar(endCalendar);
            var endPopup = new Popup { PlacementTarget = endDateButton, Placement = PlacementMode.Bottom,
                AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.Fade, VerticalOffset = 6, Child = endCalendar };
            endCalendar.SelectedDatesChanged += delegate
            {
                if (!endCalendar.SelectedDate.HasValue) return;
                endDateInclusive = endCalendar.SelectedDate.Value.Date < selectedDate ? selectedDate : endCalendar.SelectedDate.Value.Date;
                UpdateEndDateButton();
            };
            endCalendar.PreviewMouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
            { if (HasDayButtonParent(e.OriginalSource as DependencyObject)) { endPopup.IsOpen = false; e.Handled = true; } };
            endDateButton.Click += delegate
            {
                if (!endPopup.IsOpen)
                {
                    endCalendar.DisplayDateStart = selectedDate; endCalendar.DisplayDate = endDateInclusive; endCalendar.SelectedDate = endDateInclusive;
                    endCalendar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    endPopup.HorizontalOffset = endDateButton.ActualWidth - endCalendar.DesiredSize.Width + 120;
                }
                endPopup.IsOpen = !endPopup.IsOpen;
            };
            var categoryContent = new StackPanel();
            categoryContent.Children.Add(categories);
            panel.Children.Add(new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 7, 14, 2),
                Margin = new Thickness(0, 0, 0, 12), Child = categoryContent });
            panel.Children.Add(new TextBlock { Text = "알림", FontWeight = FontWeights.SemiBold, FontSize = 13 });
            foreach (var option in new[] { new { Name = "없음", Value = -1 }, new { Name = "정시", Value = 0 }, new { Name = "10분 전", Value = 10 }, new { Name = "30분 전", Value = 30 }, new { Name = "하루 전", Value = 1440 } })
                reminderOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Value, GroupName = "Reminder",
                    IsChecked = option.Value == -1, Margin = new Thickness(0, 0, 16, 5) });
            panel.Children.Add(reminderOptions);
            var recurrenceLine = new StackPanel { Margin = new Thickness(0, 1, 0, 10) };
            var recurrenceHeader = new Grid(); recurrenceHeader.ColumnDefinitions.Add(new ColumnDefinition()); recurrenceHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            recurrenceHeader.Children.Add(new TextBlock { Text = "반복", FontWeight = FontWeights.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            var recurrenceRight = new StackPanel { Orientation = Orientation.Horizontal };
            recurrenceRight.Children.Add(new TextBlock { Text = "종료일", Foreground = Brush("#64748B"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            recurrenceUntilButton.Width = 104; recurrenceUntilButton.Height = 30; recurrenceUntilButton.Content = recurrenceUntilDate.ToString("yyyy.MM.dd"); Round(recurrenceUntilButton, 9); recurrenceRight.Children.Add(recurrenceUntilButton);
            Grid.SetColumn(recurrenceRight, 1); recurrenceHeader.Children.Add(recurrenceRight); recurrenceLine.Children.Add(recurrenceHeader);
            foreach (var option in new[] { new { Name = "없음", Value = "" }, new { Name = "매일", Value = "daily" }, new { Name = "매주", Value = "weekly" }, new { Name = "매월", Value = "monthly" }, new { Name = "매년", Value = "yearly" } })
            {
                var radio = new RadioButton { Content = option.Name, Tag = option.Value, GroupName = "Recurrence", IsChecked = option.Value == "", Margin = new Thickness(0, 0, 9, 5), FontSize = 12 };
                radio.Checked += delegate { recurrenceUntilButton.IsEnabled = !string.IsNullOrWhiteSpace(radio.Tag.ToString()); UpdateRecurrenceOptions(); }; recurrenceOptions.Children.Add(radio);
            }
            if (editingSeries) { editSingleOccurrence.Margin = new Thickness(8, 0, 0, 5); recurrenceOptions.Children.Add(editSingleOccurrence); }
            recurrenceLine.Children.Add(recurrenceOptions); recurrenceAdvancedCard.Child = recurrenceAdvanced; recurrenceLine.Children.Add(recurrenceAdvancedCard); panel.Children.Add(recurrenceLine);
            UpdateRecurrenceAvailability();
            var recurrenceCalendar = new System.Windows.Controls.Calendar { SelectedDate = recurrenceUntilDate, DisplayDate = recurrenceUntilDate,
                SelectionMode = CalendarSelectionMode.SingleDate, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center,
                LayoutTransform = new ScaleTransform(1.20, 1.20) };
            StyleCalendar(recurrenceCalendar);
            var recurrencePopup = new Popup { PlacementTarget = recurrenceUntilButton, Placement = PlacementMode.Bottom,
                AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.Fade, VerticalOffset = 6, Child = recurrenceCalendar };
            recurrenceCalendar.SelectedDatesChanged += delegate
            {
                if (!recurrenceCalendar.SelectedDate.HasValue) return;
                recurrenceUntilDate = recurrenceCalendar.SelectedDate.Value.Date;
                recurrenceUntilButton.Content = recurrenceUntilDate.ToString("yyyy.MM.dd");
            };
            recurrenceCalendar.PreviewMouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
            { if (HasDayButtonParent(e.OriginalSource as DependencyObject)) { recurrencePopup.IsOpen = false; e.Handled = true; } };
            recurrenceUntilButton.Click += delegate
            {
                if (!recurrencePopup.IsOpen)
                {
                    recurrenceCalendar.DisplayDate = recurrenceUntilDate; recurrenceCalendar.SelectedDate = recurrenceUntilDate;
                    recurrenceCalendar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    recurrencePopup.HorizontalOffset = recurrenceUntilButton.ActualWidth - recurrenceCalendar.DesiredSize.Width + 120;
                }
                recurrencePopup.IsOpen = !recurrencePopup.IsOpen;
            };
            panel.Children.Add(Label("메모")); panel.Children.Add(notes);
            if (existing == null && !googleConnected)
                panel.Children.Add(new TextBlock { Text = "Google 로그아웃 상태입니다. 이 일정은 이 PC에만 저장됩니다.",
                    Foreground = Brush("#DC2626"), FontSize = 12, FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center, Margin = new Thickness(0, -5, 0, 9) });
            var saveGradient = new LinearGradientBrush(); saveGradient.StartPoint = new Point(0, .5); saveGradient.EndPoint = new Point(1, .5);
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3977E8"), 0));
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C5CE5"), 1));
            var save = new Button { Content = existing == null ? "✓  일정 저장" : "✓  수정 저장", Height = 44, Background = saveGradient,
                Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, FontSize = 14 };
            Round(save, 13);
            save.Click += Save;
            if (existing == null) panel.Children.Add(save);
            else
            {
                var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) }); footer.ColumnDefinitions.Add(new ColumnDefinition());
                var delete = new Button { Content = "삭제", Height = 44, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"),
                    BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) };
                Round(delete, 13); delete.Click += delegate { DeleteRequested = true; DialogResult = true; }; footer.Children.Add(delete);
                Grid.SetColumn(save, 1); footer.Children.Add(save); panel.Children.Add(footer);
            }
            var contentScroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Min(930, Math.Max(340, SystemParameters.WorkArea.Height - 104)) };
            var popupLayout = new Grid(); popupLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); popupLayout.RowDefinitions.Add(new RowDefinition());
            popupLayout.Children.Add(header); Grid.SetRow(contentScroll, 1); popupLayout.Children.Add(contentScroll);
            Loaded += delegate
            {
                contentScroll.MaxHeight = Math.Min(930, Math.Max(340, Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea.Height - 104));
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { UiRound.SoftenScrollBars(contentScroll); }));
            };
            var shell = new Border { Background = Brush("#FFF8FAFC"), CornerRadius = new CornerRadius(18),
                BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Child = popupLayout };
            Content = shell;
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter && !notes.IsKeyboardFocusWithin)
                { Save(sender, e); e.Handled = true; }
            };
            if (existing != null) LoadExisting(existing);
            Loaded += delegate { title.Focus(); };
        }

        void UpdateRecurrenceOptions()
        {
            var selected = recurrenceOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true);
            var frequency = selected == null ? "" : selected.Tag.ToString();
            recurrenceAdvanced.Children.Clear(); recurrenceAdvancedCard.Visibility = string.IsNullOrWhiteSpace(frequency) ? Visibility.Collapsed : Visibility.Visible;
            if (frequency == "daily")
            {
                Detach(dailyEvery); Detach(dailyWeekdays);
                var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(dailyEvery); row.Children.Add(dailyWeekdays); recurrenceAdvanced.Children.Add(row);
            }
            else if (frequency == "weekly")
            {
                if (weeklyDays.Count == 0)
                    foreach (var day in new[] { Tuple.Create("월", "MO", DayOfWeek.Monday), Tuple.Create("화", "TU", DayOfWeek.Tuesday), Tuple.Create("수", "WE", DayOfWeek.Wednesday), Tuple.Create("목", "TH", DayOfWeek.Thursday), Tuple.Create("금", "FR", DayOfWeek.Friday), Tuple.Create("토", "SA", DayOfWeek.Saturday), Tuple.Create("일", "SU", DayOfWeek.Sunday) })
                        weeklyDays.Add(new CheckBox { Content = day.Item1, Tag = day.Item2, IsChecked = day.Item3 == selectedDate.DayOfWeek, Margin = new Thickness(0, 0, 13, 4) });
                var row = new WrapPanel(); foreach (var day in weeklyDays) { Detach(day); row.Children.Add(day); } recurrenceAdvanced.Children.Add(row);
            }
            else if (frequency == "monthly")
            {
                monthlyDate.Content = "매월 " + selectedDate.Day + "일"; monthlyNth.Content = "매월 " + RecurrenceService.MonthlyPositionText(selectedDate);
                Detach(monthlyDate); Detach(monthlyNth); Detach(monthlyLast);
                var row = new WrapPanel(); row.Children.Add(monthlyDate); row.Children.Add(monthlyNth); row.Children.Add(monthlyLast); recurrenceAdvanced.Children.Add(row);
            }
            else if (frequency == "yearly")
                recurrenceAdvanced.Children.Add(new TextBlock { Text = "매년 " + selectedDate.Month + "월 " + selectedDate.Day + "일", Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(2, 0, 0, 4) });
        }

        void NormalizeEndDate()
        {
            if (endDateInclusive < selectedDate) endDateInclusive = selectedDate;
            if (multiDay.IsChecked != true) endDateInclusive = selectedDate;
            UpdateEndDateButton();
        }

        void UpdateEndDateButton()
        {
            endDateButton.Content = endDateInclusive.ToString("yyyy.MM.dd");
        }

        void UpdateRecurrenceAvailability()
        {
            var enabled = multiDay.IsChecked != true;
            recurrenceOptions.IsEnabled = enabled;
            if (enabled) return;
            var none = recurrenceOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.Tag != null && string.IsNullOrWhiteSpace(x.Tag.ToString()));
            if (none != null) none.IsChecked = true;
            recurrenceUntilButton.IsEnabled = false;
            recurrenceAdvancedCard.Visibility = Visibility.Collapsed;
        }

        static void Detach(UIElement element)
        {
            var parent = VisualTreeHelper.GetParent(element) as Panel;
            if (parent != null) parent.Children.Remove(element);
        }




        void Save(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(title.Text))
            { ShowValidation(); return; }
            var start = selectedDate;
            if (allDay.IsChecked != true)
            {
                var hour = (int)hourGrid.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                if (afternoon.IsChecked == true && hour < 12) hour += 12;
                if (morning.IsChecked == true && hour == 12) hour = 0;
                var minute = (int)minuteGrid.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                start = start.AddHours(hour).AddMinutes(minute);
            }
            var selectedOption = categoryOptions.First(x => x.IsChecked == true);
            var target = selectedOption.Tag.ToString();
            var selectedSource = target.StartsWith("google:") ? googleSources.FirstOrDefault(x => "google:" + x.Id == target) : null;
            var selectedCategory = target == "local:business" ? "업무일정" : "개인일정";
            var recurrenceFrequency = multiDay.IsChecked == true ? "" : recurrenceOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
            var recurrenceMode = recurrenceFrequency == "daily" ? (dailyWeekdays.IsChecked == true ? "weekdays" : "daily") :
                recurrenceFrequency == "monthly" ? (monthlyLast.IsChecked == true ? "monthly_last" : monthlyNth.IsChecked == true ? "monthly_nth" : "monthly_date") : recurrenceFrequency;
            var recurrenceDays = recurrenceFrequency == "weekly" ? string.Join(",", weeklyDays.Where(x => x.IsChecked == true).Select(x => x.Tag.ToString())) :
                recurrenceMode == "monthly_nth" ? RecurrenceService.MonthlyNthCode(selectedDate) : null;
            if (recurrenceFrequency == "weekly" && string.IsNullOrWhiteSpace(recurrenceDays)) recurrenceDays = RecurrenceService.DayCode(selectedDate.DayOfWeek);
            Result = new PlannerItem { Id = editingItem == null ? Guid.NewGuid().ToString() : editingItem.Id, Title = title.Text.Trim(), Start = start,
                End = allDay.IsChecked == true ? (multiDay.IsChecked == true ? endDateInclusive.AddDays(1) : start.AddDays(1)) : start.AddMinutes(30),
                AllDay = allDay.IsChecked == true, IsTodo = allDay.IsChecked != true,
                Category = selectedCategory, Notes = notes.Text.Trim(),
                GoogleEventId = editingItem == null ? null : editingItem.GoogleEventId,
                OnharuManaged = editingItem != null && editingItem.OnharuManaged,
                GoogleTaskEvent = editingItem != null && editingItem.GoogleTaskEvent,
                CreatedInOnharu = editingItem == null || editingItem.CreatedInOnharu,
                Completed = editingItem != null && editingItem.Completed,
                GoogleCalendarId = editingItem == null ? null : editingItem.GoogleCalendarId,
                GoogleCalendarName = editingItem == null ? null : editingItem.GoogleCalendarName,
                GoogleCalendarColor = editingItem == null ? null : editingItem.GoogleCalendarColor,
                GoogleReadOnly = editingItem != null && editingItem.GoogleReadOnly,
                RolloverMode = allDay.IsChecked == true ? null : SelectedRolloverMode(),
                AutoRollover = allDay.IsChecked != true && noRollover.IsChecked != true,
                Important = important.IsChecked == true,
                ReminderMinutes = (int)reminderOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag,
                ReminderConfigured = true,
                RecurrenceFrequency = recurrenceFrequency, RecurrenceMode = recurrenceMode, RecurrenceDays = recurrenceDays,
                RecurrenceUntil = recurrenceUntilDate,
                SeriesId = editingItem == null ? null : editingItem.SeriesId,
                GoogleRecurringEventId = editingItem == null ? null : editingItem.GoogleRecurringEventId,
                PendingGoogleSync = editingItem != null && editingItem.PendingGoogleSync };
            if (selectedSource != null)
            {
                Result.GoogleCalendarId = selectedSource.Id; Result.GoogleCalendarName = selectedSource.Name;
                Result.GoogleCalendarColor = selectedSource.Color; Result.GoogleReadOnly = !selectedSource.Editable;
                if (editingItem != null && editingItem.GoogleCalendarId != selectedSource.Id) Result.GoogleEventId = null;
            }
            else if (editingItem == null || !target.StartsWith("google:"))
            {
                Result.GoogleCalendarId = null; Result.GoogleCalendarName = null; Result.GoogleCalendarColor = null;
                Result.GoogleReadOnly = false; Result.GoogleEventId = null; Result.OnharuManaged = false;
            }
            ApplyToSeries = editingSeries && editSingleOccurrence.IsChecked != true;
            DialogResult = true;
        }

        void LoadExisting(PlannerItem item)
        {
            title.Text = item.Title; notes.Text = item.Notes ?? "";
            important.IsChecked = item.Important;
            recurrenceUntilDate = item.RecurrenceUntil.Year >= 1900 ? item.RecurrenceUntil : item.Start.Date.AddYears(1);
            recurrenceUntilButton.Content = recurrenceUntilDate.ToString("yyyy.MM.dd");
            foreach (var radio in recurrenceOptions.Children.OfType<RadioButton>()) radio.IsChecked = radio.Tag.ToString() == (item.RecurrenceFrequency ?? "");
            dailyWeekdays.IsChecked = item.RecurrenceMode == "weekdays"; dailyEvery.IsChecked = item.RecurrenceMode != "weekdays";
            if (item.RecurrenceFrequency == "weekly")
            {
                UpdateRecurrenceOptions(); var selectedDays = (item.RecurrenceDays ?? RecurrenceService.DayCode(item.Start.DayOfWeek)).Split(',');
                foreach (var day in weeklyDays) day.IsChecked = selectedDays.Contains(day.Tag.ToString());
            }
            monthlyLast.IsChecked = item.RecurrenceMode == "monthly_last"; monthlyNth.IsChecked = item.RecurrenceMode == "monthly_nth";
            monthlyDate.IsChecked = item.RecurrenceMode != "monthly_last" && item.RecurrenceMode != "monthly_nth";
            UpdateRecurrenceOptions();
            var reminder = item.ReminderConfigured ? item.ReminderMinutes : -1;
            foreach (var radio in reminderOptions.Children.OfType<RadioButton>()) radio.IsChecked = (int)radio.Tag == reminder;
            var mode = string.IsNullOrWhiteSpace(item.RolloverMode) && item.AutoRollover ? "next_day" : item.RolloverMode;
            noRollover.IsChecked = string.IsNullOrWhiteSpace(mode); nextDayRollover.IsChecked = mode == "next_day";
            nextWeekRollover.IsChecked = mode == "next_week"; nextWeekdayRollover.IsChecked = mode == "next_weekday";
            var target = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "google:" + item.GoogleCalendarId :
                item.Category == "업무일정" ? "local:business" : "local:personal";
            foreach (var radio in categoryOptions) radio.IsChecked = radio.Tag.ToString() == target;
            if (item.AllDay)
            {
                allDay.IsChecked = true; endDateInclusive = item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date;
                multiDay.IsChecked = endDateInclusive > item.Start.Date; UpdateEndDateButton(); UpdateRecurrenceAvailability(); return;
            }
            var hour = item.Start.Hour; afternoon.IsChecked = hour >= 12; morning.IsChecked = hour < 12;
            var displayHour = hour % 12; if (displayHour == 0) displayHour = 12;
            foreach (var radio in hourGrid.Children.OfType<RadioButton>()) radio.IsChecked = (int)radio.Tag == displayHour;
            var minute = new[] { 0, 15, 30, 45 }.OrderBy(x => Math.Abs(x - item.Start.Minute)).First();
            foreach (var radio in minuteGrid.Children.OfType<RadioButton>()) radio.IsChecked = (int)radio.Tag == minute;
        }

        void AddCategoryChoice(Panel panel, string text, string tag, bool selected, bool enabled)
        {
            var radio = new RadioButton { Content = text + (enabled ? "" : " · 읽기 전용"), Tag = tag, GroupName = "CategoryTarget",
                IsChecked = selected, IsEnabled = enabled, Margin = new Thickness(0, 0, 16, 5) };
            categoryOptions.Add(radio); panel.Children.Add(radio);
        }

        static IEnumerable<GoogleCalendarSetting> OrderedSources(IEnumerable<GoogleCalendarSetting> sources)
        {
            return sources.OrderBy(x => IsHolidaySource(x) ? 2 : x.Primary ? 0 : 1).ThenBy(x => x.Name);
        }

        static bool IsHolidaySource(GoogleCalendarSetting source)
        {
            return (source.Name ?? "").Contains("휴일") || (source.Id ?? "").IndexOf("holiday", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        async void ShowValidation()
        {
            validationMessage.Visibility = Visibility.Visible; title.Focus();
            await Task.Delay(UiRound.ErrorNoticeMilliseconds);
            validationMessage.Visibility = Visibility.Collapsed;
        }

        string SelectedRolloverMode()
        {
            var selected = rolloverOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true && x.Tag != null);
            return selected == null ? null : selected.Tag.ToString();
        }

        static TextBlock Header(string text, double size) { return new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 18) }; }
        static TextBlock Label(string text) { return new TextBlock { Text = text, Foreground = Brush("#475569"), FontSize = 12 }; }
        static void Round(Button button, double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }
        static void StyleInput(TextBox input)
        {
            input.Background = Brushes.White; input.BorderBrush = Brush("#CBD5E1"); input.BorderThickness = new Thickness(1);
            input.Padding = new Thickness(10, 4, 10, 8);
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TextBox.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(TextBox.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(TextBox.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(TextBox.PaddingProperty));
            var host = new FrameworkElementFactory(typeof(ScrollViewer)); host.Name = "PART_ContentHost"; border.AppendChild(host);
            input.Template = new ControlTemplate(typeof(TextBox)) { VisualTree = border };
        }
        static bool HasButtonParent(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        static bool HasDayButtonParent(DependencyObject source)
        {
            while (source != null)
            {
                if (source is CalendarDayButton) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        static string FormatDate(DateTime date)
        { return date.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")); }
        static void StyleCalendar(System.Windows.Controls.Calendar calendar)
        {
            calendar.Background = Brush("#FFF8F2"); calendar.BorderBrush = Brushes.Transparent;
            calendar.BorderThickness = new Thickness(0); calendar.Foreground = Brush("#6D3B47");

            var dayTemplate = new ControlTemplate(typeof(CalendarDayButton));
            var dayBorder = new FrameworkElementFactory(typeof(Border)); dayBorder.Name = "DayBorder";
            dayBorder.SetValue(Border.BackgroundProperty, Brush("#FFFEFC"));
            dayBorder.SetValue(Border.BorderBrushProperty, Brush("#F3D4C7"));
            dayBorder.SetValue(Border.BorderThicknessProperty, new Thickness(.6));
            dayBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            var dayContent = new FrameworkElementFactory(typeof(ContentPresenter));
            dayContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            dayContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            dayBorder.AppendChild(dayContent); dayTemplate.VisualTree = dayBorder;
            var today = new Trigger { Property = CalendarDayButton.IsTodayProperty, Value = true };
            today.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#DDF7F0"), "DayBorder"));
            today.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("#34B89A"), "DayBorder")); dayTemplate.Triggers.Add(today);
            var selected = new Trigger { Property = CalendarDayButton.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#E56B6F"), "DayBorder"));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); dayTemplate.Triggers.Add(selected);
            var inactive = new Trigger { Property = CalendarDayButton.IsInactiveProperty, Value = true };
            inactive.Setters.Add(new Setter(Control.OpacityProperty, .38)); dayTemplate.Triggers.Add(inactive);
            var dayStyle = new Style(typeof(CalendarDayButton)); dayStyle.Setters.Add(new Setter(Control.TemplateProperty, dayTemplate));
            dayStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(1.5)));
            dayStyle.Setters.Add(new Setter(Control.MinWidthProperty, 29.0)); dayStyle.Setters.Add(new Setter(Control.MinHeightProperty, 27.0));
            dayStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0)); dayStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#5B3540")));
            calendar.CalendarDayButtonStyle = dayStyle;

            var monthTemplate = new ControlTemplate(typeof(CalendarButton));
            var monthBorder = new FrameworkElementFactory(typeof(Border)); monthBorder.SetValue(Border.BackgroundProperty, Brush("#FDE8E3"));
            monthBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); monthBorder.SetValue(Border.MarginProperty, new Thickness(2));
            var monthContent = new FrameworkElementFactory(typeof(ContentPresenter));
            monthContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            monthContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); monthBorder.AppendChild(monthContent);
            monthTemplate.VisualTree = monthBorder;
            var monthStyle = new Style(typeof(CalendarButton)); monthStyle.Setters.Add(new Setter(Control.TemplateProperty, monthTemplate));
            monthStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#B4474D"))); monthStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            monthStyle.Setters.Add(new Setter(Control.FontSizeProperty, 16.0));
            calendar.CalendarButtonStyle = monthStyle;
        }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }
}
