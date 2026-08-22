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
        readonly TextBox title = new TextBox { Margin = new Thickness(0, 4, 0, 3), Height = 46,
            FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) };
        readonly TextBlock validationMessage = new TextBlock { Text = "제목을 입력해 주세요.", Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            FontSize = 12, FontWeight = FontWeights.SemiBold, Height = 18, Margin = new Thickness(4, 0, 0, 6), Visibility = Visibility.Collapsed };
        DateTime selectedDate;
        DateTime endDateInclusive;
        readonly CheckBox allDay = new CheckBox { Content = "하루 종일", IsChecked = true, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly CheckBox morning = new CheckBox { Content = "오전", Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly CheckBox afternoon = new CheckBox { Content = "오후", VerticalAlignment = VerticalAlignment.Center };
        readonly UniformGrid hourGrid = new UniformGrid { Columns = 6, Margin = new Thickness(0, 5, 0, 7), IsEnabled = false };
        readonly UniformGrid minuteGrid = new UniformGrid { Columns = 6, Margin = new Thickness(0, 1, 0, 3), IsEnabled = false };
        readonly Grid minuteRow = new Grid { Margin = new Thickness(0, 3, 0, 3), Visibility = Visibility.Collapsed };
        readonly CheckBox multiDay = new CheckBox { Content = "여러 날 일정", Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly Button endDateButton = new Button { Height = 32, Width = 96, IsEnabled = false, Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
        readonly RadioButton noRollover = new RadioButton { Content = "없음", GroupName = "Rollover", IsChecked = true, Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
        readonly RadioButton nextDayRollover = new RadioButton { Content = "다음날", Tag = "next_day", GroupName = "Rollover", Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
        readonly RadioButton nextWeekRollover = new RadioButton { Content = "다음주 같은 요일", Tag = "next_week", GroupName = "Rollover", Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
        readonly RadioButton nextWeekdayRollover = new RadioButton { Content = "다음 평일", Tag = "next_weekday", GroupName = "Rollover", FontSize = 12, ToolTip = "주말·대한민국 휴일을 건너뛰어 다음 평일로 이동" };
        readonly WrapPanel rolloverOptions = new WrapPanel { IsEnabled = false, VerticalAlignment = VerticalAlignment.Center };
        readonly StackPanel categories = new StackPanel { Margin = new Thickness(0, 4, 0, 7) };
        readonly List<RadioButton> categoryOptions = new List<RadioButton>();
        readonly WrapPanel reminderOptions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        readonly RadioButton customReminder = new RadioButton { Content = "직접 선택", Tag = "custom", GroupName = "Reminder", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly TextBox customReminderValue = new TextBox { Text = "15", Width = 48, Height = 27, Padding = new Thickness(4, 0, 4, 0), TextAlignment = TextAlignment.Center,
            IsEnabled = false, MaxLength = 3, VerticalContentAlignment = VerticalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        readonly ComboBox customReminderUnit = new ComboBox { Width = 80, Height = 27, IsEnabled = false, Margin = new Thickness(6, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        readonly CheckBox important = new CheckBox { Content = "★ 중요 일정", Foreground = new SolidColorBrush(Color.FromRgb(242, 13, 122)), VerticalAlignment = VerticalAlignment.Center };
        readonly CheckBox showDday = new CheckBox { Content = "D-Day 표시", Foreground = new SolidColorBrush(Color.FromRgb(3, 105, 161)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        readonly WrapPanel recurrenceOptions = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
        readonly Border recurrenceAdvancedCard = new Border { Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 8, 10, 4), Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
        readonly StackPanel recurrenceAdvanced = new StackPanel();
        readonly RadioButton dailyEvery = new RadioButton { Content = "매일", GroupName = "DailyMode", IsChecked = true, Margin = new Thickness(0, 0, 18, 4) };
        readonly RadioButton dailyWeekdays = new RadioButton { Content = "평일만 · 월~금", GroupName = "DailyMode", Margin = new Thickness(0, 0, 0, 4) };
        readonly List<CheckBox> weeklyDays = new List<CheckBox>();
        readonly RadioButton monthlyDate = new RadioButton { GroupName = "MonthlyMode", IsChecked = true, Margin = new Thickness(0, 0, 14, 4) };
        readonly RadioButton monthlyNth = new RadioButton { GroupName = "MonthlyMode", Margin = new Thickness(0, 0, 14, 4) };
        readonly RadioButton monthlyLast = new RadioButton { Content = "매월 마지막 날", GroupName = "MonthlyMode", Margin = new Thickness(0, 0, 0, 4) };
        readonly RadioButton yearlyDate = new RadioButton { GroupName = "YearlyMode", IsChecked = true, Margin = new Thickness(0, 0, 14, 4) };
        readonly RadioButton yearlyNth = new RadioButton { GroupName = "YearlyMode", Margin = new Thickness(0, 0, 0, 4) };
        readonly Button recurrenceUntilButton = new Button { Height = 34, IsEnabled = false, Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
        DateTime recurrenceUntilDate;
        readonly CheckBox recurrenceEnabled = new CheckBox { Content = "반복 일정", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        readonly StackPanel recurrenceBody = new StackPanel { Visibility = Visibility.Collapsed };
        readonly Border timeCard = new Border { Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8, 12, 4), Margin = new Thickness(0, 2, 0, 8) };
        readonly Border recurrenceCard = new Border { Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 9, 12, 5), Margin = new Thickness(0, 0, 0, 8) };
        readonly TextBlock googleTaskHint = new TextBlock { Text = "Google Task는 하루 종일 할 일로 저장되며 반복·시간·알림을 지원하지 않습니다.",
            Foreground = new SolidColorBrush(Color.FromRgb(194, 65, 12)), FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 6), Visibility = Visibility.Collapsed };
        readonly RadioButton recurrenceCountMode = new RadioButton { Content = "횟수", GroupName = "RecurrenceEnd", Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), FontSize = 11, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly RadioButton recurrenceUntilMode = new RadioButton { Content = "종료날짜", GroupName = "RecurrenceEnd", Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), FontSize = 11, IsChecked = true, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly TextBox recurrenceCountValue = new TextBox { Text = "10", Width = 42, Height = 27, Padding = new Thickness(4, 0, 4, 0), TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsEnabled = false, MaxLength = 3 };
        readonly List<GoogleCalendarSetting> googleSources;
        readonly TextBox notes = new TextBox { Margin = new Thickness(0, 3, 0, 8), Height = 58,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) };
        readonly PlannerItem editingItem;
        public PlannerItem Result;
        public bool DeleteRequested;
        public bool ApplyToSeries;
        readonly CheckBox editSingleOccurrence = new CheckBox { Content = "이번 일정만 변경", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        bool editingSeries;
        bool updatingTimeMode;
        int durationMinutes = 30;

        public AddItemWindow(DateTime selected, PlannerItem existing = null, List<GoogleCalendarSetting> sources = null, bool googleConnected = true, PlannerSettings defaults = null)
        {
            editingItem = existing;
            googleSources = sources ?? new List<GoogleCalendarSetting>();
            selectedDate = selected.Date;
            endDateInclusive = existing != null && existing.AllDay && existing.End > existing.Start ? existing.End.AddTicks(-1).Date : selectedDate;
            editingSeries = existing != null && (!string.IsNullOrWhiteSpace(existing.SeriesId) || !string.IsNullOrWhiteSpace(existing.GoogleRecurringEventId) || !string.IsNullOrWhiteSpace(existing.RecurrenceFrequency));
            recurrenceUntilDate = selectedDate.AddYears(1);
            Title = existing == null ? "새 일정" : "일정 수정";
            Width = 480; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.GetPosition(this).Y < 70 && !HasButtonParent(e.OriginalSource as DependencyObject) && Mouse.LeftButton == MouseButtonState.Pressed)
                    DragMove();
            };
            for (var h = 0; h < 12; h++)
                hourGrid.Children.Add(new RadioButton { Content = h.ToString("00") + "시", Tag = h, GroupName = "Hour", IsChecked = h == 9, Margin = new Thickness(2, 4, 2, 4) });
            foreach (var minute in new[] { 0, 10, 20, 30, 40, 50 })
                minuteGrid.Children.Add(new RadioButton { Content = minute + "분", Tag = minute, GroupName = "Minute",
                    IsChecked = minute == 0, Margin = new Thickness(0, 4, 4, 4), HorizontalAlignment = HorizontalAlignment.Left });
            allDay.Checked += delegate { SelectTimeMode(allDay); };
            morning.Checked += delegate { SelectTimeMode(morning); };
            afternoon.Checked += delegate { SelectTimeMode(afternoon); };
            allDay.Unchecked += delegate { EnsureTimeModeSelected(); };
            morning.Unchecked += delegate { EnsureTimeModeSelected(); };
            afternoon.Unchecked += delegate { EnsureTimeModeSelected(); };
            categories.Children.Add(new TextBlock { Text = "온하루 · 로컬 전용", Foreground = Brush("#64748B"), FontSize = 11, Margin = new Thickness(0, 0, 0, 5) });
            var localChoices = new WrapPanel();
            AddCategoryChoice(localChoices, "업무일정", "local:business", true, true);
            AddCategoryChoice(localChoices, "개인일정", "local:personal", false, true);
            AddCategoryChoice(localChoices, "야구", "local:baseball", false, true); categories.Children.Add(localChoices);
            if (googleSources.Count > 0)
            {
                categories.Children.Add(new TextBlock { Text = "Google · 동기화", Foreground = Brush("#64748B"), FontSize = 11, Margin = new Thickness(0, 9, 0, 5) });
                var googleChoices = new WrapPanel();
                foreach (var source in OrderedSources(googleSources.Where(x => !GoogleTasks.IsSource(x.Id) || defaults != null && defaults.ShowGoogleTasks).ToList()))
                    AddCategoryChoice(googleChoices, GoogleTasks.IsSource(source.Id) ? "Google Task · " + source.Name.Replace("Tasks · ", "") : (source.Primary ? "내 캘린더 · " : "") + source.Name,
                        "google:" + source.Id, false, source.Editable);
                categories.Children.Add(googleTaskHint);
                categories.Children.Add(googleChoices);
            }

            StyleInput(title); StyleInput(notes); StyleInput(customReminderValue); StyleInput(recurrenceCountValue);
            customReminderValue.Padding = new Thickness(4, 0, 4, 0);
            recurrenceCountValue.Padding = new Thickness(4, 0, 4, 0);
            var panel = new StackPanel { Margin = new Thickness(22, 8, 14, 12) };
            var header = new DockPanel { Margin = new Thickness(22, 10, 10, 8) };
            var saveGradient = new LinearGradientBrush(); saveGradient.StartPoint = new Point(0, .5); saveGradient.EndPoint = new Point(1, .5);
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3977E8"), 0));
            saveGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C5CE5"), 1));
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"),
                Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            Round(close, 10);
            close.Click += delegate { DialogResult = false; };
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var topSave = new Button { Content = "✓  일정 저장", Width = 88, Height = 32, Background = saveGradient,
                Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            Round(topSave, 10); topSave.Click += Save; DockPanel.SetDock(topSave, Dock.Right); header.Children.Add(topSave);
            var headerTitle = new StackPanel { Orientation = Orientation.Horizontal };
            headerTitle.Children.Add(new TextBlock { Text = existing == null ? "✦  새 일정" : "✎  일정 수정", FontSize = 22, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center });
            if (existing != null)
                headerTitle.Children.Add(new Border { Background = string.IsNullOrWhiteSpace(existing.GoogleCalendarId) ? Brush("#EEF2FF") : Brush("#F0FDF4"),
                    CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(12, 2, 0, 0),
                    Child = new TextBlock { Text = string.IsNullOrWhiteSpace(existing.GoogleCalendarId) ? "온하루 등록" : GoogleTasks.IsTask(existing) ? "Google Task" : "Google Calendar",
                        Foreground = string.IsNullOrWhiteSpace(existing.GoogleCalendarId) ? Brush("#4338CA") : Brush("#15803D"), FontSize = 11, FontWeight = FontWeights.SemiBold } });
            header.Children.Add(headerTitle);
            var dateCard = new Border { Background = Brush("#EFF6FF"), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 0, 10) };
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
                    SelectionMode = CalendarSelectionMode.SingleDate };
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
            titleLabelRow.Children.Add(Label("제목"));
            var titleOptions = new StackPanel { Orientation = Orientation.Horizontal }; titleOptions.Children.Add(important); titleOptions.Children.Add(showDday);
            Grid.SetColumn(titleOptions, 1); titleLabelRow.Children.Add(titleOptions);
            panel.Children.Add(titleLabelRow); panel.Children.Add(title); panel.Children.Add(validationMessage);
            var timeCardContent = new StackPanel();
            timeCardContent.Children.Add(new TextBlock { Text = "로컬 일정은 시간 유무와 관계없이 완료 체크할 수 있습니다.", FontSize = 11,
                Foreground = Brush("#64748B"), Margin = new Thickness(0, 0, 0, 8) });
            var durationRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 4) };
            endDateButton.Content = endDateInclusive.ToString("yyyy.MM.dd"); Round(endDateButton, 9);
            multiDay.IsEnabled = true; multiDay.IsChecked = endDateInclusive > selectedDate; endDateButton.IsEnabled = multiDay.IsChecked == true;
            multiDay.Checked += delegate { if (endDateInclusive <= selectedDate) endDateInclusive = selectedDate.AddDays(1); UpdateEndDateButton(); endDateButton.IsEnabled = true; UpdateRecurrenceAvailability(); };
            multiDay.Unchecked += delegate { endDateInclusive = selectedDate; UpdateEndDateButton(); endDateButton.IsEnabled = false; UpdateRecurrenceAvailability(); };
            durationRow.Children.Add(allDay); durationRow.Children.Add(multiDay); durationRow.Children.Add(new TextBlock { Text = "종료날짜", FontSize = 11, Foreground = Brush("#64748B"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); durationRow.Children.Add(endDateButton);
            timeCardContent.Children.Add(durationRow);
            var timeModes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 2) };
            timeModes.Children.Add(morning); timeModes.Children.Add(afternoon); timeCardContent.Children.Add(timeModes);
            timeCardContent.Children.Add(hourGrid);
            minuteRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); minuteRow.ColumnDefinitions.Add(new ColumnDefinition());
            minuteRow.Children.Add(new TextBlock { Text = "분", FontSize = 11, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            minuteGrid.Margin = new Thickness(0); Grid.SetColumn(minuteGrid, 1); minuteRow.Children.Add(minuteGrid); timeCardContent.Children.Add(minuteRow);
            rolloverOptions.Children.Add(noRollover); rolloverOptions.Children.Add(nextDayRollover);
            rolloverOptions.Children.Add(nextWeekRollover); rolloverOptions.Children.Add(nextWeekdayRollover);
            var rolloverLine = new Grid { Margin = new Thickness(0, 3, 0, 3) }; rolloverLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); rolloverLine.ColumnDefinitions.Add(new ColumnDefinition());
            rolloverLine.Visibility = defaults == null || defaults.UseRollover ? Visibility.Visible : Visibility.Collapsed;
            rolloverLine.Children.Add(new TextBlock { Text = "이월", FontSize = 11, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(rolloverOptions, 1); rolloverLine.Children.Add(rolloverOptions); timeCardContent.Children.Add(rolloverLine);
            var reminderLine = new Grid { Margin = new Thickness(0, 3, 0, 3) }; reminderLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); reminderLine.ColumnDefinitions.Add(new ColumnDefinition());
            reminderLine.Children.Add(new TextBlock { Text = "알림", FontSize = 11, Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Center });
            foreach (var option in new[] { new { Name = "없음", Value = -1 }, new { Name = "정시", Value = 0 } })
                reminderOptions.Children.Add(new RadioButton { Content = option.Name, Tag = option.Value, GroupName = "Reminder",
                    IsChecked = option.Value == -1, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center });
            customReminderUnit.Items.Add(new ComboBoxItem { Content = "분 전", Tag = 1 });
            customReminderUnit.Items.Add(new ComboBoxItem { Content = "시간 전", Tag = 60 });
            customReminderUnit.Items.Add(new ComboBoxItem { Content = "일 전", Tag = 1440 });
            customReminderUnit.SelectedIndex = 0;
            customReminderUnit.Width = 80; customReminderUnit.Height = 27; customReminderUnit.Background = Brushes.White;
            customReminderUnit.BorderBrush = Brush("#C7D2FE"); customReminderUnit.Cursor = Cursors.Hand;
            SettingsWindow.StyleComboBox(customReminderUnit);
            customReminderValue.PreviewTextInput += DigitsOnly;
            recurrenceCountValue.PreviewTextInput += DigitsOnly;
            customReminder.Checked += delegate { customReminderValue.IsEnabled = true; customReminderUnit.IsEnabled = true; };
            customReminder.Unchecked += delegate { customReminderValue.IsEnabled = false; customReminderUnit.IsEnabled = false; };
            reminderOptions.Children.Add(customReminder); reminderOptions.Children.Add(customReminderValue); reminderOptions.Children.Add(customReminderUnit);
            Grid.SetColumn(reminderOptions, 1); reminderLine.Children.Add(reminderOptions); timeCardContent.Children.Add(reminderLine);
            timeCard.Child = timeCardContent; panel.Children.Add(timeCard);
            var endCalendar = new System.Windows.Controls.Calendar { SelectedDate = endDateInclusive, DisplayDate = endDateInclusive,
                SelectionMode = CalendarSelectionMode.SingleDate };
            StyleCalendar(endCalendar);
            var endPopup = new Popup { PlacementTarget = endDateButton, Placement = PlacementMode.Bottom,
                AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.Fade, VerticalOffset = 6, Child = endCalendar };
            endCalendar.SelectedDatesChanged += delegate
            {
                if (!endCalendar.SelectedDate.HasValue) return;
                endDateInclusive = endCalendar.SelectedDate.Value.Date < selectedDate ? selectedDate : endCalendar.SelectedDate.Value.Date;
                UpdateEndDateButton(); UpdateRecurrenceAvailability();
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
            var recurrenceLine = new StackPanel();
            var recurrenceHeader = new Grid(); recurrenceHeader.ColumnDefinitions.Add(new ColumnDefinition()); recurrenceHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            recurrenceHeader.Children.Add(recurrenceEnabled);
            var recurrenceRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            recurrenceRight.Children.Add(recurrenceCountMode); recurrenceRight.Children.Add(recurrenceCountValue);
            recurrenceRight.Children.Add(new TextBlock { Text = "회", Foreground = Brush("#64748B"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) });
            recurrenceRight.Children.Add(recurrenceUntilMode);
            recurrenceUntilButton.Width = 94; recurrenceUntilButton.Height = 30; recurrenceUntilButton.Content = recurrenceUntilDate.ToString("yyyy.MM.dd");
            recurrenceUntilButton.Background = Brushes.White; recurrenceUntilButton.BorderBrush = Brush("#A5B4FC"); recurrenceUntilButton.BorderThickness = new Thickness(1);
            Round(recurrenceUntilButton, 9); recurrenceRight.Children.Add(recurrenceUntilButton);
            Grid.SetColumn(recurrenceRight, 1); recurrenceHeader.Children.Add(recurrenceRight); recurrenceLine.Children.Add(recurrenceHeader);
            recurrenceCountMode.Checked += delegate { recurrenceCountValue.IsEnabled = recurrenceEnabled.IsChecked == true; recurrenceUntilButton.IsEnabled = false; };
            recurrenceUntilMode.Checked += delegate { recurrenceCountValue.IsEnabled = false; recurrenceUntilButton.IsEnabled = recurrenceEnabled.IsChecked == true; };
            foreach (var option in new[] { new { Name = "없음", Value = "" }, new { Name = "매일", Value = "daily" }, new { Name = "매주", Value = "weekly" }, new { Name = "매월", Value = "monthly" }, new { Name = "매년", Value = "yearly" } })
            {
                var radio = new RadioButton { Content = option.Name, Tag = option.Value, GroupName = "Recurrence", IsChecked = option.Value == "", Margin = new Thickness(0, 0, 9, 5), FontSize = 12 };
                if (option.Value == "") radio.Visibility = Visibility.Collapsed;
                radio.Checked += delegate { UpdateRecurrenceOptions(); }; recurrenceOptions.Children.Add(radio);
            }
            if (editingSeries) { editSingleOccurrence.Margin = new Thickness(8, 0, 0, 5); recurrenceOptions.Children.Add(editSingleOccurrence); }
            recurrenceBody.Children.Add(recurrenceOptions); recurrenceAdvancedCard.Child = recurrenceAdvanced; recurrenceBody.Children.Add(recurrenceAdvancedCard);
            recurrenceLine.Children.Add(recurrenceBody);
            recurrenceEnabled.Checked += delegate
            {
                recurrenceBody.Visibility = Visibility.Visible;
                var selectedFrequency = recurrenceOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true && !string.IsNullOrWhiteSpace((x.Tag ?? "").ToString()));
                if (selectedFrequency == null)
                {
                    selectedFrequency = recurrenceOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsEnabled && !string.IsNullOrWhiteSpace((x.Tag ?? "").ToString()));
                    if (selectedFrequency != null) selectedFrequency.IsChecked = true;
                }
                UpdateRecurrenceOptions();
            };
            recurrenceEnabled.Unchecked += delegate
            {
                recurrenceOptions.Children.OfType<RadioButton>().First(x => string.IsNullOrWhiteSpace((x.Tag ?? "").ToString())).IsChecked = true;
                recurrenceBody.Visibility = Visibility.Collapsed; UpdateRecurrenceOptions();
            };
            recurrenceCard.Child = recurrenceLine; panel.Children.Add(recurrenceCard);
            var categoryContent = new StackPanel();
            categoryContent.Children.Add(categories);
            panel.Children.Add(new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 7, 14, 2),
                Margin = new Thickness(0, 0, 0, 8), Child = categoryContent });
            UpdateRecurrenceAvailability();
            var recurrenceCalendar = new System.Windows.Controls.Calendar { SelectedDate = recurrenceUntilDate, DisplayDate = recurrenceUntilDate,
                SelectionMode = CalendarSelectionMode.SingleDate };
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
            var save = new Button { Content = "✓  일정 저장", Height = 40, Background = saveGradient,
                Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, FontSize = 14 };
            Round(save, 13);
            save.Click += Save;
            if (existing == null) panel.Children.Add(save);
            else
            {
                var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) }); footer.ColumnDefinitions.Add(new ColumnDefinition());
                var delete = new Button { Content = "삭제", Height = 40, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"),
                    BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) };
                Round(delete, 13); delete.Click += delegate { DeleteRequested = true; DialogResult = true; }; footer.Children.Add(delete);
                Grid.SetColumn(save, 1); footer.Children.Add(save); panel.Children.Add(footer);
            }
            var contentScroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = CompactScrollHeight(SystemParameters.WorkArea.Height) };
            var popupLayout = new Grid(); popupLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); popupLayout.RowDefinitions.Add(new RowDefinition());
            popupLayout.Children.Add(header); Grid.SetRow(contentScroll, 1); popupLayout.Children.Add(contentScroll);
            Loaded += delegate
            {
                contentScroll.MaxHeight = CompactScrollHeight(Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea.Height);
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { UiRound.SoftenScrollBars(contentScroll); }));
            };
            var shell = new Border { Background = Brush("#FFF8FAFC"), CornerRadius = new CornerRadius(18),
                BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), Child = popupLayout };
            Content = UiRound.EmphasizePopup(shell);
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter && !notes.IsKeyboardFocusWithin)
                { Save(sender, e); e.Handled = true; }
            };
            if (existing == null) ApplyDefaults(defaults);
            else LoadExisting(existing);
            ApplyTargetMode();
            SelectTimeMode(allDay.IsChecked == true ? allDay : morning.IsChecked == true ? morning : afternoon);
            Loaded += delegate { title.Focus(); };
        }

        void SelectTimeMode(CheckBox selected)
        {
            if (updatingTimeMode) return;
            updatingTimeMode = true;
            allDay.IsChecked = selected == allDay;
            morning.IsChecked = selected == morning;
            afternoon.IsChecked = selected == afternoon;
            updatingTimeMode = false;

            var isAllDay = selected == allDay;
            if (!isAllDay) UpdateHourOptions(selected == afternoon);
            hourGrid.IsEnabled = !isAllDay;
            minuteGrid.IsEnabled = !isAllDay;
            hourGrid.Visibility = isAllDay ? Visibility.Collapsed : Visibility.Visible;
            minuteRow.Visibility = isAllDay ? Visibility.Collapsed : Visibility.Visible;
            rolloverOptions.IsEnabled = !isAllDay;
            multiDay.IsEnabled = isAllDay;
            if (!isAllDay) multiDay.IsChecked = false;
            endDateButton.IsEnabled = isAllDay && multiDay.IsChecked == true;
        }

        void UpdateHourOptions(bool useAfternoon)
        {
            var offset = useAfternoon ? 12 : 0;
            var options = hourGrid.Children.OfType<RadioButton>().ToList();
            for (var index = 0; index < options.Count; index++)
            {
                var hour = offset + index;
                options[index].Tag = hour;
                options[index].Content = hour.ToString("00") + "시";
            }
        }

        void EnsureTimeModeSelected()
        {
            if (updatingTimeMode || allDay.IsChecked == true || morning.IsChecked == true || afternoon.IsChecked == true) return;
            SelectTimeMode(allDay);
        }

        void UpdateRecurrenceOptions()
        {
            var selected = recurrenceOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true);
            var frequency = selected == null ? "" : selected.Tag.ToString();
            var recurring = recurrenceEnabled.IsChecked == true && !string.IsNullOrWhiteSpace(frequency);
            recurrenceCountMode.IsEnabled = recurring;
            recurrenceUntilMode.IsEnabled = recurring;
            recurrenceCountValue.IsEnabled = recurring && recurrenceCountMode.IsChecked == true;
            recurrenceUntilButton.IsEnabled = recurring && recurrenceUntilMode.IsChecked == true;
            recurrenceAdvanced.Children.Clear(); recurrenceAdvancedCard.Visibility = string.IsNullOrWhiteSpace(frequency) ? Visibility.Collapsed : Visibility.Visible;
            if (frequency == "daily")
            {
                Detach(dailyEvery); Detach(dailyWeekdays);
                var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(dailyEvery); row.Children.Add(dailyWeekdays); recurrenceAdvanced.Children.Add(row);
            }
            else if (frequency == "weekly")
            {
                if (multiDay.IsChecked == true)
                {
                    recurrenceAdvanced.Children.Add(new TextBlock { Text = "매주 " + new[] { "일", "월", "화", "수", "목", "금", "토" }[(int)selectedDate.DayOfWeek] + "요일부터 같은 기간",
                        Foreground = Brush("#475569"), FontSize = 12, Margin = new Thickness(2, 0, 0, 4) });
                    return;
                }
                if (weeklyDays.Count == 0)
                    foreach (var day in new[] { Tuple.Create("월", "MO", DayOfWeek.Monday), Tuple.Create("화", "TU", DayOfWeek.Tuesday), Tuple.Create("수", "WE", DayOfWeek.Wednesday), Tuple.Create("목", "TH", DayOfWeek.Thursday), Tuple.Create("금", "FR", DayOfWeek.Friday), Tuple.Create("토", "SA", DayOfWeek.Saturday), Tuple.Create("일", "SU", DayOfWeek.Sunday) })
                        weeklyDays.Add(new CheckBox { Content = day.Item1, Tag = day.Item2, IsChecked = day.Item3 == selectedDate.DayOfWeek, Margin = new Thickness(0, 0, 13, 4) });
                var row = new WrapPanel(); foreach (var day in weeklyDays) { Detach(day); row.Children.Add(day); } recurrenceAdvanced.Children.Add(row);
            }
            else if (frequency == "monthly")
            {
                var multi = multiDay.IsChecked == true;
                monthlyDate.Content = "매월 " + selectedDate.Day + "일" + (multi ? "부터 같은 기간" : "");
                monthlyNth.Content = "매월 " + RecurrenceService.MonthlyPositionText(selectedDate) + (multi ? "부터 같은 기간" : "");
                Detach(monthlyDate); Detach(monthlyNth); Detach(monthlyLast);
                var startsOnLastDay = selectedDate.Day == DateTime.DaysInMonth(selectedDate.Year, selectedDate.Month);
                if (!startsOnLastDay && monthlyLast.IsChecked == true) monthlyDate.IsChecked = true;
                var row = new WrapPanel(); row.Children.Add(monthlyDate); row.Children.Add(monthlyNth);
                if (startsOnLastDay)
                {
                    monthlyLast.Content = multi ? "매월 마지막 날부터 같은 기간" : "매월 마지막 날";
                    row.Children.Add(monthlyLast);
                }
                recurrenceAdvanced.Children.Add(row);
            }
            else if (frequency == "yearly")
            {
                yearlyDate.Content = "매년 같은 날짜 · " + selectedDate.Month + "월 " + selectedDate.Day + "일";
                yearlyNth.Content = "매년 같은 주·요일 · " + selectedDate.Month + "월 " + RecurrenceService.MonthlyPositionText(selectedDate);
                Detach(yearlyDate); Detach(yearlyNth);
                var row = new WrapPanel(); row.Children.Add(yearlyDate); row.Children.Add(yearlyNth); recurrenceAdvanced.Children.Add(row);
            }
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
            var radios = recurrenceOptions.Children.OfType<RadioButton>().Where(x => x.Tag != null).ToList();
            var multi = multiDay.IsChecked == true;
            var durationDays = Math.Max(1, (endDateInclusive - selectedDate).Days + 1);
            foreach (var radio in radios)
            {
                var frequency = radio.Tag.ToString();
                radio.IsEnabled = !multi || string.IsNullOrWhiteSpace(frequency) ||
                    (durationDays <= 7 && frequency == "weekly") || (durationDays >= 8 && frequency == "monthly");
                if (multi && frequency == "weekly") radio.ToolTip = durationDays <= 7 ? "같은 기간을 매주 반복합니다." : "8일 이상 일정은 매주 반복할 수 없습니다.";
                if (multi && frequency == "monthly") radio.ToolTip = durationDays >= 8 ? "같은 기간을 매월 반복합니다." : "8일 이상 여러 날 일정에서 사용할 수 있습니다.";
            }
            var selected = radios.FirstOrDefault(x => x.IsChecked == true);
            if (multi && selected != null && !selected.IsEnabled)
            {
                radios.First(x => string.IsNullOrWhiteSpace(x.Tag.ToString())).IsChecked = true;
                var replacement = radios.FirstOrDefault(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.Tag.ToString()));
                if (recurrenceEnabled.IsChecked == true && replacement != null) replacement.IsChecked = true;
            }
            UpdateRecurrenceOptions();
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
                var minute = (int)minuteGrid.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag;
                start = start.AddHours(hour).AddMinutes(minute);
            }
            var selectedOption = categoryOptions.First(x => x.IsChecked == true);
            var target = selectedOption.Tag.ToString();
            var selectedSource = target.StartsWith("google:") ? googleSources.FirstOrDefault(x => "google:" + x.Id == target) : null;
            var taskTarget = selectedSource != null && GoogleTasks.IsSource(selectedSource.Id);
            var selectedCategory = target == "local:business" ? "업무일정" : target == "local:baseball" ? "야구" : "개인일정";
            var recurrenceFrequency = taskTarget ? "" : recurrenceOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
            var recurrenceMode = recurrenceFrequency == "daily" ? (dailyWeekdays.IsChecked == true ? "weekdays" : "daily") :
                recurrenceFrequency == "monthly" ? (monthlyLast.IsChecked == true ? "monthly_last" : monthlyNth.IsChecked == true ? "monthly_nth" : "monthly_date") :
                recurrenceFrequency == "yearly" ? (yearlyNth.IsChecked == true ? "yearly_nth" : "yearly_date") : recurrenceFrequency;
            var recurrenceDays = recurrenceFrequency == "weekly" && multiDay.IsChecked == true ? RecurrenceService.DayCode(selectedDate.DayOfWeek) :
                recurrenceFrequency == "weekly" ? string.Join(",", weeklyDays.Where(x => x.IsChecked == true).Select(x => x.Tag.ToString())) :
                recurrenceMode == "monthly_nth" || recurrenceMode == "yearly_nth" ? RecurrenceService.MonthlyNthCode(selectedDate) : null;
            if (recurrenceFrequency == "weekly" && string.IsNullOrWhiteSpace(recurrenceDays)) recurrenceDays = RecurrenceService.DayCode(selectedDate.DayOfWeek);
            Result = new PlannerItem { Id = editingItem == null ? Guid.NewGuid().ToString() : editingItem.Id, Title = title.Text.Trim(), Start = start,
                End = allDay.IsChecked == true ? (multiDay.IsChecked == true ? endDateInclusive.AddDays(1) : start.AddDays(1)) : start.AddMinutes(durationMinutes),
                AllDay = taskTarget || allDay.IsChecked == true, IsTodo = taskTarget || UsesCompletionCheck(allDay.IsChecked == true, target.StartsWith("local:")),
                Category = selectedCategory, Notes = notes.Text.Trim(),
                GoogleEventId = editingItem == null ? null : editingItem.GoogleEventId,
                GoogleEventType = editingItem == null ? null : editingItem.GoogleEventType,
                OnharuManaged = taskTarget || editingItem != null && editingItem.OnharuManaged,
                GoogleTaskEvent = taskTarget,
                CreatedInOnharu = editingItem == null || editingItem.CreatedInOnharu,
                Completed = editingItem != null && editingItem.Completed,
                GoogleCalendarId = editingItem == null ? null : editingItem.GoogleCalendarId,
                GoogleCalendarName = editingItem == null ? null : editingItem.GoogleCalendarName,
                GoogleCalendarColor = editingItem == null ? null : editingItem.GoogleCalendarColor,
                GoogleReadOnly = editingItem != null && editingItem.GoogleReadOnly,
                RolloverMode = taskTarget || allDay.IsChecked == true ? null : SelectedRolloverMode(),
                AutoRollover = !taskTarget && allDay.IsChecked != true && noRollover.IsChecked != true,
                Important = important.IsChecked == true,
                ShowDday = showDday.IsChecked == true,
                ReminderMinutes = taskTarget ? -1 : SelectedReminderMinutes(),
                ReminderConfigured = true,
                RecurrenceFrequency = recurrenceFrequency, RecurrenceMode = recurrenceMode, RecurrenceDays = recurrenceDays,
                RecurrenceUntil = recurrenceUntilDate, RecurrenceCount = string.IsNullOrWhiteSpace(recurrenceFrequency) ? 0 : SelectedRecurrenceCount(),
                SeriesId = editingItem == null ? null : editingItem.SeriesId,
                GoogleRecurringEventId = editingItem == null ? null : editingItem.GoogleRecurringEventId,
                PendingGoogleSync = editingItem != null && editingItem.PendingGoogleSync };
            if (selectedSource != null)
            {
                Result.GoogleCalendarId = selectedSource.Id; Result.GoogleCalendarName = selectedSource.Name;
                Result.GoogleCalendarColor = selectedSource.Color; Result.GoogleReadOnly = !selectedSource.Editable;
                if (editingItem != null && editingItem.GoogleCalendarId != selectedSource.Id) { Result.GoogleEventId = null; Result.GoogleEventType = null; }
                if (taskTarget) { Result.GoogleEventType = "task"; Result.GoogleTaskEvent = true; Result.OnharuManaged = true; Result.CreatedInOnharu = true; }
            }
            else if (editingItem == null || !target.StartsWith("google:"))
            {
                Result.GoogleCalendarId = null; Result.GoogleCalendarName = null; Result.GoogleCalendarColor = null;
                Result.GoogleReadOnly = false; Result.GoogleEventId = null; Result.GoogleEventType = null; Result.OnharuManaged = false; Result.GoogleTaskEvent = false;
            }
            ApplyToSeries = editingSeries && editSingleOccurrence.IsChecked != true;
            DialogResult = true;
        }

        void LoadExisting(PlannerItem item)
        {
            durationMinutes = item.AllDay ? 30 : Math.Max(1, (int)Math.Round((item.End - item.Start).TotalMinutes));
            title.Text = item.Title; notes.Text = item.Notes ?? "";
            important.IsChecked = item.Important; showDday.IsChecked = item.ShowDday;
            recurrenceUntilDate = item.RecurrenceUntil.Year >= 1900 ? item.RecurrenceUntil : item.Start.Date.AddYears(1);
            recurrenceCountMode.IsChecked = item.RecurrenceCount > 0;
            recurrenceUntilMode.IsChecked = item.RecurrenceCount <= 0;
            if (item.RecurrenceCount > 0) recurrenceCountValue.Text = item.RecurrenceCount.ToString(CultureInfo.InvariantCulture);
            recurrenceUntilButton.Content = recurrenceUntilDate.ToString("yyyy.MM.dd");
            foreach (var radio in recurrenceOptions.Children.OfType<RadioButton>()) radio.IsChecked = radio.Tag.ToString() == (item.RecurrenceFrequency ?? "");
            recurrenceEnabled.IsChecked = !string.IsNullOrWhiteSpace(item.RecurrenceFrequency);
            dailyWeekdays.IsChecked = item.RecurrenceMode == "weekdays"; dailyEvery.IsChecked = item.RecurrenceMode != "weekdays";
            if (item.RecurrenceFrequency == "weekly")
            {
                UpdateRecurrenceOptions(); var selectedDays = (item.RecurrenceDays ?? RecurrenceService.DayCode(item.Start.DayOfWeek)).Split(',');
                foreach (var day in weeklyDays) day.IsChecked = selectedDays.Contains(day.Tag.ToString());
            }
            monthlyLast.IsChecked = item.RecurrenceMode == "monthly_last"; monthlyNth.IsChecked = item.RecurrenceMode == "monthly_nth";
            monthlyDate.IsChecked = item.RecurrenceMode != "monthly_last" && item.RecurrenceMode != "monthly_nth";
            yearlyNth.IsChecked = item.RecurrenceMode == "yearly_nth"; yearlyDate.IsChecked = item.RecurrenceMode != "yearly_nth";
            UpdateRecurrenceOptions();
            var reminder = item.ReminderConfigured ? item.ReminderMinutes : -1;
            var fixedReminder = reminderOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.Tag is int && (int)x.Tag == reminder);
            if (fixedReminder != null) fixedReminder.IsChecked = true;
            else
            {
                customReminder.IsChecked = true;
                var multiplier = reminder > 0 && reminder % 1440 == 0 ? 1440 : reminder > 0 && reminder % 60 == 0 ? 60 : 1;
                customReminderUnit.SelectedIndex = multiplier == 1440 ? 2 : multiplier == 60 ? 1 : 0;
                customReminderValue.Text = Math.Max(1, reminder / multiplier).ToString(CultureInfo.InvariantCulture);
            }
            var mode = string.IsNullOrWhiteSpace(item.RolloverMode) && item.AutoRollover ? "next_day" : item.RolloverMode;
            noRollover.IsChecked = string.IsNullOrWhiteSpace(mode); nextDayRollover.IsChecked = mode == "next_day";
            nextWeekRollover.IsChecked = mode == "next_week"; nextWeekdayRollover.IsChecked = mode == "next_weekday";
            var target = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) ? "google:" + item.GoogleCalendarId :
                item.Category == "업무일정" ? "local:business" : item.Category == "야구" ? "local:baseball" : "local:personal";
            foreach (var radio in categoryOptions) radio.IsChecked = radio.Tag.ToString() == target;
            if (item.AllDay)
            {
                allDay.IsChecked = true; endDateInclusive = item.End > item.Start ? item.End.AddTicks(-1).Date : item.Start.Date;
                multiDay.IsChecked = endDateInclusive > item.Start.Date; UpdateEndDateButton(); UpdateRecurrenceAvailability(); return;
            }
            var hour = item.Start.Hour; afternoon.IsChecked = hour >= 12; morning.IsChecked = hour < 12;
            foreach (var radio in hourGrid.Children.OfType<RadioButton>()) radio.IsChecked = (int)radio.Tag == hour;
            var minute = new[] { 0, 10, 20, 30, 40, 50 }.OrderBy(x => Math.Abs(x - item.Start.Minute)).First();
            foreach (var radio in minuteGrid.Children.OfType<RadioButton>()) radio.IsChecked = (int)radio.Tag == minute;
        }

        void AddCategoryChoice(Panel panel, string text, string tag, bool selected, bool enabled)
        {
            var radio = new RadioButton { Content = text + (enabled ? "" : " · 읽기 전용"), Tag = tag, GroupName = "CategoryTarget",
                IsChecked = selected, IsEnabled = enabled, Margin = new Thickness(0, 0, 16, 5) };
            radio.Checked += delegate { ApplyTargetMode(); };
            categoryOptions.Add(radio); panel.Children.Add(radio);
        }

        void ApplyTargetMode()
        {
            var selected = categoryOptions.FirstOrDefault(x => x.IsChecked == true);
            var source = selected == null || selected.Tag == null ? null : googleSources.FirstOrDefault(x => "google:" + x.Id == selected.Tag.ToString());
            var task = source != null && GoogleTasks.IsSource(source.Id);
            timeCard.Visibility = task ? Visibility.Collapsed : Visibility.Visible;
            recurrenceCard.Visibility = task ? Visibility.Collapsed : Visibility.Visible;
            googleTaskHint.Visibility = task ? Visibility.Visible : Visibility.Collapsed;
            if (!task) return;
            allDay.IsChecked = true; multiDay.IsChecked = false; recurrenceEnabled.IsChecked = false;
            noRollover.IsChecked = true;
        }

        void ApplyDefaults(PlannerSettings defaults)
        {
            if (defaults == null) return;
            durationMinutes = Math.Max(1, defaults.DefaultDurationMinutes);
            var category = categoryOptions.FirstOrDefault(x => (x.Tag ?? "").ToString() == defaults.DefaultCalendarKey && x.IsEnabled);
            if (category != null) category.IsChecked = true;
            if (!defaults.DefaultAllDay)
            {
                var hour = Math.Max(0, Math.Min(23, defaults.DefaultStartHour));
                (hour < 12 ? morning : afternoon).IsChecked = true;
                foreach (var option in hourGrid.Children.OfType<RadioButton>()) option.IsChecked = (int)option.Tag == hour;
                var minute = new[] { 0, 10, 20, 30, 40, 50 }.OrderBy(x => Math.Abs(x - defaults.DefaultStartMinute)).First();
                foreach (var option in minuteGrid.Children.OfType<RadioButton>()) option.IsChecked = (int)option.Tag == minute;
            }
            var reminder = reminderOptions.Children.OfType<RadioButton>().FirstOrDefault(x => x.Tag is int && (int)x.Tag == defaults.DefaultReminderMinutes);
            if (reminder != null) reminder.IsChecked = true;
            else
            {
                customReminder.IsChecked = true; customReminderValue.Text = Math.Max(1, defaults.DefaultReminderMinutes).ToString(CultureInfo.InvariantCulture);
                customReminderUnit.SelectedIndex = 0;
            }
        }

        int SelectedReminderMinutes()
        {
            var selected = reminderOptions.Children.OfType<RadioButton>().First(x => x.IsChecked == true);
            if (selected != customReminder) return (int)selected.Tag;
            int value;
            if (!int.TryParse(customReminderValue.Text, out value)) value = 15;
            value = Math.Max(1, Math.Min(999, value));
            var unit = customReminderUnit.SelectedItem as ComboBoxItem;
            return value * (unit == null ? 1 : (int)unit.Tag);
        }

        static void DigitsOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(x => !char.IsDigit(x));
        }

        int SelectedRecurrenceCount()
        {
            if (recurrenceCountMode.IsChecked != true) return 0;
            int count;
            if (!int.TryParse(recurrenceCountValue.Text, out count)) count = 10;
            return Math.Max(2, Math.Min(500, count));
        }

        internal static bool UsesCompletionCheck(bool isAllDay, bool isLocal)
        {
            return !isAllDay || isLocal;
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

        static double CompactScrollHeight(double workAreaHeight)
        {
            return Math.Max(360, Math.Min(720, workAreaHeight * .78 - 64));
        }

        static TextBlock Header(string text, double size) { return new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 18) }; }
        static TextBlock Label(string text) { return new TextBlock { Text = text, Foreground = Brush("#475569"), FontSize = 12 }; }
        static void Round(Button button, double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }
        static void StyleInput(TextBox input)
        {
            input.Padding = new Thickness(10, 4, 10, 8);
            input.BorderBrush = Brush("#CBD5E1"); UiRound.StyleTextBox(input, 9);
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
        internal static void StyleCalendar(System.Windows.Controls.Calendar calendar)
        {
            OnharuCalendarStyle.Apply(calendar);
        }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }
}
