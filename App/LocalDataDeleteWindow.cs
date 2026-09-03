using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public sealed class LocalDataDeleteEntry
    {
        public PlannerItem Item;
        public string Key;
        public bool GoogleDdayOnly;
        public bool Matches(PlannerItem item)
        {
            if (GoogleDdayOnly) return Store.IsGoogleItem(item) && item.ShowDday && GoogleKey(item) == Key;
            return !Store.IsGoogleItem(item) && LocalKey(item) == Key;
        }
        public static string LocalKey(PlannerItem item) { return !string.IsNullOrWhiteSpace(item.SeriesId) ? "s:" + item.SeriesId : "i:" + item.Id; }
        public static string GoogleKey(PlannerItem item) { return !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId) ? "r:" + item.GoogleRecurringEventId : !string.IsNullOrWhiteSpace(item.GoogleEventId) ? "e:" + item.GoogleEventId : "i:" + item.Id; }
    }

    public class LocalDataDeleteWindow : Window
    {
        readonly List<Tuple<CheckBox, LocalDataDeleteEntry>> rows = new List<Tuple<CheckBox, LocalDataDeleteEntry>>();
        readonly Dictionary<CheckBox, Border> rowCards = new Dictionary<CheckBox, Border>();
        public List<LocalDataDeleteEntry> SelectedEntries = new List<LocalDataDeleteEntry>();

        public LocalDataDeleteWindow(List<PlannerItem> source)
        {
            Title = "일정 삭제"; Width = 570; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24, 18, 24, 16) };
            panel.Children.Add(OnharuPopupChrome.Header(this, "🗑  일정 삭제", "#9F1239"));
            panel.Children.Add(new TextBlock { Text = "삭제할 로컬 일정과 ONHARU 전용 항목을 선택하세요.", Foreground = Brush("#64748B"), Margin = new Thickness(0, 5, 0, 3) });
            panel.Children.Add(new TextBlock { Text = "Google D-Day는 Google 일정 원본을 남기고 ONHARU D-Day 설정만 제거합니다.", Foreground = Brush("#C2410C"), FontSize = 11, Margin = new Thickness(0, 0, 0, 11) });

            var entries = BuildEntries(source);
            var categoryRow = new WrapPanel { Margin = new Thickness(0, 1, 2, 8) }; var categoryFilters = new List<RadioButton>();
            foreach (var category in new[] { "전체 카테고리", "업무일정", "개인일정", "야구", "기념일", "Google D-Day 설정" })
            {
                var caption = category == "전체 카테고리" ? "전체" : category == "업무일정" ? "업무" : category == "개인일정" ? "개인" : category;
                var categoryFilter = new RadioButton { Content = caption, Tag = category, GroupName = "DeleteCategoryFilter", Margin = new Thickness(2, 0, 15, 0), Foreground = Brush("#475569"), Cursor = Cursors.Hand, IsChecked = category == "전체 카테고리" };
                categoryFilters.Add(categoryFilter); categoryRow.Children.Add(categoryFilter);
            }
            panel.Children.Add(categoryRow);
            var selectionHeader = new DockPanel { Margin = new Thickness(2, 5, 4, 7) };
            var countText = new TextBlock { Text = entries.Count + "개 관리 가능", Foreground = Brush("#4F46E5"), FontSize = 10.5 };
            var count = new Border { Background = Brush("#EEF2FF"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2, 8, 2),
                Child = countText };
            DockPanel.SetDock(count, Dock.Right); selectionHeader.Children.Add(count);
            var all = new CheckBox { Content = "전체 선택", Foreground = Brush("#4338CA"), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
            all.Checked += delegate { foreach (var row in rows.Where(x => rowCards[x.Item1].Visibility == Visibility.Visible)) row.Item1.IsChecked = true; };
            all.Unchecked += delegate { foreach (var row in rows.Where(x => rowCards[x.Item1].Visibility == Visibility.Visible)) row.Item1.IsChecked = false; };
            selectionHeader.Children.Add(all); panel.Children.Add(selectionHeader);
            var list = new StackPanel();
            foreach (var entry in entries)
            {
                var item = entry.Item;
                var kind = entry.GoogleDdayOnly ? "Google D-Day 설정" : !string.IsNullOrWhiteSpace(item.AnniversaryType) ? "기념일" : item.Category;
                var info = new StackPanel();
                info.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, Foreground = Brush("#1E293B"), TextTrimming = TextTrimming.CharacterEllipsis });
                info.Children.Add(new TextBlock { Text = kind + "  ·  " + (item.AnniversaryDate.Year >= 1900 && !string.IsNullOrWhiteSpace(item.AnniversaryType) ? item.AnniversaryDate : item.Start).ToString("yyyy.MM.dd"), FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(0, 2, 0, 0) });
                var check = new CheckBox { Content = info, Padding = new Thickness(2), Cursor = Cursors.Hand, VerticalContentAlignment = VerticalAlignment.Center };
                rows.Add(Tuple.Create(check, entry));
                var card = new Border { Background = Brushes.White, BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(2, 3, 5, 3), Child = check }; rowCards[check] = card; list.Children.Add(card);
            }
            foreach (var filter in categoryFilters) filter.Checked += delegate(object sender, RoutedEventArgs e)
            {
                var selectedCategory = (string)((RadioButton)sender).Tag; all.IsChecked = false; var visible = 0;
                foreach (var row in rows) { row.Item1.IsChecked = false; var show = selectedCategory == "전체 카테고리" || EntryKind(row.Item2) == selectedCategory; rowCards[row.Item1].Visibility = show ? Visibility.Visible : Visibility.Collapsed; if (show) visible++; }
                countText.Text = visible + "개 표시 중";
            };
            if (entries.Count == 0) list.Children.Add(new TextBlock { Text = "삭제할 ONHARU 데이터가 없습니다.", Foreground = Brush("#94A3B8"), TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 45, 0, 0) });
            var scroll = new ScrollViewer { Content = list, Height = 285, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            panel.Children.Add(new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(6, 5, 2, 5), Child = scroll });
            var buttons = new Grid { Margin = new Thickness(0, 12, 0, 0) }; buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = OnharuPopupChrome.FooterButton("취소", "#E2E8F0", "#475569"); cancel.Margin = new Thickness(0, 0, 5, 0); cancel.Click += delegate { DialogResult = false; };
            var delete = OnharuPopupChrome.FooterButton("🗑  선택 일정 삭제", "#FFF1F2", "#BE123C"); delete.BorderBrush = Brush("#FECDD3"); delete.Margin = new Thickness(5, 0, 0, 0); delete.IsEnabled = entries.Count > 0;
            delete.Click += delegate
            {
                SelectedEntries = rows.Where(x => x.Item1.IsChecked == true).Select(x => x.Item2).ToList();
                if (SelectedEntries.Count == 0) return;
                var confirm = new LocalDeleteConfirmWindow("선택한 " + SelectedEntries.Count + "개 항목을 삭제할까요?", "삭제 직전 복구용 백업을 별도로 저장합니다.") { Owner = this };
                if (confirm.ShowDialog() == true) DialogResult = true;
            };
            buttons.Children.Add(cancel); Grid.SetColumn(delete, 1); buttons.Children.Add(delete); panel.Children.Add(buttons);
            Content = OnharuPopupChrome.Shell(panel);
            Loaded += delegate { UiRound.SoftenScrollBars(scroll); };
        }

        static List<LocalDataDeleteEntry> BuildEntries(List<PlannerItem> source)
        {
            var local = source.Where(x => !Store.IsGoogleItem(x)).GroupBy(LocalDataDeleteEntry.LocalKey).Select(x => new LocalDataDeleteEntry { Item = x.OrderBy(y => y.Start).First(), Key = x.Key });
            var googleDday = source.Where(x => Store.IsGoogleItem(x) && x.ShowDday).GroupBy(LocalDataDeleteEntry.GoogleKey).Select(x => new LocalDataDeleteEntry { Item = x.OrderBy(y => y.Start).First(), Key = x.Key, GoogleDdayOnly = true });
            return local.Concat(googleDday).OrderBy(x => x.Item.Start).ThenBy(x => x.Item.Title).ToList();
        }

        static string EntryKind(LocalDataDeleteEntry entry) { return entry.GoogleDdayOnly ? "Google D-Day 설정" : !string.IsNullOrWhiteSpace(entry.Item.AnniversaryType) ? "기념일" : entry.Item.Category; }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }

    sealed class LocalDeleteConfirmWindow : Window
    {
        public LocalDeleteConfirmWindow(string title, string message)
        {
            Title = "삭제 확인"; Width = 390; Height = 205; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24, 18, 24, 20) };
            panel.Children.Add(OnharuPopupChrome.Header(this, "!  삭제 확인", "#9F1239"));
            panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brush("#9F1239"), TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = message, Foreground = Brush("#64748B"), Margin = new Thickness(0, 7, 0, 18) });
            var buttons = new Grid(); buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = OnharuPopupChrome.FooterButton("취소", "#E2E8F0", "#475569"); cancel.Margin = new Thickness(0, 0, 5, 0); cancel.Click += delegate { DialogResult = false; };
            var remove = OnharuPopupChrome.FooterButton("삭제", "#FFF1F2", "#BE123C"); remove.BorderBrush = Brush("#FECDD3"); remove.Margin = new Thickness(5, 0, 0, 0); remove.Click += delegate { DialogResult = true; };
            buttons.Children.Add(cancel); Grid.SetColumn(remove, 1); buttons.Children.Add(remove); panel.Children.Add(buttons);
            Content = OnharuPopupChrome.Shell(panel);
        }
        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
