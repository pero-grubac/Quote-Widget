using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuoteWidget.Models;
using QuoteWidget.Services;
using Wpf.Ui.Controls;

namespace QuoteWidget.Views.Pages;

public partial class MonitorCard : UserControl
{
    private record CopyTarget(string Key, string DisplayName);


    private readonly string _deviceName;
    private readonly SettingsStore _settingsStore;
    private readonly WidgetManager _widgetManager;
    private readonly SnackbarPresenter _snackbarPresenter;
    private readonly Action _requestFullReload;
    private bool _isLoading = true;

    public MonitorCard(
        string deviceName,
        MonitorInfo? liveInfo,
        SettingsStore settingsStore,
        WidgetManager widgetManager,
        SnackbarPresenter snackbarPresenter,
        Action requestFullReload)
    {
        _deviceName = deviceName;
        _settingsStore = settingsStore;
        _widgetManager = widgetManager;
        _snackbarPresenter = snackbarPresenter;
        _requestFullReload = requestFullReload;

        InitializeComponent();

        TitleTextBlock.Text = liveInfo?.DisplayName ?? "Disconnected display";
        SubtitleTextBlock.Text = liveInfo != null
            ? $"{liveInfo.Bounds.Width} x {liveInfo.Bounds.Height} - {_deviceName}"
            : $"Not currently connected - {_deviceName}";

        LoadFromSettings();
        LoadCopyTargets();
        _isLoading = false;
    }

    private void LoadCopyTargets()
    {
        var connected = MonitorService.GetMonitors().ToDictionary(m => m.DeviceName, m => m.DisplayName);

        var targets = _settingsStore.Current.Monitors.Keys
            .Where(key => key != _deviceName)
            .Select(key => new CopyTarget(key, connected.TryGetValue(key, out var name) ? name : $"Disconnected ({key})"))
            .OrderBy(t => t.DisplayName)
            .ToList();

        CopyTargetComboBox.ItemsSource = targets;
        if (targets.Count > 0)
        {
            CopyTargetComboBox.SelectedIndex = 0;
        }
    }

    private MonitorWidgetSettings Settings => _settingsStore.Current.Monitors[_deviceName];

    private WidgetWindow? LiveWidget => _widgetManager.Widgets.TryGetValue(_deviceName, out var widget) ? widget : null;

    private void LoadFromSettings()
    {
        var settings = Settings;

        EnabledToggle.IsChecked = settings.Enabled;
        UnlockToggle.IsChecked = !settings.IsLocked;
        QuoteFontSizeSlider.Value = settings.QuoteFontSize;
        DetailsFontSizeSlider.Value = settings.DetailsFontSize;
        OpacitySlider.Value = settings.BackgroundOpacity * 100;
        IntervalNumberBox.Value = settings.RotationIntervalMinutes;
        XNumberBox.Value = settings.RelativeX;
        YNumberBox.Value = settings.RelativeY;
        WidthNumberBox.Value = settings.Width;
        HeightNumberBox.Value = settings.Height;
        UpdateColorSwatch(TextColorSwatch, settings.TextColor);
        UpdateColorSwatch(BackgroundColorSwatch, settings.BackgroundColor);
    }

