using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FamilyPlanner
{
    public sealed class CalendarPrintWindow : Window
    {
        readonly Image preview;
        readonly ComboBox marginOption;
        public CalendarPrintWindow(Visual source)
        {
            Title = "달력 인쇄 미리보기"; Width = 820; Height = 650; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowActivated = true; ShowInTaskbar = false; Topmost = true;
            var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition());
            var header = new DockPanel { Margin = new Thickness(18, 14, 14, 10) };
            var close = OnharuPopupChrome.CloseButton(this); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var print = OnharuPopupChrome.Button("▣ 인쇄", 74, "#4F46E5", "#FFFFFF"); print.Margin = new Thickness(0, 0, 8, 0); print.Click += Print; DockPanel.SetDock(print, Dock.Right); header.Children.Add(print);
            marginOption = new ComboBox { Width = 132, Height = 30, Margin = new Thickness(0, 0, 8, 0), Background = Brushes.White,
                BorderBrush = Brush("#CBD5E1"), Foreground = Brush("#475569"), VerticalContentAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            marginOption.Items.Add(new ComboBoxItem { Content = "표준 여백 · 15mm", Tag = 15d });
            marginOption.Items.Add(new ComboBoxItem { Content = "좁은 여백 · 7mm", Tag = 7d });
            marginOption.Items.Add(new ComboBoxItem { Content = "여백 없음", Tag = 0d }); marginOption.SelectedIndex = 0;
            SettingsWindow.StyleComboBox(marginOption);
            DockPanel.SetDock(marginOption, Dock.Right); header.Children.Add(marginOption);
            header.Children.Add(new TextBlock { Text = "달력 인쇄 미리보기", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brush("#1E293B"), VerticalAlignment = VerticalAlignment.Center });
            OnharuPopupChrome.EnableDrag(this, header);
            root.Children.Add(header);
            preview = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(18, 0, 18, 18) };
            if (source != null)
            {
                var element = source as FrameworkElement; var width = Math.Max(1, (int)Math.Ceiling(element == null ? 1000 : element.ActualWidth));
                var height = Math.Max(1, (int)Math.Ceiling(element == null ? 700 : element.ActualHeight));
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(source); preview.Source = bitmap;
            }
            var paper = new Border { Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Margin = new Thickness(18, 0, 18, 18), Padding = new Thickness(12), Child = preview };
            Grid.SetRow(paper, 1); root.Children.Add(paper);
            Content = OnharuPopupChrome.Shell(root);
            Loaded += delegate { Activate(); Focus(); Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { Activate(); })); };
        }
        void Print(object sender, RoutedEventArgs e)
        {
            var dialog = new PrintDialog(); Topmost = false;
            if (dialog.ShowDialog() != true || preview.Source == null) { Topmost = true; Activate(); return; }
            var millimeters = marginOption.SelectedItem == null ? 15d : (double)((ComboBoxItem)marginOption.SelectedItem).Tag;
            var inset = millimeters * 96d / 25.4d;
            var width = Math.Max(1, dialog.PrintableAreaWidth - inset * 2); var height = Math.Max(1, dialog.PrintableAreaHeight - inset * 2);
            var image = new Image { Source = preview.Source, Stretch = Stretch.Uniform, Width = width, Height = height };
            image.Measure(new Size(width, height)); image.Arrange(new Rect(inset, inset, width, height));
            var page = new Canvas { Width = dialog.PrintableAreaWidth, Height = dialog.PrintableAreaHeight, Background = Brushes.White };
            page.Children.Add(image); Canvas.SetLeft(image, inset); Canvas.SetTop(image, inset);
            page.Measure(new Size(page.Width, page.Height)); page.Arrange(new Rect(new Size(page.Width, page.Height)));
            dialog.PrintVisual(page, "ONHARU 달력");
            Topmost = true; Activate();
        }
        static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
