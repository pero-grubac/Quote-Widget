using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using QuoteWidget.Models;
using QuoteWidget.Services;

namespace QuoteWidget.Views;

public partial class WidgetWindow : Window
{
    private readonly QuoteStore _quoteStore;
    private readonly SettingsStore _settingsStore;
    private readonly string _monitorKey;
    private readonly DispatcherTimer _rotationTimer = new();
    private Quote? _currentQuote;
    private IntPtr _hwnd;
    private MonitorInfo _monitor;

    // Tracked separately from Topmost so the z-order hook never races WPF updating that property.
    private bool _keepAtBottom;

    public event EventHandler? BoundsChanged;

    public WidgetWindow(QuoteStore quoteStore, SettingsStore settingsStore, string monitorKey, MonitorInfo monitor)
    {
        InitializeComponent();

        _quoteStore = quoteStore;
        _settingsStore = settingsStore;
        _monitorKey = monitorKey;
        _monitor = monitor;

        PositionOnMonitor();
        _keepAtBottom = !_settingsStore.Current.AlwaysOnTop;
        Topmost = _settingsStore.Current.AlwaysOnTop;

        ApplyAppearance();
        ShowNextQuote();

        _rotationTimer.Tick += (_, _) => ShowNextQuote();

        LocationChanged += (_, _) => RaiseBoundsChanged();
        Closing += WidgetWindow_Closing;
    }

    public MonitorWidgetSettings Settings => _settingsStore.Current.Monitors[_monitorKey];

