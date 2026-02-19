using System;
using System.Collections.Generic;
using NotSoBright.Interop;

namespace NotSoBright.Services;

/// <summary>
/// Registers system-wide hotkeys and raises <see cref="HotkeyPressed"/> when one fires.
/// The caller is responsible for forwarding WM_HOTKEY messages via <see cref="HandleWmHotkey"/>.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly List<int> _registeredIds = new();
    private bool _disposed;

    public event EventHandler<int>? HotkeyPressed;

    public HotkeyService(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    /// <summary>Registers a hotkey. Returns true on success.</summary>
    public bool Register(int id, int modifiers, int virtualKey)
    {
        if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers, virtualKey))
        {
            _registeredIds.Add(id);
            return true;
        }

        return false;
    }

    /// <summary>Call this from WndProc when msg == WM_HOTKEY.</summary>
    public void HandleWmHotkey(int id) => HotkeyPressed?.Invoke(this, id);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var id in _registeredIds)
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }

        _registeredIds.Clear();
    }
}