    private static void UpdateColorSwatch(Border swatch, string hexColor)
    {
        try
        {
            swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)!);
        }
        catch (FormatException)
        {
            swatch.Background = Brushes.Transparent;
        }
    }

    private void Save()
    {
        _settingsStore.Save();
        LiveWidget?.ApplyAppearance();
    }

    private void EnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        Settings.Enabled = EnabledToggle.IsChecked == true;
        _settingsStore.Save();
        _widgetManager.Refresh();
    }

    private void UnlockToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var locked = UnlockToggle.IsChecked != true;
        Settings.IsLocked = locked;
        _settingsStore.Save();
        LiveWidget?.SetLocked(locked);
    }

    private void QuoteFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        QuoteFontSizeValueText.Text = ((int)e.NewValue).ToString();
        Settings.QuoteFontSize = e.NewValue;
        Save();
    }

    private void DetailsFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        DetailsFontSizeValueText.Text = ((int)e.NewValue).ToString();
        Settings.DetailsFontSize = e.NewValue;
        Save();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        OpacityValueText.Text = $"{(int)e.NewValue}%";
        Settings.BackgroundOpacity = e.NewValue / 100.0;
        Save();
    }

    private void IntervalNumberBox_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading || IntervalNumberBox.Value is not { } value) return;
        Settings.RotationIntervalMinutes = Math.Max(1, (int)value);
        _settingsStore.Save();
        LiveWidget?.RestartRotationTimer();
        _widgetManager.ApplySameQuoteMode();
    }

    private void BoundsNumberBox_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (XNumberBox.Value is not { } x || YNumberBox.Value is not { } y ||
            WidthNumberBox.Value is not { } width || HeightNumberBox.Value is not { } height)
        {
            return;
        }

        Settings.RelativeX = x;
        Settings.RelativeY = y;
        Settings.Width = width;
        Settings.Height = height;

        var widget = LiveWidget;
        if (widget != null)
        {
            widget.SetRelativeBounds(x, y, width, height);
        }
        else
        {
            _settingsStore.Save();
        }
    }

    private void ChooseTextColor_Click(object sender, RoutedEventArgs e)
    {
        var color = PickColor(Settings.TextColor);
        if (color == null) return;

        Settings.TextColor = color;
        UpdateColorSwatch(TextColorSwatch, color);
        Save();
    }

    private void ChooseBackgroundColor_Click(object sender, RoutedEventArgs e)
    {
        var color = PickColor(Settings.BackgroundColor);
        if (color == null) return;

        Settings.BackgroundColor = color;
        UpdateColorSwatch(BackgroundColorSwatch, color);
        Save();
    }

    private static string? PickColor(string currentHexColor)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };

        try
        {
            var current = (Color)ColorConverter.ConvertFromString(currentHexColor)!;
            dialog.Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
        }
        catch (FormatException)
        {
            // Keep the dialog's own default.
        }

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return null;
        }

        return $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
    }

    private void CopyToOne_Click(object sender, RoutedEventArgs e)
    {
        if (CopyTargetComboBox.SelectedItem is not CopyTarget target)
        {
            return;
        }

        MonitorSettingsService.CopyAppearanceToMonitor(_settingsStore.Current, _deviceName, target.Key);
        _settingsStore.Save();

        if (_widgetManager.Widgets.TryGetValue(target.Key, out var targetWidget))
        {
            targetWidget.ApplyAppearance();
        }

        var snackbar = new Snackbar(_snackbarPresenter)
        {
            Title = "Appearance copied",
            Content = $"Copied this monitor's appearance to {target.DisplayName}.",
            Appearance = ControlAppearance.Success,
            Timeout = TimeSpan.FromSeconds(3)
        };
        _snackbarPresenter.AddToQue(snackbar);

        _requestFullReload();
    }

    private void ApplyToAll_Click(object sender, RoutedEventArgs e)
    {
        MonitorSettingsService.ApplyAppearanceToAllMonitors(_settingsStore.Current, _deviceName);
        _settingsStore.Save();

        foreach (var widget in _widgetManager.Widgets.Values)
        {
            widget.ApplyAppearance();
        }

        var snackbar = new Snackbar(_snackbarPresenter)
        {
            Title = "Appearance applied",
            Content = "Copied this monitor's appearance to all other monitors.",
            Appearance = ControlAppearance.Success,
            Timeout = TimeSpan.FromSeconds(3)
        };
        _snackbarPresenter.AddToQue(snackbar);

        _requestFullReload();
    }

    private void ResetThis_Click(object sender, RoutedEventArgs e)
    {
        MonitorSettingsService.ResetAppearanceToDefaults(Settings);
        _settingsStore.Save();
        LiveWidget?.ApplyAppearance();
        LoadFromSettings();
    }
}