    public MonitorInfo Monitor => _monitor;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        NativeWindowHelper.MakeToolWindow(_hwnd);
        ApplyLockState(Settings.IsLocked);

        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        if (_keepAtBottom)
        {
            NativeWindowHelper.SendToBottom(_hwnd);
        }
    }

    // A desktop widget must never pop up over the user's programs: unless "always on top" is on,
    // every z-order change (being shown again, Windows raising it) is turned into "stay at the bottom".
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeWindowHelper.WM_WINDOWPOSCHANGING && _keepAtBottom)
        {
            NativeWindowHelper.ForceBottomZOrder(lParam);
        }

        return IntPtr.Zero;
    }

    private void PositionOnMonitor()
    {
        var scale = NativeWindowHelper.GetDpiScaleForBounds(_monitor.Bounds);
        var settings = Settings;

        Width = settings.Width;
        Height = settings.Height;
        Left = _monitor.Bounds.Left / scale + settings.RelativeX;
        Top = _monitor.Bounds.Top / scale + settings.RelativeY;
    }

    /// <summary>
    /// Called when the monitor this widget is pinned to changes bounds (resolution/arrangement
    /// change), so its absolute position can be recomputed from its stored relative offset.
    /// </summary>
    public void UpdateMonitor(MonitorInfo monitor)
    {
        _monitor = monitor;
        PositionOnMonitor();
    }

    public void ApplyAppearance()
    {
        var settings = Settings;

        var textBrush = new SolidColorBrush(ParseColor(settings.TextColor, Colors.White));
        QuoteTextBlock.Foreground = textBrush;
        MovieTextBlock.Foreground = textBrush;
        CharacterTextBlock.Foreground = textBrush;

        QuoteTextBlock.FontSize = settings.QuoteFontSize;
        MovieTextBlock.FontSize = settings.DetailsFontSize;
        CharacterTextBlock.FontSize = settings.DetailsFontSize * 0.8;

        // Gap between the quote and the info block stays proportional to the quote font size,
        // instead of a fixed pixel value that looks wrong at other sizes.
        DetailsPanel.Margin = new Thickness(0, settings.QuoteFontSize * 0.5, 0, 0);

        var backgroundColor = ParseColor(settings.BackgroundColor, Colors.Black);
        backgroundColor.A = (byte)Math.Clamp(settings.BackgroundOpacity * 255, 0, 255);
        BackgroundBorder.Background = new SolidColorBrush(backgroundColor);

        RestartRotationTimer();
    }

    private static Color ParseColor(string value, Color fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value)!;
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    /// <summary>Restarts this widget's own timer at its monitor's configured interval. No-op while in shared-quote mode.</summary>
    public void RestartRotationTimer()
    {
        if (!_rotationTimer.IsEnabled)
        {
            return;
        }

        _rotationTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, Settings.RotationIntervalMinutes));
    }

    /// <summary>Enables or disables this widget's own rotation timer (disabled while all monitors share one quote).</summary>
    public void SetIndependentRotation(bool enabled)
    {
        if (enabled)
        {
            _rotationTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, Settings.RotationIntervalMinutes));
            _rotationTimer.Start();
        }
        else
        {
            _rotationTimer.Stop();
        }
    }

    public void ShowNextQuote()
    {
        var next = _quoteStore.GetRandom(_currentQuote);
        ShowQuote(next);
    }

    public void ShowQuote(Quote? quote)
    {
        if (quote == null)
        {
            QuoteTextBlock.Text = "No quotes available. Open the manager to add some.";
            MovieTextBlock.Text = string.Empty;
            CharacterTextBlock.Text = string.Empty;
            return;
        }

        _currentQuote = quote;

        QuoteTextBlock.Text = $"“{quote.Text}”";
        MovieTextBlock.Text = quote.Year.HasValue ? $"{quote.Movie} ({quote.Year})" : quote.Movie;
        CharacterTextBlock.Text = quote.Character ?? string.Empty;
        CharacterTextBlock.Visibility = string.IsNullOrWhiteSpace(quote.Character) ? Visibility.Collapsed : Visibility.Visible;

        var storyboard = (Storyboard)FindResource("FadeInStoryboard");
        storyboard.Begin();
    }

    public void SetAlwaysOnTop(bool alwaysOnTop)
    {
        _keepAtBottom = !alwaysOnTop;
        Topmost = alwaysOnTop;
        if (_keepAtBottom && _hwnd != IntPtr.Zero)
        {
            NativeWindowHelper.SendToBottom(_hwnd);
        }
    }

    public void SetLocked(bool locked)
    {
        Settings.IsLocked = locked;
        _settingsStore.Save();
        ApplyLockState(locked);
    }

    private void ApplyLockState(bool locked)
    {
        BackgroundBorder.IsHitTestVisible = !locked;
        UnlockOutline.Visibility = locked ? Visibility.Collapsed : Visibility.Visible;

        foreach (var thumb in new[] { ThumbTop, ThumbBottom, ThumbLeft, ThumbRight, ThumbTopLeft, ThumbTopRight, ThumbBottomLeft, ThumbBottomRight })
        {
            thumb.IsHitTestVisible = !locked;
        }

        if (_hwnd != IntPtr.Zero)
        {
            NativeWindowHelper.SetClickThrough(_hwnd, locked);
        }
    }

    public void SetBounds(double x, double y, double width, double height)
    {
        Left = x;
        Top = y;
        Width = Math.Max(MinWidth, width);
        Height = Math.Max(MinHeight, height);
        RaiseBoundsChanged();
    }

    /// <summary>Like <see cref="SetBounds"/>, but expressed relative to this widget's own monitor.</summary>
    public void SetRelativeBounds(double relativeX, double relativeY, double width, double height)
    {
        var scale = NativeWindowHelper.GetDpiScaleForBounds(_monitor.Bounds);
        SetBounds(_monitor.Bounds.Left / scale + relativeX, _monitor.Bounds.Top / scale + relativeY, width, height);
    }

    private void RaiseBoundsChanged()
    {
        SaveWindowBounds();
        BoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SaveWindowBounds()
    {
        var scale = NativeWindowHelper.GetDpiScaleForBounds(_monitor.Bounds);
        var settings = Settings;
        settings.RelativeX = Left - _monitor.Bounds.Left / scale;
        settings.RelativeY = Top - _monitor.Bounds.Top / scale;
        settings.Width = Width;
        settings.Height = Height;
        _settingsStore.Save();
    }

    /// <summary>Closes for real, bypassing the hide-instead-of-close behavior (used when its monitor disconnects or the app exits).</summary>
    public void ForceClose()
    {
        Closing -= WidgetWindow_Closing;
        _rotationTimer.Stop();
        Close();
    }

    private void WidgetWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!App.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void BackgroundBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Resize(double deltaX, double deltaY, bool fromLeft, bool fromTop)
    {
        var newWidth = Math.Max(MinWidth, Width + (fromLeft ? -deltaX : deltaX));
        var newHeight = Math.Max(MinHeight, Height + (fromTop ? -deltaY : deltaY));

        if (fromLeft)
        {
            Left += Width - newWidth;
        }
        if (fromTop)
        {
            Top += Height - newHeight;
        }

        Width = newWidth;
        Height = newHeight;
        RaiseBoundsChanged();
    }

    private void ThumbTop_DragDelta(object sender, DragDeltaEventArgs e) => Resize(0, e.VerticalChange, false, true);
    private void ThumbBottom_DragDelta(object sender, DragDeltaEventArgs e) => Resize(0, e.VerticalChange, false, false);
    private void ThumbLeft_DragDelta(object sender, DragDeltaEventArgs e) => Resize(e.HorizontalChange, 0, true, false);
    private void ThumbRight_DragDelta(object sender, DragDeltaEventArgs e) => Resize(e.HorizontalChange, 0, false, false);
    private void ThumbTopLeft_DragDelta(object sender, DragDeltaEventArgs e) => Resize(e.HorizontalChange, e.VerticalChange, true, true);
    private void ThumbTopRight_DragDelta(object sender, DragDeltaEventArgs e) => Resize(e.HorizontalChange, e.VerticalChange, false, true);
    private void ThumbBottomLeft_DragDelta(object sender, DragDeltaEventArgs e) => Resize(e.HorizontalChange, e.VerticalChange, true, false);
    private void ThumbBottomRight_DragDelta(object sender, DragDeltaEventArgs e) => Resize(e.HorizontalChange, e.VerticalChange, false, false);
}
