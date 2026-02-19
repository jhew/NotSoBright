using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace NotSoBright.Services;

/// <summary>
/// Manages the Windows run-at-startup registry entry for NotSoBright.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NotSoBright";

    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(AppName) is not null;
    }

    /// <summary>
    /// Sets or clears the run-at-startup registry entry.
    /// Throws <see cref="Exception"/> on registry access failure so callers can revert UI state.
    /// </summary>
    public static void SetStartup(bool enable)
    {
        if (enable)
        {
            // CreateSubKey opens existing or creates missing key — avoids silent no-op.
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            var exePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? string.Empty;

            if (!string.IsNullOrEmpty(exePath))
            {
                key.SetValue(AppName, $"\"{exePath}\"");
            }
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}
