using BatteryMonitor.Core.Domain;

namespace BatteryMonitor.Core.Icons;

/// <summary>What each pixel of a tray icon is: nothing, the dark outline, or the colored digit.</summary>
public enum IconPixel : byte { Empty = 0, Outline = 1, Fill = 2 }

/// <summary>
/// Lays out the battery percent as a crisp pixel font for a square tray icon.
/// Each glyph is a 3x5 grid scaled by a whole number, so every pixel is fully on or off
/// (no blurry anti-aliasing at 16 px), with a 1 px outline for contrast on any taskbar color.
/// </summary>
public static class IconPixels
{
    private const int GlyphWidth = 3, GlyphHeight = 5;

    // '#' = lit cell. One string per row, top to bottom.
    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['0'] = ["###", "#.#", "#.#", "#.#", "###"],
        ['1'] = [".#.", "##.", ".#.", ".#.", "###"],
        ['2'] = ["###", "..#", "###", "#..", "###"],
        ['3'] = ["###", "..#", "###", "..#", "###"],
        ['4'] = ["#.#", "#.#", "###", "..#", "..#"],
        ['5'] = ["###", "#..", "###", "..#", "###"],
        ['6'] = ["###", "#..", "###", "#.#", "###"],
        ['7'] = ["###", "..#", ".#.", ".#.", ".#."],
        ['8'] = ["###", "#.#", "###", "#.#", "###"],
        ['9'] = ["###", "#.#", "###", "..#", "###"],
        ['F'] = ["###", "#..", "##.", "#..", "#.."],
        ['?'] = ["###", "..#", ".##", "...", ".#."],
    };

    /// <summary>"0".."99", "F" for full (three digits do not fit at 16 px), "?" when unknown.</summary>
    public static string TextFor(int? percent) => percent switch
    {
        null => "?",
        >= 100 => "F",
        < 0 => "0",
        _ => percent.Value.ToString(),
    };

    /// <summary>Biggest whole scale where two digits, their gap and the outline fit: 16 px → 2, 24 px → 3.</summary>
    public static int ScaleFor(int size) => Math.Max(1, (size - 2) / (2 * GlyphWidth + 1));

    /// <summary>Number of lit cells in a glyph (before scaling).</summary>
    public static int LitCells(char c) => Glyphs[c].Sum(row => row.Count(ch => ch == '#'));

    public static IconPixel[,] Build(int? percent, int size)
    {
        string text = TextFor(percent);
        int scale = ScaleFor(size);
        int width = (text.Length * (GlyphWidth + 1) - 1) * scale;
        int left = (size - width) / 2, top = (size - GlyphHeight * scale) / 2;

        var grid = new IconPixel[size, size];
        for (int i = 0; i < text.Length; i++)
            DrawMask(grid, Glyphs[text[i]], left + i * (GlyphWidth + 1) * scale, top, scale);
        AddOutline(grid);
        return grid;
    }

    /// <summary>The device picture for a kind, centered, scaled by whole steps of 16 px (32 px → 2x).</summary>
    public static IconPixel[,] BuildPicture(DeviceKind kind, int size)
    {
        string[] art = DevicePictures.For(kind);
        int scale = Math.Max(1, size / 16);
        int width = art[0].Length * scale, height = art.Length * scale;

        var grid = new IconPixel[size, size];
        DrawMask(grid, art, (size - width) / 2, (size - height) / 2, scale);
        AddOutline(grid);
        return grid;
    }

    private static void DrawMask(IconPixel[,] grid, string[] mask, int x0, int y0, int scale)
    {
        for (int row = 0; row < mask.Length; row++)
            for (int col = 0; col < mask[row].Length; col++)
            {
                if (mask[row][col] != '#') continue;
                for (int dy = 0; dy < scale; dy++)
                    for (int dx = 0; dx < scale; dx++)
                        grid[y0 + row * scale + dy, x0 + col * scale + dx] = IconPixel.Fill;
            }
    }

    private static void AddOutline(IconPixel[,] grid)
    {
        int size = grid.GetLength(0);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (grid[y, x] != IconPixel.Empty || !TouchesFill(grid, x, y)) continue;
                grid[y, x] = IconPixel.Outline;
            }
    }

    private static bool TouchesFill(IconPixel[,] grid, int x, int y)
    {
        int size = grid.GetLength(0);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < size && ny < size && grid[ny, nx] == IconPixel.Fill) return true;
            }
        return false;
    }
}
