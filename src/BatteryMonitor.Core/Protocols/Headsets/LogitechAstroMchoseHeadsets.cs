namespace BatteryMonitor.Core.Protocols.Headsets;

/// <summary>
/// Logitech headsets that do not speak HID++ (facts from the public HeadsetControl definitions;
/// the PRO X 2 request also from the hid-logitech-headset kernel patch). Battery "get" requests only.
/// Not included: the G435, whose dongle has no public read-only battery command.
/// </summary>
public static class LogitechHeadsets
{
    public const ushort LogitechVendorId = 0x046D, AstroVendorId = 0x9886;

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        new("Logitech PRO X 2 LIGHTSPEED", LogitechVendorId, [0x0AF7], 0xFFA0, 0x0001, 3, (l, _) => ProX2(l)),
        new("ASTRO A50", LogitechVendorId, [0x0B1C], 0xFF32, 0x0074, 8, (l, _) => AstroA50(l)),
        new("ASTRO A50 (Gen 4)", AstroVendorId, [0x002C], 0xFF32, 0x0074, 6, (l, _) => AstroA50Gen4(l)),
    ];

    // Older-firmware request (newer firmware answers through Logitech's "Centurion" protocol, not
    // implemented): reply 51 0B .. [8] 04, [10] percent, [12] 02 = charging.
    private static HeadsetStatus? ProX2(IHidLink link)
    {
        byte[] get = [0x51, 0x08, 0x00, 0x03, 0x1A, 0x00, 0x03, 0x00, 0x04, 0x0A];
        var r = link.Exchange(get, x => x.Length >= 13 && x[0] == 0x51 && x[1] == 0x0B && x[8] == 0x04);
        return r is null || r[10] > 100 ? null : new HeadsetStatus(r[10], r[12] == 0x02);
    }

    // 02 0C LEN 00 CMD HANDLE: GET battery (06) with handle 0C. The reply has the same CMD and a payload
    // (LEN > 3): [6] percent, [8] != 0 docked / charging. Pushes and bare acks are skipped.
    private static HeadsetStatus? AstroA50(IHidLink link)
    {
        var r = link.Exchange([0x02, 0x0C, 0x03, 0x00, 0x06, 0x0C],
            x => x.Length >= 9 && x[0] == 0x02 && x[1] == 0x0C && x[4] == 0x06 && x[2] > 3);
        return r is null || r[6] > 100 ? null : new HeadsetStatus(r[6], r[8] != 0);
    }

    // 02 CMD -> 02 STATUS LEN payload (STATUS 02 = OK). 54: payload bit 1 = headset linked (the base
    // keeps the last level after the headset turns off). 7C: bit 7 charging, bits 0-6 percent.
    private static HeadsetStatus? AstroA50Gen4(IHidLink link)
    {
        if (Gen4Get(link, 0x54) is not byte status || (status & 0x02) == 0) return null;
        if (Gen4Get(link, 0x7C) is not byte battery || (battery & 0x7F) > 100) return null;
        return new HeadsetStatus(battery & 0x7F, (battery & 0x80) != 0);
    }

    private static byte? Gen4Get(IHidLink link, byte command) =>
        link.Exchange([0x02, command], x => x.Length >= 4 && x[0] == 0x02) is { } r && r[1] == 0x02 ? r[3] : null;
}

/// <summary>
/// MCHOSE X9 and V9 Pro (facts from the public HeadsetControl definitions): report 55 query
/// 55 65 01, answered by 55 65 VV 02 (VV = percent); heartbeats with another field are skipped.
/// </summary>
public static class MchoseHeadsets
{
    public static IEnumerable<HeadsetSpec> Specs =>
    [
        new("MCHOSE X9 Wireless", 0x3837, [0x6045], 0xFF90, 0x0001, 0, (l, _) => Query(l)),
        new("MCHOSE V9 Pro", 0x291D, [0x385D], 0xFF90, 0x0001, 0, (l, _) => Query(l)),
    ];

    private static HeadsetStatus? Query(IHidLink link)
    {
        var r = link.Exchange([0x55, 0x65, 0x01], x => x.Length >= 4 && x[0] == 0x55 && x[1] == 0x65 && x[3] == 0x02);
        return r is null || r[2] > 100 ? null : new HeadsetStatus(r[2], Charging: false);
    }
}
