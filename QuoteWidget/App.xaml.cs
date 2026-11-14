using System.Runtime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using H.NotifyIcon;
using QuoteWidget.Services;
using QuoteWidget.Views;
using Wpf.Ui.Appearance;

namespace QuoteWidget;

public partial class App : Application
{
    private const string MutexName = "QuoteWidget-SingleInstance-Mutex";

    private static App Instance => (App)Application.Current;

    private Mutex? _singleInstanceMutex;
    private TaskbarIcon? _trayIcon;
    private ManagerWindow? _managerWindow;
    private WidgetManager? _widgetManager;

    private QuoteStore _quoteStore = null!;
    private SettingsStore _settingsStore = null!;

    public static bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Quote Widget is already running.", "Quote Widget", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // The UI is almost entirely static, so a GPU device (and the memory its driver commits)
        // buys nothing here.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        ApplicationThemeManager.Apply(ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica, true);

        _quoteStore = new QuoteStore(QuoteStore.GetDefaultFilePath());
        _settingsStore = new SettingsStore(SettingsStore.GetDefaultFilePath());

        _widgetManager = new WidgetManager(_quoteStore, _settingsStore);
        _widgetManager.Start();
        _widgetManager.SetAllAlwaysOnTop(_settingsStore.Current.AlwaysOnTop);

        SetUpTrayIcon();
        ShowManagerWindow();
    }

    private ManagerWindow CreateManagerWindow()
    {
        var window = new ManagerWindow(_quoteStore, _settingsStore, _widgetManager!);
        window.Closed += (_, _) => OnManagerClosed();
        return window;
    }

    // The Manager is by far the heaviest part of the app, so it is destroyed when closed (and rebuilt
    // when reopened) instead of just hidden; only the light widget windows keep running.
    private void OnManagerClosed()
    {
        _managerWindow = null;
        if (IsExiting)
        {
            return;
        }

        ShowTrayHintOnce();
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, ReleaseMemory);
    }

    private static void ReleaseMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        NativeWindowHelper.TrimWorkingSet();
    }

    private void ShowTrayHintOnce()
    {
        if (_settingsStore.Current.HasShownTrayHint || _trayIcon == null)
        {
            return;
        }

        _settingsStore.Current.HasShownTrayHint = true;
        _settingsStore.Save();
        _trayIcon.ShowNotification(
            "Quote Widget is still running",
            "Your widgets stay on the desktop. Double-click this tray icon to reopen the manager, or right-click it and choose Exit to quit.");
    }

    private void SetUpTrayIcon()
    {
        var contextMenu = new ContextMenu();

        var showHideItem = new MenuItem { Header = "Show/hide widgets" };
        showHideItem.Click += (_, _) => ToggleWidgetsVisibility();
        contextMenu.Items.Add(showHideItem);

        var nextQuoteItem = new MenuItem { Header = "Next quote" };
        nextQuoteItem.Click += (_, _) => _widgetManager?.NextQuote();
        contextMenu.Items.Add(nextQuoteItem);

        var manageItem = new MenuItem { Header = "Open manager" };
        manageItem.Click += (_, _) => ShowManagerWindow();
        contextMenu.Items.Add(manageItem);

        contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        var icon = Environment.ProcessPath is { } processPath
            ? System.Drawing.Icon.ExtractAssociatedIcon(processPath)
            : null;

        _trayIcon = new TaskbarIcon
        {
            Icon = icon ?? System.Drawing.SystemIcons.Application,
            ToolTipText = "Quote Widget",
            ContextMenu = contextMenu
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowManagerWindow();

        // Created purely in code (not in a XAML visual tree), so the native tray icon is never
        // actually registered unless this is called explicitly.
        _trayIcon.ForceCreate();
    }

    private void ToggleWidgetsVisibility()
    {
        if (_widgetManager == null)
        {
            return;
        }

        var anyVisible = _widgetManager.Widgets.Values.Any(w => w.IsVisible);
        foreach (var widget in _widgetManager.Widgets.Values)
        {
            if (anyVisible)
            {
                widget.Hide();
            }
            else
            {
                widget.Show();
            }
        }
    }

    public static void ShowManagerWindow()
    {
        var app = Instance;
        app._managerWindow ??= app.CreateManagerWindow();
        app._managerWindow.Show();
        app._managerWindow.WindowState = WindowState.Normal;
        app._managerWindow.Activate();
    }

    public static void ExitApplication()
    {
        IsExiting = true;
        Instance._widgetManager?.CloseAll();
        Instance._managerWindow?.Close();
        Instance._trayIcon?.Dispose();
        Instance.Shutdown();
    }
}
