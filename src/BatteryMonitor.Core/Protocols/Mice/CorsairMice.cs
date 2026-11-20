using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Protocols.Headsets;

namespace BatteryMonitor.Core.Protocols.Mice;

/// <summary>
/// Corsair Slipstream wireless dongles, Bragi protocol (facts from ckb-next and OpenLinkHub). Only
/// "get property" is sent: 08|child 02 prop on interface 1, child 1 (09) = the only paired device.
/// Property 0F = level in tenths of a percent, 10 = status (1 charging, 2 discharging, 3 charged).
/// Unlike those tools, the dongle is NOT switched to "software mode" first (that is a SET); if a
/// device only answers in software mode, it simply shows no level.
/// </summary>
public static class CorsairMice
{
    public const ushort VendorId = 0x1B1C;
    private const byte PairedDevice = 0x09, Get = 0x02, Level = 0x0F, Status = 0x10, Charging = 1;

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        Dongle("Corsair Dark Core RGB PRO SE", DeviceKind.Mouse, 0x1B7F),
        Dongle("Corsair Dark Core RGB PRO", DeviceKind.Mouse, 0x1B81),
        Dongle("Corsair Harpoon RGB Wireless", DeviceKind.Mouse, 0x1B65),
        // Generic Slipstream receivers ship with mice and keyboards alike.
        Dongle("Corsair Slipstream device", DeviceKind.Other, 0x1BDC, 0x1BA6, 0x2B00, 0x2B1B),
    ];

    private static HeadsetSpec Dongle(string name, DeviceKind kind, params ushort[] pids) =>
        new(name, VendorId, pids, null, null, 1, (l, _) => Query(l), kind);

    private static HeadsetStatus? Query(IHidLink link)
    {
        if (GetProperty(link, Level) is not int tenths || tenths == 0) return null; // 0: asleep or not paired
        bool charging = GetProperty(link, Status) == Charging;
        return new HeadsetStatus(HeadsetMath.Clamp100(tenths / 10), charging);
    }

    // Reply: [1] 02, [2] error code (0 = ok), [3..5] value, little endian.
    private static int? GetProperty(IHidLink link, byte property)
    {
        var r = link.Exchange([0x00, PairedDevice, Get, property], x => x.Length >= 6 && x[1] == Get);
        if (r is null || r[2] != 0x00) return null;
        return r[3] | (r[4] << 8) | (r[5] << 16);
    }
}
