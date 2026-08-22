using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;

namespace FamilyPlanner
{
    partial class MainWindow
    {
        const int DwmwaCloak = 13;
        int layerTransitionVersion;
        bool windowCloaked;
        DispatcherTimer preparedWpfTimer;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(TransitionPoint point);
        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr window, ref TransitionPoint point);
        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct TransitionPoint { public int X, Y; }

        void SetWindowCloaked(bool cloaked)
        {
            if (windowCloaked == cloaked) return;
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            var value = cloaked ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(handle, DwmwaCloak, ref value, sizeof(int)) == 0)
                    windowCloaked = cloaked;
            }
            catch { }
        }

        void SchedulePublish()
        {
            if (publishPending) return;
            publishPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render,
                new Action(delegate { publishPending = false; PublishAndCloak(); }));
        }

        void ScheduleFixedVisualRefresh()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
            {
                RefreshFixedVisualNow();
            }));
        }

        void RefreshFixedVisualNow()
        {
            if (!positionLocked || calendarMinimized || Content == null) return;
            UpdateLayout();
            explorerFrame.Publish(this, Content as Visual, settings.Opacity);
            explorerFrame.SetActionSink(desktopActions.WindowHandle);
        }

        void PublishAndCloak()
        {
            if (!positionLocked || Content == null) return;
            if (calendarMinimized)
            {
                explorerFrame.Disable();
                SetWindowCloaked(false);
                if (IsVisible) Hide();
                return;
            }

            UpdateLayout();
            explorerFrame.Publish(this, Content as Visual, settings.Opacity);
            if (!LayerHostController.Start())
            {
                SetWindowCloaked(false);
                if (!IsVisible) Show();
                UpdateLayout();
                return;
            }

            explorerFrame.SetActionSink(desktopActions.WindowHandle);
            if (!IsVisible) Show();
            SetWindowCloaked(true);
            SchedulePointerRefresh();
        }

        void PublishAndHide() { PublishAndCloak(); }

        void ShowForDialog()
        {
            ShowPreparedWpf(delegate { Activate(); }, true);
        }

        void EnterEditMode()
        {
            PlacementTrace.Write("ENTER_EDIT begin");
            positionLocked = false;
            settings.PositionLocked = false;
            Store.SaveSettings(settings);
            UpdateModeButtons();
            ShowPositionEditor();
            PlacementTrace.Write("ENTER_EDIT queued-show");
        }

        void ShowPositionEditor()
        {
            Topmost = false;
            ShowInTaskbar = false;
            ShowPreparedWpf(delegate { Activate(); });
        }

        void ShowPreparedWpf(Action afterShown, bool allowWhileLocked = false)
        {
            var transition = ++layerTransitionVersion;
            var intendedOpacity = settings.Opacity;
            PlacementTrace.Write("SHOW_WPF begin transition=" + transition + " locked=" + positionLocked);
            if (!IsVisible) Show();
            // Join the DWM composition tree invisibly before removing the
            // Explorer frame. Uncloaking and removing that frame in the same
            // turn can expose a wallpaper-only frame on slower displays.
            Opacity = 0;
            UpdateLayout();
            var contentElement = Content as UIElement;
            if (contentElement != null) contentElement.InvalidateVisual();

            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
            {
                if (transition != layerTransitionVersion || (positionLocked && !allowWhileLocked)) return;
                UpdateLayout();
                SetWindowCloaked(false);
                BeginPreparedWpfSettle(transition, intendedOpacity, afterShown, allowWhileLocked);
            }));
        }

        void BeginPreparedWpfSettle(int transition, double intendedOpacity, Action afterShown, bool allowWhileLocked)
        {
            if (preparedWpfTimer != null) preparedWpfTimer.Stop();
            Rect target;
            var hasTarget = explorerFrame.TryGetPublishedScreenRectangle(out target);
            var attempts = 0; var stableTicks = 0;
            preparedWpfTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(20) };
            preparedWpfTimer.Tick += delegate
            {
                if (transition != layerTransitionVersion || (positionLocked && !allowWhileLocked))
                { preparedWpfTimer.Stop(); return; }
                attempts++;
                var ready = !hasTarget || MatchWindowToPhysicalFrame(target);
                stableTicks = ready ? stableTicks + 1 : 0;
                if (stableTicks < 2 && attempts < 25) return;
                preparedWpfTimer.Stop();
                if (hasTarget) MatchWindowToPhysicalFrame(target);
                SavePhysicalPlacement();
                Opacity = intendedOpacity;
                explorerFrame.Disable();
                PlacementTrace.Write("SHOW_WPF visible transition=" + transition + " attempts=" + attempts +
                    " stable=" + stableTicks + " locked=" + positionLocked + " wpf=" + WpfRectText());
                if (afterShown != null) afterShown();
                SchedulePointerRefresh();
            };
            preparedWpfTimer.Start();
        }

        void SchedulePointerRefresh()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate
            {
                Mouse.Capture(null);
                Mouse.Synchronize();
                var screen = System.Windows.Forms.Control.MousePosition;
                var point = new TransitionPoint { X = screen.X, Y = screen.Y };
                var target = WindowFromPoint(point);
                if (target == IntPtr.Zero || !ScreenToClient(target, ref point)) return;
                var packed = new IntPtr((point.X & 0xFFFF) | ((point.Y & 0xFFFF) << 16));
                PostMessage(target, 0x0200, IntPtr.Zero, packed); // WM_MOUSEMOVE
                PostMessage(target, 0x0020, target, new IntPtr(1)); // WM_SETCURSOR / HTCLIENT
            }));
        }
    }
}
