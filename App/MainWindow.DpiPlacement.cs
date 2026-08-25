using System;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        HwndSource dpiPlacementSource;
        bool nativeMoveSizeActive;

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
            if (message == 0x0231) // WM_ENTERSIZEMOVE
            {
                nativeMoveSizeActive = true;
                PlacementTrace.Write("MOVE_SIZE begin wpf=" + WpfRectText());
                return IntPtr.Zero;
            }
            if (message == 0x0232) // WM_EXITSIZEMOVE
            {
                nativeMoveSizeActive = false;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                {
                    ApplyPhysicalMinimums(GetDpiForWindow(hwnd));
                    SavePhysicalPlacement();
                    Store.SaveSettings(settings);
                    PlacementTrace.Write("MOVE_SIZE end wpf=" + WpfRectText());
                }));
                return IntPtr.Zero;
            }
            if (message != 0x02E0) return IntPtr.Zero; // WM_DPICHANGED

            NativeRect physicalBefore;
            if (!GetWindowRect(hwnd, out physicalBefore)) return IntPtr.Zero;
            var packed = unchecked((ulong)wParam.ToInt64());
            ApplyPhysicalMinimums((uint)(packed & 0xFFFF));

            // During an interactive move Windows owns the suggested physical
            // rectangle. Restoring the previous monitor rectangle here makes
            // the window bounce forever across mixed-DPI monitor boundaries.
            if (nativeMoveSizeActive && !positionLocked)
            {
                PlacementTrace.Write("DPI_MOVE allow suggested before=" + RectText(physicalBefore) + " wpf=" + WpfRectText());
                handled = false;
                return IntPtr.Zero;
            }

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
