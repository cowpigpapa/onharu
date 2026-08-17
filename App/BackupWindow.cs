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
        public BackupWindow(string[] localFiles, string[] externalFiles)
        {
            Title = "백업 복원"; Width = 430; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 4) };
            header.Children.Add(new TextBlock { Text = "↶  백업 복원", FontSize = 21, FontWeight = FontWeights.Bold });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; panel.Children.Add(header);
            panel.Children.Add(new TextBlock { Text = "복원할 위치와 날짜를 선택하세요 · 위치별 최근 30일 보관", Foreground = Brush("#64748B"), Margin = new Thickness(0, 3, 0, 12) });
            var list = new StackPanel();
            Action<string, string[], string> addGroup = delegate(string title, string[] files, string color)
            {
                list.Children.Add(new TextBlock { Text = title, Foreground = Brush(color), FontWeight = FontWeights.Bold,
                    FontSize = 12, Margin = new Thickness(2, list.Children.Count == 0 ? 0 : 12, 0, 5) });
                if (files.Length == 0)
                { list.Children.Add(new TextBlock { Text = "저장된 백업이 없습니다.", Foreground = Brush("#94A3B8"), Margin = new Thickness(4, 3, 0, 8) }); return; }
                foreach (var file in files.Take(30))
                {
                    var name = Path.GetFileNameWithoutExtension(file); var date = name.Substring(name.Length - 8);
                    var button = new Button { Content = "↶   " + date.Substring(0, 4) + "년 " + date.Substring(4, 2) + "월 " + date.Substring(6, 2) + "일 백업", Tag = file,
                        Height = 42, Margin = new Thickness(0, 3, 0, 3), Background = Brush(color == "#4338CA" ? "#EEF2FF" : "#ECFDF5"), Foreground = Brush(color),
                        BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 0, 0, 0), Cursor = Cursors.Hand,
                        ToolTip = file };
                    UiRound.Apply(button, 10);
                    button.Click += delegate { SelectedPath = button.Tag.ToString(); DialogResult = true; }; list.Children.Add(button);
                }
            };
            addGroup("기본 백업 폴더", localFiles ?? new string[0], "#4338CA");
            if (externalFiles != null && externalFiles.Length > 0) addGroup("지정 백업 폴더", externalFiles, "#047857");
            panel.Children.Add(new ScrollViewer { Content = list, MaxHeight = 390, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            UiRound.Apply(close, 10);
            close.Click += delegate { DialogResult = false; };
            var frame = new Grid(); frame.Children.Add(UiRound.EmphasizePopup(new Border { Background = Brush("#FFFAFCFF"), CornerRadius = new CornerRadius(18), Child = panel }));
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
