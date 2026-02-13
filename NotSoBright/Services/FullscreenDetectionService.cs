using System;
using System.Windows.Threading;
using NotSoBright.Interop;

namespace NotSoBright.Services;

public sealed class FullscreenDetectionService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<bool> _isOverlayVisible;
    private readonly Action _onFullscreenBlocked;
    private readonly Action _onFullscreenExit;
    private bool _notified;
    private bool _isExclusiveFullscreen;
    private bool _disposed;

    public FullscreenDetectionService(Func<bool> isOverlayVisible, Action onFullscreenBlocked, Action onFullscreenExit)
    {
        _isOverlayVisible = isOverlayVisible ?? throw new ArgumentNullException(nameof(isOverlayVisible));
        _onFullscreenBlocked = onFullscreenBlocked ?? throw new ArgumentNullException(nameof(onFullscreenBlocked));
        _onFullscreenExit = onFullscreenExit ?? throw new ArgumentNullException(nameof(onFullscreenExit));

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += (_, _) => CheckFullscreen();
        _timer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
    }

    private void CheckFullscreen()
    {
        var isExclusiveFullscreen = IsExclusiveFullscreen();

        if (isExclusiveFullscreen && !_isExclusiveFullscreen)
        {
            _isExclusiveFullscreen = true;

            if (_isOverlayVisible() && !_notified)
            {
                _notified = true;
                _onFullscreenBlocked();
            }

            return;
        }

        if (!isExclusiveFullscreen && _isExclusiveFullscreen)
        {
            _isExclusiveFullscreen = false;
            _notified = false;
            _onFullscreenExit();
        }
    }

    private static bool IsExclusiveFullscreen()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(foreground, out var rect))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new NativeMethods.MonitorInfo
        {
            Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfo>()
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        var isFullscreenBounds = rect.Left <= info.Monitor.Left
            && rect.Top <= info.Monitor.Top
            && rect.Right >= info.Monitor.Right
            && rect.Bottom >= info.Monitor.Bottom;

        if (!isFullscreenBounds)
        {
            return false;
        }

        var style = NativeMethods.GetWindowLongPtr(foreground, NativeMethods.GwlStyle).ToInt64();
        var isPopup = (style & (long)NativeMethods.WsPopup) != 0;
        var hasCaption = (style & NativeMethods.WsCaption) != 0;

        return isPopup && !hasCaption;
    }
}
