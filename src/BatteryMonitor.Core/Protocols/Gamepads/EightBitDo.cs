namespace BatteryMonitor.Core.Protocols.Gamepads;

/// <summary>
/// 8BitDo controllers whose input report carries the battery without any request (facts from
/// SDL's HIDAPI 8BitDo driver). Byte 14: bits 0-6 level in percent, bit 7 charging. Other models
/// need a feature request first (Pro 2/3, SN30/SF30 Pro) or have no battery byte (Ultimate 2C);
/// they are not read.
/// </summary>
public static class EightBitDo
{
    public const ushort VendorId = 0x2DC8;
    public const ushort Ultimate3 = 0x202F, Ultimate2Wireless = 0x6012;

    private const int BatteryOffset = 14;
    private const int Ultimate2MinLength = 34; // firmware 1.03+; older reports have no battery

    public static string? NameFor(ushort productId) => productId switch
    {
        Ultimate3 => "8BitDo Ultimate 3",
        Ultimate2Wireless => "8BitDo Ultimate 2 Wireless",
        _ => null,
    };

    /// <param name="report">Input report including its report id at index 0.</param>
    public static GamepadStatus? Parse(ushort productId, ReadOnlySpan<byte> report)
    {
        if (NameFor(productId) is null || report.Length <= BatteryOffset) return null;
        if (report[0] is not (0x01 or 0x03 or 0x04)) return null; // BT enhanced, standard, USB enhanced
        if (productId == Ultimate2Wireless && report.Length < Ultimate2MinLength) return null;
        byte battery = report[BatteryOffset];
        int level = battery & 0x7F;
        if (level > 100) return null;
        bool charging = (battery & 0x80) != 0 && level < 100; // 100 % means charged
        return new GamepadStatus(level, charging);
    }
}
