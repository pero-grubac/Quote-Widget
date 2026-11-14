using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace QuoteWidget.Services;

public static class NativeWindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint minimumSize, nint maximumSize);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    public const int WM_WINDOWPOSCHANGING = 0x0046;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_BOTTOM = new(1);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    /// <summary>Moves the window behind all other windows, without moving, resizing or activating it.</summary>
    public static void SendToBottom(IntPtr handle)
    {
        SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>
    /// For a WM_WINDOWPOSCHANGING message: whatever z-order change was requested (being shown,
    /// clicked, raised by Windows), turn it into "behind all other windows".
    /// </summary>
    public static void ForceBottomZOrder(IntPtr windowPosPointer)
    {
        var windowPos = Marshal.PtrToStructure<WINDOWPOS>(windowPosPointer);
        if ((windowPos.flags & SWP_NOZORDER) != 0)
        {
            return;
        }

        windowPos.hwndInsertAfter = HWND_BOTTOM;
        Marshal.StructureToPtr(windowPos, windowPosPointer, false);
    }

    /// <summary>
    /// Bounds (physical pixels) of the foreground window if it could be a fullscreen app (a game,
    /// a video, a browser in F11), or null. The desktop, maximized windows and this app's own
    /// windows are never counted.
    /// </summary>
    public static System.Drawing.Rectangle? GetForegroundAppBounds()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || IsZoomed(window))
        {
            return null;
        }

        GetWindowThreadProcessId(window, out var processId);
        if (processId == Environment.ProcessId)
        {
            return null;
        }

        var className = new StringBuilder(64);
        GetClassName(window, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW")
        {
            return null;
        }

        if (!GetWindowRect(window, out var rect))
        {
            return null;
        }

        return System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    /// <summary>Hands this process's unused memory pages back to Windows.</summary>
    public static void TrimWorkingSet()
    {
        using var process = Process.GetCurrentProcess();
        SetProcessWorkingSetSize(process.Handle, -1, -1);
    }

    /// <summary>
    /// The DPI scale factor (1.0 = 96 DPI) of the monitor that best matches the given physical
    /// pixel bounds (as reported by System.Windows.Forms.Screen). Used to convert those physical
    /// pixel coordinates into the device-independent units WPF expects for window placement.
    /// </summary>
    public static double GetDpiScaleForBounds(System.Drawing.Rectangle bounds)
    {
        var rect = new RECT { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom };
        var monitor = MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);

        if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
        {
            return dpiX / 96.0;
        }

        return 1.0;
    }

    /// <summary>
    /// Hides the window from the taskbar and Alt+Tab, and stops it from stealing keyboard focus.
    /// </summary>
    public static void MakeToolWindow(IntPtr handle)
    {
        var exStyle = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// Toggles click-through: when enabled, mouse input passes through the window to whatever is behind it.
    /// </summary>
    public static void SetClickThrough(IntPtr handle, bool clickThrough)
    {
        var exStyle = GetWindowLong(handle, GWL_EXSTYLE) | WS_EX_LAYERED;
        exStyle = clickThrough ? exStyle | WS_EX_TRANSPARENT : exStyle & ~WS_EX_TRANSPARENT;
        SetWindowLong(handle, GWL_EXSTYLE, exStyle);
    }
}
