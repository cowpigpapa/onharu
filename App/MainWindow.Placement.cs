using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void DisplaySettingsChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(delegate { EnsureWindowOnScreen(false); if (positionLocked) PublishAndHide(); }));
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
            settings.Width = Width; settings.Height = Height; Store.SaveSettings(settings);
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
