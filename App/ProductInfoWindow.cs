using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class ProductInfoWindow : Window
    {
        public ProductInfoWindow()
        {
            Title = "ONHARU 제품 정보"; Width = 440; SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel { Margin = new Thickness(28, 16, 28, 24) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
            var close = OnharuPopupChrome.CloseButton(this); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(new TextBlock { Text = "온하루 · ONHARU", FontSize = 25, FontWeight = FontWeights.Bold,
                Foreground = Brush("#312E81"), VerticalAlignment = VerticalAlignment.Center });
            OnharuPopupChrome.EnableDrag(this, header); panel.Children.Add(header);
            panel.Children.Add(new TextBlock { Text = "바탕화면에서 시작하는 나의 하루", FontSize = 14,
                Foreground = Brush("#6366F1"), Margin = new Thickness(0, 0, 0, 17) });
            panel.Children.Add(Card("버전", "2.2.3"));
            panel.Children.Add(Card("데이터", "로컬 일정은 내 PC에 저장 · Google 일정은 Google에서 관리"));
            panel.Children.Add(Card("주요 기능", "바탕화면 달력 · Google 동기화 · Todo · 반복 일정 · D-Day · 기념일"));
            panel.Children.Add(new TextBlock { Text = "개인 명의 프리웨어 · 광고 및 유료 기능 잠금 없음\nMADE BY JUAN.HJLEE",
                Foreground = Brush("#64748B"), FontSize = 11.5, TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 17, 0, 0) });
            Content = OnharuPopupChrome.Shell(panel);
        }

        static Border Card(string title, string body)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = title, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush("#7C3AED") });
            panel.Children.Add(new TextBlock { Text = body, FontSize = 12.5, Foreground = Brush("#334155"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
            return new Border { Background = Brushes.White, BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(13, 10, 13, 10), Margin = new Thickness(0, 0, 0, 7), Child = panel };
        }
        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
