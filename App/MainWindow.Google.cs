using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        bool localItemsOfferShown;
        bool randomizePaletteAfterConnect;

        async void GoogleClick(object sender, RoutedEventArgs e)
        {
            if (googleConnecting)
            {
                GoogleCalendar.CancelConnect(); googleConnecting = false; UpdateGoogleButton();
                ShowGoogleStatus("Google 로그인을 취소했습니다.", 1500); return;
            }
            if (!GoogleCalendar.IsConnected)
            {
                if (!await ConnectGoogle(true)) return;
                LoadConnectedAccountItems();
            }
            await SyncGoogle(true);
        }

        async void OpenGoogleAccountSettings(object sender, RoutedEventArgs e)
        {
            if (ActivateBlockingDialog()) return;
            if (!GoogleCalendar.IsConnected) { GoogleClick(sender, e); return; }
            var primary = settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
            var chooser = new GoogleAccountActionWindow(primary == null ? null : primary.Name); PlaceCalendarDialog(chooser);
            if (ShowBlockingDialog(chooser) != true) return;
            var logout = chooser.SelectedAction == "logout";
            if (!logout && chooser.SelectedAction != "change") return;
            GoogleCalendar.Disconnect(); Store.SetAccount(null); items.Clear();
            settings.ActiveGoogleAccountId = null; settings.GoogleCalendars.Clear();
            if (logout) items.AddRange(Store.LoadLocal());
            Store.SaveSettings(settings); BuildGoogleFilters(); RenderAll(); UpdateGoogleButton();
            if (!logout && await ConnectGoogle(false)) { LoadConnectedAccountItems(); await SyncGoogle(true); }
            StartAutoSync();
        }

        void LoadConnectedAccountItems()
        {
            var accountId = GoogleCalendar.ConnectedAccountId;
            Store.SetAccount(accountId);
            items.Clear(); items.AddRange(Store.Load());
        }

        async Task<bool> ConnectGoogle(bool saveLocal)
        {
            try
            {
                if (saveLocal) Store.SaveLocal(items);
                googleConnecting = true; googleButton.IsEnabled = true; googleButton.Content = "로그인 취소";
                await GoogleCalendar.ConnectAsync();
                randomizePaletteAfterConnect = !settings.LockPalettePlacement && settings.SelectedPaletteIndex >= 0 && settings.SelectedPaletteIndex < 5;
                return true;
            }
            catch (HttpListenerException ex) { return GoogleConnectFailed(ex); }
            catch (ObjectDisposedException ex) { return GoogleConnectFailed(ex); }
            catch (Exception ex)
            {
                ErrorLog.Write("Connect Google account", ex);
                ShowGoogleStatus("Google 로그인 실패 또는 취소", UiRound.ErrorNoticeMilliseconds); return false;
            }
            finally { googleConnecting = false; UpdateGoogleButton(); }
        }

        bool GoogleConnectFailed(Exception ex)
        {
            if (googleConnecting)
            {
                ErrorLog.Write("Connect Google account", ex);
                ShowGoogleStatus("Google 로그인 실패 또는 취소", UiRound.ErrorNoticeMilliseconds);
            }
            return false;
        }

        void ShowGoogleStatus(string message, int milliseconds)
        { ShowGoogleStatus(message, "#DC2626", milliseconds); }

        void ShowGoogleStatus(string message, string color, int milliseconds)
        {
            if (googleStatusTimer != null) googleStatusTimer.Stop();
            googleStatus.Text = message; googleStatus.Foreground = Brush(color); googleStatus.Visibility = Visibility.Visible;
            googleStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            googleStatusTimer.Tick += delegate
            {
                googleStatusTimer.Stop(); googleStatusTimer = null;
                googleStatus.Visibility = Visibility.Collapsed; googleStatus.Text = "동기화 완료";
            };
            googleStatusTimer.Start();
        }

        async Task SyncGoogle(bool showSuccess)
        {
            if (googleSyncing || !GoogleCalendar.IsConnected) return;
            googleSyncing = true;
            var syncWatch = Stopwatch.StartNew();
            if (googleStatusTimer != null) { googleStatusTimer.Stop(); googleStatusTimer = null; }
            googleStatus.Text = "동기화 중…"; googleStatus.Foreground = Brush("#4F46E5"); googleStatus.Visibility = Visibility.Visible;
            try
            {
                if (showSuccess)
                {
                    googleButton.IsEnabled = false; googleButton.Content = "동기화 중…";
                    ShowAccountCardState("↻  Google 동기화 중…", "#4F46E5", "#FFFFFF", 0);
                }
                var previousTaskSources = (settings.GoogleCalendars ?? new List<GoogleCalendarSetting>()).Where(x => GoogleTasks.IsSource(x.Id)).ToList();
                var calendarSources = await GoogleCalendar.SyncAsync(items, (settings.GoogleCalendars ?? new List<GoogleCalendarSetting>()).Where(x => !GoogleTasks.IsSource(x.Id)).ToList());
                var taskSources = previousTaskSources;
                string taskWarning = null;
                if (settings.ShowGoogleTasks && !GoogleCalendar.HasTasksPermission) taskWarning = "Google Tasks는 계정 재연결 후 동기화됩니다";
                else if (settings.ShowGoogleTasks)
                {
                    try { taskSources = await GoogleTasks.SyncAsync(items, previousTaskSources); }
                    catch (Exception taskError)
                    {
                        ErrorLog.Write("Synchronize Google Tasks", taskError);
                        taskWarning = "Google Tasks를 동기화하지 못했습니다";
                    }
                }
                settings.GoogleCalendars = calendarSources.Concat(taskSources).ToList();
                var paletteChanged = randomizePaletteAfterConnect && RandomizeRecommendedPalettePlacement();
                randomizePaletteAfterConnect = false;
                syncProblem = null;
                var primary = settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
                if (primary != null)
                {
                    settings.ActiveGoogleAccountId = primary.Id; GoogleCalendar.RememberAccount(primary.Id); Store.SetAccount(primary.Id);
                }
                var allowedCalendars = new HashSet<string>(settings.GoogleCalendars.Select(x => x.Id));
                items.RemoveAll(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId) && !allowedCalendars.Contains(x.GoogleCalendarId) && !x.PendingGoogleSync);
                settings.CategoryOrder = (settings.CategoryOrder ?? new List<string>()).Where(x => !x.StartsWith("google:") || allowedCalendars.Contains(x.Substring(7))).ToList();
                Store.Save(items); Store.SaveSettings(settings); BuildGoogleFilters();
                if (paletteChanged) ApplyTheme(settings.ThemeId); else RenderAll();
                OfferDormantLocalItems();
                syncWatch.Stop(); ShowGoogleStatus(taskWarning ?? "동기화 완료", taskWarning == null ? "#16A34A" : "#D97706", taskWarning == null ? 1800 : 3500);
                if (showSuccess)
                {
                    ShowAccountCardState(taskWarning == null ? "✓  동기화 완료 · " + syncWatch.Elapsed.TotalSeconds.ToString("0.0") + "초" : "!  캘린더 완료 · Tasks 확인 필요",
                        taskWarning == null ? "#DCFCE7" : "#FFF7ED", taskWarning == null ? "#15803D" : "#C2410C", taskWarning == null ? 1800 : 3500);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Synchronize Google Calendar", ex);
                var authenticationError = IsGoogleAuthenticationError(ex);
                syncProblem = authenticationError ? "인증 만료" : IsOffline(ex) ? "오프라인" : "Google 오류"; UpdateAccountStatus();
                var failureMessage = authenticationError
                    ? "Google 연결 인증이 만료되었거나 취소되었습니다 · 다시 로그인해 주세요"
                    : syncProblem + " · " + ShortGoogleError(ex.Message);
                ShowGoogleStatus(failureMessage, "#DC2626", authenticationError ? 7000 : UiRound.ErrorNoticeMilliseconds);
                if (showSuccess)
                {
                    ShowAccountCardState(authenticationError ? "!  Google 인증 만료 · 다시 로그인해 주세요" : "!  " + syncProblem + " · 다시 시도해 주세요",
                        "#FCE7F3", "#BE185D", authenticationError ? 7000 : UiRound.ErrorNoticeMilliseconds);
                }
            }
            finally
            {
                googleSyncing = false; googleButton.IsEnabled = true;
                googleButton.Content = GoogleCalendar.IsConnected ? "G 동기화" : "G 연결";
                googleButton.Background = GoogleCalendar.IsConnected ? Brush("#DBEAFE") : Brushes.White;
                if (!showSuccess) UpdateAccountStatus();
                if (positionLocked) SchedulePublish();
            }
        }

        void OfferDormantLocalItems()
        {
            if (localItemsOfferShown || !GoogleCalendar.IsConnected || HasBlockingDialog) return;
            localItemsOfferShown = true;
            var localItems = Store.LoadLocal();
            var activeIds = new HashSet<string>(items.Select(x => x.Id));
            localItems = localItems.Where(x => !activeIds.Contains(x.Id)).ToList();
            if (localItems.Count == 0) return;

            var offer = new LocalItemsOfferWindow(localItems.Count); PlaceCalendarDialog(offer);
            if (ShowBlockingDialog(offer) != true || !offer.ReviewItems) return;
            var picker = new LocalImportWindow(localItems); PlaceCalendarDialog(picker);
            if (ShowBlockingDialog(picker) != true) return;
            foreach (var item in picker.SelectedItems)
                if (!items.Any(x => x.Id == item.Id)) items.Add(item);
            localItems.RemoveAll(x => picker.SelectedItems.Any(y => y.Id == x.Id));
            Store.Save(items); Store.SaveLocal(localItems); BuildGoogleFilters(); RenderAll();
            ShowGoogleStatus("로컬 일정 " + picker.SelectedItems.Count + "개를 가져왔습니다", "#16A34A", 2200);
        }

        void StartAutoSync()
        {
            if (autoSyncTimer != null) autoSyncTimer.Stop();
            if (settings.AutoSyncMinutes <= 0 || !GoogleCalendar.IsConnected) return;
            autoSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(settings.AutoSyncMinutes) };
            autoSyncTimer.Tick += async delegate { await SyncGoogle(false); };
            autoSyncTimer.Start();
        }

        async Task SaveGoogleItem(PlannerItem item, bool wholeSeries = false)
        {
            try
            {
                if (GoogleTasks.IsTask(item)) await GoogleTasks.UpsertAsync(item);
                else if (wholeSeries) await GoogleCalendar.UpsertSeriesAsync(item);
                else await GoogleCalendar.UpsertAsync(item);
                item.PendingGoogleSync = false; syncProblem = null; AttachPrimaryCalendar(item); Store.Save(items); RenderAll(); UpdateAccountStatus();
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Save Google event", ex);
                item.PendingGoogleSync = true; syncProblem = IsOffline(ex) ? "오프라인" : "Google 오류"; Store.Save(items); UpdateAccountStatus();
                ShowItemNotice(item, "로컬 저장됨 · " + ShortGoogleError(ex.Message));
            }
        }

        static string ShortGoogleError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "다시 동기화해 주세요";
            if (message.IndexOf("Bad Request", StringComparison.OrdinalIgnoreCase) >= 0) return "반복 일정 형식을 확인해 주세요";
            if (message.IndexOf("time zone", StringComparison.OrdinalIgnoreCase) >= 0) return "일정 시간대를 확인해 주세요";
            if (message.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0) return "수정 권한을 확인해 주세요";
            if (message.IndexOf("Unauthorized", StringComparison.OrdinalIgnoreCase) >= 0) return "Google 계정을 다시 연결해 주세요";
            return "Google 요청을 처리하지 못했습니다";
        }

        static bool IsOffline(Exception ex) { return ex is HttpRequestException || ex is TaskCanceledException; }

        static bool IsGoogleAuthenticationError(Exception ex)
        {
            var message = ex == null ? "" : ex.ToString();
            return message.IndexOf("invalid_grant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("expired or revoked", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("Unauthorized", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("HTTP 401", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void AttachPrimaryCalendar(PlannerItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.GoogleCalendarId)) return;
            var primary = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
            if (primary == null) return;
            item.GoogleCalendarId = primary.Id; item.GoogleCalendarName = primary.Name;
            item.GoogleCalendarColor = primary.Color; item.GoogleReadOnly = false;
        }

        void UpdateGoogleButton()
        {
            if (googleButton == null) return;
            googleButton.Content = GoogleCalendar.IsConnected ? "G 동기화" : "G 연결";
            googleButton.Background = GoogleCalendar.IsConnected ? Brush("#DBEAFE") : Brushes.White;
            UpdateAccountStatus();
        }

        void UpdateAccountStatus()
        {
            if (accountStatus == null) return;
            googleAccountVisualVersion++;
            if (googleAccountCard != null) googleAccountCard.Background = Brush("#EEF2FF");
            var primary = settings.GoogleCalendars == null ? null : settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
            if (GoogleCalendar.IsConnected)
            {
                var pending = items.Count(x => x.PendingGoogleSync && !string.IsNullOrWhiteSpace(x.GoogleCalendarId));
                var state = syncProblem != null
                    ? " · " + syncProblem + (pending > 0 ? " (동기화 대기 " + pending + "건)" : "")
                    : pending > 0 ? " · 동기화 대기 " + pending + "건" : " · Gmail";
                accountStatus.Text = "G  " + (primary == null ? "Google 계정" : primary.Name) + state;
                accountStatus.Foreground = syncProblem != null || pending > 0 ? Brush("#DB2777") : Brush("#4338CA");
            }
            else
            {
                accountStatus.Text = "●  로그아웃됨 · 로컬 저장";
                accountStatus.Foreground = Brush("#7C3AED");
            }
            accountStatus.ToolTip = accountStatus.Text;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(StartAccountMarquee));
        }

        void ShowAccountCardState(string text, string background, string foreground, int restoreAfterMilliseconds)
        {
            if (accountStatus == null || googleAccountCard == null) return;
            var version = ++googleAccountVisualVersion;
            accountStatusShift.BeginAnimation(TranslateTransform.XProperty, null); accountStatusShift.X = 0;
            accountStatus.Text = text; accountStatus.Foreground = Brush(foreground); accountStatus.ToolTip = text;
            googleAccountCard.Background = Brush(background);
            if (positionLocked) RefreshFixedVisualNow();
            if (restoreAfterMilliseconds <= 0) return;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(restoreAfterMilliseconds) };
            timer.Tick += delegate
            {
                timer.Stop(); if (version != googleAccountVisualVersion) return; UpdateAccountStatus();
                if (positionLocked) RefreshFixedVisualNow();
            };
            timer.Start();
        }

        void StartAccountMarquee()
        {
            accountStatusShift.BeginAnimation(TranslateTransform.XProperty, null); accountStatusShift.X = 0;
            var overflow = accountStatus.ActualWidth - accountStatusViewport.ActualWidth;
            if (overflow <= 2) return;
            var animation = new System.Windows.Media.Animation.DoubleAnimation(0, -overflow,
                TimeSpan.FromSeconds(Math.Max(3, overflow / 18))) { AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(1) };
            accountStatusShift.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        void OpenPendingSync(object sender, MouseButtonEventArgs e)
        {
            var pending = items.Where(x => x.PendingGoogleSync && !string.IsNullOrWhiteSpace(x.GoogleCalendarId)).OrderBy(x => x.Start).ToList();
            if (pending.Count == 0) { ShowGoogleStatus("모든 일정이 동기화되었습니다", 1200); return; }
            var window = new PendingSyncWindow(pending); PlaceCalendarDialog(window); ShowBlockingDialog(window);
        }
    }
}
