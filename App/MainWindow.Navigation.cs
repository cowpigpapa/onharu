using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        string ActiveCalendarRangeMode { get { return temporaryMonthView ? settings.MonthRangeMode : "weeks"; } }

        void SetTemporaryMonthView(bool month)
        {
            if (temporaryMonthView == month) return;
            if (month) { periodViewAnchor = shownMonth; shownMonth = new DateTime(shownMonth.Year, shownMonth.Month, 1); }
            else if (periodViewAnchor != default(DateTime)) shownMonth = periodViewAnchor;
            temporaryMonthView = month;
            settings.UseMonthView = month;
            settings.CalendarRangeMode = month ? settings.MonthRangeMode : "weeks";
            Store.SaveSettings(settings);
            RenderAll();
        }

        void MoveCalendar(int direction)
        {
            DateTime candidate;
            try
            {
                candidate = ActiveCalendarRangeMode == "weeks"
                    ? shownMonth.AddDays(direction * Math.Max(1, Math.Min(6, settings.VisibleWeekCount)) * 7)
                    : new DateTime(shownMonth.Year, shownMonth.Month, 1).AddMonths(direction);
            }
            catch (ArgumentOutOfRangeException) { return; }
            if (candidate.Year < 1900 || candidate.Year > 9998) return;
            shownMonth = candidate;
            RenderAll();
        }

        void MoveCalendarSingleStep(int direction)
        {
            if (ActiveCalendarRangeMode != "weeks") { MoveCalendar(direction); return; }
            DateTime candidate;
            try { candidate = shownMonth.AddDays(direction * 7); }
            catch (ArgumentOutOfRangeException) { return; }
            if (candidate.Year < 1900 || candidate.Year > 9998) return;
            shownMonth = candidate; RenderAll();
        }

        void UpdatePeriodNavigationButtons()
        {
            var enabled = ActiveCalendarRangeMode == "weeks";
            if (previousPeriodButton != null) { previousPeriodButton.IsEnabled = enabled; previousPeriodButton.Opacity = enabled ? 1.0 : .38; }
            if (nextPeriodButton != null) { nextPeriodButton.IsEnabled = enabled; nextPeriodButton.Opacity = enabled ? 1.0 : .38; }
            if (calendarRangeSwitch != null) { calendarRangeSwitch.SetLabel(0, Math.Max(1, Math.Min(6, settings.VisibleWeekCount)) + "주"); calendarRangeSwitch.SetSelected(temporaryMonthView ? 1 : 0, false); }
        }

        void OpenMonthJump(object sender, RoutedEventArgs e)
        {
            var window = new MonthJumpWindow(shownMonth); PlaceCalendarDialog(window);
            if (ShowBlockingDialog(window) == true)
            {
                shownMonth = window.SelectedMonth;
                selectedDate = window.SelectedMonth;
                detailMode = "selected"; RenderAll();
            }
            if (positionLocked) SchedulePublish();
        }

        static int ResizeEdgeAt(Point point, FrameworkElement surface)
        {
            const double corner = 18, edge = 10;
            var leftCorner = point.X <= corner; var rightCorner = point.X >= surface.ActualWidth - corner;
            var topCorner = point.Y <= corner; var bottomCorner = point.Y >= surface.ActualHeight - corner;
            if (leftCorner && topCorner) return 1;
            if (rightCorner && topCorner) return 2;
            if (leftCorner && bottomCorner) return 3;
            if (rightCorner && bottomCorner) return 4;
            if (point.X <= edge) return 5;
            if (point.X >= surface.ActualWidth - edge) return 6;
            if (point.Y <= edge) return 7;
            if (point.Y >= surface.ActualHeight - edge) return 8;
            return 0;
        }

        static Cursor ResizeCursor(int edge)
        {
            return edge == 1 || edge == 4 ? UiCursor.ResizeNwSe : edge == 2 || edge == 3 ? UiCursor.ResizeNeSw
                : edge == 5 || edge == 6 ? UiCursor.ResizeHorizontal : edge == 7 || edge == 8 ? UiCursor.ResizeVertical : Cursors.Arrow;
        }

        void BeginResize(FrameworkElement surface, int edge)
        {
            surface.Cursor = ResizeCursor(edge);
            DesktopLayer.BeginResize(this, edge);
        }

        void ResizeSurfaceMouseMove(object sender, MouseEventArgs e)
        {
            var surface = (FrameworkElement)sender;
            if (positionLocked) { surface.Cursor = Cursors.Arrow; return; }
            surface.Cursor = ResizeCursor(ResizeEdgeAt(e.GetPosition(surface), surface));
        }

        void GoToday()
        {
            shownMonth = ActiveCalendarRangeMode == "weeks" ? DateTime.Today : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            selectedDate = DateTime.Today; detailMode = "selected"; RenderAll();
        }

        void OpenDateColorPopup(Button target)
        {
            CloseTransientPopup();
            var colors = new[] { "#FFF1F2", "#FEF3C7", "#DCFCE7", "#DBEAFE", "#EDE9FE", "#F1F5F9" };
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var popup = new Popup { PlacementTarget = target, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
            foreach (var hex in colors)
            {
                var color = hex;
                var swatch = Button("", null, 28); swatch.Height = 28; swatch.Background = Brush(color); swatch.BorderBrush = Brush("#CBD5E1");
                swatch.BorderThickness = new Thickness(1); swatch.Margin = new Thickness(3); swatch.Cursor = Cursors.Hand;
                swatch.Click += delegate
                {
                    settings.DateBackgroundColors[DateKey(selectedDate)] = color; Store.SaveSettings(settings); popup.IsOpen = false; RenderAll();
                };
                panel.Children.Add(swatch);
            }
            var clear = Button("×", null, 28); clear.Height = 28; clear.Margin = new Thickness(3); clear.Foreground = Brush("#DC2626");
            clear.ToolTip = "날짜 배경색 지우기";
            clear.Click += delegate { settings.DateBackgroundColors.Remove(DateKey(selectedDate)); Store.SaveSettings(settings); popup.IsOpen = false; RenderAll(); };
            panel.Children.Add(clear);
            popup.Child = new Border { Background = Brushes.White, BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(6), Margin = new Thickness(0, 5, 0, 0), Child = panel };
            transientPopup = popup;
            popup.Closed += delegate
            {
                if (ReferenceEquals(transientPopup, popup)) transientPopup = null;
                if (positionLocked) PublishAndHide();
            };
            popup.IsOpen = true;
        }

        void CloseTransientPopup()
        {
            var popup = transientPopup; transientPopup = null;
            if (popup != null) popup.IsOpen = false;
        }

        static string DateKey(DateTime date) { return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture); }

        public static string SolarTerm(DateTime date)
        {
            var names = new[] { "소한", "대한", "입춘", "우수", "경칩", "춘분", "청명", "곡우", "입하", "소만", "망종", "하지",
                "소서", "대서", "입추", "처서", "백로", "추분", "한로", "상강", "입동", "소설", "대설", "동지" };
            var minutes = new[] { 0, 21208, 42467, 63836, 85337, 107014, 128867, 150921, 173149, 195551, 218072, 240693,
                263343, 285989, 308563, 331033, 353350, 375494, 397447, 419210, 440795, 462224, 483532, 504758 };
            var correctionMinutes = new[] { 178, 167, 167, 143, 132, 98, 83, 47, 33, 1, -5, -26,
                -23, -28, -14, -3, 18, 42, 67, 96, 119, 145, 160, 176 };
            var origin = new DateTime(1900, 1, 6, 2, 5, 0, DateTimeKind.Utc);
            for (var i = 0; i < names.Length; i++)
                if (origin.AddMilliseconds(31556925974.7 * (date.Year - 1900) + (minutes[i] + correctionMinutes[i]) * 60000.0).Date == date.Date) return names[i];
            return null;
        }

        IEnumerable<PlannerItem> VisibleItems(DateTime date)
        {
            var day = ProjectItems(date.Date, date.Date).Where(x => OccursOnDate(x, date) && IsItemVisible(x) && ShowCompleted(x));
            if (settings.CalendarOrderMode == "time")
                return day.OrderBy(CompletedRank).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start).ThenBy(x => x.Title);
            return day.OrderBy(CompletedRank).ThenBy(GroupOrder).ThenBy(DisplayGroup).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Start);
        }
    }
}
