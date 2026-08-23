using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FamilyPlanner
{
    sealed class DesktopActionWindow : IDisposable
    {
        const int Message = 0x8000 + 0x4F;
        readonly HwndSource source;
        public event Action<int, int> Received;
        public IntPtr WindowHandle { get { return source.Handle; } }

        public DesktopActionWindow()
        {
            var parameters = new HwndSourceParameters("Onharu.ActionSink")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
                ExtendedWindowStyle = 0x00000080 | 0x08000000
            };
            source = new HwndSource(parameters);
            source.AddHook(WndProc);
            ChangeWindowMessageFilterEx(source.Handle, Message, 1, IntPtr.Zero);
        }

        IntPtr WndProc(IntPtr hwnd, int value, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (value == Message && Received != null)
            {
                handled = true; Received(wParam.ToInt32(), lParam.ToInt32());
            }
            return IntPtr.Zero;
        }

        public void Dispose() { source.RemoveHook(WndProc); source.Dispose(); }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool ChangeWindowMessageFilterEx(IntPtr window, uint message, uint action, IntPtr changeInfo);
    }
}
