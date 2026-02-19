using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using NotSoBright.Interop;

namespace NotSoBright.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _showHideItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly MainWindow _window;
    private bool _disposed;

    public TrayService(MainWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));

        _showHideItem = new ToolStripMenuItem();
        _showHideItem.Click += (_, _) => ToggleVisibility();

        var toggleModeItem = new ToolStripMenuItem("Toggle Edit/Passive");
        toggleModeItem.Click += (_, _) => ExecuteOnUi(_window.ToggleMode);

        var increaseOpacityItem = new ToolStripMenuItem("Opacity +");
        increaseOpacityItem.Click += (_, _) => ExecuteOnUi(_window.IncreaseOpacity);

        var decreaseOpacityItem = new ToolStripMenuItem("Opacity -");
        decreaseOpacityItem.Click += (_, _) => ExecuteOnUi(_window.DecreaseOpacity);

        var presetsMenu = new ToolStripMenuItem("Opacity Presets");
        foreach (var preset in new[] { 10, 20, 35, 50, 75, 95 })
        {
            var pct = preset; // capture
            var item = new ToolStripMenuItem($"{pct}%");
            item.Click += (_, _) => ExecuteOnUi(() => _window.SetOpacity(pct));
            presetsMenu.DropDownItems.Add(item);
        }

        var shortcutsItem = new ToolStripMenuItem("Shortcuts\u2026");
        shortcutsItem.Click += (_, _) => ExecuteOnUi(ShowShortcuts);

        // Tint colour submenu
        var tintMenu = new ToolStripMenuItem("Overlay Tint");
        foreach (var (label, hex) in new[] { ("Black (default)", "#000000"), ("Amber (warm)", "#B35900"), ("Red", "#8B0000"), ("Sepia", "#3D2B1F") })
        {
            var h = hex;
            var ti = new ToolStripMenuItem(label);
            ti.Click += (_, _) => ExecuteOnUi(() => _window.SetTintColor(h));
            tintMenu.DropDownItems.Add(ti);
        }

        // Per-monitor coverage submenu — populated dynamically each time it opens
        var monitorMenu = new ToolStripMenuItem("Cover Monitor");
        monitorMenu.DropDownOpening += (_, _) =>
        {
            monitorMenu.DropDownItems.Clear();
            var screens = System.Windows.Forms.Screen.AllScreens;
            for (var i = 0; i < screens.Length; i++)
            {
                var idx = i;
                var b = screens[i].Bounds;
                var name = screens[i].DeviceName.TrimStart('\\').TrimStart('.');
                var mi = new ToolStripMenuItem($"Monitor {idx + 1}  ({b.Width}\u00d7{b.Height})  {name}");
                mi.Click += (_, _) => ExecuteOnUi(() => _window.CoverMonitor(idx));
                monitorMenu.DropDownItems.Add(mi);
            }
        };

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExecuteOnUi(() => System.Windows.Application.Current.Shutdown());

        _startupItem = new ToolStripMenuItem("Run at Startup")
        {
            Checked = StartupService.IsStartupEnabled(),
            CheckOnClick = true
        };
        _startupItem.CheckedChanged += (_, _) => StartupService.SetStartup(_startupItem.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _showHideItem,
            toggleModeItem,
            new ToolStripSeparator(),
            increaseOpacityItem,
            decreaseOpacityItem,
            presetsMenu,
            tintMenu,
            monitorMenu,
            new ToolStripSeparator(),
            _startupItem,
            shortcutsItem,
            new ToolStripSeparator(),
            exitItem
        });

        Icon icon;
        var hIcon = IntPtr.Zero;
        try
        {
            var sri = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/NotSoBright.png"));
            using var bitmap = new Bitmap(sri!.Stream);
            hIcon = bitmap.GetHicon();
            // Clone produces a fully managed Icon that owns its copy of the bitmap
            // data, so we can safely destroy the raw GDI HICON immediately after.
            icon = (Icon)Icon.FromHandle(hIcon).Clone();
        }
        catch
        {
            icon = SystemIcons.Application; // Fallback
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
            {
                NativeMethods.DestroyIcon(hIcon);
            }
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "NotSoBright — Dimmer Tool",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _window.IsVisibleChanged += OnWindowVisibilityChanged;
        UpdateShowHideText();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.IsVisibleChanged -= OnWindowVisibilityChanged;
        _notifyIcon.MouseClick -= OnNotifyIconMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    public void ShowFullscreenBlockedOnce()
    {
        _notifyIcon.ShowBalloonTip(4000, "NotSoBright — Dimmer Tool", "Overlay cannot render over exclusive fullscreen. Switch to borderless or windowed mode.", ToolTipIcon.Info);
    }

    private static void ShowShortcuts()
    {
        System.Windows.MessageBox.Show(
            "Global hotkeys (work even when overlay is in passive mode):\n\n" +
            "  Win + Shift + D      Toggle overlay visibility\n" +
            "  Win + Shift + ↑      Increase opacity\n" +
            "  Win + Shift + ↓      Decrease opacity\n" +
            "  Win + Shift + M      Toggle Edit / Passive mode\n" +
            "  Win + Shift + H      Show / hide control panel\n\n" +
            "Keyboard shortcuts (when overlay window is focused):\n\n" +
            "  Ctrl + ↑             Increase opacity\n" +
            "  Ctrl + ↓             Decrease opacity\n" +
            "  Ctrl + M             Toggle Edit / Passive mode\n\n" +
            "Mouse:\n\n" +
            "  Scroll wheel         Adjust opacity (in edit mode)",
            "Keyboard Shortcuts",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleVisibility();
        }
    }

    private void ToggleVisibility()
    {
        ExecuteOnUi(_window.ToggleVisibility);
    }

    private void OnWindowVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateShowHideText();
    }

    private void UpdateShowHideText()
    {
        _showHideItem.Text = _window.IsVisible ? "Hide Overlay" : "Show Overlay";
    }

    private static void ExecuteOnUi(Action action)
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(action);
    }
}
