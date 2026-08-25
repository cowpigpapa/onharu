using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FamilyPlanner
{
    internal sealed class OnharuSegmentedSwitch : Border
    {
        readonly Border thumb;
        readonly TranslateTransform shift = new TranslateTransform();
        readonly List<Button> buttons = new List<Button>();
        readonly double[] widths;
        readonly Action<int> changed;
        internal event Action<int, bool> Clicked;
        int selectedIndex;
        Brush selectedForeground = Brushes.White;

        internal OnharuSegmentedSwitch(string[] labels, double[] segmentWidths, int selected, Action<int> onChanged)
        {
            widths = new double[labels.Length];
            for (var i = 0; i < labels.Length; i++) widths[i] = Math.Max(segmentWidths[i], LabelWidth(labels[i]) + 4);
            changed = onChanged; selectedIndex = selected;
            Height = 26; CornerRadius = new CornerRadius(10); BorderThickness = new Thickness(1);
            BorderBrush = Brush("#C7D2FE"); Background = Brush("#F8FAFC"); Padding = new Thickness(1); ClipToBounds = true;
            var grid = new Grid(); var canvas = new Canvas { Height = 22, VerticalAlignment = VerticalAlignment.Center }; grid.Children.Add(canvas);
            thumb = new Border { Height = 22, Width = widths[selected], CornerRadius = new CornerRadius(8), Background = Brush("#4F46E5"), RenderTransform = shift };
            canvas.Children.Add(thumb);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 22, VerticalAlignment = VerticalAlignment.Center };
            for (var i = 0; i < labels.Length; i++)
            {
                var index = i; var button = new Button { Content = labels[i], Width = widths[i], Height = 22, Padding = new Thickness(2, 0, 2, 0),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 12.5, FontWeight = FontWeights.SemiBold };
                var buttonBorder = new FrameworkElementFactory(typeof(Border));
                buttonBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                var content = new FrameworkElementFactory(typeof(ContentPresenter));
                content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
                buttonBorder.AppendChild(content);
                button.Template = new ControlTemplate(typeof(Button)) { VisualTree = buttonBorder };
                button.Click += delegate
                {
                    var wasSelected = index == selectedIndex;
                    if (!wasSelected) { SetSelected(index, true); if (changed != null) changed(index); }
                    if (Clicked != null) Clicked(index, wasSelected);
                };
                buttons.Add(button); row.Children.Add(button);
            }
            grid.Children.Add(row); Child = grid; SetSelected(selected, false);
        }

        internal void SetSelected(int index, bool animate)
        {
            if (index < 0 || index >= widths.Length) return; selectedIndex = index;
            var left = 0.0; for (var i = 0; i < index; i++) left += widths[i];
            thumb.Width = widths[index];
            if (animate) shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(left, TimeSpan.FromMilliseconds(130)) { EasingFunction = new QuadraticEase() });
            else { shift.BeginAnimation(TranslateTransform.XProperty, null); shift.X = left; }
            for (var i = 0; i < buttons.Count; i++) buttons[i].Foreground = i == selectedIndex ? selectedForeground : Brush("#64748B");
        }

        internal void SetLabel(int index, string text) { if (index >= 0 && index < buttons.Count) buttons[index].Content = text; }
        internal void SetAccent(string background) { thumb.Background = Brush(background); }
        internal void SetAccent(string background, string foreground)
        {
            thumb.Background = Brush(background); selectedForeground = Brush(foreground); SetSelected(selectedIndex, false);
        }
        internal void SetAccent(Brush background, Brush foreground)
        {
            thumb.Background = background; selectedForeground = foreground; SetSelected(selectedIndex, false);
        }
        internal int SelectedIndex { get { return selectedIndex; } }
        internal double SegmentWidth(int index) { return index >= 0 && index < widths.Length ? widths[index] : 0; }
        internal FrameworkElement SegmentTarget(int index) { if (index >= 0 && index < buttons.Count) return buttons[index]; return this; }
        static double LabelWidth(string text)
        {
            var probe = new TextBlock { Text = text, FontSize = 12.5, FontWeight = FontWeights.SemiBold };
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return Math.Ceiling(probe.DesiredSize.Width);
        }
        static Brush Brush(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
    }
}
