namespace QuoteWidget.Models;

public class MonitorWidgetSettings
{
    public bool Enabled { get; set; } = true;
    public bool IsLocked { get; set; } = true;

    public double QuoteFontSize { get; set; } = 36;
    public double DetailsFontSize { get; set; } = 16;
    public string TextColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "#000000";
    public double BackgroundOpacity { get; set; } = 0.0;
    public int RotationIntervalMinutes { get; set; } = 30;

    // Stored relative to this monitor's own bounds origin, so saved positions survive
    // resolution changes and monitors being rearranged.
    public double RelativeX { get; set; } = 100;
    public double RelativeY { get; set; } = 100;
    public double Width { get; set; } = 700;
    public double Height { get; set; } = 320;
}
