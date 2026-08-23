using System;
using System.Linq;
using System.Windows;

namespace FamilyPlanner
{
    public sealed class DiaryDateHitTarget
    {
        public DateTime Date;
        public DiaryDateHitTarget(DateTime date) { Date = date.Date; }
    }

    public partial class MainWindow
    {
        DiaryReaderWindow diaryReaderWindow;

        void OpenDiaryEditor(DateTime date)
        {
            if (!settings.UseDiary) { selectedDate = date.Date; AddItem(null, null); return; }
            var existing = DiaryStore.Load().FirstOrDefault(x => x.Date.Date == date.Date);
            var window = new DiaryEditorWindow(date, existing);
            PlaceCalendarDialog(window);
            if (ShowBlockingDialog(window) == true && window.Result != null) { DiaryStore.Upsert(window.Result, existing == null ? (DateTime?)null : existing.Date); RefreshDiaryDates(); }
            if (positionLocked && IsVisible) PublishAndHide();
        }

        void OpenDiaryReader(object sender, RoutedEventArgs e)
        {
            if (!settings.UseDiary) return;
            if (diaryReaderWindow != null)
            {
                if (diaryReaderWindow.WindowState == WindowState.Minimized) diaryReaderWindow.WindowState = WindowState.Normal;
                diaryReaderWindow.Activate(); return;
            }
            diaryReaderWindow = new DiaryReaderWindow(selectedDate);
            PlaceCalendarDialog(diaryReaderWindow);
            diaryReaderWindow.Changed += RefreshDiaryDates;
            diaryReaderWindow.Closed += delegate { diaryReaderWindow = null; };
            diaryReaderWindow.Show(); diaryReaderWindow.Activate();
        }

        void RefreshDiaryDates()
        {
            if (!settings.UseDiary) { diaryDates.Clear(); diaryDatesLoaded = false; RenderAll(); return; }
            diaryDates.Clear(); foreach (var entry in DiaryStore.Load()) diaryDates.Add(entry.Date.Date); diaryDatesLoaded = true; RenderAll();
        }
    }
}
