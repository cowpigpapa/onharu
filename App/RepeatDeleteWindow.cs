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
    public class RepeatDeleteWindow : Window
    {
        public string Scope = "single";
        public RepeatDeleteWindow(PlannerItem item)
        {
            Title = "반복 일정 삭제"; Width = 390; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel { Margin = new Thickness(24, 21, 24, 22) };
            panel.Children.Add(new TextBlock { Text = "반복 일정 삭제", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = B("#1E293B") });
            panel.Children.Add(new TextBlock { Text = "‘" + item.Title + "’의 삭제 범위를 선택해 주세요.", FontSize = 12, Foreground = B("#64748B"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 17) });
            AddChoice(panel, "이번 일정만", "선택한 하루만 삭제합니다.", "single", "#EFF6FF", "#2563EB");
            AddChoice(panel, "이번 일정부터 미래", "지난 기록은 남기고 이후 반복을 종료합니다.", "future", "#FFF7ED", "#EA580C");
            AddChoice(panel, "과거 포함 전체", "이 반복 일정의 모든 기록을 삭제합니다.", "all", "#FFF1F2", "#E11D48");
            var cancel = new Button { Content = "취소", Height = 38, Margin = new Thickness(0, 8, 0, 0), Background = B("#F1F5F9"), Foreground = B("#475569"), BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            cancel.Click += delegate { DialogResult = false; }; panel.Children.Add(cancel);
            var shell = new Border { Background = B("#FFFDFD"), BorderBrush = B("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(17), Child = panel };
            Content = UiRound.EmphasizePopup(shell); panel.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (e.GetPosition(panel).Y < 58) DragMove(); };
        }
        void AddChoice(Panel panel, string title, string description, string scope, string background, string foreground)
        {
            var text = new StackPanel(); text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 14, Foreground = B(foreground) });
            text.Children.Add(new TextBlock { Text = description, FontSize = 11, Foreground = B("#64748B"), Margin = new Thickness(0, 3, 0, 0) });
            var button = new Button { Content = text, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 8), Background = B(background), BorderBrush = B(foreground), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
            button.Click += delegate { Scope = scope; DialogResult = true; }; panel.Children.Add(button);
        }
        static Brush B(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
    }
}
