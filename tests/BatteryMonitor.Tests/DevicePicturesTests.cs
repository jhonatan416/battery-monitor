using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Icons;

namespace BatteryMonitor.Tests;

public class DevicePicturesTests
{
    public static TheoryData<DeviceKind, int> KindsAndSizes()
    {
        var data = new TheoryData<DeviceKind, int>();
        foreach (var kind in Enum.GetValues<DeviceKind>())
            foreach (int size in new[] { 16, 20, 24, 32 })
                data.Add(kind, size);
        return data;
    }

    [Theory]
    [MemberData(nameof(KindsAndSizes))]
    public void Every_device_kind_has_a_picture_that_fits_with_its_outline(DeviceKind kind, int size)
    {
        var grid = IconPixels.BuildPicture(kind, size);

        Assert.Equal(size, grid.GetLength(0));
        int scale = Math.Max(1, size / 16);
        Assert.Equal(DevicePictures.LitCells(kind) * scale * scale, Count(grid, IconPixel.Fill)); // nothing clipped
        Assert.True(Count(grid, IconPixel.Outline) > 0);
        for (int i = 0; i < size; i++) // the outline keeps a free border: nothing touches the edge with fill
        {
            Assert.NotEqual(IconPixel.Fill, grid[0, i]);
            Assert.NotEqual(IconPixel.Fill, grid[size - 1, i]);
            Assert.NotEqual(IconPixel.Fill, grid[i, 0]);
            Assert.NotEqual(IconPixel.Fill, grid[i, size - 1]);
        }
    }

    [Fact]
    public void Each_device_kind_looks_different()
    {
        var pictures = Enum.GetValues<DeviceKind>()
            .Select(k => string.Concat(IconPixels.BuildPicture(k, 16).Cast<IconPixel>().Select(p => (char)('0' + (int)p))))
            .ToList();

        Assert.Equal(pictures.Count, pictures.Distinct().Count());
    }

    private static int Count(IconPixel[,] grid, IconPixel kind)
    {
        int n = 0;
        foreach (var p in grid) if (p == kind) n++;
        return n;
    }
}
