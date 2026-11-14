using QuoteWidget.Models;
using QuoteWidget.Services;

namespace QuoteWidget.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _filePath;

    public SettingsStoreTests()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"quotewidget-settings-test-{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    private const string LegacySettingsJson = """
    {
      "RotationIntervalMinutes": 45,
      "QuoteFontSize": 40,
      "DetailsFontSize": 18,
      "TextColor": "#FF0000",
      "BackgroundColor": "#00FF00",
      "BackgroundOpacity": 0.5,
      "AlwaysOnTop": true,
      "StartWithWindows": true,
      "IsLocked": false,
      "WindowLeft": 150,
      "WindowTop": 75,
      "WindowWidth": 800,
      "WindowHeight": 300
    }
    """;

    [Fact]
    public void Load_MigratesLegacySingleWidgetFile_IntoMonitorsDictionary()
    {
        File.WriteAllText(_filePath, LegacySettingsJson);

        var store = new SettingsStore(_filePath);

        Assert.False(store.IsFirstRun);
        Assert.True(store.Current.Monitors.ContainsKey(SettingsStore.MigratedPrimaryKey));

        var monitor = store.Current.Monitors[SettingsStore.MigratedPrimaryKey];
        Assert.Equal(45, monitor.RotationIntervalMinutes);
        Assert.Equal(40, monitor.QuoteFontSize);
        Assert.Equal(18, monitor.DetailsFontSize);
        Assert.Equal("#FF0000", monitor.TextColor);
        Assert.Equal("#00FF00", monitor.BackgroundColor);
        Assert.Equal(0.5, monitor.BackgroundOpacity);
        Assert.False(monitor.IsLocked);
        Assert.Equal(150, monitor.RelativeX);
        Assert.Equal(75, monitor.RelativeY);
        Assert.Equal(800, monitor.Width);
        Assert.Equal(300, monitor.Height);

        Assert.True(store.Current.AlwaysOnTop);
        Assert.True(store.Current.StartWithWindows);
    }

    [Fact]
    public void Load_NewSchemaFile_IsNotTreatedAsLegacy()
    {
        var settings = new Settings();
        settings.Monitors["\\\\.\\DISPLAY1"] = new MonitorWidgetSettings { QuoteFontSize = 50 };
        var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);

        var store = new SettingsStore(_filePath);

        Assert.False(store.Current.Monitors.ContainsKey(SettingsStore.MigratedPrimaryKey));
        Assert.Equal(50, store.Current.Monitors["\\\\.\\DISPLAY1"].QuoteFontSize);
    }

    [Fact]
    public void Load_UsesDefaultsForSettingsMissingFromAnOlderFile()
    {
        // A settings file written before HideWidgetsWhenFullscreen / HasShownTrayHint existed.
        File.WriteAllText(_filePath, """{ "AlwaysOnTop": true, "Monitors": {} }""");

        var store = new SettingsStore(_filePath);

        Assert.True(store.Current.AlwaysOnTop);
        Assert.True(store.Current.HideWidgetsWhenFullscreen);
        Assert.False(store.Current.HasShownTrayHint);
    }

    [Fact]
    public void Load_CreatesDefaults_WhenFileDoesNotExist()
    {
        var store = new SettingsStore(_filePath);

        Assert.True(store.IsFirstRun);
        Assert.Empty(store.Current.Monitors);
        Assert.True(File.Exists(_filePath));
    }

    [Fact]
    public void RenameMonitorKey_MovesEntry_AndPersists()
    {
        File.WriteAllText(_filePath, LegacySettingsJson);
        var store = new SettingsStore(_filePath);

        store.RenameMonitorKey(SettingsStore.MigratedPrimaryKey, "\\\\.\\DISPLAY1");

        Assert.False(store.Current.Monitors.ContainsKey(SettingsStore.MigratedPrimaryKey));
        Assert.True(store.Current.Monitors.ContainsKey("\\\\.\\DISPLAY1"));

        var reloaded = new SettingsStore(_filePath);
        Assert.True(reloaded.Current.Monitors.ContainsKey("\\\\.\\DISPLAY1"));
    }

    [Fact]
    public void RenameMonitorKey_DoesNothing_WhenNewKeyAlreadyExists()
    {
        var settings = new Settings();
        settings.Monitors["old"] = new MonitorWidgetSettings { QuoteFontSize = 10 };
        settings.Monitors["new"] = new MonitorWidgetSettings { QuoteFontSize = 20 };
        File.WriteAllText(_filePath, System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        var store = new SettingsStore(_filePath);
        store.RenameMonitorKey("old", "new");

        Assert.True(store.Current.Monitors.ContainsKey("old"));
        Assert.Equal(20, store.Current.Monitors["new"].QuoteFontSize);
    }
}

public class MonitorSettingsServiceTests
{
    [Fact]
    public void ApplyAppearanceToAllMonitors_CopiesAppearance_ButNotEnabledLockOrBounds()
    {
        var settings = new Settings();
        settings.Monitors["source"] = new MonitorWidgetSettings
        {
            QuoteFontSize = 50,
            DetailsFontSize = 22,
            TextColor = "#111111",
            BackgroundColor = "#222222",
            BackgroundOpacity = 0.7,
            RotationIntervalMinutes = 10,
            Enabled = false,
            IsLocked = false,
            RelativeX = 999,
            Width = 1234
        };
        settings.Monitors["other"] = new MonitorWidgetSettings
        {
            Enabled = true,
            IsLocked = true,
            RelativeX = 5,
            Width = 700
        };

        MonitorSettingsService.ApplyAppearanceToAllMonitors(settings, "source");

        var other = settings.Monitors["other"];
        Assert.Equal(50, other.QuoteFontSize);
        Assert.Equal(22, other.DetailsFontSize);
        Assert.Equal("#111111", other.TextColor);
        Assert.Equal("#222222", other.BackgroundColor);
        Assert.Equal(0.7, other.BackgroundOpacity);
        Assert.Equal(10, other.RotationIntervalMinutes);

        // Not appearance - must be left alone.
        Assert.True(other.Enabled);
        Assert.True(other.IsLocked);
        Assert.Equal(5, other.RelativeX);
        Assert.Equal(700, other.Width);
    }

    [Fact]
    public void ApplyAppearanceToAllMonitors_DoesNothing_WhenSourceKeyMissing()
    {
        var settings = new Settings();
        settings.Monitors["other"] = new MonitorWidgetSettings { QuoteFontSize = 20 };

        MonitorSettingsService.ApplyAppearanceToAllMonitors(settings, "does-not-exist");

        Assert.Equal(20, settings.Monitors["other"].QuoteFontSize);
    }

    [Fact]
    public void CopyAppearanceToMonitor_CopiesAppearance_OnlyToTheChosenTarget()
    {
        var settings = new Settings();
        settings.Monitors["source"] = new MonitorWidgetSettings { QuoteFontSize = 60 };
        settings.Monitors["target"] = new MonitorWidgetSettings { QuoteFontSize = 10 };
        settings.Monitors["untouched"] = new MonitorWidgetSettings { QuoteFontSize = 10 };

        MonitorSettingsService.CopyAppearanceToMonitor(settings, "source", "target");

        Assert.Equal(60, settings.Monitors["target"].QuoteFontSize);
        Assert.Equal(10, settings.Monitors["untouched"].QuoteFontSize);
    }

    [Fact]
    public void CopyAppearanceToMonitor_DoesNothing_WhenSourceOrTargetMissing()
    {
        var settings = new Settings();
        settings.Monitors["a"] = new MonitorWidgetSettings { QuoteFontSize = 60 };

        MonitorSettingsService.CopyAppearanceToMonitor(settings, "a", "does-not-exist");
        MonitorSettingsService.CopyAppearanceToMonitor(settings, "does-not-exist", "a");

        Assert.Equal(60, settings.Monitors["a"].QuoteFontSize);
        Assert.Single(settings.Monitors);
    }

    [Fact]
    public void ResetAppearanceToDefaults_RestoresDefaultAppearance_ButKeepsBoundsAndEnabled()
    {
        var target = new MonitorWidgetSettings
        {
            QuoteFontSize = 99,
            DetailsFontSize = 99,
            TextColor = "#ABCDEF",
            BackgroundColor = "#ABCDEF",
            BackgroundOpacity = 0.99,
            RotationIntervalMinutes = 999,
            Enabled = false,
            IsLocked = false,
            RelativeX = 321,
            RelativeY = 321,
            Width = 1600,
            Height = 900
        };

        MonitorSettingsService.ResetAppearanceToDefaults(target);

        var defaults = new MonitorWidgetSettings();
        Assert.Equal(defaults.QuoteFontSize, target.QuoteFontSize);
        Assert.Equal(defaults.DetailsFontSize, target.DetailsFontSize);
        Assert.Equal(defaults.TextColor, target.TextColor);
        Assert.Equal(defaults.BackgroundColor, target.BackgroundColor);
        Assert.Equal(defaults.BackgroundOpacity, target.BackgroundOpacity);
        Assert.Equal(defaults.RotationIntervalMinutes, target.RotationIntervalMinutes);

        // Bounds/enabled/lock are not appearance - must be preserved.
        Assert.False(target.Enabled);
        Assert.False(target.IsLocked);
        Assert.Equal(321, target.RelativeX);
        Assert.Equal(1600, target.Width);
    }

    [Fact]
    public void ResetAllMonitorsToDefaults_ResetsEveryEntry()
    {
        var settings = new Settings();
        settings.Monitors["a"] = new MonitorWidgetSettings { QuoteFontSize = 10 };
        settings.Monitors["b"] = new MonitorWidgetSettings { QuoteFontSize = 20 };

        MonitorSettingsService.ResetAllMonitorsToDefaults(settings);

        var defaults = new MonitorWidgetSettings();
        Assert.Equal(defaults.QuoteFontSize, settings.Monitors["a"].QuoteFontSize);
        Assert.Equal(defaults.QuoteFontSize, settings.Monitors["b"].QuoteFontSize);
    }
}
