using System.IO;
using System.Text.Json;
using QuoteWidget.Models;

namespace QuoteWidget.Services;

public class SettingsStore
{
    /// <summary>
    /// Key a migrated single-widget settings file is stored under, until
    /// <see cref="RenameMonitorKey"/> moves it to the real primary monitor's device name.
    /// </summary>
    public const string MigratedPrimaryKey = "__migrated_primary__";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public SettingsStore(string filePath)
    {
        _filePath = filePath;
        Current = Load();
    }

    public Settings Current { get; private set; }

    public bool IsFirstRun { get; private set; }

    public static string GetDefaultFilePath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuoteWidget");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "settings.json");
    }

    private Settings Load()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty(nameof(Settings.Monitors), out _))
                {
                    var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
                else
                {
                    // Pre-multi-monitor file: migrate the single widget's settings into the new shape.
                    return MigrateLegacySettings(root);
                }
            }
            catch (JsonException)
            {
                // Fall through to defaults.
            }
        }

        IsFirstRun = true;
        var defaults = new Settings();
        WriteToDisk(defaults);
        return defaults;
    }

    private static Settings MigrateLegacySettings(JsonElement root)
    {
        var monitor = new MonitorWidgetSettings();

        if (TryGetDouble(root, "QuoteFontSize", out var quoteFontSize)) monitor.QuoteFontSize = quoteFontSize;
        if (TryGetDouble(root, "DetailsFontSize", out var detailsFontSize)) monitor.DetailsFontSize = detailsFontSize;
        if (TryGetString(root, "TextColor", out var textColor)) monitor.TextColor = textColor;
        if (TryGetString(root, "BackgroundColor", out var backgroundColor)) monitor.BackgroundColor = backgroundColor;
        if (TryGetDouble(root, "BackgroundOpacity", out var backgroundOpacity)) monitor.BackgroundOpacity = backgroundOpacity;
        if (TryGetInt(root, "RotationIntervalMinutes", out var interval)) monitor.RotationIntervalMinutes = interval;
        if (TryGetBool(root, "IsLocked", out var isLocked)) monitor.IsLocked = isLocked;
        if (TryGetDouble(root, "WindowLeft", out var left)) monitor.RelativeX = left;
        if (TryGetDouble(root, "WindowTop", out var top)) monitor.RelativeY = top;
        if (TryGetDouble(root, "WindowWidth", out var width)) monitor.Width = width;
        if (TryGetDouble(root, "WindowHeight", out var height)) monitor.Height = height;

        var settings = new Settings();
        settings.Monitors[MigratedPrimaryKey] = monitor;

        if (TryGetBool(root, "AlwaysOnTop", out var alwaysOnTop)) settings.AlwaysOnTop = alwaysOnTop;
        if (TryGetBool(root, "StartWithWindows", out var startWithWindows)) settings.StartWithWindows = startWithWindows;

        return settings;
    }

    private static bool TryGetDouble(JsonElement root, string propertyName, out double value)
    {
        if (root.TryGetProperty(propertyName, out var element) && element.TryGetDouble(out var parsed))
        {
            value = parsed;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetInt(JsonElement root, string propertyName, out int value)
    {
        if (root.TryGetProperty(propertyName, out var element) && element.TryGetInt32(out var parsed))
        {
            value = parsed;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        if (root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString()!;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static bool TryGetBool(JsonElement root, string propertyName, out bool value)
    {
        if (root.TryGetProperty(propertyName, out var element) &&
            (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
        {
            value = element.GetBoolean();
            return true;
        }
        value = false;
        return false;
    }

    /// <summary>
    /// Moves a monitor's settings entry to a new key (e.g. when the migrated entry needs to be
    /// attached to the real primary monitor's device name once monitors are enumerated).
    /// Does nothing if the old key is missing or the new key is already taken.
    /// </summary>
    public void RenameMonitorKey(string oldKey, string newKey)
    {
        if (oldKey == newKey)
        {
            return;
        }

        if (!Current.Monitors.TryGetValue(oldKey, out var settings) || Current.Monitors.ContainsKey(newKey))
        {
            return;
        }

        Current.Monitors.Remove(oldKey);
        Current.Monitors[newKey] = settings;
        Save();
    }

    public void Save() => WriteToDisk(Current);

    private void WriteToDisk(Settings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
