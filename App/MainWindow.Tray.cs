using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void RequestExit()
        {
            if (ActivateBlockingDialog()) return;
            var wasLocked = positionLocked;
            var wasMinimized = calendarMinimized;
            var window = new ExitConfirmWindow { Topmost = wasLocked, ShowInTaskbar = false }; PlaceCalendarDialog(window); ShowBlockingDialog(window);
            if (window.Choice == "exit") { ExitApplication(); return; }
            if (window.Choice == "minimize") { MinimizeToTray(); return; }
            if (wasMinimized) { MinimizeToTray(); return; }
            calendarMinimized = false; UpdateTrayVisibilityText();
            if (!wasLocked) ShowPositionEditor();
        }

        ContextMenu CreateCloseContextMenu()
        {
            var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
            var minimize = new MenuItem { Header = "트레이로 최소화" };
            minimize.Click += delegate { MinimizeToTray(); }; menu.Items.Add(minimize);
            var maximize = new MenuItem { Header = "최대화", IsEnabled = !positionLocked };
            maximize.Click += delegate { if (!positionLocked) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }; menu.Items.Add(maximize);
            var exit = new MenuItem { Header = "끝내기", Foreground = Brush("#DC2626"), FontWeight = FontWeights.Bold };
            exit.Click += delegate { ExitApplication(); }; menu.Items.Add(exit);
            UiRound.StyleContextMenu(menu);
            return menu;
        }

        void OpenCloseContextMenu()
        {
            var menu = CreateCloseContextMenu(); menu.IsOpen = true;
        }

        void MinimizeToTray()
        {
            calendarMinimized = true; explorerFrame.Disable(); SetWindowCloaked(false); Hide(); UpdateTrayVisibilityText();
        }

        void ShowFromTray()
        {
            calendarMinimized = false; UpdateTrayVisibilityText();
            if (positionLocked) SchedulePublish();
            else { Show(); UpdateLayout(); Activate(); }
        }

        internal void ShowFromExternalLaunch()
        {
            if (!IsLoaded) return;
            ShowFromTray();
        }

        void ToggleTrayVisibility()
        {
            if (calendarMinimized) ShowFromTray(); else MinimizeToTray();
        }

        void UpdateTrayVisibilityText()
        {
            if (trayVisibilityItem != null) trayVisibilityItem.Text = calendarMinimized ? "화면에 보이기" : "화면에서 최소화";
        }

        void CreateTrayIcon()
        {
            var appIcon = Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            trayIcon = new Forms.NotifyIcon { Icon = appIcon ?? Drawing.SystemIcons.Application, Text = "온하루", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            trayVisibilityItem = new Forms.ToolStripMenuItem("화면에서 최소화", null, delegate { ToggleTrayVisibility(); });
            menu.Items.Add(trayVisibilityItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            trayPositionItem = new Forms.ToolStripMenuItem("위치·크기 조정", null, delegate
            {
                calendarMinimized = false; UpdateTrayVisibilityText();
                if (positionLocked)
                {
                    EnterEditMode();
                }
                else
                {
                    LockCurrentPlacement();
                }
            });
            menu.Items.Add(trayPositionItem);
            menu.Items.Add("현재 화면으로 가져오기", null, delegate
            {
                calendarMinimized = false; UpdateTrayVisibilityText();
                positionLocked = false; settings.PositionLocked = false; Store.SaveSettings(settings);
                ShowPositionEditor(); EnsureWindowOnScreen(true); UpdateModeButtons();
            });
            menu.Items.Add("투명도 복구 (70%)", null, delegate
            {
                settings.Opacity = .70;
                if (opacitySlider != null) opacitySlider.Value = settings.Opacity;
                Opacity = settings.Opacity; explorerFrame.UpdateOpacity(settings.Opacity);
                Store.SaveSettings(settings);
                calendarMinimized = false; UpdateTrayVisibilityText();
                if (positionLocked) SchedulePublish(); else ShowFromTray();
            });
            menu.Items.Add("종료", null, delegate { ExitApplication(); });
            trayIcon.ContextMenuStrip = menu;
            menu.Opening += delegate { UpdateTrayVisibilityText(); };
            trayIcon.DoubleClick += delegate { ShowFromTray(); };
        }

        void ExitApplication()
        {
            if (applicationExitRequested) return;
            applicationExitRequested = true;
            CloseAuxiliaryWindows();
            Close();
        }

        void CloseAuxiliaryWindows()
        {
            var windows = Application.Current == null ? new Window[0] : Application.Current.Windows.Cast<Window>().ToArray();
            foreach (var window in windows)
            {
                if (window == this) continue;
                try { window.Close(); } catch { }
            }
        }
    }
}
