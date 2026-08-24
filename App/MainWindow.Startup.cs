using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void RestoreConnectedGoogleAccount()
        {
            if (!GoogleCalendar.IsConnected) settings.ActiveGoogleAccountId = null;
            var connectedAccount = GoogleCalendar.ConnectedAccountId;
            if (GoogleCalendar.IsConnected && !string.IsNullOrWhiteSpace(connectedAccount))
                settings.ActiveGoogleAccountId = connectedAccount;
            else if (GoogleCalendar.IsConnected && string.IsNullOrWhiteSpace(settings.ActiveGoogleAccountId)
                && settings.GoogleCalendars != null)
            {
                var savedPrimary = settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
                if (savedPrimary != null) settings.ActiveGoogleAccountId = savedPrimary.Id;
            }
        }

        void RepairLoadedData()
        {
            if (!settings.AnniversarySeparationComplete)
            {
                foreach (var legacy in items.Where(x => x.ShowDday && string.IsNullOrWhiteSpace(x.AnniversaryType)))
                { legacy.AnniversaryType = InferAnniversaryType(legacy.Title); legacy.Category = "기념일"; }
                settings.AnniversarySeparationComplete = true; Store.SaveSettings(settings); Store.Save(items);
            }

            var correctedAnniversaryCategory = false;
            foreach (var anniversary in items.Where(x => !string.IsNullOrWhiteSpace(x.AnniversaryType) && x.Category != "기념일"))
            { anniversary.Category = "기념일"; correctedAnniversaryCategory = true; }
            foreach (var anniversary in items.Where(x => !string.IsNullOrWhiteSpace(x.AnniversaryType) && x.CreatedInOnharu &&
                (!string.IsNullOrWhiteSpace(x.GoogleCalendarId) || !string.IsNullOrWhiteSpace(x.GoogleEventId))))
            {
                anniversary.GoogleCalendarId = null; anniversary.GoogleCalendarName = null; anniversary.GoogleCalendarColor = null;
                anniversary.GoogleEventId = null; anniversary.GoogleEventType = null; anniversary.GoogleRecurringEventId = null;
                anniversary.GoogleReadOnly = false; anniversary.OnharuManaged = false; anniversary.PendingGoogleSync = false;
                correctedAnniversaryCategory = true;
            }
            if (correctedAnniversaryCategory) Store.Save(items);

            var clearedOrphanPending = false;
            foreach (var orphan in items.Where(x => x.PendingGoogleSync && string.IsNullOrWhiteSpace(x.GoogleCalendarId)))
            { orphan.PendingGoogleSync = false; clearedOrphanPending = true; }
            if (clearedOrphanPending) Store.Save(items);

            var orphanSeries = items.Where(x => string.IsNullOrWhiteSpace(x.GoogleCalendarId)
                && !string.IsNullOrWhiteSpace(x.RecurrenceFrequency) && string.IsNullOrWhiteSpace(x.SeriesId)).ToList();
            foreach (var master in orphanSeries) ExpandLocalRecurrence(master);
            if (orphanSeries.Count > 0) Store.Save(items);
        }

        void ConfigureInitialWindow()
        {
            RandomizeRecommendedPalettePlacement();
            if (!string.IsNullOrWhiteSpace(settings.BusinessColor)) Colors["업무일정"] = settings.BusinessColor;
            if (!string.IsNullOrWhiteSpace(settings.PersonalColor)) Colors["개인일정"] = settings.PersonalColor;
            if (!string.IsNullOrWhiteSpace(settings.BaseballColor)) Colors["야구"] = settings.BaseballColor;
            if (!string.IsNullOrWhiteSpace(settings.DdayColor)) Colors["D-Day"] = settings.DdayColor;
            if (!string.IsNullOrWhiteSpace(settings.AnniversaryColor)) Colors["기념일"] = settings.AnniversaryColor;
            if (!string.IsNullOrWhiteSpace(settings.HolidayColor)) Colors["국경일"] = settings.HolidayColor;
            positionLocked = settings.HasPosition ? settings.PositionLocked : true;
            calendarMinimized = false;
            if (settings.StartupPositionMode == "locked") positionLocked = true;
            else if (settings.StartupPositionMode == "editable") positionLocked = false;
            if (PlacementTrace.IsEnabled && string.Equals(
                Environment.GetEnvironmentVariable("ONHARU_PLACEMENT_START_LOCKED"), "1", StringComparison.Ordinal))
                positionLocked = true;

            var startDate = settings.StartViewMode == "last" && settings.LastShownDate.Year >= 1900
                ? settings.LastShownDate : DateTime.Today;
            temporaryMonthView = settings.UseMonthView;
            shownMonth = !temporaryMonthView ? startDate
                : new DateTime(startDate.Year, startDate.Month, 1);
            selectedDate = startDate.Date;
            Title = "온하루"; Width = settings.Width >= 820 ? settings.Width : 1120;
            Height = settings.Height >= 560 ? settings.Height : 700; MinWidth = 820; MinHeight = 560;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            FontSize = settings.FontSize > 0 ? settings.FontSize : 12;
            UpdateCompactHeaderTypography(); monthTitle.Foreground = BrandBrush(); selectedTitle.Foreground = T("Text"); selectedTitle.FontSize = Ui(16);
            Opacity = settings.Opacity;
            if (settings.HasPosition)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.Left; Top = settings.Top;
            }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = false; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
        }

        bool RandomizeRecommendedPalettePlacement()
        {
            if (settings.LockPalettePlacement || settings.SelectedPaletteIndex < 0 || settings.SelectedPaletteIndex >= 5) return false;
            var palettes = OnharuColorPresets.Palettes();
            var selected = palettes[settings.SelectedPaletteIndex];
            if (settings.SavedPalettes != null && settings.SavedPalettes.Count > settings.SelectedPaletteIndex)
            {
                var saved = (settings.SavedPalettes[settings.SelectedPaletteIndex] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (saved.Length >= 6) selected = saved;
            }

            var sources = (settings.GoogleCalendars ?? new List<GoogleCalendarSetting>())
                .Where(x => !GoogleTasks.IsSource(x.Id) && !IsHolidayCalendar(x)).ToList();
            var primary = GoogleCalendar.IsConnected ? sources.FirstOrDefault(x => x.Primary) : null;
            var representative = OnharuColorPresets.RepresentativeColor(settings.SelectedPaletteIndex);
            var colors = selected.Skip(6).Concat(new[] { selected[1], selected[3], selected[4] })
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals(representative, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => Guid.NewGuid()).ToList();
            colors.AddRange(sources.Select(x => x.Color).Concat(new[] { settings.BusinessColor, settings.PersonalColor, settings.DdayColor, settings.AnniversaryColor })
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals(representative, StringComparison.OrdinalIgnoreCase))
                .Where(x => !colors.Contains(x, StringComparer.OrdinalIgnoreCase)));

            var colorIndex = 0;
            Func<string> next = delegate { return colorIndex < colors.Count ? colors[colorIndex++] : representative; };
            if (primary != null) primary.Color = representative; else settings.BusinessColor = representative;
            if (primary != null) settings.BusinessColor = next();
            settings.PersonalColor = next();
            settings.DdayColor = next();
            settings.AnniversaryColor = next();
            foreach (var source in sources.Where(x => x != primary)) source.Color = next();
            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId)))
            {
                var source = sources.FirstOrDefault(x => x.Id == item.GoogleCalendarId);
                if (source != null) item.GoogleCalendarColor = source.Color;
            }
            Colors["업무일정"] = settings.BusinessColor; Colors["개인일정"] = settings.PersonalColor;
            Colors["D-Day"] = settings.DdayColor; Colors["기념일"] = settings.AnniversaryColor;
            return true;
        }

        void AttachWindowLifecycle()
        {
            Loaded += async delegate
            {
                explorerFrame.SetActionSink(desktopActions.WindowHandle);
                RestorePhysicalPlacement();
                EnsureWindowOnScreen(false);
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
                CreateTrayIcon(); UpdateModeButtons(); UpdateGoogleButton();
                UpdateLayout(); RenderAll(); UpdateLayout();
                if (positionLocked) SchedulePublish();
                else ShowPositionEditor();
                StartAutoSync();
                await CheckForUpdatesAsync(false);
                if (GoogleCalendar.IsConnected) await SyncGoogle(false);
            };
            Closing += delegate
            {
                CloseAuxiliaryWindows();
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
                Store.Save(items); SaveWindowSettings();
                explorerFrame.Dispose(); desktopActions.Dispose();
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
            };
            new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Normal,
                delegate { Rollover(); }, Dispatcher);
            reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            reminderTimer.Tick += delegate { SafeCheckReminders(); }; reminderTimer.Start();
            syncRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            syncRetryTimer.Tick += async delegate
            {
                if (GoogleCalendar.IsConnected && (syncProblem != null || items.Any(x => x.PendingGoogleSync)))
                    await SyncGoogle(false);
            };
            syncRetryTimer.Start();
        }
    }
}
