using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        DispatcherTimer displaySettingsTimer;
        [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);

        const double MinimumPhysicalWidth = 820;
        const double MinimumPhysicalHeight = 560;
        const double MinimumLayoutWidth = 720;

        void ApplyPhysicalMinimums(uint dpi)
        {
            var scale = dpi > 0 ? dpi / 96.0 : 1.0;
            MinWidth = Math.Max(MinimumPhysicalWidth / scale, MinimumLayoutWidth);
            MinHeight = MinimumPhysicalHeight / scale;
        }

        void SavePhysicalPlacement()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeRect rect;
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out rect)) return;
            var width = Math.Max(1, rect.Right - rect.Left); var height = Math.Max(1, rect.Bottom - rect.Top);
            var screen = Forms.Screen.FromRectangle(new System.Drawing.Rectangle(rect.Left, rect.Top, width, height));
            settings.MonitorDeviceName = screen.DeviceName;
            settings.PhysicalLeft = rect.Left; settings.PhysicalTop = rect.Top;
            settings.PhysicalWidth = width; settings.PhysicalHeight = height;
            PlacementTrace.Write("SAVE native=" + RectText(rect) + " wpf=" + WpfRectText() + " locked=" + positionLocked);
        }

        void RestorePhysicalPlacement()
        {
            if (!settings.HasPosition || settings.PhysicalWidth < 320 || settings.PhysicalHeight < 240) return;
            var screens = Forms.Screen.AllScreens;
            var screen = screens.FirstOrDefault(x => string.Equals(x.DeviceName, settings.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))
                ?? screens.FirstOrDefault(x => x.Primary) ?? screens.First();
            var work = screen.WorkingArea;
            var width = Math.Min(settings.PhysicalWidth, work.Width);
            var height = Math.Min(settings.PhysicalHeight, work.Height);
            var left = Math.Max(work.Left, Math.Min(settings.PhysicalLeft, work.Right - width));
            var top = Math.Max(work.Top, Math.Min(settings.PhysicalTop, work.Bottom - height));
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, IntPtr.Zero, left, top, width, height, 0x0014);
        }

        void LockCurrentPlacement()
        {
            if (RestoreBlockingDialog()) { UpdateModeButtons(); return; }
            if (positionLocked) return;
            positionLocked = true;
            settings.HasPosition = true; settings.Left = Left; settings.Top = Top;
            settings.Width = ActualWidth; settings.Height = ActualHeight;
            SavePhysicalPlacement(); settings.PositionLocked = true; Store.SaveSettings(settings);
            UpdateModeButtons(); SchedulePublish();
            PlacementTrace.Write("LOCK queued wpf=" + WpfRectText());
        }
        void DisplaySettingsChanged(object sender, EventArgs e)
        {
            PlacementTrace.Write("DISPLAY_CHANGED queued wpf=" + WpfRectText());
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
            {
                if (displaySettingsTimer != null) displaySettingsTimer.Stop();
                displaySettingsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                displaySettingsTimer.Tick += delegate
                {
                    displaySettingsTimer.Stop();
                    PlacementTrace.Write("DISPLAY_CHANGED tick-before wpf=" + WpfRectText());
                    EnsureWindowOnScreen(false);
                    SavePhysicalPlacement(); Store.SaveSettings(settings);
                    if (positionLocked) PublishAndHide();
                    PlacementTrace.Write("DISPLAY_CHANGED tick-after wpf=" + WpfRectText());
                };
                displaySettingsTimer.Start();
            }));
        }

        void MatchWindowToPublishedFrame()
        {
            Rect frame;
            if (!explorerFrame.TryGetPublishedScreenRectangle(out frame)) return;
            MatchWindowToPhysicalFrame(frame);
        }

        bool MatchWindowToPhysicalFrame(Rect frame)
        {
            PlacementTrace.Write("MATCH begin frame=" + RectText(frame) + " wpf=" + WpfRectText());
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return false;
            var nativeDpi = GetDpiForWindow(hwnd);
            var scale = nativeDpi > 0 ? nativeDpi / 96.0 : 1.0;
            ApplyPhysicalMinimums(nativeDpi);
            var logicalWidth = frame.Width / scale; var logicalHeight = frame.Height / scale;
            // A cloaked WPF window can keep the previous monitor DPI until it is
            // reintroduced to DWM. GetDpiForWindow is authoritative during that
            // hand-off; PresentationSource may still describe the old surface.
            Left = frame.Left / scale; Top = frame.Top / scale;
            Width = Math.Max(MinWidth, logicalWidth); Height = Math.Max(MinHeight, logicalHeight);
            UpdateLayout();
            SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Round(frame.Left), (int)Math.Round(frame.Top),
                Math.Max(1, (int)Math.Round(frame.Width)), Math.Max(1, (int)Math.Round(frame.Height)), 0x0014);
            UpdateLayout();

            NativeRect actual;
            var rectReady = GetWindowRect(hwnd, out actual)
                && Math.Abs(actual.Left - frame.Left) <= 1 && Math.Abs(actual.Top - frame.Top) <= 1
                && Math.Abs((actual.Right - actual.Left) - frame.Width) <= 1
                && Math.Abs((actual.Bottom - actual.Top) - frame.Height) <= 1;
            var source = PresentationSource.FromVisual(this);
            var sourceDpi = source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformToDevice.M11 * 96.0 : nativeDpi;
            var dpiReady = nativeDpi == 0 || Math.Abs(sourceDpi - nativeDpi) < .5;
            settings.HasPosition = true; settings.Left = Left; settings.Top = Top;
            settings.Width = ActualWidth; settings.Height = ActualHeight;
            PlacementTrace.Write("MATCH end frame=" + RectText(frame) + " dpi=" + nativeDpi + "/" +
                sourceDpi.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
                " ready=" + rectReady + "/" + dpiReady + " wpf=" + WpfRectText());
            return rectReady && dpiReady;
        }

        string WpfRectText()
        {
            NativeRect rect; var hwnd = new WindowInteropHelper(this).Handle;
            var native = hwnd != IntPtr.Zero && GetWindowRect(hwnd, out rect) ? RectText(rect) : "none";
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "L={0:0.##},T={1:0.##},W={2:0.##},H={3:0.##},Actual={4:0.##}x{5:0.##},native={6}",
                Left, Top, Width, Height, ActualWidth, ActualHeight, native);
        }

        static string RectText(NativeRect rect)
        {
            return rect.Left + "," + rect.Top + "," + (rect.Right - rect.Left) + "x" + (rect.Bottom - rect.Top);
        }

        static string RectText(Rect rect)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.##},{1:0.##},{2:0.##}x{3:0.##}", rect.Left, rect.Top, rect.Width, rect.Height);
        }

        void EnsureWindowOnScreen(bool forcePrimary)
        {
            var source = PresentationSource.FromVisual(this);
            var transform = source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            var areas = Forms.Screen.AllScreens.OrderByDescending(x => x.Primary).Select(screen =>
            {
                var topLeft = transform.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
                var bottomRight = transform.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
                return new Rect(topLeft, bottomRight);
            }).ToArray();
            var current = new Rect(Left, Top, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
            var fitted = FitWindowToScreens(current, areas, forcePrimary);
            if (fitted == current) return;
            Width = fitted.Width; Height = fitted.Height; Left = fitted.Left; Top = fitted.Top;
            settings.HasPosition = true; settings.Left = Left; settings.Top = Top;
            settings.Width = Width; settings.Height = Height; SavePhysicalPlacement(); Store.SaveSettings(settings);
        }

        public static Rect FitWindowToScreens(Rect window, Rect[] workAreas, bool forcePrimary)
        {
            if (workAreas == null || workAreas.Length == 0) return window;
            var target = forcePrimary ? workAreas[0] : workAreas.OrderByDescending(area =>
            {
                var overlap = Rect.Intersect(window, area);
                return overlap.IsEmpty ? 0 : overlap.Width * overlap.Height;
            }).First();
            var targetOverlap = Rect.Intersect(window, target);
            if (!forcePrimary && !targetOverlap.IsEmpty && targetOverlap.Width >= Math.Min(160, window.Width)
                && targetOverlap.Height >= Math.Min(72, window.Height))
            {
                var fittedWidth = Math.Min(window.Width, target.Width);
                var fittedHeight = Math.Min(window.Height, target.Height);
                return new Rect(Math.Max(target.Left, Math.Min(window.Left, target.Right - fittedWidth)),
                    Math.Max(target.Top, Math.Min(window.Top, target.Bottom - fittedHeight)), fittedWidth, fittedHeight);
            }

            var primary = workAreas[0];
            var width = Math.Min(window.Width > 0 ? window.Width : 1120, Math.Max(820, primary.Width - 32));
            var height = Math.Min(window.Height > 0 ? window.Height : 700, Math.Max(560, primary.Height - 32));
            return new Rect(primary.Left + Math.Max(0, (primary.Width - width) / 2),
                primary.Top + Math.Max(0, (primary.Height - height) / 2), width, height);
        }
    }
}
