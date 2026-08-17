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
                items.Clear();
            }
            await SyncGoogle(true);
        }

        async Task<bool> ConnectGoogle(bool saveLocal)
        {
            try
            {
                if (saveLocal) Store.SaveLocal(items);
                googleConnecting = true; googleButton.IsEnabled = true; googleButton.Content = "로그인 취소";
                await GoogleCalendar.ConnectAsync(); return true;
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
        {
            googleStatus.Text = message; googleStatus.Visibility = Visibility.Visible;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); googleStatus.Visibility = Visibility.Collapsed; googleStatus.Text = "동기화가 완료되었습니다"; };
            timer.Start();
        }

        async Task SyncGoogle(bool showSuccess)
        {
            if (googleSyncing || !GoogleCalendar.IsConnected) return;
            googleSyncing = true;
            var syncWatch = Stopwatch.StartNew();
            try
            {
                if (showSuccess) { googleButton.IsEnabled = false; googleButton.Content = "동기화 중…"; }
                settings.GoogleCalendars = await GoogleCalendar.SyncAsync(items, settings.GoogleCalendars);
                syncProblem = null;
                var primary = settings.GoogleCalendars.FirstOrDefault(x => x.Primary);
                if (primary != null)
                {
                    settings.ActiveGoogleAccountId = primary.Id; GoogleCalendar.RememberAccount(primary.Id); Store.SetAccount(primary.Id);
                }
                var allowedCalendars = new HashSet<string>(settings.GoogleCalendars.Select(x => x.Id));
                items.RemoveAll(x => !string.IsNullOrWhiteSpace(x.GoogleCalendarId) && !allowedCalendars.Contains(x.GoogleCalendarId) && !x.PendingGoogleSync);
                settings.CategoryOrder = (settings.CategoryOrder ?? new List<string>()).Where(x => !x.StartsWith("google:") || allowedCalendars.Contains(x.Substring(7))).ToList();
                Store.Save(items); Store.SaveSettings(settings); BuildGoogleFilters(); RenderAll();
                if (showSuccess)
                {
                    syncWatch.Stop(); googleStatus.Text = "동기화 완료 · " + syncWatch.Elapsed.TotalSeconds.ToString("0.0") + "초";
                    googleStatus.Visibility = Visibility.Visible; await Task.Delay(1000);
                    googleStatus.Visibility = Visibility.Collapsed; googleStatus.Text = "동기화가 완료되었습니다";
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Synchronize Google Calendar", ex);
                syncProblem = IsOffline(ex) ? "오프라인" : "Google 오류"; UpdateAccountStatus();
                if (showSuccess)
                {
                    googleStatus.Text = syncProblem + " · " + ShortGoogleError(ex.Message); googleStatus.Visibility = Visibility.Visible;
                    var hideStatus = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(UiRound.ErrorNoticeMilliseconds) };
                    hideStatus.Tick += delegate { hideStatus.Stop(); googleStatus.Visibility = Visibility.Collapsed; googleStatus.Text = "동기화가 완료되었습니다"; };
                    hideStatus.Start();
                }
            }
            finally
            {
                googleSyncing = false; googleButton.IsEnabled = true; UpdateGoogleButton();
                if (positionLocked) SchedulePublish();
            }
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
                if (wholeSeries) await GoogleCalendar.UpsertSeriesAsync(item); else await GoogleCalendar.UpsertAsync(item);
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
            var window = new PendingSyncWindow(pending); PlaceCalendarDialog(window); window.ShowDialog();
        }
    }
}
