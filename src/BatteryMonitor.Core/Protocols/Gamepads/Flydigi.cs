using BatteryMonitor.Core.Protocols.Headsets;

namespace BatteryMonitor.Core.Protocols.Gamepads;

/// <summary>
/// Flydigi V2 controllers (facts from SDL's HIDAPI Flydigi driver). One read-only "get info"
/// request, 5A A5 01 02 00, sent as report 03 (unnumbered on Vader and Apex 6); the reply starts
/// 5A A5 01 and byte 11 holds the power state (high nibble: 0 battery, 1 charging, 2 charged) and
/// the level (low nibble: fifths, tenths on Apex 6). Nothing that changes the controller is sent.
/// V1 controllers (04B4:2412) have no public battery source.
/// </summary>
public static class Flydigi
{
    public const ushort VendorId = 0x37D7;
    public const ushort Apex = 0x2501, Apex6 = 0x2502, Vader = 0x2401;
    public const ushort VendorUsagePage = 0xFFA0;

    private static readonly byte[] InfoCommand = [0x5A, 0xA5, 0x01, 0x02, 0x00];
    private const byte CommandReportId = 0x03;
    private const int MinReplyLength = 31, BatteryOffset = 11;

    public static string? NameFor(ushort productId) => productId switch
    {
        Apex => "Flydigi Apex",
        Apex6 => "Flydigi Apex 6",
        Vader => "Flydigi Vader",
        _ => null,
    };

    public static GamepadStatus? Query(IHidLink link, ushort productId)
    {
        byte reportId = productId is Vader or Apex6 ? (byte)0x00 : CommandReportId;
        return link.Exchange([reportId, .. InfoCommand], IsInfoReply) is { } reply ? Parse(productId, Strip(reply)) : null;
    }

    /// <summary>Drops a leading report id: replies themselves start with 5A.</summary>
    private static byte[] Strip(byte[] reply) => reply.Length > 0 && reply[0] != 0x5A ? reply[1..] : reply;

    private static bool IsInfoReply(byte[] report) =>
        Strip(report) is { Length: >= MinReplyLength } r && r[0] == 0x5A && r[1] == 0xA5 && r[2] == 0x01;

    private static GamepadStatus? Parse(ushort productId, byte[] reply)
    {
        if (!IsInfoReply(reply)) return null;
        int level = reply[BatteryOffset] & 0x0F;
        int percent = Math.Min(level * (productId == Apex6 ? 10 : 20), 100);
        return (reply[BatteryOffset] >> 4) switch
        {
            0 => new GamepadStatus(percent, Charging: false),
            1 => new GamepadStatus(percent, Charging: true),
            2 => new GamepadStatus(100, Charging: false),
            _ => null,
        };
    }
}
