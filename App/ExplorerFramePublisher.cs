using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace FamilyPlanner
{
    sealed class ExplorerFramePublisher : IDisposable
    {
        const string MappingName = "Local\\Onharu.DesktopFrame";
        const int Magic = 0x3356484F, HitMapMagic = 0x32544948, HeaderBytes = 64, MaxWidth = 4096, MaxHeight = 2160, MaxHitRecords = 256;
        const long SlotCapacity = (long)MaxWidth * MaxHeight * 4;
        const int HitMapBytes = 16 + MaxHitRecords * 28;
        readonly MemoryMappedFile mapping;
        readonly MemoryMappedViewAccessor view;
        int activeSlot, generation;
        IntPtr actionSink;
        NativeRect lastPanel;
        bool hasLastPanel;
        Rect lastScreenPanel;
        bool hasLastScreenPanel;
        RenderTargetBitmap bitmap;
        byte[] pixels;
        int bitmapWidth, bitmapHeight;
        double bitmapDpiX, bitmapDpiY;

        public ExplorerFramePublisher()
        {
            mapping = MemoryMappedFile.CreateOrOpen(MappingName, HeaderBytes + SlotCapacity * 2 + HitMapBytes, MemoryMappedFileAccess.ReadWrite);
            view = mapping.CreateViewAccessor(0, HeaderBytes + SlotCapacity * 2 + HitMapBytes, MemoryMappedFileAccess.ReadWrite);
            activeSlot = Math.Max(0, Math.Min(1, view.ReadInt32(40)));
            generation = Math.Max(0, view.ReadInt32(44));
            var oldWidth = view.ReadInt32(28); var oldHeight = view.ReadInt32(32);
            if (view.ReadInt32(8) > 0 && oldWidth > 0 && oldHeight > 0)
            {
                lastPanel = new NativeRect { Left = view.ReadInt32(20), Top = view.ReadInt32(24) };
                lastPanel.Right = lastPanel.Left + oldWidth; lastPanel.Bottom = lastPanel.Top + oldHeight;
                hasLastPanel = true;
            }
            Disable();
        }

        public void SetActionSink(IntPtr handle)
        {
            actionSink = handle;
            view.Write(60, unchecked((int)actionSink.ToInt64())); Thread.MemoryBarrier();
        }

        public void Publish(Window window, Visual visual, double opacity)
        {
            if (window == null || visual == null || window.ActualWidth <= 0 || window.ActualHeight <= 0) return;
            var handle = new WindowInteropHelper(window).Handle;
            var nativeWindow = new NativeRect();
            var hasNativeRect = handle != IntPtr.Zero && Native.GetWindowRect(handle, out nativeWindow)
                && nativeWindow.Right > nativeWindow.Left && nativeWindow.Bottom > nativeWindow.Top;
            var source = PresentationSource.FromVisual(window);
            var fallback = source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformToDevice : Matrix.Identity;
            // After a live resolution change WPF can retain a stale DPI transform
            // until the next surface transition. The native HWND rectangle is the
            // exact shape the user is looking at, so it is authoritative when the
            // movable window becomes an Explorer frame.
            var nativeWidth = hasNativeRect ? nativeWindow.Right - nativeWindow.Left : (int)Math.Round(window.ActualWidth * fallback.M11);
            var nativeHeight = hasNativeRect ? nativeWindow.Bottom - nativeWindow.Top : (int)Math.Round(window.ActualHeight * fallback.M22);
            var width = Math.Min(MaxWidth, Math.Max(1, nativeWidth));
            var height = Math.Min(MaxHeight, Math.Max(1, nativeHeight));
            var scaleX = width / window.ActualWidth; var scaleY = height / window.ActualHeight;
            var toDevice = new Matrix(scaleX, 0, 0, scaleY, 0, 0);
            var dpiX = 96 * scaleX; var dpiY = 96 * scaleY;
            if (bitmap == null || bitmapWidth != width || bitmapHeight != height || bitmapDpiX != dpiX || bitmapDpiY != dpiY)
            {
                bitmap = new RenderTargetBitmap(width, height, dpiX, dpiY, PixelFormats.Pbgra32);
                bitmapWidth = width; bitmapHeight = height; bitmapDpiX = dpiX; bitmapDpiY = dpiY;
            }
            else bitmap.Clear();
            bitmap.Render(visual);
            var stride = width * 4;
            if (pixels == null || pixels.Length != stride * height) pixels = new byte[stride * height];
            bitmap.CopyPixels(pixels, stride, 0);

            var screenPoint = hasNativeRect ? new Point(nativeWindow.Left, nativeWindow.Top) : window.PointToScreen(new Point(0, 0));
            lastScreenPanel = new Rect(Math.Round(screenPoint.X), Math.Round(screenPoint.Y), width, height);
            hasLastScreenPanel = true;
            PlacementTrace.Write("PUBLISH frame=" + lastScreenPanel.Left + "," + lastScreenPanel.Top + "," +
                lastScreenPanel.Width + "x" + lastScreenPanel.Height + " actual=" + window.ActualWidth + "x" + window.ActualHeight);
            var panelOrigin = DesktopClientPoint(screenPoint);
            var next = 1 - activeSlot;
            var destination = IntPtr.Add(view.SafeMemoryMappedViewHandle.DangerousGetHandle(),
                checked((int)(HeaderBytes + next * SlotCapacity)));
            Marshal.Copy(pixels, 0, destination, pixels.Length);
            view.Write(0, Magic); view.Write(4, 1); view.Write(8, width); view.Write(12, height); view.Write(16, stride);
            view.Write(20, panelOrigin.X);
            view.Write(24, panelOrigin.Y);
            view.Write(28, width); view.Write(32, height); view.Write(36, pixels.Length);
            view.Write(48, Math.Max(0, Math.Min(255, (int)Math.Round(opacity * 255))));
            view.Write(52, (int)SlotCapacity); view.Write(56, width);
            // Publish the receiver together with the frame. Clearing it here and
            // restoring it afterwards left a short interval in which clicks vanished.
            view.Write(60, unchecked((int)actionSink.ToInt64()));
            WriteHitMap(visual, toDevice, generation + 1);
            Thread.MemoryBarrier();
            activeSlot = next; view.Write(40, activeSlot); view.Write(44, ++generation); Thread.MemoryBarrier();
            var panel = new NativeRect { Left = panelOrigin.X, Top = panelOrigin.Y };
            panel.Right = panel.Left + width; panel.Bottom = panel.Top + height;
            var update = hasLastPanel ? NativeRect.Union(lastPanel, panel) : panel;
            lastPanel = panel; hasLastPanel = true; RedrawDesktop(update);
        }

        static NativePoint DesktopClientPoint(Point screenPoint)
        {
            var point = new NativePoint { X = (int)Math.Round(screenPoint.X), Y = (int)Math.Round(screenPoint.Y) };
            var list = FindDesktopIconList();
            if (list != IntPtr.Zero && Native.ScreenToClient(list, ref point)) return point;
            var virtualScreen = Forms.SystemInformation.VirtualScreen;
            point.X -= virtualScreen.Left; point.Y -= virtualScreen.Top;
            return point;
        }

        public Point FrameToLogicalPoint(int x, int y)
        {
            return new Point(bitmapDpiX > 0 ? x * 96.0 / bitmapDpiX : x, bitmapDpiY > 0 ? y * 96.0 / bitmapDpiY : y);
        }

        public bool TryGetPublishedScreenRectangle(out Rect rectangle)
        {
            rectangle = lastScreenPanel;
            return hasLastScreenPanel && rectangle.Width > 0 && rectangle.Height > 0;
        }

        public void UpdateOpacity(double opacity)
        {
            if (!hasLastPanel) return;
            view.Write(48, Math.Max(0, Math.Min(255, (int)Math.Round(opacity * 255))));
            Thread.MemoryBarrier();
            RedrawDesktop(lastPanel);
        }

        void WriteHitMap(Visual root, Matrix toDevice, int nextGeneration)
        {
            var records = new List<NativeHit>();
            CollectHitRecords(root, root, toDevice, records);
            var offset = HeaderBytes + SlotCapacity * 2;
            view.Write(offset, HitMapMagic); view.Write(offset + 4, nextGeneration); view.Write(offset + 8, records.Count); view.Write(offset + 12, 0);
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index]; var item = offset + 16 + index * 28;
                view.Write(item, record.Bounds.Left); view.Write(item + 4, record.Bounds.Top); view.Write(item + 8, record.Bounds.Right); view.Write(item + 12, record.Bounds.Bottom);
                view.Write(item + 16, record.Kind == 2 ? 104 : record.Kind == 3 ? 107 : record.Kind == 6 ? 20 : 100);
                view.Write(item + 20, record.Kind == 1 ? 101 : 0); view.Write(item + 24, record.Kind);
            }
        }

        static void CollectHitRecords(Visual root, DependencyObject parent, Matrix toDevice, List<NativeHit> records)
        {
            if (records.Count >= MaxHitRecords) return;
            var element = parent as FrameworkElement;
            var googleSyncAction = element != null && element.Tag as string == "google_sync";
            var sidebarToggle = element != null && element.Tag as string == "toggle_sidebar";
            var taggedAction = element != null && (element.Tag as string == "open_pending_sync" || googleSyncAction);
            var contentAction = element != null && (element.Tag is DateTime || element.Tag is PlannerItem || element.Tag is ItemHitTarget || element.Tag is DetailGroupHitTarget);
            var closeButton = element != null && element.Tag as string == "close_button";
            var detailScroller = element is ScrollViewer && element.Tag as string == "detail_scroll";
            if ((element is Button || element is CheckBox || element is Slider || taggedAction || contentAction || detailScroller) && element.Visibility == Visibility.Visible && element.IsEnabled && element.ActualWidth > 0 && element.ActualHeight > 0)
            {
                try
                {
                    var origin = element.TransformToAncestor(root).Transform(new Point());
                    var bounds = new NativeRect { Left = (int)Math.Floor(origin.X * toDevice.M11), Top = (int)Math.Floor(origin.Y * toDevice.M22),
                        Right = (int)Math.Ceiling((origin.X + element.ActualWidth) * toDevice.M11), Bottom = (int)Math.Ceiling((origin.Y + element.ActualHeight) * toDevice.M22) };
                    if (detailScroller) bounds.Left = Math.Max(bounds.Left, bounds.Right - (int)Math.Ceiling(18 * toDevice.M11));
                    records.Add(new NativeHit { Bounds = bounds, Kind = sidebarToggle ? 6 : googleSyncAction ? 5 : closeButton ? 4 : element is Slider ? 2 : detailScroller ? 3 : 1 });
                }
                catch { }
            }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent) && records.Count < MaxHitRecords; index++)
                CollectHitRecords(root, VisualTreeHelper.GetChild(parent, index), toDevice, records);
        }

        public void Disable()
        {
            view.Write(8, 0); view.Write(44, ++generation); Thread.MemoryBarrier();
            if (hasLastPanel) { RedrawDesktop(lastPanel); hasLastPanel = false; }
        }

        static void RedrawDesktop(NativeRect update)
        {
            var list = FindDesktopIconList();
            // Do not request RDW_ERASE here. Explorer already paints its clean
            // background during WM_PAINT; forcing a separate erase exposed a
            // wallpaper-only DWM frame before CDDS_PREPAINT drew the calendar.
            if (list != IntPtr.Zero) Native.RedrawWindow(list, ref update, IntPtr.Zero, 0x0001 | 0x0100);
        }

        static IntPtr FindDesktopIconList()
        {
            IntPtr found = IntPtr.Zero;
            Native.EnumWindows(delegate(IntPtr top, IntPtr state)
            {
                var defView = Native.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView == IntPtr.Zero) return true;
                found = Native.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                return found == IntPtr.Zero;
            }, IntPtr.Zero);
            return found;
        }

        public void Dispose() { SetActionSink(IntPtr.Zero); Disable(); view.Dispose(); mapping.Dispose(); }

        static class Native
        {
            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            public static extern IntPtr FindWindow(string className, string title);
            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
            public delegate bool EnumWindowsProc(IntPtr window, IntPtr state);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool RedrawWindow(IntPtr window, ref NativeRect update, IntPtr region, uint flags);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct NativePoint { public int X, Y; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct NativeRect
        {
            public int Left, Top, Right, Bottom;
            public static NativeRect Union(NativeRect a, NativeRect b)
            {
                return new NativeRect { Left = Math.Min(a.Left, b.Left), Top = Math.Min(a.Top, b.Top),
                    Right = Math.Max(a.Right, b.Right), Bottom = Math.Max(a.Bottom, b.Bottom) };
            }
        }

        struct NativeHit { public NativeRect Bounds; public int Kind; }
    }
}
