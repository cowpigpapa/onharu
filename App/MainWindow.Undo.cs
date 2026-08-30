using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        sealed class CalendarUndoAction
        {
            public string Name;
            public Func<Task> Apply;
            public string Confirmation;
            public string Unsupported;
        }

        void RegisterUndo(string name, Func<Task> apply)
        {
            calendarUndo = new CalendarUndoAction { Name = name, Apply = apply };
        }

        void RegisterUnsupportedUndo(string message)
        {
            calendarUndo = new CalendarUndoAction { Name = "실행 취소", Unsupported = message };
        }

        async Task UndoCalendarAction()
        {
            if (calendarUndo == null) { ShowNotice("되돌릴 일정 작업이 없습니다.", false); return; }
            var action = calendarUndo;
            if (!string.IsNullOrWhiteSpace(action.Unsupported))
            {
                calendarUndo = null; ShowNotice(action.Unsupported, true, "실행 취소 안내"); return;
            }
            if (!string.IsNullOrWhiteSpace(action.Confirmation))
            {
                var confirm = new UndoConfirmWindow(action.Confirmation); PlaceCalendarDialog(confirm);
                if (ShowBlockingDialog(confirm) != true) return;
            }
            calendarUndo = null;
            try { await action.Apply(); ShowNotice(action.Name + " 작업을 되돌렸습니다.", false); }
            catch (Exception ex)
            {
                ErrorLog.Write("Undo " + action.Name, ex);
                ShowNotice(action.Name + " 작업을 되돌리지 못했습니다.", true);
            }
        }

        void RegisterCreateUndo(PlannerItem created)
        {
            RegisterUndo("일정 등록", async delegate
            {
                var current = items.FirstOrDefault(x => x.Id == created.Id);
                if (current != null && !string.IsNullOrWhiteSpace(current.GoogleEventId) && GoogleCalendar.IsConnected)
                    await GoogleCalendar.DeleteAsync(current);
                items.RemoveAll(x => x.Id == created.Id); Store.Save(items); RenderAll();
            });
            calendarUndo.Confirmation = "방금 등록한 일정을 삭제하시겠습니까?";
        }

        void RegisterEditUndo(PlannerItem original)
        {
            RegisterUndo("일정 수정", async delegate
            {
                var current = items.FirstOrDefault(x => x.Id == original.Id);
                var currentGoogle = current != null && !string.IsNullOrWhiteSpace(current.GoogleCalendarId);
                var originalGoogle = !string.IsNullOrWhiteSpace(original.GoogleCalendarId);
                var changedCalendar = currentGoogle != originalGoogle ||
                    (currentGoogle && current.GoogleCalendarId != original.GoogleCalendarId);
                if (changedCalendar && currentGoogle && !string.IsNullOrWhiteSpace(current.GoogleEventId) && GoogleCalendar.IsConnected)
                    await GoogleCalendar.DeleteAsync(current);
                var restored = original.Clone();
                if (changedCalendar && originalGoogle)
                { restored.GoogleEventId = null; restored.GoogleRecurringEventId = null; restored.PendingGoogleSync = true; }
                var index = items.FindIndex(x => x.Id == original.Id);
                if (index >= 0) items[index] = restored; else items.Add(restored);
                Store.Save(items); RenderAll();
                if (originalGoogle && GoogleCalendar.IsConnected) await SaveGoogleItem(restored);
            });
            calendarUndo.Confirmation = "수정된 일정을 원래 상태로 복원하시겠습니까?";
        }

        void RegisterDeleteUndo(IEnumerable<PlannerItem> deleted, bool recreateGoogle)
        {
            var snapshots = deleted.Select(x => x.Clone()).ToList();
            if (snapshots.Count == 0) return;
            RegisterUndo("일정 삭제", async delegate
            {
                foreach (var snapshot in snapshots)
                {
                    if (items.Any(x => x.Id == snapshot.Id)) continue;
                    var restored = snapshot.Clone();
                    if (recreateGoogle)
                    {
                        restored.GoogleEventId = null; restored.GoogleRecurringEventId = null;
                        restored.PendingGoogleSync = true;
                    }
                    items.Add(restored);
                    if (recreateGoogle && GoogleCalendar.IsConnected) await SaveGoogleItem(restored);
                }
                Store.Save(items); RenderAll();
            });
        }
    }


    sealed class UndoConfirmWindow : System.Windows.Window
    {
        public UndoConfirmWindow(string message)
        {
            Title = "실행 취소 확인"; Width = 410; SizeToContent = System.Windows.SizeToContent.Height;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner; WindowStyle = System.Windows.WindowStyle.None;
            AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent; ResizeMode = System.Windows.ResizeMode.NoResize;
            var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(26, 20, 26, 18) };
            panel.Children.Add(OnharuPopupChrome.Header(this, "↶  실행 취소", "#4338CA"));
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = message, TextWrapping = System.Windows.TextWrapping.Wrap,
                Foreground = OnharuPopupChrome.Brush("#475569"), FontSize = 13, Margin = new System.Windows.Thickness(0, 11, 0, 14) });
            var buttons = new System.Windows.Controls.Grid(); buttons.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
            buttons.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
            var cancel = OnharuPopupChrome.FooterButton("취소", "#E2E8F0", "#475569"); cancel.Margin = new System.Windows.Thickness(0, 0, 5, 0);
            cancel.Click += delegate { DialogResult = false; }; buttons.Children.Add(cancel);
            var restore = OnharuPopupChrome.FooterButton("되돌리기", "#4F46E5", "#FFFFFF"); restore.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            restore.Click += delegate { DialogResult = true; }; System.Windows.Controls.Grid.SetColumn(restore, 1); buttons.Children.Add(restore);
            panel.Children.Add(buttons); Content = OnharuPopupChrome.Shell(panel);
        }
    }
}
