using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        bool HasBlockingDialog { get { return blockingDialogDepth > 0; } }

        bool ActivateBlockingDialog()
        {
            if (!HasBlockingDialog) return false;
            var front = Application.Current.Windows.Cast<Window>()
                .LastOrDefault(window => window.IsVisible && window.Dispatcher == Dispatcher && IsBlockingDialogOrChild(window)) ?? blockingDialog;
            if (front == null) return true;
            if (front.WindowState == WindowState.Minimized) front.WindowState = WindowState.Normal;
            front.Topmost = true; front.Activate(); front.Focus(); front.Topmost = false;
            return true;
        }

        bool IsBlockingDialogOrChild(Window window)
        {
            for (var current = window; current != null; current = current.Owner)
                if (current == blockingDialog) return true;
            return false;
        }

        bool? ShowBlockingDialog(Window window)
        {
            if (ActivateBlockingDialog()) return null;
            CloseTransientPopup();
            blockingDialog = window;
            blockingDialogDepth++;
            var wasHitTestVisible = IsHitTestVisible;
            IsHitTestVisible = false;
            try { return window.ShowDialog(); }
            finally
            {
                if (blockingDialog == window) blockingDialog = null;
                blockingDialogDepth = Math.Max(0, blockingDialogDepth - 1);
                if (!applicationExitRequested) IsHitTestVisible = blockingDialogDepth == 0 || wasHitTestVisible;
            }
        }

        Forms.DialogResult ShowBlockingFileDialog(Forms.CommonDialog dialog)
        {
            if (ActivateBlockingDialog()) return Forms.DialogResult.Cancel;
            CloseTransientPopup(); blockingDialogDepth++;
            var wasHitTestVisible = IsHitTestVisible; IsHitTestVisible = false;
            try { return dialog.ShowDialog(); }
            finally { blockingDialogDepth = Math.Max(0, blockingDialogDepth - 1); if (!applicationExitRequested) IsHitTestVisible = wasHitTestVisible; }
        }

        void PlaceCalendarDialog(Window window)
        {
            OnharuPopupChrome.EnableTopDrag(window);
            window.Owner = null;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            var width = double.IsNaN(window.Width) || window.Width <= 0 ? 420 : window.Width;
            var height = double.IsNaN(window.Height) || window.Height <= 0 ? 0 : window.Height;
            if (height <= 0)
            {
                var content = window.Content as FrameworkElement;
                if (content != null) { content.Measure(new Size(width, double.PositiveInfinity)); height = content.DesiredSize.Height; }
                if (height <= 0) height = 420;
            }
            var left = Left + (ActualWidth - width) / 2;
            var top = Top + (ActualHeight - height) / 2;
            var center = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
            var area = Forms.Screen.FromPoint(new Drawing.Point((int)center.X, (int)center.Y)).WorkingArea;
            var source = PresentationSource.FromVisual(this);
            var fromDevice = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            var areaTopLeft = fromDevice.Transform(new Point(area.Left, area.Top));
            var areaBottomRight = fromDevice.Transform(new Point(area.Right, area.Bottom));
            window.Left = Math.Max(areaTopLeft.X, Math.Min(left, areaBottomRight.X - width));
            window.Top = Math.Max(areaTopLeft.Y, Math.Min(top, areaBottomRight.Y - height));
        }

        void ShowNotice(string message, bool warning, string heading = null)
        {
            var window = new NoticeWindow(message, warning, heading); PlaceCalendarDialog(window); ShowBlockingDialog(window);
        }
    }
}
