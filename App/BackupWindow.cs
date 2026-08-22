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
        public BackupWindow(string[] localFiles, string[] externalFiles, string localFolder)
        {
            Title = "백업 복원"; Width = 430; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            panel.Children.Add(OnharuPopupChrome.Header(this, "↶  백업 복원", "#4338CA"));
            panel.Children.Add(new TextBlock { Text = "복원할 백업 날짜를 선택하세요 · 최근 30일 보관", Foreground = Brush("#64748B"), Margin = new Thickness(0, 3, 0, 10) });
            var folderRow = new Grid();
            folderRow.ColumnDefinitions.Add(new ColumnDefinition()); folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var folderText = new TextBlock { Text = localFolder, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#475569"), FontSize = 11,
                ToolTip = localFolder, Margin = new Thickness(0, 0, 8, 0) };
            var folderButtonContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            folderButtonContent.Children.Add(new TextBlock { Text = "📁", FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
            folderButtonContent.Children.Add(new TextBlock { Text = "백업 폴더 열기", FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            var openFolder = new Button { Content = folderButtonContent, Height = 28, Padding = new Thickness(9, 0, 9, 0),
                Background = Brush("#F1F5F9"), Foreground = Brush("#4338CA"), BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            UiRound.Apply(openFolder, 9); openFolder.Click += delegate
            {
                try { Process.Start(new ProcessStartInfo { FileName = localFolder, UseShellExecute = true }); }
                catch (Exception ex) { ErrorLog.Write("Open backup folder", ex); }
            };
            folderRow.Children.Add(folderText); Grid.SetColumn(openFolder, 1); folderRow.Children.Add(openFolder);
            panel.Children.Add(new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(11, 5, 8, 5), Margin = new Thickness(0, 0, 0, 12), Child = folderRow });
            var list = new StackPanel();
            Button selectedButton = null;
            var restore = new Button { Content = "선택한 날짜로 복원", Height = 38, Margin = new Thickness(0, 12, 0, 0),
                Background = Brush("#4F46E5"), Foreground = Brushes.White, BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, IsEnabled = false, Opacity = .45 };
            UiRound.Apply(restore, 11);
            restore.Click += delegate { if (string.IsNullOrWhiteSpace(SelectedPath)) return; DialogResult = true; };
            Action<string, string[], string> addGroup = delegate(string title, string[] files, string color)
            {
                if (!string.IsNullOrWhiteSpace(title)) list.Children.Add(new TextBlock { Text = title, Foreground = Brush(color), FontWeight = FontWeights.Bold,
                    FontSize = 12, Margin = new Thickness(2, list.Children.Count == 0 ? 0 : 12, 0, 5) });
                if (files.Length == 0)
                { list.Children.Add(new TextBlock { Text = "저장된 백업이 없습니다.", Foreground = Brush("#94A3B8"), Margin = new Thickness(4, 3, 0, 8) }); return; }
                foreach (var file in files.Take(30))
                {
                    var baseBackground = color == "#4338CA" ? "#EEF2FF" : "#ECFDF5";
                    var button = new Button { Content = "↶  " + BackupLabel(file), Tag = file, DataContext = baseBackground,
                        Height = 42, Margin = new Thickness(0, 3, 0, 3), Background = Brush(baseBackground), Foreground = Brush(color),
                        BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(20, 0, 12, 0), Cursor = Cursors.Hand,
                        ToolTip = file };
                    UiRound.Apply(button, 10);
                    button.Click += delegate
                    {
                        if (selectedButton != null)
                        {
                            selectedButton.Background = Brush(selectedButton.DataContext.ToString());
                            selectedButton.BorderThickness = new Thickness(0);
                        }
                        selectedButton = button; SelectedPath = button.Tag.ToString();
                        button.Background = Brush("#E0E7FF"); button.BorderBrush = Brush("#6366F1"); button.BorderThickness = new Thickness(2);
                        restore.IsEnabled = true; restore.Opacity = 1;
                    };
                    list.Children.Add(button);
                }
            };
            addGroup("", localFiles ?? new string[0], "#4338CA");
            if (externalFiles != null && externalFiles.Length > 0) addGroup("지정 백업 폴더", externalFiles, "#047857");
            var listScroll = new ScrollViewer { Content = new Border { Padding = new Thickness(9, 6, 9, 8), Child = list },
                MaxHeight = 390, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            listScroll.Loaded += delegate { UiRound.SoftenScrollBars(listScroll); };
            panel.Children.Add(new Border { Background = Brushes.White, BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Child = listScroll });
            var buttons = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = OnharuPopupChrome.FooterButton("취소", "#E2E8F0", "#475569"); cancel.Margin = new Thickness(0, 0, 5, 0);
            cancel.Click += delegate { DialogResult = false; };
            restore.Margin = new Thickness(5, 0, 0, 0); Grid.SetColumn(restore, 1);
            buttons.Children.Add(cancel); buttons.Children.Add(restore); panel.Children.Add(buttons);
            Content = OnharuPopupChrome.Shell(panel);
        }
        internal static string BackupLabel(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file) ?? "";
            var marker = "-before-delete-";
            var markerIndex = name.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            DateTime value;
            if (markerIndex >= 0 && DateTime.TryParseExact(name.Substring(markerIndex + marker.Length), "yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
                return value.ToString("yyyy년 MM월 dd일 HH:mm") + " 삭제 전 안전 백업";
            var tail = name.Length >= 8 ? name.Substring(name.Length - 8) : "";
            if (DateTime.TryParseExact(tail, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
                return value.ToString("yyyy년 MM월 dd일") + " 백업";
            value = File.Exists(file) ? File.GetLastWriteTime(file) : DateTime.Today;
            return value.ToString("yyyy년 MM월 dd일 HH:mm") + " 백업";
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
