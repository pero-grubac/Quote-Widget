using System.Windows;
using System.Windows.Controls;
using QuoteWidget.Services;
using Wpf.Ui.Controls;

namespace QuoteWidget.Views.Pages;

public partial class WidgetsPage : UserControl
{
    private readonly SettingsStore _settingsStore;
    private readonly WidgetManager _widgetManager;
    private readonly SnackbarPresenter _snackbarPresenter;
    private bool _isLoading = true;

    public WidgetsPage(SettingsStore settingsStore, WidgetManager widgetManager, SnackbarPresenter snackbarPresenter)
    {
        InitializeComponent();

        _settingsStore = settingsStore;
        _widgetManager = widgetManager;
        _snackbarPresenter = snackbarPresenter;

        Reload();
    }

    public void Reload()
    {
        _isLoading = true;
        SameQuoteToggle.IsChecked = _settingsStore.Current.ShowSameQuoteOnAllMonitors;
        _isLoading = false;

        var connected = MonitorService.GetMonitors().ToDictionary(m => m.DeviceName);

        CardsPanel.Children.Clear();
        foreach (var deviceName in _settingsStore.Current.Monitors.Keys.OrderBy(k => k))
        {
            connected.TryGetValue(deviceName, out var liveInfo);
            var card = new MonitorCard(deviceName, liveInfo, _settingsStore, _widgetManager, _snackbarPresenter, Reload);
            CardsPanel.Children.Add(card);
        }
    }

    private void SameQuoteToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settingsStore.Current.ShowSameQuoteOnAllMonitors = SameQuoteToggle.IsChecked == true;
        _settingsStore.Save();
        _widgetManager.ApplySameQuoteMode();
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        MonitorSettingsService.ResetAllMonitorsToDefaults(_settingsStore.Current);
        _settingsStore.Save();

        foreach (var widget in _widgetManager.Widgets.Values)
        {
            widget.ApplyAppearance();
        }

        var snackbar = new Snackbar(_snackbarPresenter)
        {
            Title = "Reset complete",
            Content = "All monitors reset to default appearance.",
            Appearance = ControlAppearance.Success,
            Timeout = TimeSpan.FromSeconds(3)
        };
        _snackbarPresenter.AddToQue(snackbar);

        Reload();
    }
}
