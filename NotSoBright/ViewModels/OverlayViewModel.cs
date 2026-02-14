using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NotSoBright.Models;
using NotSoBright.Utilities;

namespace NotSoBright.ViewModels;

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private const double MinOpacityPercent = 1;
    private const double MaxOpacityPercent = 60;

    private double _opacityPercent = 35;
    private string _opacityText = "35%";
    private InteractionMode _mode = InteractionMode.Edit;
    private bool _isOpacityTextValid = true;

    public OverlayViewModel()
    {
        IncreaseOpacityCommand = new RelayCommand(_ => AdjustOpacity(1));
        DecreaseOpacityCommand = new RelayCommand(_ => AdjustOpacity(-1));
        ToggleModeCommand = new RelayCommand(_ => ToggleMode());
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;

    public ICommand IncreaseOpacityCommand { get; }
    public ICommand DecreaseOpacityCommand { get; }
    public ICommand ToggleModeCommand { get; }
    public ICommand CloseCommand { get; }

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
