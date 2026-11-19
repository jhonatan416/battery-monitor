using BatteryMonitor.Core.Domain;

namespace BatteryMonitor.Core.Icons;

/// <summary>
/// Pixel-art pictures that identify each device kind in the tray, drawn on a 16 px grid
/// (at most 14 x 14, so the 1 px outline still fits). '#' = lit pixel.
/// </summary>
public static class DevicePictures
{
    private static readonly Dictionary<DeviceKind, string[]> Art = new()
    {
        [DeviceKind.Mouse] =
        [
            "..#####..",
            ".#..#..#.",
            "#...#...#",
            "#...#...#",
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            ".#.....#.",
            "..#####..",
        ],
        [DeviceKind.Gamepad] =
        [
            "..###....###..",
            ".#...####...#.",
            "#..#......#..#",
            "#.###....#.#.#",
            "#..#......#..#",
            "#............#",
            "#....####....#",
            ".#..#....#..#.",
            "..##......##..",
        ],
        [DeviceKind.Headset] =
        [
            "...######...",
            "..#......#..",
            ".#........#.",
            "#..........#",
            "#..........#",
            "#..........#",
            "##........##",
            "###......###",
            "###......###",
            "###......###",
            "##........##",
        ],
        [DeviceKind.Keyboard] =
        [
            "##############",
            "#............#",
            "#.#.#.#.#.#..#",
            "#..#.#.#.#.#.#",
            "#.#.#.#.#.#..#",
            "#...######...#",
            "#............#",
            "##############",
        ],
        [DeviceKind.Other] = // generic battery
        [
            "###########.",
            "#.........#.",
            "#.........##",
            "#.........##",
            "#.........#.",
            "###########.",
        ],
    };

    public static string[] For(DeviceKind kind) => Art.TryGetValue(kind, out var art) ? art : Art[DeviceKind.Other];

    /// <summary>Number of lit pixels in a picture (before scaling).</summary>
    public static int LitCells(DeviceKind kind) => For(kind).Sum(row => row.Count(ch => ch == '#'));
}
