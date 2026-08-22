using System;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        HwndSource dpiPlacementSource;

        void AttachDpiPlacement()
        {
            SourceInitialized += delegate
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                ApplyPhysicalMinimums(GetDpiForWindow(hwnd));
                RestorePhysicalPlacement();
                dpiPlacementSource = HwndSource.FromHwnd(hwnd);
                if (dpiPlacementSource != null) dpiPlacementSource.AddHook(DpiPlacementWindowProc);
            };
            Closed += delegate
            {
                if (dpiPlacementSource != null) dpiPlacementSource.RemoveHook(DpiPlacementWindowProc);
                dpiPlacementSource = null;
            };
        }

        IntPtr DpiPlacementWindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != 0x02E0) return IntPtr.Zero; // WM_DPICHANGED

            NativeRect physicalBefore;
            if (!GetWindowRect(hwnd, out physicalBefore)) return IntPtr.Zero;
            var packed = unchecked((ulong)wParam.ToInt64());
            ApplyPhysicalMinimums((uint)(packed & 0xFFFF));

            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
            {
                ApplyPhysicalMinimums(GetDpiForWindow(hwnd));
                SetWindowPos(hwnd, IntPtr.Zero, physicalBefore.Left, physicalBefore.Top,
                    Math.Max(1, physicalBefore.Right - physicalBefore.Left),
                    Math.Max(1, physicalBefore.Bottom - physicalBefore.Top), 0x0014);
                PlacementTrace.Write("DPI_RESTORE frame=" + RectText(physicalBefore) + " wpf=" + WpfRectText());
            }));

            handled = false;
            return IntPtr.Zero;
        }
    }
}
