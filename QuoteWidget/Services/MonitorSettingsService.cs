using QuoteWidget.Models;

namespace QuoteWidget.Services;

/// <summary>
/// Pure helpers over <see cref="Settings.Monitors"/>: copying appearance between monitors and
/// resetting appearance to defaults. Kept free of any real monitor/window dependency so it is
/// simple to unit test.
/// </summary>
public static class MonitorSettingsService
{
    public static void ApplyAppearanceToAllMonitors(Settings settings, string sourceKey)
    {
        if (!settings.Monitors.TryGetValue(sourceKey, out var source))
        {
            return;
        }

        foreach (var (key, target) in settings.Monitors)
        {
            if (key == sourceKey)
            {
                continue;
            }

            CopyAppearance(source, target);
        }
    }

    public static void CopyAppearanceToMonitor(Settings settings, string sourceKey, string targetKey)
    {
        if (sourceKey == targetKey
            || !settings.Monitors.TryGetValue(sourceKey, out var source)
            || !settings.Monitors.TryGetValue(targetKey, out var target))
        {
            return;
        }

        CopyAppearance(source, target);
    }

    public static void ResetAppearanceToDefaults(MonitorWidgetSettings target)
    {
        CopyAppearance(new MonitorWidgetSettings(), target);
    }

    public static void ResetAllMonitorsToDefaults(Settings settings)
    {
        foreach (var target in settings.Monitors.Values)
        {
            ResetAppearanceToDefaults(target);
        }
    }

    private static void CopyAppearance(MonitorWidgetSettings source, MonitorWidgetSettings target)
    {
        target.QuoteFontSize = source.QuoteFontSize;
        target.DetailsFontSize = source.DetailsFontSize;
        target.TextColor = source.TextColor;
        target.BackgroundColor = source.BackgroundColor;
        target.BackgroundOpacity = source.BackgroundOpacity;
        target.RotationIntervalMinutes = source.RotationIntervalMinutes;
    }
}
