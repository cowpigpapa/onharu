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
        public int CandidateCount { get { return choices.Count; } }

        public LocalImportWindow(List<PlannerItem> localItems, int googleExcluded = 0, List<PlannerItem> currentItems = null, bool externalCopies = false)
        {
            Title = "로컬 일정 가져오기"; Width = 500; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            panel.Children.Add(OnharuPopupChrome.Header(this, "⇧  로컬 일정 가져오기", "#4338CA"));
            panel.Children.Add(new TextBlock { Text = "현재 계정으로 옮길 일정을 선택하세요.", Foreground = Brush("#64748B"), Margin = new Thickness(0, 5, 0, 12) });
            if (googleExcluded > 0)
                panel.Children.Add(new TextBlock { Text = "Google 일정 " + googleExcluded + "개는 안전을 위해 제외했습니다.",
                    Foreground = Brush("#C2410C"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, -5, 0, 10) });
            if (externalCopies)
                panel.Children.Add(new TextBlock { Text = "CSV의 Google 일정도 선택하면 Google과 연결되지 않은 ONHARU 로컬 복사본으로 저장됩니다.",
                    Foreground = Brush("#C2410C"), FontWeight = FontWeights.SemiBold, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -5, 0, 10) });
            if (currentItems != null)
                panel.Children.Add(new TextBlock { Text = "신규는 현재 목록에 없는 일정이며, 이전에 삭제한 일정도 포함될 수 있습니다.",
                    Foreground = Brush("#64748B"), FontSize = 11, Margin = new Thickness(0, -4, 0, 9) });
            var selectAll = new CheckBox { Content = "전체 선택", FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#4338CA"), Margin = new Thickness(4, 0, 4, 8), Cursor = Cursors.Hand };
            selectAll.Checked += delegate { foreach (var choice in choices) choice.Item1.IsChecked = true; };
            selectAll.Unchecked += delegate { foreach (var choice in choices) choice.Item1.IsChecked = false; };
            panel.Children.Add(selectAll);
            var list = new StackPanel();
            foreach (var item in localItems.OrderBy(x => x.Start))
            {
                var current = currentItems == null ? null : currentItems.FirstOrDefault(x => x.Id == item.Id) ??
                    currentItems.FirstOrDefault(x => SameAnniversary(x, item));
                if (current != null)
                {
                    item.Id = current.Id;
                    if (!string.IsNullOrWhiteSpace(current.AnniversaryType) && string.IsNullOrWhiteSpace(item.AnniversaryType)) item.AnniversaryType = current.AnniversaryType;
                    if (!string.IsNullOrWhiteSpace(current.RecurrenceMode) && string.IsNullOrWhiteSpace(item.RecurrenceMode)) item.RecurrenceMode = current.RecurrenceMode;
                }
                if (current != null && SameContent(current, item)) continue;
                var status = current == null ? "신규" : "변경된 일정";
                var color = current == null ? "#047857" : "#C2410C";
                var row = new DockPanel();
                var badge = new Border { Background = Brush(current == null ? "#ECFDF5" : "#FFF7ED"), CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(8, 0, 0, 0),
                    Child = new TextBlock { Text = status, Foreground = Brush(color), FontSize = 10, FontWeight = FontWeights.SemiBold } };
                DockPanel.SetDock(badge, Dock.Right); row.Children.Add(badge);
                row.Children.Add(new TextBlock { Text = item.Start.ToString("yyyy.MM.dd") + "  ·  " + item.Title,
                    TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
                var check = new CheckBox { Content = row, Margin = new Thickness(4, 6, 4, 6), FontSize = 13 };
                choices.Add(Tuple.Create(check, item)); list.Children.Add(check);
            }
            var scroll = new ScrollViewer { Content = list, MaxHeight = 332, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Loaded += delegate { UiRound.SoftenScrollBars(scroll); }; panel.Children.Add(scroll);
            var buttons = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = OnharuPopupChrome.FooterButton("취소", "#E2E8F0", "#475569"); cancel.Margin = new Thickness(0, 0, 5, 0);
            var import = OnharuPopupChrome.FooterButton("선택 일정 가져오기", "#4F46E5", "#FFFFFF"); import.Margin = new Thickness(5, 0, 0, 0);
            cancel.Click += delegate { DialogResult = false; };
            import.Click += delegate { SelectedItems = choices.Where(x => x.Item1.IsChecked == true).Select(x => x.Item2).ToList(); if (SelectedItems.Count > 0) DialogResult = true; };
            buttons.Children.Add(cancel); Grid.SetColumn(import, 1); buttons.Children.Add(import); panel.Children.Add(buttons);
            Content = OnharuPopupChrome.Shell(panel);
        }

        static bool SameContent(PlannerItem left, PlannerItem right)
        {
            return Text(left.Title) == Text(right.Title) && left.Start == right.Start && left.End == right.End && left.AllDay == right.AllDay &&
                left.IsTodo == right.IsTodo && left.Completed == right.Completed && Text(left.Category) == Text(right.Category) && Text(left.Notes) == Text(right.Notes) &&
                left.Important == right.Important && left.ImportantBackgroundColor == right.ImportantBackgroundColor &&
                left.ImportantTextColor == right.ImportantTextColor && left.ShowDday == right.ShowDday && left.AnniversaryDate == right.AnniversaryDate &&
                Text(left.AnniversaryType) == Text(right.AnniversaryType) && Text(left.RecurrenceFrequency) == Text(right.RecurrenceFrequency) &&
                Text(left.RecurrenceMode) == Text(right.RecurrenceMode) && Text(left.RecurrenceDays) == Text(right.RecurrenceDays) &&
                left.RecurrenceUntil == right.RecurrenceUntil && left.RecurrenceCount == right.RecurrenceCount &&
                EffectiveReminder(left) == EffectiveReminder(right);
        }

        static bool SameAnniversary(PlannerItem left, PlannerItem right)
        {
            return left.AnniversaryDate.Year >= 1900 && right.AnniversaryDate.Year >= 1900 && Text(left.Title) == Text(right.Title) &&
                (string.IsNullOrWhiteSpace(left.AnniversaryType) || string.IsNullOrWhiteSpace(right.AnniversaryType) || Text(left.AnniversaryType) == Text(right.AnniversaryType)) &&
                left.AnniversaryDate.Date == right.AnniversaryDate.Date;
        }

        static string Text(string value) { return (value ?? "").Trim(); }
        static int EffectiveReminder(PlannerItem item) { return item.ReminderConfigured && item.ReminderMinutes >= 0 ? item.ReminderMinutes : -1; }

        static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    }
}
