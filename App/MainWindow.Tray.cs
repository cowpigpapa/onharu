using System.Diagnostics;
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
            var wasLocked = positionLocked;
            var wasMinimized = calendarMinimized;
            var window = new ExitConfirmWindow { Topmost = wasLocked, ShowInTaskbar = false }; PlaceCalendarDialog(window); window.ShowDialog();
            if (window.Choice == "exit") { Close(); return; }
            if (window.Choice == "minimize") { MinimizeToTray(); return; }
            if (wasMinimized) { MinimizeToTray(); return; }
            calendarMinimized = false; UpdateTrayVisibilityText();
            if (!wasLocked) ShowPositionEditor();
        }

        void ExecuteCloseButtonAction()
        {
            if (settings.CloseButtonAction == "confirm_exit") RequestExit(); else MinimizeToTray();
        }

        ContextMenu CreateCloseContextMenu()
        {
            var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
            menu.Items.Add(new MenuItem { Header = "취소" });
            var minimize = new MenuItem { Header = "트레이로 최소화" };
            minimize.Click += delegate { MinimizeToTray(); }; menu.Items.Add(minimize);
            var exit = new MenuItem { Header = "종료", Foreground = Brush("#DC2626"), FontWeight = FontWeights.Bold };
            exit.Click += delegate { Close(); }; menu.Items.Add(exit);
            UiRound.StyleContextMenu(menu);
            return menu;
        }

        void OpenCloseContextMenu()
        {
            var menu = CreateCloseContextMenu(); menu.IsOpen = true;
        }

        void MinimizeToTray()
        {
            calendarMinimized = true; explorerFrame.Disable(); Hide(); UpdateTrayVisibilityText();
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
                    positionLocked = false; ShowPositionEditor();
                }
                else
                {
                    positionLocked = true; settings.HasPosition = true; settings.Left = Left; settings.Top = Top;
                    settings.Width = ActualWidth; settings.Height = ActualHeight;
                    Topmost = false; ShowInTaskbar = false; SchedulePublish();
                }
                settings.PositionLocked = positionLocked; Store.SaveSettings(settings); UpdateModeButtons();
            });
            menu.Items.Add(trayPositionItem);
            menu.Items.Add("현재 화면으로 가져오기", null, delegate
            {
                calendarMinimized = false; UpdateTrayVisibilityText();
                positionLocked = false; settings.PositionLocked = false; Store.SaveSettings(settings);
                ShowPositionEditor(); EnsureWindowOnScreen(true); UpdateModeButtons();
            });
            menu.Items.Add("종료", null, delegate { Close(); });
            trayIcon.ContextMenuStrip = menu;
            menu.Opening += delegate { UpdateTrayVisibilityText(); };
            trayIcon.DoubleClick += delegate { ShowFromTray(); };
        }
    }
}
