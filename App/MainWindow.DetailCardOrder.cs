using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        const string DetailGroupDragFormat = "ONHARU_DETAIL_GROUP";
        static bool IsSpecialDetailGroup(string groupKey) { return groupKey != null && groupKey.StartsWith("special:"); }

        void EnableDetailCardOrderSurface()
        {
            detail.Background = System.Windows.Media.Brushes.Transparent;
            // 2% 확대 시 ScrollViewer 경계에서 카드 모서리가 잘리지 않도록
            // 양쪽에 최소 안전 영역을 둔다. 평상시 시각 폭 변화는 8px로 제한한다.
            detail.Margin = new Thickness(4, 0, 4, 0);
        }

        void EnableDetailCardOrder(FrameworkElement header, FrameworkElement card, string groupName, bool toggleCollapse = true)
        {
            Point start = default(Point); bool armed = false; bool clickOnly = false; bool textStart = false; bool grabbed = false; bool dragging = false; bool canMove = false;
            bool darkLift = false; double liftOffset = -3; double liftScale = 1.012;
            System.Windows.Media.Transform originalTransform = null;
            System.Windows.Media.Effects.Effect originalEffect = null;
            System.Windows.Media.Effects.Effect originalKeyEffect = null;
            Point originalTransformOrigin = default(Point);
            var liftSurface = (card as System.Windows.Controls.Border) == null ? null
                : (card as System.Windows.Controls.Border).Child as System.Windows.Controls.Border;
            int originalZ = 0;
            Action restoreGrabVisual = delegate
            {
                card.RenderTransform = originalTransform;
                card.RenderTransformOrigin = originalTransformOrigin;
                card.Effect = originalEffect;
                if (liftSurface != null) liftSurface.Effect = originalKeyEffect;
                System.Windows.Controls.Panel.SetZIndex(card, originalZ);
                if (Mouse.OverrideCursor == UiCursor.DragMove) Mouse.OverrideCursor = null;
                card.Cursor = Cursors.Arrow;
            };
            card.Cursor = Cursors.Arrow; header.Cursor = Cursors.Arrow; ApplyDetailHeaderClickCursors(header);
            Action beginGrabVisual = delegate
            {
                if (grabbed) return;
                grabbed = true; originalTransform = card.RenderTransform; originalEffect = card.Effect;
                originalKeyEffect = liftSurface == null ? null : liftSurface.Effect;
                originalTransformOrigin = card.RenderTransformOrigin;
                originalZ = System.Windows.Controls.Panel.GetZIndex(card);
                darkLift = settings.ThemeId == "dark"; liftOffset = -3; liftScale = 1.02;
                card.RenderTransformOrigin = new Point(.5, .5);
                card.RenderTransform = DetailCardLiftTransform(liftOffset, liftScale);
                card.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = darkLift ? System.Windows.Media.Colors.White : System.Windows.Media.Colors.Black,
                    BlurRadius = darkLift ? 16 : 18, ShadowDepth = 0, Direction = 270,
                    Opacity = darkLift ? .24 : .18
                };
                if (liftSurface != null)
                    liftSurface.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = darkLift ? System.Windows.Media.Colors.White : System.Windows.Media.Colors.Black,
                        BlurRadius = 4, ShadowDepth = 6, Direction = 280, Opacity = darkLift ? .48 : .34
                    };
                System.Windows.Controls.Panel.SetZIndex(card, 100);
                card.Cursor = UiCursor.DragMove; Mouse.OverrideCursor = UiCursor.DragMove; Mouse.Capture(card);
            };
            card.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                start = e.GetPosition(detail); armed = true; grabbed = false; dragging = false;
                var original = e.OriginalSource as DependencyObject;
                textStart = IsDetailCardText(original, card);
                clickOnly = IsWithin(original, header) && textStart;
                if (IsDetailCardNonDragControl(original, card)) { armed = false; return; }
                canMove = settings.AllowDragMove &&
                    (IsSpecialDetailGroup(groupName) ? settings.AllowSpecialCardDrag : settings.AllowDetailCardDrag);
                if (canMove && !textStart) { beginGrabVisual(); e.Handled = true; }
            };
            card.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!armed || !canMove || e.LeftButton != MouseButtonState.Pressed) return;
                var point = e.GetPosition(detail);
                if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                beginGrabVisual();
                dragging = true;
                card.RenderTransform = DetailCardLiftTransform(point.Y - start.Y + liftOffset, liftScale);
                e.Handled = true;
            };
            card.PreviewMouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                var point = e.GetPosition(detail);
                if (grabbed)
                {
                    grabbed = false;
                    restoreGrabVisual();
                }
                if (Mouse.Captured == card) Mouse.Capture(null);
                card.Cursor = Cursors.Arrow;
                if (!armed) return;
                armed = false;
                if (!dragging)
                {
                    if (!toggleCollapse || !clickOnly) return;
                    if (!collapsedDetailGroups.Add(groupName)) collapsedDetailGroups.Remove(groupName);
                    RenderDetail(); return;
                }
                var targetCard = detail.Children.OfType<System.Windows.Controls.Border>()
                    .Where(x => x.Tag is string && (string)x.Tag != groupName &&
                        IsSpecialDetailGroup((string)x.Tag) == IsSpecialDetailGroup(groupName))
                    .OrderBy(x => Math.Abs(x.TranslatePoint(new Point(0, x.ActualHeight / 2), detail).Y - point.Y)).FirstOrDefault();
                if (targetCard == null) return;
                var center = targetCard.TranslatePoint(new Point(0, targetCard.ActualHeight / 2), detail).Y;
                ReorderDetailGroup(groupName, (string)targetCard.Tag, point.Y >= center);
            };
            card.LostMouseCapture += delegate
            {
                if (!grabbed) return;
                grabbed = false; armed = false; dragging = false;
                restoreGrabVisual();
            };
        }

        static void ApplyDetailHeaderClickCursors(DependencyObject parent)
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                var element = child as FrameworkElement;
                if (element is System.Windows.Controls.TextBlock || element is System.Windows.Controls.CheckBox) element.Cursor = Cursors.Hand;
                ApplyDetailHeaderClickCursors(child);
            }
        }

        static bool IsDetailCardText(DependencyObject source, DependencyObject dragSurface)
        {
            while (source != null && source != dragSurface)
            {
                var tagged = source as FrameworkElement;
                if (source is System.Windows.Controls.TextBlock || source is System.Windows.Documents.Run || IsDisplayOnlyCheckBox(source) ||
                    (tagged != null && (tagged.Tag as string) == "UnavailableTextSurface")) return true;
                source = GetDragParent(source);
            }
            return false;
        }

        static bool IsDetailCardNonDragControl(DependencyObject source, DependencyObject dragSurface)
        {
            while (source != null && source != dragSurface)
            {
                if ((source is System.Windows.Controls.CheckBox && !IsDisplayOnlyCheckBox(source)) || source is System.Windows.Controls.Button) return true;
                source = GetDragParent(source);
            }
            return false;
        }

        static bool IsDisplayOnlyCheckBox(DependencyObject source)
        {
            var check = source as System.Windows.Controls.CheckBox;
            return check != null && ((check.Tag as string) == "Unavailable" || !check.IsHitTestVisible);
        }

        static System.Windows.Media.Transform DetailCardLiftTransform(double offsetY, double scale)
        {
            var transforms = new System.Windows.Media.TransformGroup();
            transforms.Children.Add(new System.Windows.Media.ScaleTransform(scale, scale));
            transforms.Children.Add(new System.Windows.Media.TranslateTransform(-1, offsetY));
            return transforms;
        }



        static bool IsWithin(DependencyObject source, DependencyObject ancestor)
        {
            while (source != null)
            {
                if (source == ancestor) return true;
                source = GetDragParent(source);
            }
            return false;
        }

        System.Collections.Generic.List<string> VisibleDetailGroupNames()
        {
            return detail.Children.OfType<FrameworkElement>()
                .OfType<System.Windows.Controls.Border>().Where(x => x.Tag is string)
                .Select(x => (string)x.Tag).ToList();
        }

        void ApplyDetailCardOrder()
        {
            var snapshot = detail.Children.Cast<UIElement>().ToList();
            var cards = snapshot.OfType<System.Windows.Controls.Border>().Where(x => x.Tag is string).ToList();
            if (cards.Count < 2) return;
            var order = settings.DetailOrderMode == "time" ? settings.DetailTimeOrder : settings.DetailCategoryOrder;
            var ordered = cards.Select((card, index) => new { Card = card, Index = index,
                    Order = order == null ? -1 : order.IndexOf((string)card.Tag) })
                .OrderBy(x => IsSpecialDetailGroup((string)x.Card.Tag) ? 1 : 0)
                .ThenBy(x => x.Order < 0 ? int.MaxValue : x.Order).ThenBy(x => x.Index).Select(x => x.Card).ToList();
            var next = 0;
            detail.Children.Clear();
            foreach (var element in snapshot)
                detail.Children.Add(element is System.Windows.Controls.Border && ((System.Windows.Controls.Border)element).Tag is string
                    ? ordered[next++] : element);
        }

        void ReorderDetailGroup(string source, string target, bool after)
        {
            var visible = VisibleDetailGroupNames();
            var order = settings.DetailOrderMode == "time"
                ? settings.DetailTimeOrder ?? new System.Collections.Generic.List<string>()
                : settings.DetailCategoryOrder ?? new System.Collections.Generic.List<string>();
            foreach (var name in visible) if (!order.Contains(name)) order.Add(name);
            order.Remove(source);
            var index = order.IndexOf(target); if (index < 0) index = order.Count;
            order.Insert(Math.Min(order.Count, index + (after ? 1 : 0)), source);
            if (settings.DetailOrderMode == "time") settings.DetailTimeOrder = order;
            else settings.DetailCategoryOrder = order;
            Store.SaveSettings(settings); RenderDetail();
        }
    }
}
