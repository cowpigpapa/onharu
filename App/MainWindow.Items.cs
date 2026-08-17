using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        async void AddItem(object sender, RoutedEventArgs e)
        {
            var window = new AddItemWindow(selectedDate, null, settings.GoogleCalendars, GoogleCalendar.IsConnected, settings);
            PlaceCalendarDialog(window);
            var accepted = window.ShowDialog() == true;
            if (positionLocked && IsVisible) PublishAndHide();
            if (accepted)
            {
                if (window.RegisterAsAnniversary && AnniversaryCount() >= 10)
                { ShowNotice("기념일은 최대 10개까지 등록할 수 있습니다.", true); return; }
                items.Add(window.Result);
                if (string.IsNullOrWhiteSpace(window.Result.GoogleCalendarId)) ExpandLocalRecurrence(window.Result);
                Store.Save(items); RenderAll();
                if (!string.IsNullOrWhiteSpace(window.Result.GoogleCalendarId) && GoogleCalendar.IsConnected)
                { await SaveGoogleItem(window.Result); if (!string.IsNullOrWhiteSpace(window.Result.RecurrenceFrequency)) await SyncGoogle(false); }
            }
        }

        void ExpandLocalRecurrence(PlannerItem master)
        {
            if (string.IsNullOrWhiteSpace(master.RecurrenceFrequency) || (master.RecurrenceCount <= 0 && master.RecurrenceUntil <= master.Start.Date)) return;
            master.SeriesId = string.IsNullOrWhiteSpace(master.SeriesId) ? Guid.NewGuid().ToString() : master.SeriesId;
            var start = master.Start; var duration = master.End - master.Start; var count = 0;
            while (count++ < 500)
            {
                start = RecurrenceService.NextOccurrence(master, start);
                if (master.RecurrenceCount > 0 && count >= master.RecurrenceCount) break;
                if (master.RecurrenceCount <= 0 && start.Date > master.RecurrenceUntil.Date) break;
                items.Add(new PlannerItem { Id = Guid.NewGuid().ToString(), Title = master.Title, Start = start, End = start.Add(duration), AllDay = master.AllDay,
                    IsTodo = master.IsTodo, Category = master.Category, Notes = master.Notes, CreatedInOnharu = true, RolloverMode = master.RolloverMode,
                    AutoRollover = master.AutoRollover, Important = master.Important, ShowDday = master.ShowDday, ReminderMinutes = master.ReminderMinutes, ReminderConfigured = master.ReminderConfigured,
                    AnniversaryDate = master.AnniversaryDate,
                    AnniversaryType = master.AnniversaryType,
                    RecurrenceFrequency = master.RecurrenceFrequency, RecurrenceMode = master.RecurrenceMode, RecurrenceDays = master.RecurrenceDays,
                    RecurrenceUntil = master.RecurrenceUntil, RecurrenceCount = master.RecurrenceCount, SeriesId = master.SeriesId });
            }
        }





        async void OpenEdit(PlannerItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.AnniversaryType)) { OpenAnniversary(item); return; }
            if (item.GoogleReadOnly)
            {
                ShowItemNotice(item, item.Category == "국경일" ? "읽기 전용 일정입니다." : "설정에서 수정 가능을 선택해주세요.");
                return;
            }
            var oldRecurrence = item.RecurrenceFrequency; var oldMode = item.RecurrenceMode; var oldDays = item.RecurrenceDays; var oldUntil = item.RecurrenceUntil; var oldCount = item.RecurrenceCount;
            var originalSeriesStart = string.IsNullOrWhiteSpace(item.SeriesId) ? item.Start : items.Where(x => x.SeriesId == item.SeriesId).Min(x => x.Start);
            var window = new AddItemWindow(item.Start.Date, item, settings.GoogleCalendars, GoogleCalendar.IsConnected);
            PlaceCalendarDialog(window);
            var accepted = window.ShowDialog() == true;
            if (positionLocked && IsVisible) PublishAndHide();
            if (!accepted) return;
            if (window.RegisterAsAnniversary && AnniversaryCount() >= 10)
            { ShowNotice("기념일은 최대 10개까지 등록할 수 있습니다.", true); return; }
            if (window.RegisterAsAnniversary)
            {
                var oldGoogleAnniversarySource = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) || !string.IsNullOrWhiteSpace(item.GoogleEventId);
                if (oldGoogleAnniversarySource && GoogleCalendar.IsConnected)
                    try { await GoogleCalendar.DeleteAsync(item, window.ApplyToSeries); }
                    catch (Exception ex) { ErrorLog.Write("Convert Google event to anniversary", ex); ShowItemNotice(item, "Google 일정을 변경하지 못했습니다."); return; }
                if (!string.IsNullOrWhiteSpace(item.SeriesId)) items.RemoveAll(x => x.SeriesId == item.SeriesId);
                else if (!string.IsNullOrWhiteSpace(item.GoogleRecurringEventId)) items.RemoveAll(x => x.GoogleRecurringEventId == item.GoogleRecurringEventId);
                else items.RemoveAll(x => x.Id == item.Id);
                items.Add(window.Result); ExpandLocalRecurrence(window.Result); Store.Save(items); RenderAll(); return;
            }
            if (window.DeleteRequested)
            {
                var recurring = !string.IsNullOrWhiteSpace(item.SeriesId) || !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId) || !string.IsNullOrWhiteSpace(item.RecurrenceFrequency);
                var deleteScope = "single";
                if (recurring)
                {
                    var deleteWindow = new RepeatDeleteWindow(item); PlaceCalendarDialog(deleteWindow);
                    if (deleteWindow.ShowDialog() != true) return;
                    deleteScope = deleteWindow.Scope;
                }
                var isGoogle = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) || !string.IsNullOrWhiteSpace(item.GoogleEventId);
                if (isGoogle && GoogleCalendar.IsConnected)
                {
                    try
                    {
                        if (deleteScope == "future") await GoogleCalendar.TrimSeriesBeforeAsync(item);
                        else await GoogleCalendar.DeleteAsync(item, deleteScope == "all");
                    }
                    catch (Exception ex) { ErrorLog.Write("Delete Google event", ex); ShowItemNotice(item, "Google에서 삭제하지 못했습니다 · 일정은 유지됩니다."); return; }
                }
                if (deleteScope == "all" && !string.IsNullOrWhiteSpace(item.SeriesId)) items.RemoveAll(x => x.SeriesId == item.SeriesId);
                else if (deleteScope == "all" && !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId)) items.RemoveAll(x => x.GoogleRecurringEventId == item.GoogleRecurringEventId);
                else if (deleteScope == "future" && !string.IsNullOrWhiteSpace(item.SeriesId)) items.RemoveAll(x => x.SeriesId == item.SeriesId && x.Start >= item.Start);
                else if (deleteScope == "future" && !string.IsNullOrWhiteSpace(item.GoogleRecurringEventId)) items.RemoveAll(x => x.GoogleRecurringEventId == item.GoogleRecurringEventId && x.Start >= item.Start);
                else items.RemoveAll(x => x.Id == item.Id);
            }
            else
            {
                var oldGoogle = !string.IsNullOrWhiteSpace(item.GoogleCalendarId) || !string.IsNullOrWhiteSpace(item.GoogleEventId);
                var newGoogle = !string.IsNullOrWhiteSpace(window.Result.GoogleCalendarId);
                if (!window.ApplyToSeries && !string.IsNullOrWhiteSpace(item.SeriesId) && !oldGoogle)
                { window.Result.SeriesId = null; window.Result.RecurrenceFrequency = null; window.Result.RecurrenceMode = null; window.Result.RecurrenceDays = null; window.Result.RecurrenceUntil = window.Result.Start.Date; window.Result.RecurrenceCount = 0; }
                var movedCalendar = oldGoogle && (!newGoogle || item.GoogleCalendarId != window.Result.GoogleCalendarId);
                if (movedCalendar && GoogleCalendar.IsConnected)
                    try { await GoogleCalendar.DeleteAsync(item, window.ApplyToSeries); } catch (Exception ex) { ErrorLog.Write("Delete Google recurrence", ex); ShowItemNotice(item, "Google 캘린더를 변경하지 못했습니다."); return; }
                var index = items.FindIndex(x => x.Id == item.Id);
                if (index >= 0) items[index] = window.Result;
                if (window.ApplyToSeries && !string.IsNullOrWhiteSpace(item.SeriesId) && string.IsNullOrWhiteSpace(item.GoogleCalendarId) &&
                    (oldRecurrence != window.Result.RecurrenceFrequency || oldMode != window.Result.RecurrenceMode || oldDays != window.Result.RecurrenceDays || oldUntil.Date != window.Result.RecurrenceUntil.Date || oldCount != window.Result.RecurrenceCount))
                {
                    RebuildLocalSeries(window.Result, originalSeriesStart);
                }
                if (window.ApplyToSeries && !string.IsNullOrWhiteSpace(item.SeriesId))
                {
                    var duration = window.Result.End - window.Result.Start;
                    foreach (var sibling in items.Where(x => x.SeriesId == item.SeriesId && x.Id != window.Result.Id))
                    {
                        sibling.Title = window.Result.Title; sibling.Notes = window.Result.Notes; sibling.Category = window.Result.Category;
                        sibling.AllDay = window.Result.AllDay; sibling.IsTodo = window.Result.IsTodo;
                        sibling.RolloverMode = window.Result.RolloverMode; sibling.AutoRollover = window.Result.AutoRollover;
                        sibling.Important = window.Result.Important; sibling.ShowDday = window.Result.ShowDday; sibling.ReminderMinutes = window.Result.ReminderMinutes; sibling.ReminderConfigured = true;
                        sibling.AnniversaryDate = window.Result.AnniversaryDate;
                        sibling.RecurrenceFrequency = window.Result.RecurrenceFrequency; sibling.RecurrenceMode = window.Result.RecurrenceMode;
                        sibling.RecurrenceDays = window.Result.RecurrenceDays; sibling.RecurrenceUntil = window.Result.RecurrenceUntil; sibling.RecurrenceCount = window.Result.RecurrenceCount;
                        sibling.Start = sibling.Start.Date.Add(window.Result.Start.TimeOfDay); sibling.End = sibling.Start.Add(duration);
                    }
                }
                if (newGoogle && GoogleCalendar.IsConnected)
                {
                    // D-Day 등 사용자가 방금 바꾼 내용은 Google 응답을 기다리지 않고
                    // 먼저 로컬 화면에 반영한다. 동기화 결과는 이어지는 저장에서 갱신한다.
                    window.Result.PendingGoogleSync = true;
                    Store.Save(items); RenderAll();
                    await SaveGoogleItem(window.Result, window.ApplyToSeries);
                }
            }
            Store.Save(items); RenderAll();
        }

        void RebuildLocalSeries(PlannerItem edited, DateTime originalStart)
        {
            var seriesId = edited.SeriesId; var duration = edited.End - edited.Start; items.RemoveAll(x => x.SeriesId == seriesId && x.Id != edited.Id);
            edited.Start = originalStart.Date.Add(edited.Start.TimeOfDay); edited.End = edited.Start.Add(duration);
            if (!string.IsNullOrWhiteSpace(edited.RecurrenceFrequency)) ExpandLocalRecurrence(edited);
        }

        async void ShowItemNotice(PlannerItem item, string message)
        {
            itemNoticeId = item.Id; itemNoticeText = message; var version = ++itemNoticeVersion;
            RenderDetail();
            if (positionLocked) SchedulePublish();
            await Task.Delay(1600);
            if (version != itemNoticeVersion) return;
            itemNoticeId = null; itemNoticeText = null; RenderDetail();
            if (positionLocked) SchedulePublish();
        }
    }
}
