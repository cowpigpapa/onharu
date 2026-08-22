using System.Windows;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        TimetableWindow timetableWindow;

        void OpenTimetable(object sender, RoutedEventArgs e)
        {
            if (timetableWindow != null)
            {
                if (timetableWindow.WindowState == WindowState.Minimized) timetableWindow.WindowState = WindowState.Normal;
                timetableWindow.Activate(); return;
            }
            timetableWindow = new TimetableWindow();
            PlaceCalendarDialog(timetableWindow);
            timetableWindow.Closed += delegate { timetableWindow = null; };
            timetableWindow.Show(); timetableWindow.Activate();
        }
    }
}
