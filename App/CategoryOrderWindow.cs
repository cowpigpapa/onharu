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
    public class CategoryOrderWindow : Window
    {
        readonly StackPanel list = new StackPanel(); readonly List<Tuple<string, string>> entries;
        ScrollViewer scroller;
        string selectedKey;
        public List<string> Result;
        public CategoryOrderWindow(List<Tuple<string, string>> values)
        {
            entries = values; Title = "카테고리 순서"; Width = 440; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            OnharuPopupChrome.EnableTopDrag(this);
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 4) };
            header.Children.Add(new TextBlock { Text = "☷  카테고리 표시 순서", FontSize = 21, FontWeight = FontWeights.Bold });
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }; panel.Children.Add(header);
            panel.Children.Add(new TextBlock { Text = "온하루와 Google 캘린더를 원하는 순서로 이동하세요.", Foreground = Brush("#64748B"), Margin = new Thickness(0, 3, 0, 10) });
            scroller = new ScrollViewer { Content = list, Height = Math.Min(288, Math.Max(48, entries.Count * 48)),
                VerticalScrollBarVisibility = entries.Count > 6 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            scroller.Loaded += delegate { UiRound.SoftenScrollBars(scroller); };
            panel.Children.Add(scroller);
            Render();
            var save = new Button { Content = "✓  순서 적용", Height = 42, Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 10, 0, 0) };
            UiRound.Apply(save, 12);
            save.Click += delegate { Result = entries.Select(x => x.Item1).ToList(); DialogResult = true; }; panel.Children.Add(save);
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"), Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            UiRound.Apply(close, 10);
            close.Click += delegate { DialogResult = false; };
            var frame = new Grid(); frame.Children.Add(UiRound.EmphasizePopup(new Border { Background = Brush("#FFFAFCFF"), CornerRadius = new CornerRadius(18), Child = panel }));
            close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); frame.Children.Add(close); Content = frame;
        }
        void Render()
        {
            list.Children.Clear();
            for (var i = 0; i < entries.Count; i++)
            {
                var index = i; var row = new Grid { Height = 42 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                var number = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(9), Background = Brush(entries[i].Item1.StartsWith("google:") ? "#DBEAFE" : "#FCE7F3"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = (i + 1).ToString(), Foreground = Brush(entries[i].Item1.StartsWith("google:") ? "#2563EB" : "#DB2777"), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                row.Children.Add(number);
                row.Children.Add(new TextBlock { Text = entries[i].Item2, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#312E81"), FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(2, 0, 5, 0) }); Grid.SetColumn(row.Children[row.Children.Count - 1], 1);
                var up = new Button { Content = "↑", IsEnabled = i > 0, Width = 28, Height = 28, BorderThickness = new Thickness(0), Background = Brush("#E0E7FF"), Foreground = Brush("#4338CA") };
                var down = new Button { Content = "↓", IsEnabled = i < entries.Count - 1, Width = 28, Height = 28, BorderThickness = new Thickness(0), Background = Brush("#E0E7FF"), Foreground = Brush("#4338CA") };
                UiRound.Apply(up, 9); UiRound.Apply(down, 9);
                up.Click += delegate { Move(index, -1); };
                down.Click += delegate { Move(index, 1); };
                Grid.SetColumn(up, 2); Grid.SetColumn(down, 3); row.Children.Add(up); row.Children.Add(down);
                var key = entries[i].Item1;
                var card = new Border { Tag = key, Child = row, Background = Brush(key == selectedKey ? "#FEF3C7" : "#F8FAFF"),
                    BorderBrush = Brush(key == selectedKey ? "#FCD34D" : "#E0E7FF"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(4, 0, 7, 0), Cursor = Cursors.Hand };
                card.MouseLeftButtonDown += delegate { Select(key); };
                list.Children.Add(card);
            }
        }
        void Select(string key)
        {
            selectedKey = key;
            foreach (var card in list.Children.OfType<Border>())
            {
                var selected = string.Equals(card.Tag as string, key, StringComparison.Ordinal);
                card.Background = Brush(selected ? "#FEF3C7" : "#F8FAFF");
                card.BorderBrush = Brush(selected ? "#FCD34D" : "#E0E7FF");
            }
        }
        void Move(int index, int direction)
        {
            var target = index + direction;
            if (target < 0 || target >= entries.Count) return;
            var previousOffset = scroller == null ? 0 : scroller.VerticalOffset;
            var value = entries[index]; selectedKey = value.Item1; entries.RemoveAt(index); entries.Insert(target, value); Render();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
            {
                UpdateLayout();
                if (scroller != null) scroller.ScrollToVerticalOffset(Math.Max(0, previousOffset + direction * 48));
                Mouse.Capture(null); Mouse.Synchronize();
            }));
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
