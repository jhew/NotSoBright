using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
    private HwndSource? _hwndSource;
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

        ApplyConfig();
        LocationChanged += (_, _) => ScheduleSave();
        SizeChanged += (_, _) => ScheduleSave();
        StateChanged += (_, _) => ScheduleSave();
        Closing += (_, _) => SaveConfig();
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
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);
        UpdateClickThroughStyle();
    }

    private void OnCloseRequested(object? sender, System.EventArgs e)
    {
        Close();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlayViewModel.Mode) || e.PropertyName == nameof(OverlayViewModel.IsEditMode))
        {
            UpdateClickThroughStyle();
        }

        if (e.PropertyName == nameof(OverlayViewModel.Mode) || e.PropertyName == nameof(OverlayViewModel.OpacityPercent))
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
            DragMove();
        }
        catch (System.InvalidOperationException)
        {
        }
    }

    private bool IsWithinControlPanel(DependencyObject? source)
    {
        if (source is null)
        {
            return false;
        }

        if (ReferenceEquals(source, ControlPanel))
        {
            return true;
        }

        return IsWithinControlPanel(VisualTreeHelper.GetParent(source));
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
            exStyleValue &= ~NativeMethods.WsExTransparent;
            Topmost = true;
        }
        else
        {
            exStyleValue |= NativeMethods.WsExTransparent;
            Topmost = false;
        }

        NativeMethods.SetWindowLongPtr(_hwndSource.Handle, NativeMethods.GwlExStyle, new IntPtr(exStyleValue));

        // Ensure the popup is not click-through and is topmost
        if (ControlPanelPopup is not null)
        {
            var popupSource = PresentationSource.FromVisual(ControlPanelPopup) as System.Windows.Interop.HwndSource;
            if (popupSource is not null)
            {
                var popupExStyle = NativeMethods.GetWindowLongPtr(popupSource.Handle, NativeMethods.GwlExStyle);
                var popupExStyleValue = popupExStyle.ToInt64() & ~NativeMethods.WsExTransparent | NativeMethods.WsExTopmost;
                NativeMethods.SetWindowLongPtr(popupSource.Handle, NativeMethods.GwlExStyle, new IntPtr(popupExStyleValue));
            }
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
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
                // In Passive Mode, keep the overlay click-through, but allow interaction with control panel.
                if (IsPointInControlPanel(windowPoint))
                {
                    handled = true;
                    return new IntPtr(NativeMethods.HtClient);
                }
                handled = true;
                return new IntPtr(NativeMethods.HtTransparent);
            }
        }

        return IntPtr.Zero;
    }

    private bool IsPointInControlPanel(System.Windows.Point point)
    {
        // Prefer using the actual bounds of the control panel element, falling back to the
        // previous hardcoded rectangle only if layout information is not available yet.
        if (ControlPanel is not null &&
            ControlPanel.IsLoaded &&
            ControlPanel.ActualWidth > 0 &&
            ControlPanel.ActualHeight > 0)
        {
            GeneralTransform transform;
            try
            {
                transform = ControlPanel.TransformToAncestor(this);
            }
            catch (InvalidOperationException)
            {
                // Layout may not be ready; fall back to the approximate rectangle.
                return point.X >= 8 && point.X <= 158 && point.Y >= 8 && point.Y <= 48;
            }

            var topLeft = transform.Transform(new System.Windows.Point(0, 0));
            var bottomRight = transform.Transform(
                new System.Windows.Point(ControlPanel.ActualWidth, ControlPanel.ActualHeight));

            return point.X >= topLeft.X &&
                   point.X <= bottomRight.X &&
                   point.Y >= topLeft.Y &&
                   point.Y <= bottomRight.Y;
        }

        // Fallback: approximate position and size relative to the window
        return point.X >= 8 && point.X <= 158 && point.Y >= 8 && point.Y <= 48;
    }

    private int GetHitTestResult(System.Windows.Point point)
    {
        const int resizeBorder = 6;

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

    private void ApplyConfig()
    {
        Width = Math.Max(200, _config.Width);
        Height = Math.Max(200, _config.Height);
        Left = double.IsNaN(_config.Left) ? Left : _config.Left;
        Top = double.IsNaN(_config.Top) ? Top : _config.Top;

        _viewModel.OpacityPercent = _config.OpacityPercent;
        _viewModel.Mode = _config.Mode;
    }

    private void ScheduleSave()
    {
        if (!_saveTimer.IsEnabled)
        {
            _saveTimer.Start();
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveConfig()
    {
        if (_configService is null)
        {
            return;
        }

        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

        _config.OpacityPercent = _viewModel.OpacityPercent;
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