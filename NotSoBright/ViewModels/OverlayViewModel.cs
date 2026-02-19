using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using NotSoBright.Models;
using NotSoBright.Utilities;

namespace NotSoBright.ViewModels;

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private const double MinOpacityPercent = 1;
    private const double MaxOpacityPercent = 95;

    private double _opacityPercent = 35;
    private string _opacityText = "35%";
    private InteractionMode _mode = InteractionMode.Edit;
    private bool _isOpacityTextValid = true;
    private string _maximizeRestoreLabel = "\u25A1"; // □
    private string _tintColor = "#000000";

    public OverlayViewModel()
    {
        IncreaseOpacityCommand = new RelayCommand(_ => AdjustOpacity(1));
        DecreaseOpacityCommand = new RelayCommand(_ => AdjustOpacity(-1));
        ToggleModeCommand = new RelayCommand(_ => ToggleMode());
        MinimizeCommand = new RelayCommand(_ => MinimizeRequested?.Invoke(this, EventArgs.Empty));
        MaximizeRestoreCommand = new RelayCommand(_ => MaximizeRestoreRequested?.Invoke(this, EventArgs.Empty));
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? MinimizeRequested;
    public event EventHandler? MaximizeRestoreRequested;
    public event EventHandler? CloseRequested;

    public ICommand IncreaseOpacityCommand { get; }
    public ICommand DecreaseOpacityCommand { get; }
    public ICommand ToggleModeCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand MaximizeRestoreCommand { get; }
    public ICommand CloseCommand { get; }

    public string MaximizeRestoreLabel
    {
        get => _maximizeRestoreLabel;
        set
        {
            if (_maximizeRestoreLabel == value) return;
            _maximizeRestoreLabel = value;
            OnPropertyChanged();
        }
    }

    public double OpacityPercent
    {
        get => _opacityPercent;
        set
        {
            var clamped = Math.Clamp(value, MinOpacityPercent, MaxOpacityPercent);
            if (Math.Abs(_opacityPercent - clamped) < 0.001)
            {
                return;
            }

            _opacityPercent = clamped;
            OpacityText = $"{clamped:0}%";
            OnPropertyChanged();
            OnPropertyChanged(nameof(OverlayOpacity));
        }
    }

    public string OpacityText
    {
        get => _opacityText;
        set
        {
            if (_opacityText == value)
            {
                return;
            }

            _opacityText = value;
            OnPropertyChanged();
        }
    }

    public double OverlayOpacity => OpacityPercent / 100.0;

    // Accepts #RGB, #RRGGBB, #ARGB, #AARRGGBB only — rejects named colours and
    // other ColorConverter syntax so a tampered config.json cannot supply
    // unexpected input to the converter on every render pass.
    private static readonly Regex HexColorRegex =
        new(@"^#(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string TintColor
    {
        get => _tintColor;
        set
        {
            // Silently fall back to black for any value that isn't a plain hex colour.
            var sanitized = IsValidHexColor(value) ? value : "#000000";
            if (_tintColor == sanitized) return;
            _tintColor = sanitized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TintBrush));
        }
    }

    public System.Windows.Media.Brush TintBrush
    {
        get
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_tintColor);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch (Exception)
            {
                return System.Windows.Media.Brushes.Black;
            }
        }
    }

    public static bool IsValidHexColor(string? value) =>
        value is not null && HexColorRegex.IsMatch(value);

    public void ApplyOpacityText()
    {
        var text = OpacityText?.Trim().TrimEnd('%') ?? string.Empty;
        if (double.TryParse(text, out var value))
        {
            if (value >= MinOpacityPercent && value <= MaxOpacityPercent)
            {
                OpacityPercent = value;
                IsOpacityTextValid = true;
            }
            else
            {
                OpacityText = $"{OpacityPercent:0}%";
                IsOpacityTextValid = false;
            }
        }
        else
        {
            OpacityText = $"{OpacityPercent:0}%";
            IsOpacityTextValid = false;
        }
    }

    public InteractionMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(ModeLabel));
        }
    }

    public bool IsEditMode => Mode == InteractionMode.Edit;

    public string ModeLabel => IsEditMode ? "Edit" : "Passive";

    public bool IsOpacityTextValid
    {
        get => _isOpacityTextValid;
        private set
        {
            if (_isOpacityTextValid == value)
            {
                return;
            }

            _isOpacityTextValid = value;
            OnPropertyChanged();
        }
    }

    private void AdjustOpacity(int delta)
    {
        OpacityPercent += delta;
    }

    private void ToggleMode()
    {
        Mode = IsEditMode ? InteractionMode.Passive : InteractionMode.Edit;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
