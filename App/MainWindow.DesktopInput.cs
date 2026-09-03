using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        async void HandleDesktopAction(int action, int value)
        {
            if (ActivateBlockingDialog()) return;
            if (action == 109 && PlacementTrace.IsEnabled)
            {
                if (positionLocked) EnterEditMode();
                else LockCurrentPlacement();
                return;
            }
            if (!positionLocked) return;
            if (action == 29) { if (weekCountOverlay == null) CloseTransientPopup(); return; }
            if (action == 100 || action == 101) { HitTestDesktop(value, action == 101); return; }
            CloseTransientPopup();
            if (action == 102 || action == 103) { ScrollDesktopDetail(value, action == 102 ? 1 : -1); return; }
            if (action == 104) { AdjustDesktopOpacity(value, false, false); return; }
            if (action == 105) { FlushFixedOpacityPreview(); Store.SaveSettings(settings); SchedulePublish(); return; }
            if (action == 107) { AdjustDesktopDetailScroll(value); return; }
            if (action == 108) { GoogleClick(null, null); return; }
            if (action == 110) { await UndoCalendarAction(); return; }
            if (action == 1) { MoveCalendar(-1); return; }
            else if (action == 2) { GoToday(); return; }
            else if (action == 3) { MoveCalendar(1); return; }
            else if (action == 4) { SelectDesktopDate(value); ShowForDialog(); AddItem(null, null); return; }
            else if (action == 5) SelectDesktopDate(value);
            else if (action == 12) { EnterEditMode(); return; }
            else if (action == 14) { ShowForDialog(); GoogleClick(null, null); return; }
            else if (action == 15) { ShowForDialog(); OpenSearch(null, null); return; }
            else if (action == 16) { ShowForDialog(); OpenSettings(null, null); return; }
            else if (action == 20) { ToggleSidebar(null, null); if (positionLocked) SchedulePublish(); return; }
            else if (action == 25) { MinimizeToTray(); return; }
            else if (action == 26) { settings.Opacity = Math.Max(.10, Math.Min(1.0, value / 100.0)); Opacity = settings.Opacity; explorerFrame.UpdateOpacity(settings.Opacity); Store.SaveSettings(settings); }
            else return;
            RenderAll();
        }

        void HitTestDesktop(int packedPoint, bool doubleClick)
        {
            var point = explorerFrame.FrameToLogicalPoint((short)(packedPoint & 0xFFFF), (short)((packedPoint >> 16) & 0xFFFF));
            var root = Content as Visual;
            if (!doubleClick && TryApplyWeekCountAt(root, point)) return;
            var current = root == null ? null : FindDesktopElement(root, root, point);
            object target = null;
            FrameworkElement targetElement = null;
            while (current != null)
            {
                var slider = current as Slider;
                if (slider != null) { target = slider; targetElement = slider; break; }
                var button = current as Button;
                if (button != null) { target = button; targetElement = button; break; }
                var check = current as CheckBox;
                if (check != null) { target = check; targetElement = check; break; }
                var element = current as FrameworkElement;
                if (element != null && (element.Tag as string == "google_sync" || element.Tag as string == "toggle_section")) { target = element.Tag; targetElement = element; break; }
                if (element != null && (element.Tag is DateTime || element.Tag is PlannerItem || element.Tag is ItemHitTarget || element.Tag is DetailGroupHitTarget)) { target = element.Tag; break; }
                current = VisualTreeHelper.GetParent(current);
            }
            // 기간 스위치를 다시 누르면 스위치 자신의 `Clicked`가 토글로 닫는다. 여기서 먼저 닫아 버리면
            // 이어지는 RaiseEvent(Click)이 `weekCountOverlay == null`을 보고 다시 열어, 고정 상태에서만
            // 두 번째 클릭이 먹히지 않았다(2026-09-03 사용자 확인). 이동 상태는 이 경로를 타지 않아 정상이었다.
            if (weekCountOverlay != null && !IsInside(targetElement, weekCountOverlay)
                && !IsInside(targetElement, calendarRangeSwitch)) CloseWeekCountOverlay();
            if (doubleClick) target = target ?? lastDesktopClickTarget;
            else lastDesktopClickTarget = target;

            var targetButton = target as Button;
            if (targetButton != null)
            {
                if (!targetButton.IsEnabled) return;
                var navigation = targetButton.Tag as string;
                if (!doubleClick && navigation != null && navigation.StartsWith("week_count:", StringComparison.Ordinal))
                {
                    int weekCount;
                    if (int.TryParse(navigation.Substring("week_count:".Length), out weekCount)) ApplyWeekCount(weekCount);
                    return;
                }
                if (navigation == "calendar_previous" || navigation == "calendar_next")
                {
                    HandleCalendarNavigationClick(navigation == "calendar_previous" ? -1 : 1, doubleClick); return;
                }
                if (navigation == "calendar_edge_previous" || navigation == "calendar_edge_next")
                {
                    HandleCalendarEdgeNavigationClick(navigation == "calendar_edge_previous" ? -1 : 1, doubleClick); return;
                }
                if (doubleClick)
                {
                    // Explorer can deliver WM_LBUTTONDBLCLK without a fresh
                    // WM_LBUTTONDOWN action when clicks are close together.
                    // Ensure the palette opens, but never toggle an already
                    // open palette closed on the second half of a double-click.
                    //
                    // 2026-09-03: 기간 스위치는 반대다. 두 번째 클릭이 토글로 닫는 것이 정상 동작인데,
                    // 빠르게 두 번 누르면 그 클릭이 더블클릭으로 와서 여기서 삼켜졌다. 사용자에게는
                    // `가끔 한 번씩 안 먹는` 것으로 보였다. 천천히 누르면 두 번 다 단일 클릭이라 잘 됐다.
                    // 스위치는 더블클릭의 두 번째 절반도 보통 클릭으로 흘려보낸다.
                    if (IsInside(targetButton, calendarRangeSwitch))
                    {
                        FlashDesktopButton(targetButton);
                        targetButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, targetButton));
                        if (positionLocked) SchedulePublish();
                        return;
                    }
                    if (targetButton == dateColorButton && dateColorPalette != null &&
                        dateColorPalette.Visibility != Visibility.Visible)
                    {
                        FlashDesktopButton(targetButton);
                        targetButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, targetButton));
                        if (positionLocked) SchedulePublish();
                    }
                    return;
                }
                else
                {
                    FlashDesktopButton(targetButton);
                    targetButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, targetButton));
                    if (positionLocked) SchedulePublish();
                }
                return;
            }
            var targetSlider = target as Slider;
            if (targetSlider != null && !doubleClick) { AdjustDesktopOpacity(packedPoint, true, true); return; }
            if (target as string == "google_sync" && !doubleClick) { GoogleClick(null, null); return; }
            // 사이드바 그룹 머리글은 Button이 아니라 TextBlock이라 이동 상태에서만 동작했다.
            // 고정 상태에서는 이 경로로 같은 MouseLeftButtonUp을 대신 올려 준다.
            if (target as string == "toggle_section" && !doubleClick && targetElement != null)
            {
                targetElement.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                SchedulePublish(); return;
            }
            var targetCheck = target as CheckBox;
            if (targetCheck != null)
            {
                if (!doubleClick)
                {
                    targetCheck.IsChecked = targetCheck.IsChecked != true;
                    targetCheck.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, targetCheck));
                    if (positionLocked) SchedulePublish();
                }
                return;
            }
            if (target is DateTime)
            {
                if (doubleClick) { selectedDate = ((DateTime)target).Date; detailMode = "selected"; AddItem(null, null); }
                else SelectDateFast((DateTime)target);
                return;
            }
            var targetItem = target as PlannerItem;
            if (targetItem != null)
            {
                if (!string.IsNullOrWhiteSpace(targetItem.AnniversaryType))
                {
                    if (doubleClick) OpenAnniversary(targetItem);
                    return;
                }
                if (doubleClick) OpenEdit(targetItem);
            }
            var detailGroup = target as DetailGroupHitTarget;
            if (detailGroup != null)
            {
                if (!doubleClick)
                {
                    if (!collapsedDetailGroups.Add(detailGroup.GroupKey)) collapsedDetailGroups.Remove(detailGroup.GroupKey);
                    RenderDetail();
                    if (positionLocked) SchedulePublish();
                }
                return;
            }
            var itemHit = target as ItemHitTarget;
            if (itemHit == null) return;
            if (itemHit.DetailCard)
            {
                if (doubleClick) OpenEdit(itemHit.Item);
                return;
            }
            var days = Math.Max(1, (itemHit.SegmentEnd - itemHit.SegmentStart).Days + 1);
            Point local;
            try { local = root.TransformToDescendant(itemHit.Element).Transform(point); }
            catch { return; }
            if (!doubleClick && itemHit.Item.IsTodo && local.X <= Ui(23))
            { ToggleTodoFromDesktop(itemHit.Item); return; }
            var clickedDay = itemHit.Element.ActualWidth <= 0 ? 0 : Math.Min(days - 1,
                Math.Max(0, (int)(local.X / itemHit.Element.ActualWidth * days)));
            var clickedDate = itemHit.SegmentStart.AddDays(clickedDay);
            if (doubleClick) { selectedDate = clickedDate; detailMode = "selected"; OpenEdit(itemHit.Item); }
            else SelectDateFast(clickedDate);
        }

        static bool IsInside(DependencyObject child, DependencyObject parent)
        {
            for (var current = child; current != null; current = VisualTreeHelper.GetParent(current))
                if (current == parent) return true;
            return false;
        }

        async void ToggleTodoFromDesktop(PlannerItem item)
        {
            await SetTodoCompleted(item, !item.Completed);
        }

        void FlashDesktopButton(Button button)
        {
            var originalOpacity = button.Opacity;
            button.Opacity = .62;
            if (positionLocked) SchedulePublish();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(130) };
            timer.Tick += delegate
            {
                timer.Stop(); button.Opacity = originalOpacity;
                if (positionLocked) SchedulePublish();
            };
            timer.Start();
        }

        void AdjustDesktopOpacity(int packedPoint, bool save, bool requireHit)
        {
            var root = Content as Visual;
            if (root == null) return;
            var point = explorerFrame.FrameToLogicalPoint((short)(packedPoint & 0xFFFF), (short)((packedPoint >> 16) & 0xFFFF));
            var slider = opacitySlider;
            if (slider == null) return;
            if (requireHit)
            {
                var current = FindDesktopElement(root, root, point);
                while (current != null && !(current is Slider)) current = VisualTreeHelper.GetParent(current);
                if (current as Slider != slider) return;
            }
            var local = root.TransformToDescendant(slider).Transform(point);
            var usable = Math.Max(1, slider.ActualWidth - 10);
            var ratio = Math.Max(0, Math.Min(1, (local.X - 5) / usable));
            slider.Value = slider.Minimum + ratio * (slider.Maximum - slider.Minimum);
            settings.Opacity = slider.Value;
            if (!positionLocked) Opacity = settings.Opacity;
            if (save) Store.SaveSettings(settings);
        }

        void QueueFixedOpacityPreview(double opacity)
        {
            pendingFixedOpacity = opacity;
            if (fixedOpacityPreviewTimer == null)
            {
                fixedOpacityPreviewTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                fixedOpacityPreviewTimer.Tick += delegate
                {
                    fixedOpacityPreviewTimer.Stop(); explorerFrame.UpdateOpacity(pendingFixedOpacity);
                    if (++fixedOpacityVisualTick % 4 == 0) ScheduleFixedVisualRefresh();
                };
            }
            if (!fixedOpacityPreviewTimer.IsEnabled) fixedOpacityPreviewTimer.Start();
        }

        void FlushFixedOpacityPreview()
        {
            if (fixedOpacityPreviewTimer != null) fixedOpacityPreviewTimer.Stop();
            explorerFrame.UpdateOpacity(settings.Opacity);
            fixedOpacityVisualTick = 0;
            ScheduleFixedVisualRefresh();
        }

        void AdjustDesktopDetailScroll(int packedPoint)
        {
            if (detailScroll == null || detailScroll.ScrollableHeight <= 0 || Content == null) return;
            var point = explorerFrame.FrameToLogicalPoint((short)(packedPoint & 0xFFFF), (short)((packedPoint >> 16) & 0xFFFF));
            Point origin;
            try { origin = detailScroll.TransformToAncestor((Visual)Content).Transform(new Point()); }
            catch { return; }
            var ratio = Math.Max(0, Math.Min(1, (point.Y - origin.Y) / Math.Max(1, detailScroll.ActualHeight)));
            detailScroll.ScrollToVerticalOffset(detailScroll.ScrollableHeight * ratio);
            SchedulePublish();
        }

        void ScrollDesktopDetail(int packedPoint, int direction)
        {
            if (detailScroll == null || detailScroll.ScrollableHeight <= 0) return;
            var point = explorerFrame.FrameToLogicalPoint((short)(packedPoint & 0xFFFF), (short)((packedPoint >> 16) & 0xFFFF));
            Point origin;
            try { origin = detailScroll.TransformToAncestor((Visual)Content).Transform(new Point()); }
            catch { return; }
            if (!new Rect(origin, new Size(detailScroll.ActualWidth, detailScroll.ActualHeight)).Contains(point)) return;
            detailScroll.ScrollToVerticalOffset(Math.Max(0, Math.Min(detailScroll.ScrollableHeight,
                detailScroll.VerticalOffset - direction * 72)));
            SchedulePublish();
        }

        void SelectDateFast(DateTime date)
        {
            var previous = selectedDate.Date;
            selectedDate = date.Date; detailMode = "selected";
            Border cell;
            if (dayCells.TryGetValue(previous, out cell)) StyleDayCell(cell, previous);
            if (dayCells.TryGetValue(selectedDate, out cell)) StyleDayCell(cell, selectedDate);
            RenderDetail();
            if (positionLocked) SchedulePublish();
        }

        static DependencyObject FindDesktopElement(Visual root, DependencyObject parent, Point point)
        {
            for (var index = VisualTreeHelper.GetChildrenCount(parent) - 1; index >= 0; index--)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                var visual = child as Visual;
                var element = child as FrameworkElement;
                if (visual == null || element == null || element.ActualWidth <= 0 || element.ActualHeight <= 0) continue;
                Rect bounds;
                try
                {
                    var origin = visual == root ? new Point() : visual.TransformToAncestor(root).Transform(new Point());
                    bounds = new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
                }
                catch { continue; }
                if (!bounds.Contains(point)) continue;
                var nested = FindDesktopElement(root, child, point);
                if (nested != null) return nested;
                if (child is Button || child is CheckBox || child is Slider) return child;
                if (element.Tag as string == "toggle_section") return child;
                if (element.Tag is DateTime || element.Tag is PlannerItem || element.Tag is ItemHitTarget || element.Tag is DetailGroupHitTarget) return child;
            }
            return null;
        }

        void SelectDesktopDate(int index)
        {
            if (index < 0 || index >= 42) return;
            var offset = (7 + (int)shownMonth.DayOfWeek - (int)ConfiguredFirstDay()) % 7;
            selectedDate = shownMonth.AddDays(-offset + index); detailMode = "selected";
        }

        bool TryApplyWeekCountAt(Visual root, Point point)
        {
            if (root == null || weekCountOverlay == null) return false;
            foreach (var child in ((Panel)((Border)weekCountOverlay).Child).Children)
            {
                var button = child as Button;
                if (button == null || !button.IsEnabled) continue;
                try
                {
                    var origin = button.TransformToAncestor(root).Transform(new Point());
                    if (!new Rect(origin, new Size(button.ActualWidth, button.ActualHeight)).Contains(point)) continue;
                    int count;
                    if (int.TryParse((button.Tag as string ?? "").Replace("week_count:", ""), out count)) ApplyWeekCount(count);
                    return true;
                }
                catch { }
            }
            return false;
        }
    }
}
