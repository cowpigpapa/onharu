using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace FamilyPlanner
{
    public static class DesktopLayer
    {
        const int GWLP_HWNDPARENT = -8;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x20;
        const long WS_EX_NOACTIVATE = 0x08000000L;
        const int WM_HOTKEY = 0x0312;
        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
            HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_ALT = 0x0001;
        const uint VK_F = 0x46;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        static extern int SetWindowLong32(IntPtr window, int index, int value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        static extern int GetWindowLong32(IntPtr window, int index);
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        static void SetOwner(IntPtr window, IntPtr owner)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(window, GWLP_HWNDPARENT, owner);
            else SetWindowLong32(window, GWLP_HWNDPARENT, owner.ToInt32());
        }

        static long GetStyle(IntPtr window)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(window, GWL_EXSTYLE).ToInt64() : GetWindowLong32(window, GWL_EXSTYLE);
        }

        static void SetStyle(IntPtr window, long style)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(window, GWL_EXSTYLE, new IntPtr(style));
            else SetWindowLong32(window, GWL_EXSTYLE, (int)style);
        }

        public static void SetClickThrough(Window window, bool enabled)
        {
            var handle = new WindowInteropHelper(window).Handle;
            var style = GetStyle(handle);
            SetStyle(handle, enabled ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT);
        }

        public static void InstallInteractionToggle(Window window, Action toggle)
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            RegisterHotKey(source.Handle, 7419, MOD_CONTROL | MOD_ALT, VK_F);
            source.AddHook(delegate(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (message == WM_HOTKEY && wParam.ToInt32() == 7419) { toggle(); handled = true; }
                return IntPtr.Zero;
            });
        }

        public static void Attach(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            var desktop = FindWindow("Progman", null);
            if (handle == IntPtr.Zero || desktop == IntPtr.Zero) return;
            SetOwner(handle, desktop);
            SetStyle(handle, GetStyle(handle) | WS_EX_NOACTIVATE);
            window.Topmost = false;
            window.ShowInTaskbar = false;
            Lower(window);
        }

        public static void Detach(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            SetOwner(handle, IntPtr.Zero);
            window.Topmost = false;
            window.ShowInTaskbar = true;
        }

        public static void Lower(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
                SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }


        public static void BeginResize(Window window)
        {
            BeginResize(window, 4);
        }

        public static void BeginResize(Window window, int edge)
        {
            var handle = new WindowInteropHelper(window).Handle;
            var previous = window.ResizeMode;
            try
            {
                window.ResizeMode = ResizeMode.CanResize;
                ReleaseCapture();
                var hit = edge == 1 ? HTTOPLEFT : edge == 2 ? HTTOPRIGHT : edge == 3 ? HTBOTTOMLEFT : edge == 4 ? HTBOTTOMRIGHT
                    : edge == 5 ? HTLEFT : edge == 6 ? HTRIGHT : edge == 7 ? HTTOP : HTBOTTOM;
                SendMessage(handle, WM_NCLBUTTONDOWN, new IntPtr(hit), IntPtr.Zero);
            }
            finally { window.ResizeMode = previous; }
        }
    }
}
