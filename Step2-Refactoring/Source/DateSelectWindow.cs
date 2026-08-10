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
    public class DateSelectWindow : Window
    {
        public DateTime SelectedDate;

        public DateSelectWindow(DateTime current)
        {
            SelectedDate = current.Date; Title = "날짜 변경"; Width = 400; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 38, 14) };
            header.Children.Add(new TextBlock { Text = "📅  날짜 선택", FontSize = 21, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center }); panel.Children.Add(header);
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
            var selectedText = new TextBlock { Text = SelectedDate.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")),
                Foreground = Brush("#1D4ED8"), FontWeight = FontWeights.Bold, FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(selectedText);
            var calendarControl = new System.Windows.Controls.Calendar { SelectedDate = SelectedDate, DisplayDate = SelectedDate,
                SelectionMode = CalendarSelectionMode.SingleDate, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center,
                LayoutTransform = new ScaleTransform(1.05, 1.05), Margin = new Thickness(0, 2, 0, 10) };
            var dayStyle = new Style(typeof(CalendarDayButton));
            dayStyle.Setters.Add(new Setter(Control.FontSizeProperty, 14.0));
            dayStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(1.5)));
            dayStyle.Setters.Add(new Setter(Control.MinWidthProperty, 28.0));
            dayStyle.Setters.Add(new Setter(Control.MinHeightProperty, 26.0));
            calendarControl.CalendarDayButtonStyle = dayStyle;
            calendarControl.SelectedDatesChanged += delegate
            {
                if (calendarControl.SelectedDate.HasValue) { SelectedDate = calendarControl.SelectedDate.Value.Date;
                    selectedText.Text = SelectedDate.ToString("yyyy년 M월 d일 dddd", new CultureInfo("ko-KR")); }
            };
            calendarControl.PreviewMouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
            {
                if (HasDayButtonParent(e.OriginalSource as DependencyObject) && calendarControl.SelectedDate.HasValue)
                { SelectedDate = calendarControl.SelectedDate.Value.Date; DialogResult = true; e.Handled = true; }
            };
            panel.Children.Add(new Border { Background = Brush("#F8FAFC"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(10), Child = calendarControl });
            var confirmText = new TextBlock { Text = "✓  이 날짜 선택", Cursor = Cursors.Hand };
            var confirm = new Button { Content = confirmText, Width = 180, Height = 40, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.White,
                Background = Brush("#3977E8"), BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold,
                FontSize = 13, Margin = new Thickness(0, 12, 0, 0), Cursor = Cursors.Hand, ForceCursor = true };
            Round(confirm, 13); confirm.Click += delegate { DialogResult = true; }; panel.Children.Add(confirm);
            var close = new Button { Content = "×", Width = 32, Height = 32, Background = Brush("#FEE2E2"),
                Foreground = Brush("#DC2626"), BorderThickness = new Thickness(0), FontSize = 17 };
            Round(close, 10); close.Click += delegate { DialogResult = false; };
            var shell = new Border { Background = Brush("#FFF8FAFC"), BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = panel };
            var frame = new Grid(); frame.Children.Add(shell); close.HorizontalAlignment = HorizontalAlignment.Right;
            close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 8, 8, 0); Panel.SetZIndex(close, 10);
            frame.Children.Add(close); Content = frame;
        }

        static void Round(Button button, double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }
        static bool HasDayButtonParent(DependencyObject source)
        {
            while (source != null)
            {
                if (source is CalendarDayButton) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }
}
