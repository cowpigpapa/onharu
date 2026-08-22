using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var settingsWatch = Stopwatch.StartNew();
            var allLocalItems = Store.LoadLocal();
            var activeIds = new HashSet<string>(items.Select(x => x.Id));
            var localItems = allLocalItems.Where(x => !activeIds.Contains(x.Id)).ToList();
            var localCleanupNeeded = localItems.Count != allLocalItems.Count;
            var backupCount = Store.Backups().Length; PlacementTrace.Write("SETTINGS data-ready ms=" + settingsWatch.ElapsedMilliseconds); settingsWatch.Restart();
            var window = new SettingsWindow(Colors["업무일정"], Colors["개인일정"], settings.FontSize,
                settings.CalendarOrderMode, settings.MultiDayFirst, settings.CompletedLast, settings.Use24HourTime, settings.ShowWeekNumbers, settings.WeekNumberRule, settings.WeekStartDay, settings.RestDays,
                settings.PastelEventStyle, settings.AutoSyncMinutes, settings.GoogleCalendars,
                GoogleCalendar.IsConnected, localItems.Count, settings.ShowLunar, settings.ShowSolarTerms, settings.BackupFolder, backupCount, settings.CategoryOrder,
                settings.CustomPalette, settings.CustomPalettePastelStyle, settings.PaletteNames, settings.SavedPalettes,
                settings.CalendarRangeMode, settings.VisibleWeekCount, settings.TodayRow, settings.SelectedDateStyle,
                settings.SelectedDateFillColor, settings.SelectedDateBorderColor, settings.TodayColor, settings.TodayStyle, settings.TodayBorderColor, settings.DefaultCalendarKey, settings.DefaultAllDay,
                settings.DefaultStartHour, settings.DefaultStartMinute, settings.DefaultDurationMinutes, settings.DefaultReminderMinutes,
                settings.CompletedDisplayMode, settings.StartViewMode, settings.ReminderSound, settings.QuietStartHour, settings.QuietEndHour,
                settings.StartupPositionMode, settings.CloseButtonAction, settings.UseTimetable, settings.UseDiary, settings.UseRollover, settings.ShowGoogleTasks, settings.UseProBaseball);
            PlacementTrace.Write("SETTINGS ui-created ms=" + settingsWatch.ElapsedMilliseconds);
            window.PrintRequested += delegate
            {
                UpdateLayout();
                var printWindow = new CalendarPrintWindow(Content as System.Windows.Media.Visual) { Owner = window };
                printWindow.ShowDialog();
            };
            PlaceCalendarDialog(window);
            var settingsAccepted = window.ShowDialog() == true;
            if (positionLocked) PublishAndHide();
            if (localCleanupNeeded) Store.SaveLocal(localItems);
            if (!settingsAccepted) return;
            var googleTasksChanged = settings.ShowGoogleTasks != window.ShowGoogleTasks;
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
            settings.ShowWeekNumbers = window.ShowWeekNumbers; settings.WeekNumberRule = window.WeekRule; settings.WeekStartDay = window.WeekStartDay;
            settings.RestDays = window.RestDays == null ? new List<int> { 0, 6 } : window.RestDays;
            settings.CalendarRangeMode = window.CalendarRangeMode; settings.VisibleWeekCount = window.VisibleWeekCount; settings.TodayRow = window.TodayRow;
            if (settings.CalendarRangeMode == "weeks") shownMonth = DateTime.Today;
            settings.ShowLunar = window.ShowLunar;
            settings.ShowSolarTerms = window.ShowSolarTerms;
            settings.UseTimetable = window.UseTimetable;
            settings.UseDiary = window.UseDiary;
            settings.UseRollover = window.UseRollover;
            settings.ShowGoogleTasks = window.ShowGoogleTasks;
            settings.UseProBaseball = window.UseProBaseball;
            if (googleTasksChanged && settings.ShowGoogleTasks)
                foreach (var source in settings.GoogleCalendars.Where(x => GoogleTasks.IsSource(x.Id))) source.Editable = true;
            if (timetableButton != null) timetableButton.Visibility = settings.UseTimetable ? Visibility.Visible : Visibility.Collapsed;
            if (diaryButton != null) diaryButton.Visibility = settings.UseDiary ? Visibility.Visible : Visibility.Collapsed;
            if (sportsButton != null) sportsButton.Visibility = settings.UseProBaseball ? Visibility.Visible : Visibility.Collapsed;
            if (!settings.UseDiary && diaryReaderWindow != null) diaryReaderWindow.Close();
            diaryDates.Clear(); diaryDatesLoaded = false;
            settings.SelectedDateStyle = window.SelectedDateStyle;
            settings.SelectedDateFillColor = window.SelectedDateFillColor;
            settings.SelectedDateBorderColor = window.SelectedDateBorderColor;
            settings.TodayColor = window.TodayColor;
            settings.TodayStyle = window.TodayStyle;
            settings.TodayBorderColor = window.TodayBorderColor;
            settings.BackupFolder = window.BackupFolder;
            settings.PastelEventStyle = window.PastelEventStyle;
            settings.AutoSyncMinutes = window.AutoSyncMinutes;
            settings.DefaultCalendarKey = window.DefaultCalendarKey; settings.DefaultAllDay = window.DefaultAllDay;
            settings.DefaultStartHour = window.DefaultStartHour; settings.DefaultStartMinute = window.DefaultStartMinute;
            settings.DefaultDurationMinutes = window.DefaultDurationMinutes; settings.DefaultReminderMinutes = window.DefaultReminderMinutes;
            FontSize = settings.FontSize;
            UpdateCompactHeaderTypography(); selectedTitle.FontSize = Ui(16);
            Store.SaveSettings(settings);
            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId)))
            {
                var source = settings.GoogleCalendars.FirstOrDefault(x => x.Id == item.GoogleCalendarId);
                if (source != null)
                {
                    item.GoogleCalendarColor = source.Color;
                    item.GoogleReadOnly = GoogleTasks.IsTask(item) ? !item.OnharuManaged || !source.Editable : !source.Editable;
                }
            }
            Store.Save(items); BuildGoogleFilters(); StartAutoSync();
            foreach (var category in filters.Keys.Where(Colors.ContainsKey)) filters[category].Foreground = Brush(Colors[category]);
            RenderAll();
            if (googleTasksChanged && settings.ShowGoogleTasks && GoogleCalendar.IsConnected) await SyncGoogle(false);
            if (window.RequestedDataAction == SettingsDataAction.ImportDormantLocal)
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
            if (window.RequestedDataAction == SettingsDataAction.RestoreBackup)
            {
                var backup = new BackupWindow(Store.Backups(), new string[0], Store.BackupDirectory());
                PlaceCalendarDialog(backup);
                if (backup.ShowDialog() == true)
                {
                    try
                    {
                        var googleItems = items.Where(Store.IsGoogleItem).ToList();
                        var restored = Store.Restore(backup.SelectedPath);
                        items.Clear(); items.AddRange(googleItems); items.AddRange(restored); Store.Save(items); RenderAll();
                        ShowNotice("로컬 일정만 복원했습니다. Google 일정에는 영향을 주지 않았습니다.", false);
                    }
                    catch (Exception ex) { ErrorLog.Write("Restore backup", ex); ShowNotice("백업 파일을 읽지 못했습니다.", true); }
                }
            }
            if (window.RequestedDataAction == SettingsDataAction.ImportFile)
            {
                var importFormat = window.RequestedDataFormat ?? "json";
                var importFilter = importFormat == "ics" ? "표준 달력 ICS|*.ics" : importFormat == "csv" ? "Excel CSV 파일|*.csv" : "ONHARU JSON 일정|*.json";
                using (var picker = new Forms.OpenFileDialog { Title = "일정 파일 선택", Filter = importFilter + "|모든 파일|*.*", CheckFileExists = true,
                    InitialDirectory = DataDialogFolder(), RestoreDirectory = true })
                {
                    if (picker.ShowDialog() == Forms.DialogResult.OK)
                    {
                        RememberDataDialogFolder(picker.FileName);
                        try
                        {
                            int googleExcluded = 0;
                            var imported = importFormat == "ics" ? CalendarExchangeService.ReadIcs(picker.FileName) : importFormat == "csv" ? CalendarExchangeService.ReadCsv(picker.FileName) : Store.ReadImportFile(picker.FileName, out googleExcluded);
                            var importWindow = new LocalImportWindow(imported, googleExcluded, items.Where(x => !Store.IsGoogleItem(x)).ToList(), importFormat == "csv");
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
            if (window.RequestedDataAction == SettingsDataAction.ExportFile)
            {
                var ics = window.RequestedDataFormat == "ics";
                var csv = window.RequestedDataFormat == "csv";
                var extension = ics ? ".ics" : csv ? ".csv" : ".json";
                using (var dialog = new Forms.SaveFileDialog { FileName = (csv ? "ONHARU-전체일정-" : "ONHARU-일정-") + DateTime.Today.ToString("yyyyMMdd") + extension,
                    Filter = ics ? "표준 달력 ICS|*.ics" : csv ? "Excel CSV 파일|*.csv" : "ONHARU JSON 일정|*.json", AddExtension = true,
                    InitialDirectory = DataDialogFolder(), RestoreDirectory = true })
                    if (dialog.ShowDialog() == Forms.DialogResult.OK)
                        try
                        {
                            RememberDataDialogFolder(dialog.FileName);
                            if (ics) CalendarExchangeService.Ics(dialog.FileName, items.Where(x => !Store.IsGoogleItem(x)).OrderBy(x => x.Start).ToList());
                            else if (csv) ExportService.Csv(dialog.FileName, items.OrderBy(x => x.Start).ToList());
                            else ExportService.Json(dialog.FileName, items.Where(x => !Store.IsGoogleItem(x)).OrderBy(x => x.Start).ToList());
                            ShowNotice(csv ? "Google을 포함한 전체 일정을 Excel CSV로 저장했습니다." : "ONHARU 로컬 일정을 저장했습니다.", false, "PC로 내보내기");
                        }
                        catch (Exception ex) { ErrorLog.Write("Export calendar data", ex); ShowNotice("일정을 저장하지 못했습니다.", true); }
            }
            if (window.RequestedDataAction == SettingsDataAction.ExportEmail)
            {
                var format = window.RequestedDataFormat == "ics" ? "표준 달력 ICS" : window.RequestedDataFormat == "csv" ? "Excel CSV" : "ONHARU JSON";
                ShowNotice(format + " 파일을 내 메일로 보내는 기능은 현재 구현 중입니다.\n지금은 PC로 내보낸 파일을 직접 전달해 주세요.", false, "메일로 보내기");
            }
            if (window.RequestedDataAction == SettingsDataAction.DeleteLocalData)
            {
                var deleteWindow = new LocalDataDeleteWindow(items); PlaceCalendarDialog(deleteWindow);
                if (deleteWindow.ShowDialog() == true)
                {
                    var selected = deleteWindow.SelectedEntries;
                    Store.BackupBeforeDestructiveChange(items);
                    foreach (var entry in selected)
                    {
                        if (entry.GoogleDdayOnly)
                        {
                            foreach (var google in items.Where(x => entry.Matches(x)))
                            { google.ShowDday = false; google.AnniversaryDate = google.Start.Date; }
                        }
                        else items.RemoveAll(x => entry.Matches(x));
                    }
                    Store.Save(items); RenderAll();
                    ShowNotice(selected.Count + "개 항목을 정리했습니다. Google 원본 일정은 삭제하지 않았습니다.", false);
                }
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
                if (await ConnectGoogle(false)) { LoadConnectedAccountItems(); await SyncGoogle(true); }
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
            SavePhysicalPlacement();
            settings.LastShownDate = shownMonth;
            // The cloaked WPF relay window can temporarily be opacity 0 while the
            // Explorer frame is visible. Never persist that transition value.
            settings.FontSize = FontSize;
            if (opacitySlider != null) settings.Opacity = Math.Max(opacitySlider.Minimum, Math.Min(opacitySlider.Maximum, opacitySlider.Value));
            if (filters.ContainsKey("업무일정")) settings.BusinessVisible = filters["업무일정"].IsChecked == true;
            if (filters.ContainsKey("개인일정")) settings.PersonalVisible = filters["개인일정"].IsChecked == true;
            if (filters.ContainsKey("야구")) settings.BaseballVisible = filters["야구"].IsChecked == true;
            if (filters.ContainsKey("기념일")) settings.AnniversaryVisible = filters["기념일"].IsChecked == true;
            if (filters.ContainsKey("D-Day")) settings.DdayPanelVisible = filters["D-Day"].IsChecked == true;
            if (filters.ContainsKey("국경일")) settings.HolidayVisible = filters["국경일"].IsChecked == true;
            Store.SaveSettings(settings);
        }

        string DataDialogFolder()
        {
            return !string.IsNullOrWhiteSpace(settings.LastDataFolder) && System.IO.Directory.Exists(settings.LastDataFolder)
                ? settings.LastDataFolder : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        void RememberDataDialogFolder(string fileName)
        {
            var folder = System.IO.Path.GetDirectoryName(fileName);
            if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder)) return;
            settings.LastDataFolder = folder; Store.SaveSettings(settings);
        }
    }
}
