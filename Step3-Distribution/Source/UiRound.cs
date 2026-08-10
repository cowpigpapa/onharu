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
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        public static void SoftenScrollBars(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var bar = child as ScrollBar;
                if (bar != null)
                {
                    bar.Width = 10; bar.Margin = new Thickness(2, 3, 2, 3); bar.Background = Brushes.Transparent; bar.BorderThickness = new Thickness(0);
                    bar.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'><Grid Background='Transparent'><Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='#A5B4FC' CornerRadius='4' Margin='1'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>");
                }
                var thumb = child as Thumb;
                if (thumb != null) { thumb.Background = new SolidColorBrush(Color.FromRgb(165, 180, 252)); thumb.BorderThickness = new Thickness(0); }
                SoftenScrollBars(child);
            }
        }
    }
}
