using System.Windows;
using System.Windows.Controls;
using QuoteWidget.Services;

namespace QuoteWidget.Views.Pages;

public partial class GeneralPage : UserControl
{
    private readonly SettingsStore _settingsStore;
    private readonly WidgetManager _widgetManager;
    private bool _isLoading = true;

    public GeneralPage(SettingsStore settingsStore, WidgetManager widgetManager)
    {
        InitializeComponent();

        _settingsStore = settingsStore;
        _widgetManager = widgetManager;

        var settings = _settingsStore.Current;
        AlwaysOnTopToggle.IsChecked = settings.AlwaysOnTop;
        HideWhenFullscreenToggle.IsChecked = settings.HideWidgetsWhenFullscreen;
        StartWithWindowsToggle.IsChecked = settings.StartWithWindows;

        _isLoading = false;
    }

    private void AlwaysOnTopToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        var enabled = AlwaysOnTopToggle.IsChecked == true;
        _settingsStore.Current.AlwaysOnTop = enabled;
        _settingsStore.Save();
        _widgetManager.SetAllAlwaysOnTop(enabled);
    }

    private void HideWhenFullscreenToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settingsStore.Current.HideWidgetsWhenFullscreen = HideWhenFullscreenToggle.IsChecked == true;
        _settingsStore.Save();
        _widgetManager.UpdateFullscreenVisibility();
    }

    private void StartWithWindowsToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        var enabled = StartWithWindowsToggle.IsChecked == true;
        _settingsStore.Current.StartWithWindows = enabled;
        _settingsStore.Save();
        AutostartService.SetEnabled(enabled);
    }
}
