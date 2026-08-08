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
    public class BackupWindow : Window
    {
        public string SelectedPath;
        public BackupWindow(string[] files)
        {
            Title = "백업 복원"; Width = 430; Height = 500; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 4) };
            header.Children.Add(new TextBlock { Text = "↶  백업 복원", FontSize = 21, FontWeight = FontWeights.Bold });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; panel.Children.Add(header);
            panel.Children.Add(new TextBlock { Text = "복원할 날짜를 선택하세요 · 최근 30일 보관", Foreground = Brush("#64748B"), Margin = new Thickness(0, 3, 0, 12) });
            var list = new StackPanel();
            foreach (var file in files.Take(30))
            {
                var name = Path.GetFileNameWithoutExtension(file); var date = name.Substring(name.Length - 8);
                var button = new Button { Content = "↶   " + date.Substring(0, 4) + "년 " + date.Substring(4, 2) + "월 " + date.Substring(6, 2) + "일 백업", Tag = file,
                    Height = 42, Margin = new Thickness(0, 3, 0, 3), Background = Brush("#EEF2FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 0, 0, 0), Cursor = Cursors.Hand };
                UiRound.Apply(button, 10);
                button.Click += delegate { SelectedPath = button.Tag.ToString(); DialogResult = true; }; list.Children.Add(button);
            }
            panel.Children.Add(new ScrollViewer { Content = list, Height = 390, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            UiRound.Apply(close, 10);
            close.Click += delegate { DialogResult = false; };
            var frame = new Grid(); frame.Children.Add(new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel });
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
