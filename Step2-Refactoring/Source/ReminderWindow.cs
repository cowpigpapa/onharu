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
    public class ReminderWindow : Window
    {
        bool completed;
        public ReminderWindow(List<PlannerItem> due, Action<int?> complete)
        {
            Width = 410; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true; ShowActivated = false; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var content = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
            content.Children.Add(new TextBlock { Text = "✦  온하루 알림", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brush("#4338CA") });
            foreach (var item in due) content.Children.Add(new TextBlock { Text = (item.AllDay ? "오늘" : item.Start.ToString("HH:mm")) + "  ·  " + item.Title,
                FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 9, 0, 0), TextWrapping = TextWrapping.Wrap });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            foreach (var option in new[] { new { Name = "5분 뒤", Minutes = 5 }, new { Name = "10분 뒤", Minutes = 10 }, new { Name = "30분 뒤", Minutes = 30 } })
            {
                var button = new Button { Content = option.Name, Tag = option.Minutes, Height = 32, Width = 72, Margin = new Thickness(0, 0, 6, 0), Background = Brush("#E0E7FF"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0) };
                UiRound.Apply(button, 9); button.Click += delegate { if (!completed) { completed = true; complete((int)button.Tag); Close(); } }; actions.Children.Add(button);
            }
            var done = new Button { Content = "확인", Height = 32, Width = 64, Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            UiRound.Apply(done, 9); done.Click += delegate { if (!completed) { completed = true; complete(null); Close(); } }; actions.Children.Add(done); content.Children.Add(actions);
            Content = new Border { Background = Brush("#F7F7FF"), BorderBrush = Brush("#C7D2FE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = content };
            Closed += delegate { if (!completed) { completed = true; complete(null); } };
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
