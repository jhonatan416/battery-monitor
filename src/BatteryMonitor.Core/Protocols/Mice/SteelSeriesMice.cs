using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Protocols.Headsets;

namespace BatteryMonitor.Core.Protocols.Mice;

/// <summary>
/// SteelSeries wireless mice (facts from rivalcfg). One "battery level" output report on the
/// vendor interface; through the receiver the Aerox / Prime command byte gets the 0x40 flag.
/// </summary>
public static class SteelSeriesMice
{
    public const ushort VendorId = 0x1038;
    private const byte WirelessFlag = 0x40, AeroxBattery = 0x92, ChargingBit = 0x80;

    // (name, wired PID, receiver PID)
    private static readonly (string Name, ushort Wired, ushort Wireless)[] Aerox =
    [
        ("SteelSeries Aerox 3 Wireless", 0x183A, 0x1838), ("SteelSeries Aerox 3 Wireless", 0x187A, 0x1878),
        ("SteelSeries Aerox 5 Wireless", 0x1854, 0x1852), ("SteelSeries Aerox 5 Wireless", 0x185E, 0x185C),
        ("SteelSeries Aerox 5 Wireless", 0x1862, 0x1860), ("SteelSeries Aerox 9 Wireless", 0x185A, 0x1858),
        ("SteelSeries Aerox 9 Wireless", 0x1876, 0x1874), ("SteelSeries Prime Wireless", 0x1842, 0x1840),
        ("SteelSeries Prime Mini Wireless", 0x184A, 0x1848),
    ];

    private static readonly HashSet<ushort> AeroxWireless = Aerox.Select(a => a.Wireless).ToHashSet();

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        .. Aerox.Select(a => Mouse(a.Name, [a.Wired, a.Wireless], 3, AeroxQuery)),
        Mouse("SteelSeries Rival 3 Wireless", [0x1830, 0x1872], 3, RivalQuery),
        Mouse("SteelSeries Rival 650 Wireless", [0x172B, 0x1726], 0, RivalQuery),
    ];

    private static HeadsetSpec Mouse(string name, ushort[] pids, int iface, Func<IHidLink, ushort, HeadsetStatus?> query) =>
        new(name, VendorId, pids, null, null, iface, query, DeviceKind.Mouse);

    // 92 (D2 through the receiver) -> [1]: bit 7 charging, (low 7 bits - 1) * 5 = percent.
    private static HeadsetStatus? AeroxQuery(IHidLink link, ushort productId)
    {
        byte command = AeroxWireless.Contains(productId) ? (byte)(AeroxBattery | WirelessFlag) : AeroxBattery;
        if (link.Exchange([0x00, command]) is not { Length: >= 2 } r) return null;
        int percent = ((r[1] & ~ChargingBit) - 1) * 5;
        return percent is >= 0 and <= 100 ? new HeadsetStatus(percent, (r[1] & ChargingBit) != 0) : null;
    }

    // AA 01 -> [0] percent, [2] != 0 charging.
    private static HeadsetStatus? RivalQuery(IHidLink link, ushort _)
    {
        if (link.Exchange([0x00, 0xAA, 0x01]) is not { Length: >= 3 } r || r[0] > 100) return null;
        return new HeadsetStatus(r[0], r[2] != 0);
    }
}
