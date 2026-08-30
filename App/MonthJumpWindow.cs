using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public class MonthJumpWindow : Window
    {
        public DateTime SelectedMonth;

        public MonthJumpWindow(DateTime current)
        {
            Title = "연·월 이동"; Width = 390; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24, 19, 24, 21) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var close = SmallClose(); close.Click += delegate { DialogResult = false; }; DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var heading = new StackPanel(); heading.Children.Add(new TextBlock { Text = "📅  원하는 달로 이동", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = B("#1E293B") });
            heading.Children.Add(new TextBlock { Text = "연도와 월을 고르면 달력이 바로 이동합니다.", FontSize = 11, Foreground = B("#7C3AED"), Margin = new Thickness(2, 3, 0, 0) });
            header.Children.Add(heading); panel.Children.Add(header);

            var selectedYear = current.Year;
            var yearText = new TextBox { Text = selectedYear.ToString(), Width = 108, Height = 34, MaxLength = 4,
                FontSize = 16, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center, Foreground = B("#312E81"), Background = Brushes.White,
                BorderBrush = B("#C7D2FE"), BorderThickness = new Thickness(1), Padding = new Thickness(4, 2, 4, 2),
                ToolTip = "연도를 직접 입력한 뒤 Tab을 누르거나 월을 선택하세요" };
            UiRound.StyleTextBox(yearText, 9);
            yearText.PreviewTextInput += delegate(object sender, TextCompositionEventArgs e) { e.Handled = e.Text.Any(x => !char.IsDigit(x)); };
            var yearButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            Action commitYear = delegate
            {
                int parsed;
                if (int.TryParse(yearText.Text.Trim(), out parsed) && parsed >= 1900 && parsed <= 9998)
                { selectedYear = parsed; yearText.Text = selectedYear.ToString(); yearText.BorderBrush = B("#C7D2FE"); }
                else
                { yearText.Text = selectedYear.ToString(); yearText.BorderBrush = B("#FB7185"); }
            };
            yearText.LostKeyboardFocus += delegate { commitYear(); };
            Action<int> changeYear = delegate(int amount)
            {
                commitYear(); selectedYear = Math.Max(1900, Math.Min(9998, selectedYear + amount)); yearText.Text = selectedYear.ToString();
            };
            foreach (var step in new[] { -10, -1 })
            {
                var amount = step; var button = ActionButton(step == -10 ? "≪" : "‹", "#EEF2FF", "#4338CA"); button.Width = 48; button.Margin = new Thickness(3, 0, 3, 0);
                button.ToolTip = step == -10 ? "10년 이전" : "이전 연도"; button.Click += delegate { changeYear(amount); }; yearButtons.Children.Add(button);
            }
            yearButtons.Children.Add(yearText);
            foreach (var step in new[] { 1, 10 })
            {
                var amount = step; var button = ActionButton(step == 10 ? "≫" : "›", "#EEF2FF", "#4338CA"); button.Width = 48; button.Margin = new Thickness(3, 0, 3, 0);
                button.ToolTip = step == 10 ? "10년 이후" : "다음 연도"; button.Click += delegate { changeYear(amount); }; yearButtons.Children.Add(button);
            }
            var yearPanel = new StackPanel(); yearPanel.Children.Add(Label("연도 선택")); yearPanel.Children.Add(yearButtons);
            panel.Children.Add(new Border { Background = B("#F8FAFC"), BorderBrush = B("#E2E8F0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(13, 10, 13, 12), Child = yearPanel });

            panel.Children.Add(Label("월 선택"));
            var months = new UniformGrid { Columns = 4, Margin = new Thickness(0, 1, 0, 0) };
            var selectedMonth = current.Month;
            for (var value = 1; value <= 12; value++)
            {
                var monthValue = value;
                var button = new RadioButton { Content = value + "월", IsChecked = value == current.Month, GroupName = "JumpMonth",
                    Height = 34, Margin = new Thickness(3), Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Center };
                StyleMonth(button); button.Checked += delegate { commitYear(); selectedMonth = monthValue; }; months.Children.Add(button);
            }
            panel.Children.Add(months);
            var actions = new Grid { Margin = new Thickness(0, 14, 0, 0) }; actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = ActionButton("취소", "#F1F5F9", "#475569"); cancel.Margin = new Thickness(0, 0, 5, 0); cancel.Click += delegate { DialogResult = false; }; actions.Children.Add(cancel);
            var move = ActionButton("✓  이동", "#4F46E5", "#FFFFFF"); move.Margin = new Thickness(5, 0, 0, 0); move.Click += delegate
            {
                commitYear();
                SelectedMonth = new DateTime(selectedYear, selectedMonth, 1); DialogResult = true;
            };
            Grid.SetColumn(move, 1); actions.Children.Add(move); panel.Children.Add(actions);
            Content = OnharuPopupChrome.Shell(panel);
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { move.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; } else if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; } };
        }

        static TextBlock Label(string text) { return new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = B("#475569"), Margin = new Thickness(2, 10, 0, 6) }; }
        static Button SmallClose()
        {
            var button = new Button { Content = "×", Width = 32, Height = 32, Background = B("#FFF1F2"), Foreground = B("#E11D48"), BorderBrush = B("#FECDD3"), BorderThickness = new Thickness(1), FontSize = 16, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, ToolTip = "닫기", HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            UiRound.Apply(button, 10); return button;
        }
        static Button ActionButton(string text, string background, string foreground)
        {
            var button = new Button { Content = text, Height = 40, Background = B(background), Foreground = B(foreground), BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Center };
            UiRound.Apply(button, 11); return button;
        }
        static void StyleMonth(RadioButton radio)
        {
            var border = new FrameworkElementFactory(typeof(Border)); border.Name = "MonthBorder"; border.SetValue(Border.BackgroundProperty, Brushes.White);
            border.SetValue(Border.BorderBrushProperty, B("#E2E8F0")); border.SetValue(Border.BorderThicknessProperty, new Thickness(1)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            var template = new ControlTemplate(typeof(RadioButton)) { VisualTree = border };
            var selected = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true }; selected.Setters.Add(new Setter(Border.BackgroundProperty, B("#EDE9FE"), "MonthBorder")); selected.Setters.Add(new Setter(Border.BorderBrushProperty, B("#8B5CF6"), "MonthBorder")); template.Triggers.Add(selected);
            radio.Template = template; radio.Foreground = B("#4338CA"); radio.FontWeight = FontWeights.SemiBold;
        }
        static Brush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
