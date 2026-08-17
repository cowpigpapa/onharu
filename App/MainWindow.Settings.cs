using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        async void OpenSettings(object sender, RoutedEventArgs e)
        {
            var allLocalItems = Store.LoadLocal();
            var activeIds = new HashSet<string>(items.Select(x => x.Id));
            var localItems = allLocalItems.Where(x => !activeIds.Contains(x.Id)).ToList();
            var localCleanupNeeded = localItems.Count != allLocalItems.Count;
            var window = new SettingsWindow(Colors["업무일정"], Colors["개인일정"], settings.FontSize,
                settings.CalendarOrderMode, settings.MultiDayFirst, settings.CompletedLast, settings.Use24HourTime, settings.ShowWeekNumbers, settings.WeekNumberRule,
                settings.PastelEventStyle, settings.AutoSyncMinutes, settings.GoogleCalendars,
                GoogleCalendar.IsConnected, localItems.Count, settings.ShowLunar, settings.ShowSolarTerms, settings.BackupFolder, Store.Backups().Length, settings.CategoryOrder,
                settings.CustomPalette, settings.CustomPalettePastelStyle, settings.PaletteNames, settings.SavedPalettes,
                settings.CalendarRangeMode, settings.VisibleWeekCount, settings.TodayRow, settings.SelectedDateStyle,
                settings.SelectedDateFillColor, settings.SelectedDateBorderColor, settings.DefaultCalendarKey, settings.DefaultAllDay,
                settings.DefaultStartHour, settings.DefaultStartMinute, settings.DefaultDurationMinutes, settings.DefaultReminderMinutes,
                settings.CompletedDisplayMode, settings.StartViewMode, settings.ReminderSound, settings.QuietStartHour, settings.QuietEndHour,
                settings.StartupPositionMode, settings.CloseButtonAction);
            PlaceCalendarDialog(window);
            var settingsAccepted = window.ShowDialog() == true;
            if (positionLocked) PublishAndHide();
            if (localCleanupNeeded) Store.SaveLocal(localItems);
            if (!settingsAccepted) return;
            Colors["업무일정"] = window.BusinessColor; Colors["개인일정"] = window.PersonalColor;
            settings.BusinessColor = window.BusinessColor; settings.PersonalColor = window.PersonalColor;
            settings.FontSize = window.SelectedFontSize; settings.CalendarOrderMode = window.OrderMode;
            settings.MultiDayFirst = window.MultiDayFirst;
            settings.CompletedLast = window.CompletedLast;
            settings.CompletedDisplayMode = window.CompletedDisplayMode;
            settings.StartViewMode = window.StartViewMode;
            settings.ReminderSound = window.ReminderSound; settings.QuietStartHour = window.QuietStartHour; settings.QuietEndHour = window.QuietEndHour;
            settings.StartupPositionMode = window.StartupPositionMode;
            settings.CloseButtonAction = window.CloseButtonAction;
            settings.Use24HourTime = window.Use24HourTime;
            settings.CategoryOrder = window.CategoryOrder;
            settings.CustomPalette = window.CustomPalette;
            settings.CustomPalettePastelStyle = window.CustomPalettePastelStyle;
            settings.PaletteNames = window.PaletteNames;
            settings.SavedPalettes = window.SavedPalettes;
            settings.ShowWeekNumbers = window.ShowWeekNumbers; settings.WeekNumberRule = window.WeekRule;
            settings.CalendarRangeMode = window.CalendarRangeMode; settings.VisibleWeekCount = window.VisibleWeekCount; settings.TodayRow = window.TodayRow;
            if (settings.CalendarRangeMode == "weeks") shownMonth = DateTime.Today;
            settings.ShowLunar = window.ShowLunar;
            settings.ShowSolarTerms = window.ShowSolarTerms;
            settings.SelectedDateStyle = window.SelectedDateStyle;
            settings.SelectedDateFillColor = window.SelectedDateFillColor;
            settings.SelectedDateBorderColor = window.SelectedDateBorderColor;
            settings.BackupFolder = window.BackupFolder;
            settings.PastelEventStyle = window.PastelEventStyle;
            settings.AutoSyncMinutes = window.AutoSyncMinutes;
            settings.DefaultCalendarKey = window.DefaultCalendarKey; settings.DefaultAllDay = window.DefaultAllDay;
            settings.DefaultStartHour = window.DefaultStartHour; settings.DefaultStartMinute = window.DefaultStartMinute;
            settings.DefaultDurationMinutes = window.DefaultDurationMinutes; settings.DefaultReminderMinutes = window.DefaultReminderMinutes;
            FontSize = settings.FontSize;
            monthTitle.FontSize = Ui(24); selectedTitle.FontSize = Ui(16);
            Store.SaveSettings(settings);
            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId)))
            {
                var source = settings.GoogleCalendars.FirstOrDefault(x => x.Id == item.GoogleCalendarId);
                if (source != null) { item.GoogleCalendarColor = source.Color; item.GoogleReadOnly = !source.Editable; }
            }
            Store.Save(items); BuildGoogleFilters(); StartAutoSync();
            foreach (var category in filters.Keys.Where(Colors.ContainsKey)) filters[category].Foreground = Brush(Colors[category]);
            RenderAll();
            if (window.ImportLocalItems)
            {
                var importWindow = new LocalImportWindow(localItems); PlaceCalendarDialog(importWindow);
                if (importWindow.ShowDialog() == true)
                {
                    foreach (var item in importWindow.SelectedItems)
                        if (!items.Any(x => x.Id == item.Id)) items.Add(item);
                    localItems.RemoveAll(x => importWindow.SelectedItems.Any(y => y.Id == x.Id));
                    Store.Save(items); Store.SaveLocal(localItems); RenderAll();
                }
            }
            if (window.RestoreBackup)
            {
                var backup = new BackupWindow(Store.Backups(), new string[0]);
                PlaceCalendarDialog(backup);
                if (backup.ShowDialog() == true)
                {
                    try
                    {
                        var googleItems = items.Where(Store.IsGoogleItem).ToList();
                        var restored = Store.Restore(backup.SelectedPath);
                        items.Clear(); items.AddRange(googleItems); items.AddRange(restored); Store.Save(items); RenderAll();
                    }
                    catch (Exception ex) { ErrorLog.Write("Restore backup", ex); ShowNotice("백업 파일을 읽지 못했습니다.", true); }
                }
            }
            if (window.ImportItemsFile)
            {
                using (var picker = new Forms.OpenFileDialog { Title = "ONHARU 일정 파일 선택", Filter = "ONHARU JSON 일정|*.json|모든 파일|*.*", CheckFileExists = true })
                {
                    if (picker.ShowDialog() == Forms.DialogResult.OK)
                    {
                        try
                        {
                            int googleExcluded;
                            var imported = Store.ReadImportFile(picker.FileName, out googleExcluded);
                            var importWindow = new LocalImportWindow(imported, googleExcluded, items.Where(x => !Store.IsGoogleItem(x)).ToList());
                            if (importWindow.CandidateCount == 0) ShowNotice("신규 또는 변경된 로컬 일정이 없습니다.", false);
                            else
                            {
                                PlaceCalendarDialog(importWindow);
                                if (importWindow.ShowDialog() == true)
                                {
                                    var selectedIds = new HashSet<string>(importWindow.SelectedItems.Select(x => x.Id));
                                    items.RemoveAll(x => !Store.IsGoogleItem(x) && selectedIds.Contains(x.Id));
                                    items.AddRange(importWindow.SelectedItems); Store.Save(items); RenderAll();
                                }
                            }
                        }
                        catch (Exception ex) { ErrorLog.Write("Import calendar data", ex); ShowNotice("일정 파일을 읽지 못했습니다.", true); }
                    }
                }
            }
            if (window.ExportItems)
            {
                using (var dialog = new Forms.SaveFileDialog { FileName = "ONHARU-일정-" + DateTime.Today.ToString("yyyyMMdd") + ".json", Filter = "ONHARU JSON 일정|*.json", AddExtension = true })
                    if (dialog.ShowDialog() == Forms.DialogResult.OK)
                        try { ExportService.Json(dialog.FileName, items.Where(x => !Store.IsGoogleItem(x)).OrderBy(x => x.Start).ToList()); ShowNotice("ONHARU 로컬 일정을 저장했습니다.", false); }
                        catch (Exception ex) { ErrorLog.Write("Export local calendar data", ex); ShowNotice("일정을 저장하지 못했습니다.", true); }
            }
            if (window.ExportExcel)
            {
                using (var dialog = new Forms.SaveFileDialog { FileName = "ONHARU-전체일정-" + DateTime.Today.ToString("yyyyMMdd") + ".csv", Filter = "Excel CSV 파일|*.csv", AddExtension = true })
                    if (dialog.ShowDialog() == Forms.DialogResult.OK)
                        try { ExportService.Csv(dialog.FileName, items.OrderBy(x => x.Start).ToList()); ShowNotice("전체 일정을 Excel용 CSV로 저장했습니다.", false); }
                        catch (Exception ex) { ErrorLog.Write("Export all schedules to Excel", ex); ShowNotice("Excel 파일을 저장하지 못했습니다.", true); }
            }
            if (window.ChangeGoogleAccount || window.LogoutGoogleAccount)
            {
                GoogleCalendar.Disconnect();
                Store.SetAccount(null);
                items.Clear();
                settings.ActiveGoogleAccountId = null;
                settings.GoogleCalendars.Clear();
                if (window.LogoutGoogleAccount) items.AddRange(Store.LoadLocal());
                Store.SaveSettings(settings); BuildGoogleFilters(); RenderAll(); UpdateGoogleButton();
                if (window.LogoutGoogleAccount) { StartAutoSync(); return; }
                if (await ConnectGoogle(false)) await SyncGoogle(true);
                StartAutoSync();
            }
        }

        void OpenSearch(object sender, RoutedEventArgs e)
        {
            var window = new SearchWindow(items.Where(IsItemVisible).ToList()); PlaceCalendarDialog(window);
            if (window.ShowDialog() == true && window.SelectedItem != null)
            { selectedDate = window.SelectedItem.Start.Date; detailMode = "selected"; shownMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1); RenderAll(); OpenEdit(window.SelectedItem); }
        }

        void SaveWindowSettings()
        {
            settings.HasPosition = true; settings.Left = Left; settings.Top = Top;
            settings.Width = ActualWidth; settings.Height = ActualHeight; settings.PositionLocked = positionLocked;
            settings.LastShownDate = shownMonth;
            settings.FontSize = FontSize; settings.Opacity = Opacity;
            if (filters.ContainsKey("업무일정")) settings.BusinessVisible = filters["업무일정"].IsChecked == true;
            if (filters.ContainsKey("개인일정")) settings.PersonalVisible = filters["개인일정"].IsChecked == true;
            if (filters.ContainsKey("기념일")) settings.AnniversaryVisible = filters["기념일"].IsChecked == true;
            if (filters.ContainsKey("D-Day")) settings.DdayPanelVisible = filters["D-Day"].IsChecked == true;
            if (filters.ContainsKey("국경일")) settings.HolidayVisible = filters["국경일"].IsChecked == true;
            Store.SaveSettings(settings);
        }
    }
}
