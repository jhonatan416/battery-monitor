using BatteryMonitor.Core.Icons;

namespace BatteryMonitor.Tests;

public class IconPixelsTests
{
    [Theory]
    [InlineData(82, "82")]
    [InlineData(8, "8")]
    [InlineData(0, "0")]
    [InlineData(99, "99")]
    [InlineData(100, "F")]
    [InlineData(null, "?")]
    public void Text_shown_for_each_level(int? percent, string expected)
    {
        Assert.Equal(expected, IconPixels.TextFor(percent));
    }

    [Theory]
    [InlineData(16, 2)]
    [InlineData(20, 2)]
    [InlineData(24, 3)]
    [InlineData(32, 4)]
    public void Digits_use_the_biggest_whole_pixel_scale_that_fits(int size, int scale)
    {
        Assert.Equal(scale, IconPixels.ScaleFor(size));
    }

    [Fact]
    public void Two_digits_at_16px_are_10_pixels_tall()
    {
        var grid = IconPixels.Build(82, 16);

        var (top, bottom, _, _) = Bounds(grid, IconPixel.Fill);
        Assert.Equal(10, bottom - top + 1); // 5-row glyph at scale 2 (was ~6 px with the old font)
    }

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    public void Every_level_fits_inside_the_icon_without_clipping(int size)
    {
        for (int percent = 0; percent <= 100; percent++)
        {
            var grid = IconPixels.Build(percent, size);

            Assert.Equal(size, grid.GetLength(0));
            Assert.Equal(size, grid.GetLength(1));
            int scale = IconPixels.ScaleFor(size);
            int expectedFill = IconPixels.TextFor(percent).Sum(IconPixels.LitCells) * scale * scale;
            Assert.Equal(expectedFill, Count(grid, IconPixel.Fill)); // nothing cut off at the edges
        }
    }

    [Fact]
    public void Digits_are_centered()
    {
        var grid = IconPixels.Build(82, 16);

        var (top, bottom, left, right) = Bounds(grid, IconPixel.Outline);
        Assert.InRange(left - (15 - right), -1, 1);
        Assert.InRange(top - (15 - bottom), -1, 1);
    }

    [Fact]
    public void Every_fill_pixel_is_surrounded_by_fill_or_outline()
    {
        // The dark outline is what keeps the digits readable on light and dark taskbars.
        var grid = IconPixels.Build(45, 16);

        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                if (grid[y, x] != IconPixel.Fill) continue;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        Assert.NotEqual(IconPixel.Empty, grid[y + dy, x + dx]);
            }
    }

    private static int Count(IconPixel[,] grid, IconPixel kind)
    {
        int n = 0;
        foreach (var p in grid) if (p == kind) n++;
        return n;
    }

    /// <summary>Bounding box of every pixel that is at least <paramref name="kind"/> (Fill counts as Outline too).</summary>
    private static (int Top, int Bottom, int Left, int Right) Bounds(IconPixel[,] grid, IconPixel kind)
    {
        int top = int.MaxValue, bottom = -1, left = int.MaxValue, right = -1;
        for (int y = 0; y < grid.GetLength(0); y++)
            for (int x = 0; x < grid.GetLength(1); x++)
            {
                if (grid[y, x] < kind) continue;
                top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                left = Math.Min(left, x); right = Math.Max(right, x);
            }
        return (top, bottom, left, right);
    }
}
