using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        const string DetailGroupDragFormat = "ONHARU_DETAIL_GROUP";
        static bool IsPinnedImportantGroup(string groupKey) { return groupKey != null && groupKey.StartsWith("★ "); }

        void EnableDetailCardOrderSurface()
        {
            detail.Background = System.Windows.Media.Brushes.Transparent;
            detail.AllowDrop = true;
            detail.DragOver += delegate(object sender, DragEventArgs e)
            {
                if (!e.Data.GetDataPresent(DetailGroupDragFormat)) return;
                e.Effects = IsOverPinnedImportantCard(e.GetPosition(detail)) ? DragDropEffects.None : DragDropEffects.Move;
                e.Handled = true;
            };
            detail.Drop += delegate(object sender, DragEventArgs e)
            {
                var source = e.Data.GetData(DetailGroupDragFormat) as string;
                var point = e.GetPosition(detail);
                var targetCard = detail.Children.OfType<System.Windows.Controls.Border>()
                    .Where(x => x.Tag is string && (string)x.Tag != source && !IsPinnedImportantGroup((string)x.Tag))
                    .OrderBy(x => Math.Abs(x.TranslatePoint(new Point(0, x.ActualHeight / 2), detail).Y - point.Y))
                    .FirstOrDefault();
                var target = targetCard == null ? null : targetCard.Tag as string;
                if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target))
                {
                    var center = targetCard.TranslatePoint(new Point(0, targetCard.ActualHeight / 2), detail).Y;
                    ReorderDetailGroup(source, target, point.Y >= center);
                }
                e.Handled = true;
            };
            detailScroll.AllowDrop = true;
            detailScroll.DragOver += delegate(object sender, DragEventArgs e)
            {
                if (!e.Data.GetDataPresent(DetailGroupDragFormat)) return;
                e.Effects = IsOverPinnedImportantCard(e.GetPosition(detail)) ? DragDropEffects.None : DragDropEffects.Move;
                e.Handled = true;
            };
        }

        bool IsOverPinnedImportantCard(Point point)
        {
            return detail.Children.OfType<System.Windows.Controls.Border>().Any(x => x.Tag is string &&
                IsPinnedImportantGroup((string)x.Tag) && new Rect(x.TranslatePoint(new Point(0, 0), detail), x.RenderSize).Contains(point));
        }

        void EnableDetailCardOrder(FrameworkElement header, FrameworkElement card, string groupName, bool toggleCollapse = true)
        {
            Point start = default(Point); bool armed = false;
            header.Cursor = Cursors.Hand;
            header.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            { start = e.GetPosition(header); armed = true; };
            header.PreviewMouseLeftButtonUp += delegate
            {
                if (!armed) return;
                armed = false;
                if (!toggleCollapse) return;
                if (!collapsedDetailGroups.Add(groupName)) collapsedDetailGroups.Remove(groupName);
                RenderDetail();
            };
            if (IsPinnedImportantGroup(groupName))
            {
                header.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
                {
                    if (!armed || e.LeftButton != MouseButtonState.Pressed) return;
                    var point = e.GetPosition(header);
                    if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                        Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                    armed = false;
                    DragDrop.DoDragDrop(header, new DataObject("ONHARU_BLOCKED_ITEM_DRAG", groupName), DragDropEffects.Move);
                };
                return;
            }
            header.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!armed || e.LeftButton != MouseButtonState.Pressed) return;
                var point = e.GetPosition(header);
                if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                armed = false;
                var data = new DataObject(); data.SetData(DetailGroupDragFormat, groupName);
                DragDrop.DoDragDrop(header, data, DragDropEffects.Move);
            };
            card.AllowDrop = true;
            card.DragOver += delegate(object sender, DragEventArgs e)
            {
                if (!e.Data.GetDataPresent(DetailGroupDragFormat)) return;
                e.Effects = DragDropEffects.Move; e.Handled = true;
            };
            card.Drop += delegate(object sender, DragEventArgs e)
            {
                var source = e.Data.GetData(DetailGroupDragFormat) as string;
                if (string.IsNullOrWhiteSpace(source) || source == groupName) return;
                var visible = VisibleDetailGroupNames();
                ReorderDetailGroup(source, groupName, visible.IndexOf(source) < visible.IndexOf(groupName));
                e.Handled = true;
            };
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
                .OrderBy(x => IsPinnedImportantGroup((string)x.Card.Tag) ? 0 : 1)
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
