using NotSoBright.Models;

namespace NotSoBright.Models;

public sealed class AppConfig
{
    public double OpacityPercent { get; set; } = 35;
    public double Width { get; set; } = 640;
    public double Height { get; set; } = 480;
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public InteractionMode Mode { get; set; } = InteractionMode.Edit;
    /// <summary>Overlay tint colour as a hex string, e.g. "#000000".</summary>
    public string TintColor { get; set; } = "#000000";
}
