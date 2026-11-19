namespace BatteryMonitor.Core.Protocols.Headsets;

/// <summary>
/// SteelSeries Arctis / Arctis Nova headsets. Facts from the public HeadsetControl device
/// definitions; every request below only reads status (no setting is saved).
/// </summary>
public static class SteelSeriesHeadsets
{
    public const ushort VendorId = 0x1038;
    private const ushort ArctisPage = 0xFF43, ArctisUsage = 0x0202, NovaPage = 0xFFC0, NovaUsage = 0x0001;
    private static readonly byte[] NovaStatus = [0x00, 0xB0];

    private static readonly HashSet<ushort> Nova7PercentIds = [0x22A1, 0x227E, 0x2258, 0x229E, 0x22AD, 0x22A9, 0x22A5];

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        new("SteelSeries Arctis 1", VendorId, [0x12B3, 0x12B6, 0x12D7, 0x12D5], ArctisPage, ArctisUsage, 3, Arctis1),
        new("SteelSeries Arctis 7", VendorId, [0x1260, 0x12AD, 0x1252, 0x1280], ArctisPage, ArctisUsage, 5, Arctis7),
        new("SteelSeries Arctis 9", VendorId, [0x12C2, 0x12C4], null, null, 0, Arctis9),
        new("SteelSeries Arctis Pro Wireless", VendorId, [0x1290], null, null, 0, ArctisProWireless),
        new("SteelSeries Arctis Nova Pro Wireless", VendorId, [0x12E0, 0x12E5], null, null, 4, NovaProWireless),
        new("SteelSeries Arctis 7+", VendorId, [0x220E, 0x2212, 0x2216, 0x2236], NovaPage, NovaUsage, 3, Arctis7Plus),
        new("SteelSeries Arctis Nova 3P Wireless", VendorId, [0x2269, 0x226D], NovaPage, NovaUsage, 3, (l, _) => Nova3(l, 5)),
        new("SteelSeries Arctis Nova 5", VendorId, [0x2232, 0x2253], NovaPage, NovaUsage, 3, (l, _) => Nova3(l, 16)),
        new("SteelSeries Arctis Nova 7", VendorId, [0x2202, 0x2206, 0x223A, 0x227A, 0x22A4, .. Nova7PercentIds],
            NovaPage, NovaUsage, 3, Nova7),
    ];

    // 06 12 -> [2] == 1 offline, [3] percent; no charging flag.
    private static HeadsetStatus? Arctis1(IHidLink link, ushort _)
    {
        if (link.Exchange([0x06, 0x12]) is not { Length: >= 4 } r || r[2] == 0x01) return null;
        return new HeadsetStatus(HeadsetMath.Clamp100(r[3]), false);
    }

    // 06 18 -> [2] percent; no offline or charging information.
    private static HeadsetStatus? Arctis7(IHidLink link, ushort _) =>
        link.Exchange([0x06, 0x18]) is { Length: >= 3 } r ? new HeadsetStatus(HeadsetMath.Clamp100(r[2]), false) : null;

    // 00 20 -> [3] mapped from 0x64..0x9A, [4] == 1 charging.
    private static HeadsetStatus? Arctis9(IHidLink link, ushort _) =>
        link.Exchange([0x00, 0x20]) is { Length: >= 5 } r
            ? new HeadsetStatus(HeadsetMath.Map(r[3], 0x64, 0x9A), r[4] == 0x01)
            : null;

    // 41 AA -> [0] 0x04 online (0x02 offline); then 40 AA -> [0] level 0..4.
    private static HeadsetStatus? ArctisProWireless(IHidLink link, ushort _)
    {
        if (link.Exchange([0x41, 0xAA]) is not { Length: >= 1 } s || s[0] != 0x04) return null;
        return link.Exchange([0x40, 0xAA]) is { Length: >= 1 } r ? new HeadsetStatus(HeadsetMath.Map(r[0], 0, 4), false) : null;
    }

    // 06 B0 -> [15] status (1 offline, 2 charging on cable, 8 online), [6] level 0..8.
    private static HeadsetStatus? NovaProWireless(IHidLink link, ushort _)
    {
        if (link.Exchange([0x06, 0xB0]) is not { Length: >= 16 } r) return null;
        return r[15] switch
        {
            0x02 => new HeadsetStatus(HeadsetMath.Map(r[6], 0, 8), true),
            0x08 => new HeadsetStatus(HeadsetMath.Map(r[6], 0, 8), false),
            _ => null,
        };
    }

    // 00 B0 -> [1] == 1 offline, [2] level 0..4, [3] == 1 charging.
    private static HeadsetStatus? Arctis7Plus(IHidLink link, ushort _)
    {
        if (link.Exchange(NovaStatus) is not { Length: >= 4 } r || r[1] == 0x01) return null;
        return new HeadsetStatus(HeadsetMath.Map(r[2], 0, 4), r[3] == 0x01);
    }

    // 00 B0 -> [1] == 2 offline, [3] percent, [4] == 1 charging (Nova 5 needs a 16-byte reply).
    private static HeadsetStatus? Nova3(IHidLink link, int minLength)
    {
        if (link.Exchange(NovaStatus) is not { } r || r.Length < minLength || r[1] == 0x02) return null;
        return new HeadsetStatus(HeadsetMath.Clamp100(r[3]), r[4] == 0x01);
    }

    // 00 B0 -> [3] state (0 offline, 1/2 charging), [2] level: 0..4 on older ids, percent on newer ones.
    private static HeadsetStatus? Nova7(IHidLink link, ushort productId)
    {
        if (link.Exchange(NovaStatus) is not { Length: >= 4 } r || r[3] == 0x00) return null;
        int percent = Nova7PercentIds.Contains(productId) ? HeadsetMath.Clamp100(r[2]) : HeadsetMath.Map(r[2], 0, 4);
        return new HeadsetStatus(percent, r[3] is 0x01 or 0x02);
    }
}
