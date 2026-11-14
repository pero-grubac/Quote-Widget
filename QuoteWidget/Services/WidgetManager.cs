using System.Windows.Threading;
using Microsoft.Win32;
using QuoteWidget.Models;
using QuoteWidget.Views;

namespace QuoteWidget.Services;

/// <summary>
/// Owns one <see cref="WidgetWindow"/> per connected, enabled monitor. Creates/destroys widgets
/// as monitors are plugged in or out, and coordinates shared-vs-independent quote rotation.
/// </summary>
public class WidgetManager
{
    private readonly QuoteStore _quoteStore;
    private readonly SettingsStore _settingsStore;
    private readonly Dictionary<string, WidgetWindow> _widgets = new();
    private readonly DispatcherTimer _sharedRotationTimer = new();
    private readonly DispatcherTimer _fullscreenTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };

    // Widgets hidden because a fullscreen app covers their monitor, so only those get shown again
    // afterwards (not ones the user hid or disabled).
    private readonly HashSet<string> _hiddenForFullscreen = new();
    private Quote? _lastSharedQuote;

    public WidgetManager(QuoteStore quoteStore, SettingsStore settingsStore)
    {
        _quoteStore = quoteStore;
        _settingsStore = settingsStore;
        _sharedRotationTimer.Tick += (_, _) => RotateSharedQuote();
        _fullscreenTimer.Tick += (_, _) => UpdateFullscreenVisibility();
    }

    public IReadOnlyDictionary<string, WidgetWindow> Widgets => _widgets;

    public void Start()
    {
        MigrateLegacyEntryToPrimaryMonitor();
        SystemEvents.DisplaySettingsChanged += (_, _) => Refresh();
        Refresh();
        _fullscreenTimer.Start();
    }

    public void UpdateFullscreenVisibility()
    {
        var appBounds = _settingsStore.Current.HideWidgetsWhenFullscreen
            ? NativeWindowHelper.GetForegroundAppBounds()
            : null;

        foreach (var (key, widget) in _widgets)
        {
            var covered = appBounds is { } bounds && Covers(bounds, widget.Monitor.Bounds);

            if (covered && widget.IsVisible)
            {
                widget.Hide();
                _hiddenForFullscreen.Add(key);
            }
            else if (!covered && _hiddenForFullscreen.Remove(key) && widget.Settings.Enabled)
            {
                widget.Show();
            }
        }
    }

    /// <summary>True if <paramref name="outer"/> fully covers <paramref name="inner"/>.</summary>
    public static bool Covers(System.Drawing.Rectangle outer, System.Drawing.Rectangle inner)
    {
        return outer.Left <= inner.Left && outer.Top <= inner.Top
            && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom;
    }

    private void MigrateLegacyEntryToPrimaryMonitor()
    {
        if (!_settingsStore.Current.Monitors.ContainsKey(SettingsStore.MigratedPrimaryKey))
        {
            return;
        }

        var primary = MonitorService.GetPrimary() ?? MonitorService.GetMonitors().FirstOrDefault();
        if (primary == null)
        {
            return;
        }

        var migrated = _settingsStore.Current.Monitors[SettingsStore.MigratedPrimaryKey];
        var scale = NativeWindowHelper.GetDpiScaleForBounds(primary.Bounds);

        // The pre-multi-monitor values were absolute screen coordinates; rebase them to be
        // relative to the primary monitor's own origin.
        migrated.RelativeX -= primary.Bounds.Left / scale;
        migrated.RelativeY -= primary.Bounds.Top / scale;

        _settingsStore.RenameMonitorKey(SettingsStore.MigratedPrimaryKey, primary.DeviceName);
    }

    public void Refresh()
    {
        var monitors = MonitorService.GetMonitors();
        var connectedKeys = new HashSet<string>();

        foreach (var monitor in monitors)
        {
            connectedKeys.Add(monitor.DeviceName);

            if (!_settingsStore.Current.Monitors.TryGetValue(monitor.DeviceName, out var monitorSettings))
            {
                monitorSettings = new MonitorWidgetSettings();
                _settingsStore.Current.Monitors[monitor.DeviceName] = monitorSettings;
                _settingsStore.Save();
            }

            if (_widgets.TryGetValue(monitor.DeviceName, out var existingWidget))
            {
                existingWidget.UpdateMonitor(monitor);
                if (!monitorSettings.Enabled)
                {
                    existingWidget.Hide();
                    _hiddenForFullscreen.Remove(monitor.DeviceName);
                }
                else if (!_hiddenForFullscreen.Contains(monitor.DeviceName))
                {
                    existingWidget.Show();
                }
                continue;
            }

            if (!monitorSettings.Enabled)
            {
                continue;
            }

            var widget = new WidgetWindow(_quoteStore, _settingsStore, monitor.DeviceName, monitor);
            widget.Show();
            _widgets[monitor.DeviceName] = widget;
        }

        foreach (var key in _widgets.Keys.Except(connectedKeys).ToList())
        {
            _widgets[key].ForceClose();
            _widgets.Remove(key);
            _hiddenForFullscreen.Remove(key);
        }

        ApplySameQuoteMode();
    }

    public void ApplySameQuoteMode()
    {
        var sameQuote = _settingsStore.Current.ShowSameQuoteOnAllMonitors;

        foreach (var widget in _widgets.Values)
        {
            widget.SetIndependentRotation(!sameQuote);
        }

        if (sameQuote)
        {
            var minutes = _widgets.Values
                .Select(w => w.Settings.RotationIntervalMinutes)
                .DefaultIfEmpty(30)
                .Min();
            _sharedRotationTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
            RotateSharedQuote();
            _sharedRotationTimer.Start();
        }
        else
        {
            _sharedRotationTimer.Stop();
        }
    }

    private void RotateSharedQuote()
    {
        var next = _quoteStore.GetRandom(_lastSharedQuote);
        if (next == null)
        {
            return;
        }

        _lastSharedQuote = next;
        foreach (var widget in _widgets.Values)
        {
            widget.ShowQuote(next);
        }
    }

    public void NextQuote()
    {
        if (_settingsStore.Current.ShowSameQuoteOnAllMonitors)
        {
            RotateSharedQuote();
        }
        else
        {
            foreach (var widget in _widgets.Values)
            {
                widget.ShowNextQuote();
            }
        }
    }

    public void SetAllAlwaysOnTop(bool alwaysOnTop)
    {
        foreach (var widget in _widgets.Values)
        {
            widget.SetAlwaysOnTop(alwaysOnTop);
        }
    }

    public void CloseAll()
    {
        _fullscreenTimer.Stop();
        foreach (var widget in _widgets.Values)
        {
            widget.ForceClose();
        }
        _widgets.Clear();
    }
}
