using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace FamilyPlanner
{
    public class ExportWindow : Window
    {
        readonly List<PlannerItem> items;
        readonly TextBlock status = new TextBlock { Height = 22, Margin = new Thickness(2, 10, 2, 0), FontWeight = FontWeights.SemiBold };

        public ExportWindow(List<PlannerItem> source)
        {
            items = source ?? new List<PlannerItem>();
            Title = "일정 내보내기"; Width = 460; Height = 390; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24, 18, 24, 20) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 6) };
            header.Children.Add(new TextBlock { Text = "⇩  일정 내보내기", FontSize = 21, FontWeight = FontWeights.Bold });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; panel.Children.Add(header);
            panel.Children.Add(new TextBlock { Text = "현재 계정의 일정 " + items.Count + "개를 저장합니다", Foreground = Brush("#64748B"), Margin = new Thickness(0, 2, 0, 14) });
            panel.Children.Add(ExportButton("JSON  ·  온하루 백업", "일정과 동기화 정보를 그대로 보관", "json", "#EEF2FF", "#4338CA"));
            panel.Children.Add(ExportButton("CSV  ·  Excel에서 보기", "날짜·제목·카테고리·완료 여부를 표로 저장", "csv", "#ECFDF5", "#047857"));
            panel.Children.Add(ExportButton("ICS  ·  다른 캘린더로 이동", "Google·Outlook 등에서 가져올 수 있는 형식", "ics", "#FFF7ED", "#C2410C"));
            panel.Children.Add(status);
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17, Cursor = Cursors.Hand };
            UiRound.Apply(close, 10); close.Click += delegate { Close(); };
            var frame = new Grid(); frame.Children.Add(new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel });
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
        }

        Button ExportButton(string title, string description, string type, string background, string foreground)
        {
            var content = new StackPanel(); content.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 13 });
            content.Children.Add(new TextBlock { Text = description, FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(0, 2, 0, 0) });
            var button = new Button { Content = content, Tag = type, Height = 62, Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(15, 0, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brush(background), Foreground = Brush(foreground), BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            UiRound.Apply(button, 12); button.Click += Export; return button;
        }

        void Export(object sender, RoutedEventArgs e)
        {
            var type = ((Button)sender).Tag.ToString();
            var dialog = new Forms.SaveFileDialog { FileName = "ONHARU-일정-" + DateTime.Today.ToString("yyyyMMdd") + "." + type, Filter = type.ToUpperInvariant() + " 파일|*." + type, AddExtension = true };
            if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
            try
            {
                if (type == "json") ExportService.Json(dialog.FileName, items);
                else if (type == "csv") ExportService.Csv(dialog.FileName, items);
                else ExportService.Ics(dialog.FileName, items);
                ShowStatus("저장되었습니다", "#047857", 2000);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Export calendar data", ex); ShowStatus("저장하지 못했습니다", "#DC2626", UiRound.ErrorNoticeMilliseconds);
            }
        }

        void ShowStatus(string message, string color, int milliseconds)
        {
            status.Text = message; status.Foreground = Brush(color);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); if (status.Text == message) status.Text = ""; };
            timer.Start();
        }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
