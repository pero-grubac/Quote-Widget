using System.Windows.Forms;

namespace QuoteWidget.Services;

public record MonitorInfo(string DeviceName, string DisplayName, System.Drawing.Rectangle Bounds, bool IsPrimary);

public static class MonitorService
{
    public static List<MonitorInfo> GetMonitors()
    {
        var screens = Screen.AllScreens;
        var monitors = new List<MonitorInfo>();

        for (var i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            var label = $"Display {i + 1}" + (screen.Primary ? " (Primary)" : string.Empty);
            monitors.Add(new MonitorInfo(screen.DeviceName, label, screen.Bounds, screen.Primary));
        }

        return monitors;
    }

    public static MonitorInfo? GetPrimary() => GetMonitors().FirstOrDefault(m => m.IsPrimary);
}
