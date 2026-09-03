using System;
using System.Linq;
using System.Windows;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        DiaryReaderWindow diaryReaderWindow;

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
