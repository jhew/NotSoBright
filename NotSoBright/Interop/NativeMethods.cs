using System;
using System.Runtime.InteropServices;

namespace NotSoBright.Interop;

internal static class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const int WsExTransparent = 0x20;

    internal const int GwlStyle = -16;
    internal const uint WsPopup = 0x80000000;
    internal const int WsCaption = 0x00C00000;

    internal const int WmNcHitTest = 0x0084;
    internal const int HtTransparent = -1;
    internal const int HtClient = 1;
    internal const int HtCaption = 2;
    internal const int HtLeft = 10;
    internal const int HtRight = 11;
    internal const int HtTop = 12;
    internal const int HtTopLeft = 13;
    internal const int HtTopRight = 14;
    internal const int HtBottom = 15;
    internal const int HtBottomLeft = 16;
    internal const int HtBottomRight = 17;

    internal const int MonitorDefaultToNearest = 2;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
