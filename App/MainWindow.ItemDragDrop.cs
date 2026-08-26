using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void EnableItemDrag(FrameworkElement element, PlannerItem item)
        {
            var restriction = DragRestriction(item);
            if (restriction != null)
            {
                Point blockedStart = default(Point); bool blockedArmed = false;
                element.Cursor = Cursors.Hand;
                element.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                { blockedStart = e.GetPosition(element); blockedArmed = true; };
                element.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
                {
                    if (!blockedArmed || e.LeftButton != MouseButtonState.Pressed) return;
                    var point = e.GetPosition(element);
                    if (Math.Abs(point.X - blockedStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                        Math.Abs(point.Y - blockedStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                    blockedArmed = false;
                    DragDrop.DoDragDrop(element, new DataObject("ONHARU_BLOCKED_ITEM_DRAG", item.Id), DragDropEffects.Move);
                };
                element.PreviewMouseLeftButtonUp += delegate { blockedArmed = false; };
                return;
            }
            element.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                itemDragStart = e.GetPosition(this); itemDragCandidate = CanDragItem(item) ? item : null;
            };
            element.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (itemDragCandidate != item || e.LeftButton != MouseButtonState.Pressed) return;
                var point = e.GetPosition(this);
                if (Math.Abs(point.X - itemDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(point.Y - itemDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                itemDragCandidate = null;
                DragDrop.DoDragDrop(element, item.Id, DragDropEffects.Move | DragDropEffects.Copy);
            };
            element.PreviewMouseLeftButtonUp += delegate { itemDragCandidate = null; };
        }

        bool CanDragItem(PlannerItem item)
        {
            return DragRestriction(item) == null;
        }

        string DragRestriction(PlannerItem item)
        {
            if (item == null) return "이동할 수 없는 일정입니다.";
            if (IsAutomaticSportsItem(item)) return "KBO 자동 등록 일정은 이동할 수 없습니다.";
            if (!string.IsNullOrWhiteSpace(item.AnniversaryType)) return "기념일·D-Day 원본은 이동할 수 없습니다.";
            if (item.GoogleEventType == "birthday" || item.Category == "국경일") return "Google 생일·공휴일은 이동할 수 없습니다.";
            if (item.GoogleReadOnly) return "Google 읽기 전용 일정은 이동할 수 없습니다.";
            if (!string.IsNullOrWhiteSpace(item.RecurrenceFrequency) || !string.IsNullOrWhiteSpace(item.SeriesId)) return "반복 일정 원본은 이동할 수 없습니다.";
            if (!string.IsNullOrWhiteSpace(item.GoogleCalendarId) && !settings.AllowGoogleDragMove) return "설정에서 Google 일정 드래그 변경을 켜 주세요.";
            return null;
        }

        static bool IsAutomaticSportsItem(PlannerItem item)
        {
            return !string.IsNullOrWhiteSpace(item.SportsGameId) ||
                (!string.IsNullOrWhiteSpace(item.Notes) && item.Notes.StartsWith("KBO 경기 일정", StringComparison.Ordinal));
        }

        void EnableCalendarDrop()
        {
            calendar.AllowDrop = true;
            calendar.DragOver += delegate(object sender, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DetailGroupDragFormat) || e.Data.GetDataPresent("ONHARU_BLOCKED_ITEM_DRAG"))
                {
                    e.Effects = DragDropEffects.None; e.Handled = true; return;
                }
                e.Effects = !FindDropDate(e.GetPosition(calendar)).HasValue ? DragDropEffects.None :
                    (e.KeyStates & DragDropKeyStates.ControlKey) != 0 ? DragDropEffects.Copy : DragDropEffects.Move;
                e.Handled = true;
            };
            calendar.Drop += async delegate(object sender, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DetailGroupDragFormat) || e.Data.GetDataPresent("ONHARU_BLOCKED_ITEM_DRAG")) { e.Handled = true; return; }
                var targetDate = FindDropDate(e.GetPosition(calendar));
                var id = e.Data.GetData(typeof(string)) as string;
                var item = items.FirstOrDefault(x => x.Id == id);
                if (!targetDate.HasValue || !CanDragItem(item)) return;
                await MoveItemToDate(item, targetDate.Value, (e.KeyStates & DragDropKeyStates.ControlKey) != 0);
                e.Handled = true;
            };
        }

        DateTime? FindDropDate(Point point)
        {
            foreach (var entry in dayCells)
            {
                var topLeft = entry.Value.TranslatePoint(new Point(0, 0), calendar);
                if (new Rect(topLeft, entry.Value.RenderSize).Contains(point)) return entry.Key;
            }
            return null;
        }

        async Task MoveItemToDate(PlannerItem item, DateTime targetDate, bool copy)
        {
            var days = (targetDate.Date - item.Start.Date).Days;
            if (days == 0 && !copy) return;
            var target = copy ? CopyItem(item) : item;
            target.Start = target.Start.AddDays(days); target.End = target.End.AddDays(days);
            if (copy) items.Add(target);
            selectedDate = targetDate.Date; detailMode = "selected";
            Store.Save(items); RenderAll();
            if (!string.IsNullOrWhiteSpace(target.GoogleCalendarId) && GoogleCalendar.IsConnected)
                await SaveGoogleItem(target);
        }

        static PlannerItem CopyItem(PlannerItem item)
        {
            return new PlannerItem
            {
                Id = Guid.NewGuid().ToString(), Title = item.Title, Start = item.Start, End = item.End,
                AllDay = item.AllDay, IsTodo = item.IsTodo, Completed = false, Category = item.Category, Notes = item.Notes,
                GoogleEventType = item.GoogleEventType, OnharuManaged = item.OnharuManaged, GoogleTaskEvent = item.GoogleTaskEvent,
                CreatedInOnharu = true, AutoRollover = item.AutoRollover, RolloverMode = item.RolloverMode,
                GoogleCalendarId = item.GoogleCalendarId, GoogleCalendarName = item.GoogleCalendarName,
                GoogleCalendarColor = item.GoogleCalendarColor, GoogleReadOnly = false, Important = item.Important,
                ImportantBackgroundColor = item.ImportantBackgroundColor, ImportantTextColor = item.ImportantTextColor,
                ShowDday = item.ShowDday, ReminderMinutes = item.ReminderMinutes, ReminderConfigured = item.ReminderConfigured,
                PendingGoogleSync = !string.IsNullOrWhiteSpace(item.GoogleCalendarId)
            };
        }
    }
}
