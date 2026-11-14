namespace QuoteWidget.Models;

public class Settings
{
    public bool StartWithWindows { get; set; } = false;
    public bool AlwaysOnTop { get; set; } = false;
    public bool ShowSameQuoteOnAllMonitors { get; set; } = false;
    public bool HideWidgetsWhenFullscreen { get; set; } = true;
    public bool HasShownTrayHint { get; set; } = false;

    /// <summary>
    /// Per-monitor widget settings, keyed by the monitor's device name (e.g. "\\.\DISPLAY1").
    /// Entries for monitors that are temporarily disconnected are kept, not deleted.
    /// </summary>
    public Dictionary<string, MonitorWidgetSettings> Monitors { get; set; } = new();
}
