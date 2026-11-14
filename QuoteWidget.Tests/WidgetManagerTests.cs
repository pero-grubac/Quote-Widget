using System.Drawing;
using QuoteWidget.Services;

namespace QuoteWidget.Tests;

public class WidgetManagerTests
{
    private static readonly Rectangle Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void Covers_IsTrue_ForAWindowExactlyTheSizeOfTheMonitor()
    {
        Assert.True(WidgetManager.Covers(new Rectangle(0, 0, 1920, 1080), Monitor));
    }

    [Fact]
    public void Covers_IsTrue_ForAWindowSlightlyLargerThanTheMonitor()
    {
        Assert.True(WidgetManager.Covers(Rectangle.FromLTRB(-8, -8, 1928, 1088), Monitor));
    }

    [Fact]
    public void Covers_IsFalse_ForAWindowThatLeavesTheTaskbarVisible()
    {
        Assert.False(WidgetManager.Covers(new Rectangle(0, 0, 1920, 1040), Monitor));
    }

    [Fact]
    public void Covers_IsFalse_ForAFullscreenWindowOnAnotherMonitor()
    {
        Assert.False(WidgetManager.Covers(new Rectangle(1920, 0, 2560, 1440), Monitor));
    }
}
