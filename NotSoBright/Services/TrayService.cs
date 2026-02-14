using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace NotSoBright.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _showHideItem;
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

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExecuteOnUi(() => System.Windows.Application.Current.Shutdown());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _showHideItem,
            toggleModeItem,
            new ToolStripSeparator(),
            increaseOpacityItem,
            decreaseOpacityItem,
            new ToolStripSeparator(),
            exitItem
        });

        Icon icon;
        try
        {
            icon = SystemIcons.Warning; // Custom dimming-themed icon
        }
        catch
        {
            icon = SystemIcons.Application; // Fallback
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "NotSoBright",
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
        _notifyIcon.ShowBalloonTip(4000, "NotSoBright", "Overlay cannot render over exclusive fullscreen. Switch to borderless or windowed mode.", ToolTipIcon.Info);
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
