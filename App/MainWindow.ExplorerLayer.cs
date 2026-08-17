using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void SchedulePublish()
        {
            if (publishPending) return;
            publishPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate { publishPending = false; PublishAndHide(); }));
        }

        void PublishAndHide()
        {
            if (!positionLocked || Content == null) return;
            if (calendarMinimized) { explorerFrame.Disable(); if (IsVisible) Hide(); return; }
            UpdateLayout(); explorerFrame.Publish(this, Content as Visual, settings.Opacity > 0 ? settings.Opacity : .95);
            if (!LayerHostController.Start())
            {
                // Standalone validation builds have no native host beside them.
                if (!IsVisible) Show();
                UpdateLayout();
                return;
            }
            explorerFrame.SetActionSink(desktopActions.WindowHandle);
            if (IsVisible) Hide();
        }

        void ShowForDialog()
        {
            // Compose WPF before removing the cached Explorer frame to avoid a flash.
            Show(); UpdateLayout(); Activate();
            explorerFrame.Disable();
        }

        void EnterEditMode()
        {
            positionLocked = false; settings.PositionLocked = false; Store.SaveSettings(settings);
            UpdateModeButtons(); ShowPositionEditor();
        }

        void ShowPositionEditor()
        {
            explorerFrame.Disable();
            Topmost = false; ShowInTaskbar = true;
            Show(); UpdateLayout(); Activate();
        }
    }
}
