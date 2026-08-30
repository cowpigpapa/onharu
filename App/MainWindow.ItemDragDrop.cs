using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void EnableItemDrag(FrameworkElement element, PlannerItem item)
        {
            var restriction = DragRestriction(item);
            var dragCursorTimer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(25) };
            element.Cursor = Cursors.Arrow;
            Action showDragCursor = delegate
            {
                element.Cursor = restriction == null
                    ? (UiCursor.ControlDown ? UiCursor.DragCopy : UiCursor.DragMove)
                    : Cursors.No;
                Mouse.OverrideCursor = element.Cursor;
            };
            dragCursorTimer.Tick += delegate
            {
                var cursor = restriction == null ? (UiCursor.ControlDown ? UiCursor.DragCopy : UiCursor.DragMove) : Cursors.No;
                element.Cursor = cursor; Mouse.OverrideCursor = cursor; Mouse.SetCursor(cursor);
            };
            Action startDragCursor = delegate { showDragCursor(); dragCursorTimer.Start(); };
            Action resetDragCursor = delegate
            {
                dragCursorTimer.Stop();
                Mouse.OverrideCursor = null; element.Cursor = Cursors.Arrow;
            };
            element.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount > 1) { resetDragCursor(); return; }
                var source = e.OriginalSource as DependencyObject;
                if (!IsMainCalendarNonDragControl(source, element) && !IsMainCalendarText(source, element)) showDragCursor();
            };
            element.GiveFeedback += delegate(object sender, GiveFeedbackEventArgs e)
            {
                Mouse.SetCursor(restriction == null
                    ? (UiCursor.ControlDown ? UiCursor.DragCopy : UiCursor.DragMove)
                    : Cursors.No);
                e.UseDefaultCursors = false; e.Handled = true;
            };
            element.QueryContinueDrag += delegate(object sender, QueryContinueDragEventArgs e)
            {
                if (restriction != null) { Mouse.SetCursor(Cursors.No); return; }
                Mouse.SetCursor(UiCursor.ControlDown ? UiCursor.DragCopy : UiCursor.DragMove);
            };
            element.PreviewMouseLeftButtonUp += delegate { resetDragCursor(); };
            element.LostMouseCapture += delegate { resetDragCursor(); };
            if (restriction != null)
            {
                Point blockedStart = default(Point); bool blockedArmed = false;
                element.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    if (IsMainCalendarNonDragControl(e.OriginalSource as DependencyObject, element)) { blockedArmed = false; return; }
                    blockedStart = e.GetPosition(element); blockedArmed = true;
                };
                element.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
                {
                    if (!blockedArmed || e.LeftButton != MouseButtonState.Pressed) return;
                    var point = e.GetPosition(element);
                    if (Math.Abs(point.X - blockedStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                        Math.Abs(point.Y - blockedStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                    blockedArmed = false;
                    startDragCursor();
                    DragDrop.DoDragDrop(element, new DataObject("ONHARU_BLOCKED_ITEM_DRAG", item.Id), DragDropEffects.Move);
                    resetDragCursor();
                };
                element.PreviewMouseLeftButtonUp += delegate { blockedArmed = false; };
                return;
            }
            element.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (IsMainCalendarNonDragControl(e.OriginalSource as DependencyObject, element)) { itemDragCandidate = null; return; }
                itemDragStart = e.GetPosition(this); itemDragCandidate = CanDragItem(item) ? item : null;
            };
            element.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (itemDragCandidate != item || e.LeftButton != MouseButtonState.Pressed) return;
                var point = e.GetPosition(this);
                if (Math.Abs(point.X - itemDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(point.Y - itemDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                itemDragCandidate = null;
                startDragCursor();
                DragDrop.DoDragDrop(element, item.Id, DragDropEffects.Move | DragDropEffects.Copy);
                resetDragCursor();
            };
            element.PreviewMouseLeftButtonUp += delegate { itemDragCandidate = null; };
        }

        static bool IsInteractiveDragContent(DependencyObject source, DependencyObject dragSurface)
        {
            while (source != null && source != dragSurface)
            {
                if (source is System.Windows.Controls.TextBlock || source is System.Windows.Controls.CheckBox || source is System.Windows.Controls.Button) return true;
                source = GetDragParent(source);
            }
            return false;
        }

        static bool IsMainCalendarNonDragControl(DependencyObject source, DependencyObject dragSurface)
        {
            while (source != null && source != dragSurface)
            {
                if ((source is System.Windows.Controls.CheckBox && !IsDisplayOnlyCheckBox(source)) || source is System.Windows.Controls.Button) return true;
                source = GetDragParent(source);
            }
            return false;
        }

        static bool IsMainCalendarText(DependencyObject source, DependencyObject dragSurface)
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

        static DependencyObject GetDragParent(DependencyObject source)
        {
            if (source == null) return null;
            var content = source as ContentElement;
            if (content != null)
            {
                var frameworkContent = content as FrameworkContentElement;
                return frameworkContent != null && frameworkContent.Parent != null
                    ? frameworkContent.Parent : ContentOperations.GetParent(content);
            }
            return source is Visual || source is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source) : null;
        }

        bool CanDragItem(PlannerItem item)
        {
            return DragRestriction(item) == null;
        }

        string DragRestriction(PlannerItem item)
        {
            if (item == null) return "이동할 수 없는 일정입니다.";
            if (!settings.AllowDragMove) return "설정에서 일정 드래그 이동을 켜 주세요.";
            if (IsAutomaticSportsItem(item)) return "KBO 자동 등록 일정은 이동할 수 없습니다.";
            if (!string.IsNullOrWhiteSpace(item.AnniversaryType)) return "기념일·D-Day 원본은 이동할 수 없습니다.";
            if (item.GoogleEventType == "birthday" || item.Category == "국경일") return "Google 생일·공휴일은 이동할 수 없습니다.";
            if (item.GoogleReadOnly) return "Google 읽기 전용 일정은 이동할 수 없습니다.";
            if (!string.IsNullOrWhiteSpace(item.RecurrenceFrequency) || !string.IsNullOrWhiteSpace(item.SeriesId)) return "반복 일정 원본은 이동할 수 없습니다.";
            if (string.IsNullOrWhiteSpace(item.GoogleCalendarId) && !settings.AllowLocalDragMove) return "설정에서 온하루 일정 드래그 이동을 켜 주세요.";
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
            var original = item.Clone();
            var target = copy ? CopyItem(item) : item;
            target.Start = target.Start.AddDays(days); target.End = target.End.AddDays(days);
            if (copy) items.Add(target);
            selectedDate = targetDate.Date; detailMode = "selected";
            Store.Save(items); RenderAll();
            if (!string.IsNullOrWhiteSpace(target.GoogleCalendarId) && GoogleCalendar.IsConnected)
                await SaveGoogleItem(target);
            RegisterUndo(copy ? "일정 복사" : "일정 이동", async delegate
            {
                if (copy)
                {
                    var copied = items.FirstOrDefault(x => x.Id == target.Id);
                    if (copied != null && !string.IsNullOrWhiteSpace(copied.GoogleEventId) && GoogleCalendar.IsConnected)
                        await GoogleCalendar.DeleteAsync(copied);
                    items.RemoveAll(x => x.Id == target.Id);
                }
                else
                {
                    var index = items.FindIndex(x => x.Id == original.Id);
                    if (index >= 0) items[index] = original.Clone(); else items.Add(original.Clone());
                    var restored = items.First(x => x.Id == original.Id);
                    if (!string.IsNullOrWhiteSpace(restored.GoogleCalendarId) && GoogleCalendar.IsConnected)
                        await SaveGoogleItem(restored);
                }
                Store.Save(items); RenderAll();
            });
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
