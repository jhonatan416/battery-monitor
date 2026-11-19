namespace BatteryMonitor.Core.Protocols.Gamepads;

/// <summary>
/// Valve Steam Controllers. Both models send their battery in input reports without any request
/// (layouts from SDL's controller_structs.h; the Linux hid-steam driver reads the same event).
/// Original model: 64-byte reports with a 01 00 version, byte 2 = type; type 04 is the status event,
/// percent at byte 14. 2026 model: input report 0x43, charge state at byte 1, percent at byte 2.
/// </summary>
public static class SteamController
{
    public const ushort VendorId = 0x28DE;
    public const ushort Wired = 0x1102, WirelessDongle = 0x1142;
    private const ushort Model2026First = 0x1302, Model2026Last = 0x1305;

    private const byte StatusEvent = 0x04, BatteryReport2026 = 0x43;
    private const int StatusLevelOffset = 14;
    private const byte Charging2026 = 2, ChargingDone2026 = 4;

    public static string? NameFor(ushort productId) =>
        productId is Wired or WirelessDongle or (>= Model2026First and <= Model2026Last) ? "Steam Controller" : null;

    /// <param name="report">Input report as read (Windows adds a leading 0x00 to unnumbered reports).</param>
    public static GamepadStatus? Parse(ushort productId, ReadOnlySpan<byte> report) =>
        productId is >= Model2026First and <= Model2026Last ? Parse2026(report) : ParseOriginal(report);

    private static GamepadStatus? ParseOriginal(ReadOnlySpan<byte> report)
    {
        foreach (int start in (ReadOnlySpan<int>)[0, 1]) // hidapi layout, or with the Windows 0x00 prefix
        {
            var r = report[Math.Min(start, report.Length)..];
            if (r.Length <= StatusLevelOffset || r[0] != 0x01 || r[1] != 0x00 || r[2] != StatusEvent) continue;
            byte level = r[StatusLevelOffset];
            return level <= 100 ? new GamepadStatus(level, Charging: false) : null;
        }
        return null;
    }

    private static GamepadStatus? Parse2026(ReadOnlySpan<byte> report)
    {
        if (report.Length < 3 || report[0] != BatteryReport2026 || report[2] > 100) return null;
        return report[1] switch
        {
            ChargingDone2026 => new GamepadStatus(100, Charging: false),
            Charging2026 => new GamepadStatus(report[2], Charging: true),
            _ => new GamepadStatus(report[2], Charging: false),
        };
    }
}
