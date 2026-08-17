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
        const string MappingName = "Local\\OnharuV3.DesktopFrame";
        const int Magic = 0x3356484F, HitMapMagic = 0x32544948, HeaderBytes = 64, MaxWidth = 4096, MaxHeight = 2160, MaxHitRecords = 256;
        const long SlotCapacity = (long)MaxWidth * MaxHeight * 4;
        const int HitMapBytes = 16 + MaxHitRecords * 28;
        readonly MemoryMappedFile mapping;
        readonly MemoryMappedViewAccessor view;
        int activeSlot, generation;
        IntPtr actionSink;
        NativeRect lastPanel;
        bool hasLastPanel;
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
            var source = PresentationSource.FromVisual(window);
            var toDevice = source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformToDevice : Matrix.Identity;
            var width = Math.Min(MaxWidth, Math.Max(1, (int)Math.Round(window.ActualWidth * toDevice.M11)));
            var height = Math.Min(MaxHeight, Math.Max(1, (int)Math.Round(window.ActualHeight * toDevice.M22)));
            var dpiX = 96 * toDevice.M11; var dpiY = 96 * toDevice.M22;
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

            var screenPoint = window.PointToScreen(new Point(0, 0));
            var virtualScreen = Forms.SystemInformation.VirtualScreen;
            var next = 1 - activeSlot;
            var destination = IntPtr.Add(view.SafeMemoryMappedViewHandle.DangerousGetHandle(),
                checked((int)(HeaderBytes + next * SlotCapacity)));
            Marshal.Copy(pixels, 0, destination, pixels.Length);
            view.Write(0, Magic); view.Write(4, 1); view.Write(8, width); view.Write(12, height); view.Write(16, stride);
            view.Write(20, (int)Math.Round(screenPoint.X) - virtualScreen.Left);
            view.Write(24, (int)Math.Round(screenPoint.Y) - virtualScreen.Top);
            view.Write(28, width); view.Write(32, height); view.Write(36, pixels.Length);
            view.Write(48, Math.Max(1, Math.Min(255, (int)Math.Round(opacity * 255))));
            view.Write(52, (int)SlotCapacity); view.Write(56, width);
            // Publish the receiver together with the frame. Clearing it here and
            // restoring it afterwards left a short interval in which clicks vanished.
            view.Write(60, unchecked((int)actionSink.ToInt64()));
            WriteHitMap(visual, toDevice, generation + 1);
            Thread.MemoryBarrier();
            activeSlot = next; view.Write(40, activeSlot); view.Write(44, ++generation); Thread.MemoryBarrier();
            var panel = new NativeRect { Left = (int)Math.Round(screenPoint.X) - virtualScreen.Left,
                Top = (int)Math.Round(screenPoint.Y) - virtualScreen.Top };
            panel.Right = panel.Left + width; panel.Bottom = panel.Top + height;
            var update = hasLastPanel ? NativeRect.Union(lastPanel, panel) : panel;
            lastPanel = panel; hasLastPanel = true; RedrawDesktop(update);
        }

        public Point FrameToLogicalPoint(int x, int y)
        {
            return new Point(bitmapDpiX > 0 ? x * 96.0 / bitmapDpiX : x, bitmapDpiY > 0 ? y * 96.0 / bitmapDpiY : y);
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
                view.Write(item + 16, record.Kind == 2 ? 104 : record.Kind == 3 ? 107 : 100);
                view.Write(item + 20, record.Kind == 1 ? 101 : 0); view.Write(item + 24, record.Kind);
            }
        }

        static void CollectHitRecords(Visual root, DependencyObject parent, Matrix toDevice, List<NativeHit> records)
        {
            if (records.Count >= MaxHitRecords) return;
            var element = parent as FrameworkElement;
            var taggedAction = element != null && element.Tag as string == "open_pending_sync";
            var closeButton = element != null && element.Tag as string == "close_button";
            var detailScroller = element is ScrollViewer && element.Tag as string == "detail_scroll";
            if ((element is Button || element is CheckBox || element is Slider || taggedAction || detailScroller) && element.Visibility == Visibility.Visible && element.IsEnabled && element.ActualWidth > 0 && element.ActualHeight > 0)
            {
                try
                {
                    var origin = element.TransformToAncestor(root).Transform(new Point());
                    var bounds = new NativeRect { Left = (int)Math.Floor(origin.X * toDevice.M11), Top = (int)Math.Floor(origin.Y * toDevice.M22),
                        Right = (int)Math.Ceiling((origin.X + element.ActualWidth) * toDevice.M11), Bottom = (int)Math.Ceiling((origin.Y + element.ActualHeight) * toDevice.M22) };
                    if (detailScroller) bounds.Left = Math.Max(bounds.Left, bounds.Right - (int)Math.Ceiling(18 * toDevice.M11));
                    records.Add(new NativeHit { Bounds = bounds, Kind = closeButton ? 4 : element is Slider ? 2 : detailScroller ? 3 : 1 });
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
            var progman = Native.FindWindow("Progman", null);
            var defView = Native.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            var list = Native.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
            if (list != IntPtr.Zero) Native.RedrawWindow(list, ref update, IntPtr.Zero, 0x0001 | 0x0020 | 0x0100);
        }

        public void Dispose() { SetActionSink(IntPtr.Zero); Disable(); view.Dispose(); mapping.Dispose(); }

        static class Native
        {
            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            public static extern IntPtr FindWindow(string className, string title);
            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool RedrawWindow(IntPtr window, ref NativeRect update, IntPtr region, uint flags);
        }

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
