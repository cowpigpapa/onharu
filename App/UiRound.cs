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
    static class UiRound
    {
        public const int ErrorNoticeMilliseconds = 5000;

        public static void Apply(Button button, double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        public static void StyleTextBox(TextBox input, double radius = 9)
        {
            input.Background = Brushes.White;
            if (input.BorderBrush == null) input.BorderBrush = new SolidColorBrush(Color.FromRgb(199, 210, 254));
            input.BorderThickness = new Thickness(1);
            if (!input.AcceptsReturn)
            {
                var horizontal = input.Padding;
                input.Padding = new Thickness(horizontal.Left > 0 ? horizontal.Left : 8, 0,
                    horizontal.Right > 0 ? horizontal.Right : 8, 0);
                input.VerticalContentAlignment = VerticalAlignment.Center;
            }
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TextBox.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(TextBox.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(TextBox.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(TextBox.PaddingProperty));
            var host = new FrameworkElementFactory(typeof(ScrollViewer)); host.Name = "PART_ContentHost";
            if (!input.AcceptsReturn) host.SetValue(ScrollViewer.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(host);
            input.Template = new ControlTemplate(typeof(TextBox)) { VisualTree = border };
        }

        public static Border EmphasizePopup(Border shell)
        {
            shell.BorderBrush = Application.Current != null && Application.Current.Resources.Contains("OnharuPopupAccent")
                ? Application.Current.Resources["OnharuPopupAccent"] as Brush ?? new SolidColorBrush(Color.FromRgb(99, 102, 241))
                : new SolidColorBrush(Color.FromRgb(99, 102, 241));
            shell.BorderThickness = new Thickness(2);
            shell.Effect = new System.Windows.Media.Effects.DropShadowEffect
            { Color = Color.FromRgb(30, 41, 59), BlurRadius = 18, ShadowDepth = 5, Opacity = .38 };
            return shell;
        }

        public static void StyleContextMenu(ContextMenu menu)
        {
            menu.Background = new SolidColorBrush(Color.FromRgb(250, 250, 255));
            menu.BorderBrush = new SolidColorBrush(Color.FromRgb(129, 140, 248));
            menu.BorderThickness = new Thickness(1.5);
            menu.Padding = new Thickness(5);
            menu.FontFamily = new FontFamily("Malgun Gothic");
            menu.FontSize = 13;
            menu.HasDropShadow = true;

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ContextMenu.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(ContextMenu.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(ContextMenu.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(ContextMenu.PaddingProperty));
            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            border.AppendChild(items);
            menu.Template = new ControlTemplate(typeof(ContextMenu)) { VisualTree = border };

            var itemStyle = new Style(typeof(MenuItem));
            itemStyle.Setters.Add(new Setter(Control.HeightProperty, 34.0));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 0, 18, 0)));
            itemStyle.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            itemStyle.Setters.Add(new Setter(MenuItem.StaysOpenOnClickProperty, false));
            var itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.Name = "ItemBorder";
            itemBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            itemBorder.AppendChild(content);
            var itemTemplate = new ControlTemplate(typeof(MenuItem)) { VisualTree = itemBorder };
            var hover = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(238, 242, 255)), "ItemBorder"));
            itemTemplate.Triggers.Add(hover);
            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
            menu.ItemContainerStyle = itemStyle;
        }

        public static void SoftenScrollBars(DependencyObject root)
        {
            if (Application.Current != null && !Application.Current.Resources.Contains("OnharuScrollThumb"))
                Application.Current.Resources["OnharuScrollThumb"] = new SolidColorBrush(Color.FromRgb(165, 180, 252));
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var bar = child as ScrollBar;
                if (bar != null)
                {
                    var horizontal = bar.Orientation == Orientation.Horizontal;
                    if (horizontal) { bar.Height = 10; bar.Margin = new Thickness(3, 2, 3, 2); }
                    else { bar.Width = 10; bar.Margin = new Thickness(2, 3, 2, 3); }
                    bar.Background = Brushes.Transparent; bar.BorderThickness = new Thickness(0);
                    var orientation = horizontal ? "Horizontal" : "Vertical";
                    var reversed = horizontal ? "False" : "True";
                    var decrease = horizontal ? "PageLeftCommand" : "PageUpCommand";
                    var increase = horizontal ? "PageRightCommand" : "PageDownCommand";
                    bar.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'><Grid Background='Transparent'><Track x:Name='PART_Track' Orientation='" + orientation + "' IsDirectionReversed='" + reversed + "'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar." + decrease + "}' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='{DynamicResource OnharuScrollThumb}' CornerRadius='4' Margin='1'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar." + increase + "}' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>");
                }
                var thumb = child as Thumb;
                if (thumb != null) thumb.BorderThickness = new Thickness(0);
                SoftenScrollBars(child);
            }
        }
    }
}
