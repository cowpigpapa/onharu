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
    public class LocalImportWindow : Window
    {
        readonly List<Tuple<CheckBox, PlannerItem>> choices = new List<Tuple<CheckBox, PlannerItem>>();
        public List<PlannerItem> SelectedItems = new List<PlannerItem>();

        public LocalImportWindow(List<PlannerItem> localItems)
        {
            Title = "로컬 일정 가져오기"; Width = 500; Height = 520; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            panel.Children.Add(new TextBlock { Text = "로컬 일정 가져오기", FontSize = 21, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = "현재 계정으로 옮길 일정을 선택하세요.", Foreground = Brush("#64748B"), Margin = new Thickness(0, 5, 0, 12) });
            var list = new StackPanel();
            foreach (var item in localItems.OrderBy(x => x.Start))
            {
                var check = new CheckBox { Content = item.Start.ToString("yyyy.MM.dd") + "  ·  " + item.Title,
                    Margin = new Thickness(4, 6, 4, 6), FontSize = 13 };
                choices.Add(Tuple.Create(check, item)); list.Children.Add(check);
            }
            panel.Children.Add(new ScrollViewer { Content = list, Height = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var buttons = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = new Button { Content = "취소", Height = 42, Margin = new Thickness(0, 0, 5, 0), Background = Brush("#E2E8F0"), BorderThickness = new Thickness(0) };
            var import = new Button { Content = "선택 일정 가져오기", Height = 42, Margin = new Thickness(5, 0, 0, 0), Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            cancel.Click += delegate { DialogResult = false; };
            import.Click += delegate { SelectedItems = choices.Where(x => x.Item1.IsChecked == true).Select(x => x.Item2).ToList(); if (SelectedItems.Count > 0) DialogResult = true; };
            buttons.Children.Add(cancel); Grid.SetColumn(import, 1); buttons.Children.Add(import); panel.Children.Add(buttons);
            Content = new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel };
        }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
