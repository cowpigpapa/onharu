using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public class AnniversaryWindow : Window
    {
        readonly TextBox name = new TextBox { Height = 40, FontSize = 14, Padding = new Thickness(11, 8, 11, 7), BorderBrush = new SolidColorBrush(Color.FromRgb(199,210,254)), BorderThickness = new Thickness(1), Background = Brushes.White };
        readonly WrapPanel types = new WrapPanel { Margin = new Thickness(0, 3, 0, 0) };
        readonly Button dateButton = new Button { Width = 42, Height = 38, Content = "▦", FontSize = 17,
            Background = new SolidColorBrush(Color.FromRgb(238,242,255)), Foreground = new SolidColorBrush(Color.FromRgb(79,70,229)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(199,210,254)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
            ToolTip = "달력에서 기념일자 선택", HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        readonly TextBlock validation = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)), FontSize = 12, Margin = new Thickness(2, 6, 0, 0) };
        readonly CheckBox showDday = new CheckBox { Content = "다가오는 날짜를 D-Day로 표시", IsChecked = true, Margin = new Thickness(0, 10, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(71,85,105)) };
        readonly TextBox compactDate = new TextBox { Height = 38, MaxLength = 8, Padding = new Thickness(11, 7, 11, 6),
            FontSize = 14, BorderBrush = new SolidColorBrush(Color.FromRgb(199,210,254)), BorderThickness = new Thickness(1), Background = Brushes.White };
        readonly Calendar calendar = new Calendar();
        DateTime baseDate;
        public string AnniversaryTitle;
        public string AnniversaryType;
        public DateTime BaseDate;
        public bool ShowDday;
        public bool DeleteRequested;
        public bool ConvertToScheduleRequested;

        public AnniversaryWindow(PlannerItem existing)
        {
            Title = existing == null ? "기념일 등록" : "기념일 수정"; Width = 430; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            name.Text = existing == null ? "" : existing.Title; baseDate = existing != null && existing.AnniversaryDate.Year >= 1900 ? existing.AnniversaryDate : DateTime.Today;
            showDday.IsChecked = existing == null || existing.ShowDday;
            UiRound.StyleTextBox(name, 9); UiRound.StyleTextBox(compactDate, 9);
            UiRound.Apply(dateButton, 9);
            var currentType = string.IsNullOrWhiteSpace(existing == null ? null : existing.AnniversaryType) ? "birthday" : existing.AnniversaryType;
            if (currentType == "employment") currentType = "other";
            foreach (var option in new[] { Tuple.Create("생일", "birthday"), Tuple.Create("결혼기념일", "wedding"), Tuple.Create("기타", "other") })
            {
                var choice = new RadioButton { Content = option.Item1, Tag = option.Item2, GroupName = "AnniversaryType", IsChecked = currentType == option.Item2, Margin = new Thickness(0, 0, 7, 0), Cursor = Cursors.Hand };
                StyleChoice(choice); types.Children.Add(choice);
            }
            calendar.SelectedDate = baseDate; calendar.DisplayDate = baseDate; OnharuCalendarStyle.Apply(calendar);
            var datePopup = new Popup { PlacementTarget = dateButton, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 5,
                Child = OnharuCalendarStyle.PopupHost(calendar, 7) };
            dateButton.Click += delegate { datePopup.IsOpen = !datePopup.IsOpen; };
            calendar.SelectedDatesChanged += delegate { if (!calendar.SelectedDate.HasValue) return; baseDate = calendar.SelectedDate.Value.Date; compactDate.Text = baseDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture); validation.Text = ""; datePopup.IsOpen = false; };
            compactDate.Text = baseDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            compactDate.PreviewTextInput += delegate(object sender, TextCompositionEventArgs e) { e.Handled = e.Text.Any(x => !char.IsDigit(x)); };
            compactDate.LostKeyboardFocus += delegate { ParseDate(false); };
            name.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key != Key.Tab) return;
                e.Handled = true;
                Dispatcher.BeginInvoke(new Action(delegate { compactDate.Focus(); compactDate.SelectAll(); }));
            };

            var panel = new StackPanel { Margin = new Thickness(25, 20, 25, 21) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 13) };
            var close = Btn("×", 32, "#FFF1F2", "#E11D48"); close.Height = 32; close.FontSize = 16; close.FontWeight = FontWeights.Bold;
            close.BorderBrush = new SolidColorBrush(Color.FromRgb(254,205,211)); close.BorderThickness = new Thickness(1); close.ToolTip = "닫기";
            close.HorizontalContentAlignment = HorizontalAlignment.Center; close.VerticalContentAlignment = VerticalAlignment.Center; close.Padding = new Thickness(0,0,0,2);
            close.Click += delegate { DialogResult = false; }; DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var title = new StackPanel(); title.Children.Add(new TextBlock { Text = "✦  " + Title, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(30,41,59)) });
            title.Children.Add(new TextBlock { Text = "최대 10개 · ONHARU 로컬 저장", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(219,39,119)), Margin = new Thickness(3,3,0,0) }); header.Children.Add(title); panel.Children.Add(header);
            var form = new StackPanel();
            form.Children.Add(FieldLabel("기념일 종류", "생일 · 결혼 · 입사일 등을 구분합니다.")); form.Children.Add(types);
            form.Children.Add(FieldLabel("기념일 이름", "오른쪽 목록에 표시할 이름입니다.")); form.Children.Add(name);
            form.Children.Add(FieldLabel("기념일자", "YYYYMMDD 8자리로 입력하거나 오른쪽 달력에서 선택하세요."));
            var dateRow = new Grid(); dateRow.ColumnDefinitions.Add(new ColumnDefinition()); dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            compactDate.ToolTip = "예: 19760916"; dateRow.Children.Add(compactDate); Grid.SetColumn(dateButton, 1); dateButton.Margin = new Thickness(6, 0, 0, 0); dateRow.Children.Add(dateButton);
            form.Children.Add(dateRow); form.Children.Add(showDday);
            panel.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(248,250,252)), BorderBrush = new SolidColorBrush(Color.FromRgb(224,231,255)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(16,13,16,15), Child = form });
            panel.Children.Add(validation);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var cancel = Btn("취소", 76, "#F1F5F9", "#475569"); cancel.Click += delegate { DialogResult = false; };
            if (existing != null)
            {
                var convert = Btn("일정으로 변경", 104, "#ECFDF5", "#047857"); convert.Margin = new Thickness(0,0,8,0); convert.Click += ConvertToSchedule; buttons.Children.Add(convert);
                var delete = Btn("삭제", 72, "#FFF1F2", "#BE123C"); delete.Margin = new Thickness(0,0,8,0); delete.Click += delegate { DeleteRequested = true; DialogResult = true; }; buttons.Children.Add(delete);
            }
            var save = Btn("✓  저장", 96, "#4338CA", "#FFFFFF"); save.Margin = new Thickness(8, 0, 0, 0); save.Click += Save; buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
            Content = OnharuPopupChrome.Shell(panel);
        }

        void Save(object sender, RoutedEventArgs e)
        {
            if (!CollectValues()) return; DialogResult = true;
        }
        void ConvertToSchedule(object sender, RoutedEventArgs e)
        {
            if (!CollectValues()) return; ConvertToScheduleRequested = true; DialogResult = true;
        }
        bool CollectValues()
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { validation.Text = "기념일 이름을 입력해 주세요."; name.Focus(); return false; }
            if (!ParseDate(true)) return false;
            AnniversaryTitle = name.Text.Trim(); AnniversaryType = types.Children.OfType<RadioButton>().First(x => x.IsChecked == true).Tag.ToString();
            BaseDate = baseDate; ShowDday = showDday.IsChecked == true; return true;
        }
        bool ParseDate(bool focusOnError)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(compactDate.Text.Trim(), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out parsed) || parsed.Year < 1900)
            {
                validation.Text = "기념일자를 YYYYMMDD 8자리로 정확히 입력해 주세요. 예: 19760916";
                compactDate.BorderBrush = new SolidColorBrush(Color.FromRgb(251,113,133));
                if (focusOnError) { compactDate.SelectAll(); compactDate.Focus(); }
                return false;
            }
            baseDate = parsed.Date; validation.Text = ""; compactDate.BorderBrush = new SolidColorBrush(Color.FromRgb(199,210,254));
            calendar.SelectedDate = baseDate; calendar.DisplayDate = baseDate; return true;
        }
        static StackPanel FieldLabel(string label, string help)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 6) };
            panel.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(51,65,85)) });
            panel.Children.Add(new TextBlock { Text = help, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(148,163,184)), Margin = new Thickness(0,2,0,0) }); return panel;
        }
        static void StyleChoice(RadioButton radio)
        {
            var border = new FrameworkElementFactory(typeof(Border)); border.Name = "ChoiceBorder";
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255,255,255))); border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(216,180,254)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9)); border.SetValue(Border.PaddingProperty, new Thickness(13,7,13,7));
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); border.AppendChild(content);
            var template = new ControlTemplate(typeof(RadioButton)) { VisualTree = border };
            var checkedTrigger = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(250,232,255)), "ChoiceBorder")); checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(192,132,252)), "ChoiceBorder"));
            template.Triggers.Add(checkedTrigger); radio.Template = template; radio.Foreground = new SolidColorBrush(Color.FromRgb(126,34,206)); radio.FontWeight = FontWeights.SemiBold;
        }
        static Button Btn(string text, double width, string background, string foreground)
        {
            var b = new Button { Content = text, Width = width, Height = 34, Background = (Brush)new BrushConverter().ConvertFrom(background), Foreground = (Brush)new BrushConverter().ConvertFrom(foreground), BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center }; UiRound.Apply(b, 9); return b;
        }
    }
}
