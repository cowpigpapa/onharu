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
    public class PendingSyncWindow : Window
    {
        public PendingSyncWindow(List<PlannerItem> pending)
        {
            Title = "동기화 대기"; Width = 440; MaxHeight = 560; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel { Margin = new Thickness(25, 22, 25, 21) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 38, 17) };
            header.Children.Add(new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(13), Background = B("#EEF2FF"),
                Child = new TextBlock { Text = "G", Foreground = B("#4F46E5"), FontSize = 20, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            var heading = new StackPanel { Margin = new Thickness(12, 1, 0, 0) };
            heading.Children.Add(new TextBlock { Text = "동기화 대기 일정", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = B("#1E293B") });
            heading.Children.Add(new TextBlock { Text = "Google Calendar에 아직 반영되지 않은 일정", FontSize = 11, Foreground = B("#64748B"), Margin = new Thickness(0, 3, 0, 0) });
            header.Children.Add(heading); panel.Children.Add(header);
            var status = new Border { Background = pending.Count == 0 ? B("#F0FDF4") : B("#FFF7ED"), CornerRadius = new CornerRadius(11),
                BorderBrush = pending.Count == 0 ? B("#BBF7D0") : B("#FED7AA"), BorderThickness = new Thickness(1), Padding = new Thickness(13, 10, 13, 10), Margin = new Thickness(0, 0, 0, 13),
                Child = new TextBlock { Text = pending.Count == 0 ? "✓  모든 일정이 동기화되었습니다." : "●  동기화를 기다리는 일정이 " + pending.Count + "개 있습니다.",
                    Foreground = pending.Count == 0 ? B("#15803D") : B("#C2410C"), FontSize = 12, FontWeight = FontWeights.SemiBold } };
            panel.Children.Add(status);
            var list = new StackPanel();
            foreach (var item in pending)
            {
                var card = new Grid(); card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(53) }); card.ColumnDefinitions.Add(new ColumnDefinition());
                var date = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(10), Background = B("#EEF2FF"), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = item.Start.ToString("MM.dd"), Foreground = B("#4338CA"), FontSize = 12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                card.Children.Add(date);
                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock { Text = item.Title, Foreground = B("#1E293B"), FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                info.Children.Add(new TextBlock { Text = (item.AllDay ? "하루 종일" : item.Start.ToString("HH:mm")) + "  ·  " + (item.GoogleCalendarName ?? "Google 캘린더"), Foreground = B("#64748B"), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
                Grid.SetColumn(info, 1); card.Children.Add(info);
                list.Children.Add(new Border { Background = B("#F8FAFC"), BorderBrush = B("#E2E8F0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 9, 12, 9), Margin = new Thickness(0, 0, 0, 8), Child = card });
            }
            panel.Children.Add(new ScrollViewer { Content = list, MaxHeight = 270, VerticalScrollBarVisibility = pending.Count > 4 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled });
            panel.Children.Add(new Border { Background = B("#EEF2FF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 4, 0, 0),
                Child = new TextBlock { Text = "재시도하려면 이 창을 닫고 상단의 G 동기화를 눌러 주세요.", Foreground = B("#4338CA"), FontSize = 11, TextAlignment = TextAlignment.Center } });
            var shell = new Border { Background = B("#FFFCFD"), BorderBrush = B("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel };
            var frame = new Grid(); frame.Children.Add(shell);
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = B("#FEE2E2"), Foreground = B("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17, Cursor = Cursors.Hand };
            UiRound.Apply(close, 10); close.Click += delegate { DialogResult = false; }; close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
            header.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
        }
        static Brush B(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
    }
}
