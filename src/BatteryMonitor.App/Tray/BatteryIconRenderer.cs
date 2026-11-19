using System.Runtime.InteropServices;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Icons;

namespace BatteryMonitor.App.Tray;

/// <summary>
/// Draws the two tray frames of a device: the percent as a crisp pixel font, and the device picture.
/// Layout lives in <see cref="IconPixels"/>; both frames are colored by level, with a dark outline
/// so they read on light and dark taskbars.
/// </summary>
internal static class BatteryIconRenderer
{
    private static readonly Color OutlineColor = Color.FromArgb(230, 16, 16, 16);

    private static int TraySize => SystemInformation.SmallIconSize.Width; // 16 at 100 % scaling

    public static Icon Render(int? percent, bool charging, bool stale) => Render(percent, charging, stale, TraySize);

    public static Icon Render(int? percent, bool charging, bool stale, int size) =>
        Paint(IconPixels.Build(percent, size), LevelColor(percent, charging, stale));

    public static Icon RenderPicture(DeviceKind kind, int? percent, bool charging, bool stale) =>
        RenderPicture(kind, percent, charging, stale, TraySize);

    public static Icon RenderPicture(DeviceKind kind, int? percent, bool charging, bool stale, int size) =>
        Paint(IconPixels.BuildPicture(kind, size), LevelColor(percent, charging, stale));

    private static Color LevelColor(int? percent, bool charging, bool stale)
    {
        Color fill = percent switch
        {
            null => Color.FromArgb(170, 170, 170),
            _ when charging => Color.FromArgb(0, 190, 255),
            <= 20 => Color.FromArgb(255, 70, 50),
            <= 50 => Color.FromArgb(255, 210, 0),
            _ => Color.FromArgb(60, 220, 60),
        };
        return stale ? Color.FromArgb(140, fill) : fill; // remembered value: dimmed
    }

    private static Icon Paint(IconPixel[,] grid, Color fill)
    {
        int size = grid.GetLength(0);
        using var bmp = new Bitmap(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (grid[y, x] == IconPixel.Fill) bmp.SetPixel(x, y, fill);
                else if (grid[y, x] == IconPixel.Outline) bmp.SetPixel(x, y, OutlineColor);
            }

        IntPtr hIcon = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
        finally { DestroyIcon(hIcon); }
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
