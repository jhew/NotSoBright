using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using NotSoBright.Interop;
using NotSoBright.Models;
using NotSoBright.Services;
using NotSoBright.ViewModels;

namespace NotSoBright;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Hotkey IDs — arbitrary unique integers per MSDN
    private const int HotkeyToggle          = 9001;
    private const int HotkeyIncreaseOpacity = 9002;
    private const int HotkeyDecreaseOpacity = 9003;
    private const int HotkeyToggleMode      = 9004;
    private const int HotkeyTogglePanel     = 9005;

    private HwndSource? _hwndSource;
    private HotkeyService? _hotkeyService;
    private readonly OverlayViewModel _viewModel;
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly DispatcherTimer _saveTimer;

    public MainWindow(AppConfig config, ConfigService configService)
    {
        InitializeComponent();
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _configService.ConfigSaveFailed += OnConfigSaveFailed;
        _viewModel = new OverlayViewModel();
        _viewModel.MinimizeRequested += OnMinimizeRequested;
        _viewModel.MaximizeRestoreRequested += OnMaximizeRestoreRequested;
        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveConfig();
        };

        // Ensure popup window style is set when it opens
        if (ControlPanelPopup is not null)
        {
            ControlPanelPopup.Opened += (_, _) => UpdatePopupWindowStyle();
        }

        ApplyConfig();
        LocationChanged += (_, _) => { ScheduleSave(); RefreshPopupPosition(); };
        SizeChanged += (_, e) => { ScheduleSave(); RefreshPopupPosition(); };
        StateChanged += (_, _) => { ScheduleSave(); UpdateMaximizeRestoreLabel(); };
        Closing += (_, _) => { SaveConfig(); _hotkeyService?.Dispose(); };
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        ShowOverlay();
    }

    public void ShowOverlay()
    {
        if (!IsVisible)
        {
            Show();

            // Re-open the popup so its HWND is created after the main window's
            // HWND, giving it the topmost z-order among topmost windows.
            if (ControlPanelPopup is not null && _viewModel.IsEditMode)
            {
                ControlPanelPopup.IsOpen = false;
                ControlPanelPopup.IsOpen = true;
            }
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    public bool IsEditMode => _viewModel.IsEditMode;

    public void ToggleMode()
    {
        _viewModel.ToggleModeCommand.Execute(null);
    }

    public void IncreaseOpacity()
    {
        _viewModel.IncreaseOpacityCommand.Execute(null);
    }

    public void DecreaseOpacity()
    {
        _viewModel.DecreaseOpacityCommand.Execute(null);
    }

    public void SetOpacity(double percent)
    {
        _viewModel.OpacityPercent = percent;
    }

    public void SetTintColor(string hexColor)
    {
        _viewModel.TintColor = hexColor;
    }

    public void CoverMonitor(int monitorIndex)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (monitorIndex < 0 || monitorIndex >= screens.Length)
        {
            return;
        }

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return;
        }

        var scaleX = source.CompositionTarget.TransformToDevice.M11;
        var scaleY = source.CompositionTarget.TransformToDevice.M22;

        var b = screens[monitorIndex].Bounds;

        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }

        Left   = b.Left   / scaleX;
        Top    = b.Top    / scaleY;
        Width  = b.Width  / scaleX;
        Height = b.Height / scaleY;
    }

    private void OnRootMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Each notch of a standard wheel is 120 units; scale to 1% per notch.
        var delta = e.Delta / 120;
        _viewModel.OpacityPercent += delta;
        e.Handled = true;
    }

    private void OnOpacityTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.ApplyOpacityText();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void OnOpacityTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyOpacityText();
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Up:
                    IncreaseOpacity();
                    e.Handled = true;
                    break;
                case Key.Down:
                    DecreaseOpacity();
                    e.Handled = true;
                    break;
                case Key.M:
                    ToggleMode();
                    e.Handled = true;
                    break;
            }
        }
    }

    private void RefreshPopupPosition()
    {
        if (ControlPanelPopup is null || !ControlPanelPopup.IsOpen)
        {
            return;
        }

        // Nudging the offset by 0 forces WPF to recalculate the popup's screen position.
        ControlPanelPopup.HorizontalOffset += 0.001;
        ControlPanelPopup.HorizontalOffset -= 0.001;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);
        UpdateClickThroughStyle();
        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        if (_hwndSource is null)
        {
            return;
        }

        _hotkeyService = new HotkeyService(_hwndSource.Handle);
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Win+Shift+D  — toggle overlay visibility
        _hotkeyService.Register(HotkeyToggle,          NativeMethods.ModWin | NativeMethods.ModShift, NativeMethods.VkD);
        // Win+Shift+Up — increase opacity
        _hotkeyService.Register(HotkeyIncreaseOpacity, NativeMethods.ModWin | NativeMethods.ModShift, NativeMethods.VkUp);
        // Win+Shift+Down — decrease opacity
        _hotkeyService.Register(HotkeyDecreaseOpacity, NativeMethods.ModWin | NativeMethods.ModShift, NativeMethods.VkDown);
        // Win+Shift+M  — toggle edit/passive mode
        _hotkeyService.Register(HotkeyToggleMode,      NativeMethods.ModWin | NativeMethods.ModShift, NativeMethods.VkM);
        // Win+Shift+H  — show/hide the control panel
        _hotkeyService.Register(HotkeyTogglePanel,     NativeMethods.ModWin | NativeMethods.ModShift, NativeMethods.VkH);
    }

    private void OnHotkeyPressed(object? sender, int id)
    {
        switch (id)
        {
            case HotkeyToggle:          ToggleVisibility();          break;
            case HotkeyIncreaseOpacity: IncreaseOpacity();           break;
            case HotkeyDecreaseOpacity: DecreaseOpacity();           break;
            case HotkeyToggleMode:      ToggleMode();                break;
            case HotkeyTogglePanel:     ToggleControlPanel();        break;
        }
    }

    private void ToggleControlPanel()
    {
        if (ControlPanelPopup is not null)
        {
            ControlPanelPopup.IsOpen = !ControlPanelPopup.IsOpen;
        }
    }

    private void OnMinimizeRequested(object? sender, System.EventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreRequested(object? sender, System.EventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateMaximizeRestoreLabel()
    {
        // □ = maximize, ❐ = restore
        _viewModel.MaximizeRestoreLabel = WindowState == WindowState.Maximized ? "\u2750" : "\u25A1";
    }

    private void OnCloseRequested(object? sender, System.EventArgs e)
    {
        // Temporarily make window interactive so it can own the dialog
        var wasPassive = !_viewModel.IsEditMode;
        if (wasPassive)
        {
            // Temporarily set interactive to make dialog appear on top
            Topmost = true;
            Activate();
        }

        var result = System.Windows.MessageBox.Show(
            this, 
            "Close the overlay?", 
            "Confirm", 
            System.Windows.MessageBoxButton.YesNo, 
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            Close();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // IsEditMode is a computed property that fires alongside Mode — only react to Mode
        // to avoid calling UpdateClickThroughStyle twice per toggle.
        if (e.PropertyName == nameof(OverlayViewModel.Mode))
        {
            UpdateClickThroughStyle();

            // Auto-hide the control panel in passive mode; restore it in edit mode.
            if (ControlPanelPopup is not null)
            {
                ControlPanelPopup.IsOpen = _viewModel.IsEditMode;
            }

            ScheduleSave();
        }

        if (e.PropertyName == nameof(OverlayViewModel.OpacityPercent))
        {
            ScheduleSave();
        }

        if (e.PropertyName == nameof(OverlayViewModel.TintColor))
        {
            ScheduleSave();
        }
    }

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsEditMode)
        {
            return;
        }

        if (IsWithinControlPanel(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            // Restore from maximized first (standard "drag to restore" behaviour).
            if (WindowState == WindowState.Maximized)
            {
                // Calculate where the restored window should appear so the cursor
                // stays under the title-bar area (match the cursor's X ratio).
                var cursorPos = e.GetPosition(this);
                var restoreWidth = RestoreBounds.Width;
                var screenPos = PointToScreen(cursorPos);

                WindowState = WindowState.Normal;

                // Clamp the left edge so the cursor lands roughly where it was.
                Left = screenPos.X - Math.Min(cursorPos.X, restoreWidth - 16);
                Top = screenPos.Y - 8;
            }

            DragMove();
            SnapToMonitorEdges();
        }
        catch (System.InvalidOperationException)
        {
        }
    }

    private bool IsWithinControlPanel(DependencyObject? source)
    {
        // Iterative walk avoids stack overflow on deep visual trees.
        // VisualTreeHelper.GetParent only works for Visual/Visual3D nodes;
        // content elements (e.g. Run inside a Button) use LogicalTreeHelper.
        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, ControlPanel))
            {
                return true;
            }

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private void UpdateClickThroughStyle()
    {
        if (_hwndSource is null)
        {
            return;
        }

        var exStyle = NativeMethods.GetWindowLongPtr(_hwndSource.Handle, NativeMethods.GwlExStyle);
        var exStyleValue = exStyle.ToInt64();

        if (_viewModel.IsEditMode)
        {
            // Edit mode: window is interactive
            exStyleValue &= ~NativeMethods.WsExTransparent;
        }
        else
        {
            // Passive mode: window is click-through but remains topmost and visible
            exStyleValue |= NativeMethods.WsExTransparent;
        }

        // Keep window topmost in both modes so overlay remains visible
        Topmost = true;

        NativeMethods.SetWindowLongPtr(_hwndSource.Handle, NativeMethods.GwlExStyle, new IntPtr(exStyleValue));

        // Update popup window style
        UpdatePopupWindowStyle();
    }

    private void UpdatePopupWindowStyle()
    {
        // Ensure the popup is not click-through and is topmost
        if (ControlPanelPopup is not null && ControlPanelPopup.IsOpen)
        {
            var popupSource = PresentationSource.FromVisual(ControlPanelPopup.Child) as HwndSource;
            if (popupSource is not null)
            {
                var popupExStyle = NativeMethods.GetWindowLongPtr(popupSource.Handle, NativeMethods.GwlExStyle);
                var popupExStyleValue = popupExStyle.ToInt64();
                
                // Remove transparent flag and ensure topmost
                popupExStyleValue &= ~NativeMethods.WsExTransparent;
                popupExStyleValue |= NativeMethods.WsExTopmost;
                
                NativeMethods.SetWindowLongPtr(popupSource.Handle, NativeMethods.GwlExStyle, new IntPtr(popupExStyleValue));
            }
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey)
        {
            if (_hotkeyService is not null)
            {
                _hotkeyService.HandleWmHotkey(wParam.ToInt32());
                handled = true;
            }

            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WmNcHitTest)
        {
            var screenPoint = GetScreenPoint(lParam);
            var windowPoint = PointFromScreen(screenPoint);

            if (_viewModel.IsEditMode)
            {
                // In Edit Mode, allow normal interaction
                var hitTestResult = GetHitTestResult(windowPoint);
                if (hitTestResult != 0)
                {
                    handled = true;
                    return new IntPtr(hitTestResult);
                }
            }
            else
            {
                // In Passive Mode, let WS_EX_TRANSPARENT handle click-through naturally.
                // The control panel popup has its own window and will remain interactive.
                // Do not handle the message for the main window.
            }
        }

        return IntPtr.Zero;
    }

    private int GetHitTestResult(System.Windows.Point point)
    {
        const int resizeBorder = 6;

        // When maximized, disable all resize handles. The top strip acts as a
        // caption so that double-clicking it restores the window normally
        // instead of snapping it to the screen edge via a resize operation.
        if (WindowState == WindowState.Maximized)
        {
            var isTopStrip = point.Y <= resizeBorder;
            return isTopStrip ? NativeMethods.HtCaption : NativeMethods.HtClient;
        }

        var isLeft = point.X <= resizeBorder;
        var isRight = point.X >= ActualWidth - resizeBorder;
        var isTop = point.Y <= resizeBorder;
        var isBottom = point.Y >= ActualHeight - resizeBorder;

        // Corners
        if (isTop && isLeft) return NativeMethods.HtTopLeft;
        if (isTop && isRight) return NativeMethods.HtTopRight;
        if (isBottom && isLeft) return NativeMethods.HtBottomLeft;
        if (isBottom && isRight) return NativeMethods.HtBottomRight;

        // Edges
        if (isTop) return NativeMethods.HtTop;
        if (isBottom) return NativeMethods.HtBottom;
        if (isLeft) return NativeMethods.HtLeft;
        if (isRight) return NativeMethods.HtRight;

        // Client area (buttons, controls)
        return NativeMethods.HtClient;
    }

    private static System.Windows.Point GetScreenPoint(IntPtr lParam)
    {
        var xy = lParam.ToInt64();
        var x = (short)(xy & 0xFFFF);
        var y = (short)((xy >> 16) & 0xFFFF);
        return new System.Windows.Point(x, y);
    }

    private void SnapToMonitorEdges()
    {
        const double snapThreshold = 20;

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return;
        }

        var scaleX = source.CompositionTarget.TransformToDevice.M11;
        var scaleY = source.CompositionTarget.TransformToDevice.M22;

        // Window rect in physical pixels
        var winLeft   = Left   * scaleX;
        var winTop    = Top    * scaleY;
        var winRight  = (Left + ActualWidth)  * scaleX;
        var winBottom = (Top  + ActualHeight) * scaleY;

        var thresholdX = snapThreshold * scaleX;
        var thresholdY = snapThreshold * scaleY;

        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var b = screen.Bounds;

            // Left / right edge
            if (Math.Abs(winLeft  - b.Left)  < thresholdX) Left = b.Left  / scaleX;
            else if (Math.Abs(winRight - b.Right) < thresholdX) Left = (b.Right / scaleX) - ActualWidth;

            // Top / bottom edge
            if (Math.Abs(winTop    - b.Top)    < thresholdY) Top = b.Top    / scaleY;
            else if (Math.Abs(winBottom - b.Bottom) < thresholdY) Top = (b.Bottom / scaleY) - ActualHeight;
        }
    }

    private void ApplyConfig()
    {
        Width = Math.Max(200, _config.Width);
        Height = Math.Max(200, _config.Height);
        Left = double.IsNaN(_config.Left) ? Left : _config.Left;
        Top = double.IsNaN(_config.Top) ? Top : _config.Top;

        _viewModel.OpacityPercent = _config.OpacityPercent;
        _viewModel.TintColor = _config.TintColor;
        _viewModel.Mode = _config.Mode;
    }

    private void ScheduleSave()
    {
        // Stop is a no-op on an already-stopped timer; restart unconditionally.
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveConfig()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

        _config.OpacityPercent = _viewModel.OpacityPercent;
        _config.TintColor = _viewModel.TintColor;
        _config.Mode = _viewModel.Mode;
        _config.Width = Math.Max(200, bounds.Width);
        _config.Height = Math.Max(200, bounds.Height);
        _config.Left = bounds.Left;
        _config.Top = bounds.Top;

        _configService.Save(_config);
    }

    private void OnConfigSaveFailed(object? sender, Exception e)
    {
        System.Windows.MessageBox.Show($"Failed to save configuration: {e.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
}