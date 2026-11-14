using System.Runtime.InteropServices;
using QuoteWidget.Services;

namespace QuoteWidget.Tests;

public class NativeWindowHelperTests
{
    // WINDOWPOS layout: HWND hwnd, HWND hwndInsertAfter, int x, y, cx, cy, UINT flags.
    private static readonly int InsertAfterOffset = IntPtr.Size;
    private static readonly int FlagsOffset = IntPtr.Size * 2 + 16;
    private static readonly int WindowPosSize = IntPtr.Size * 2 + 20;

    private const uint SWP_NOZORDER = 0x0004;
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private static readonly IntPtr HWND_BOTTOM = new(1);

    private static IntPtr NewWindowPos(IntPtr insertAfter, uint flags)
    {
        var pointer = Marshal.AllocHGlobal(WindowPosSize);
        for (var i = 0; i < WindowPosSize; i++)
        {
            Marshal.WriteByte(pointer, i, 0);
        }

        Marshal.WriteIntPtr(pointer, InsertAfterOffset, insertAfter);
        Marshal.WriteInt32(pointer, FlagsOffset, (int)flags);
        return pointer;
    }

    [Fact]
    public void ForceBottomZOrder_TurnsARaiseIntoStayAtTheBottom()
    {
        var windowPos = NewWindowPos(HWND_TOP, flags: 0);
        try
        {
            NativeWindowHelper.ForceBottomZOrder(windowPos);

            Assert.Equal(HWND_BOTTOM, Marshal.ReadIntPtr(windowPos, InsertAfterOffset));
        }
        finally
        {
            Marshal.FreeHGlobal(windowPos);
        }
    }

    [Fact]
    public void ForceBottomZOrder_LeavesMovesThatDontTouchTheZOrderAlone()
    {
        var windowPos = NewWindowPos(HWND_TOP, SWP_NOZORDER);
        try
        {
            NativeWindowHelper.ForceBottomZOrder(windowPos);

            Assert.Equal(HWND_TOP, Marshal.ReadIntPtr(windowPos, InsertAfterOffset));
        }
        finally
        {
            Marshal.FreeHGlobal(windowPos);
        }
    }
}
