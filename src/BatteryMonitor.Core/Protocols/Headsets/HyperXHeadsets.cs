namespace BatteryMonitor.Core.Protocols.Headsets;

/// <summary>HyperX wireless headsets. Facts from the public HeadsetControl device definitions; status reads only.</summary>
public static class HyperXHeadsets
{
    public const ushort HpVendorId = 0x03F0, KingstonVendorId = 0x0951;

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        new("HyperX Cloud II Wireless", HpVendorId, [0x0696, 0x018B], 0xFF90, 0x0303, 0, CloudIIWireless),
        new("HyperX Cloud Flight", KingstonVendorId, [0x16C4, 0x1723], null, null, 0, CloudFlight),
        new("HyperX Cloud Alpha Wireless", HpVendorId, [0x098D], null, null, 0, CloudAlphaWireless),
        new("HyperX Cloud Alpha 2 Wireless", HpVendorId, [0x08BE], 0xFF13, 0xFF00, 2, CloudAlpha2Wireless),
    ];

    // 06 FF BB <cmd>; the reply echoes that header. cmd 02: [7] percent. cmd 03: [4] == 1 charging.
    private static HeadsetStatus? CloudIIWireless(IHidLink link, ushort _)
    {
        if (CloudII(link, 0x02) is not { Length: >= 8 } level) return null;
        bool charging = CloudII(link, 0x03) is { Length: >= 5 } c && c[4] == 0x01;
        return new HeadsetStatus(HeadsetMath.Clamp100(level[7]), charging);
    }

    private static byte[]? CloudII(IHidLink link, byte cmd) =>
        link.Exchange([0x06, 0xFF, 0xBB, cmd], r => r.Length >= 4 && r[0] == 0x06 && r[1] == 0xFF && r[2] == 0xBB && r[3] == cmd);

    // 21 FF 05 -> voltage [3]<<8 | [4] in mV; above 4107 mV it is charging and reports no level.
    private static HeadsetStatus? CloudFlight(IHidLink link, ushort _)
    {
        if (link.Exchange([0x21, 0xFF, 0x05]) is not { Length: >= 5 } r) return null;
        int millivolts = (r[3] << 8) | r[4];
        if (millivolts > 4107) return new HeadsetStatus(null, true);
        return new HeadsetStatus(FlightPercent(millivolts), false);
    }

    /// <summary>The Cloud Flight discharge curve published in HeadsetControl.</summary>
    internal static int FlightPercent(int millivolts)
    {
        if (millivolts <= 3648) return HeadsetMath.Clamp100((int)Math.Round(0.00125 * millivolts));
        if (millivolts > 3975) return 100;
        double v = millivolts;
        double p = 2.547505e-8 * Math.Pow(v, 4) - 3.900299e-4 * Math.Pow(v, 3) + 2.238321 * v * v - 5706.256 * v + 5452299.0;
        return HeadsetMath.Clamp100((int)Math.Round(p));
    }

    // 21 BB 03 -> [3] == 1 offline; 21 BB 0C -> [3] == 1 charging (no level); 21 BB 0B -> [3] percent.
    private static HeadsetStatus? CloudAlphaWireless(IHidLink link, ushort _)
    {
        if (link.Exchange([0x21, 0xBB, 0x03]) is not { Length: >= 4 } conn || conn[3] == 0x01) return null;
        if (link.Exchange([0x21, 0xBB, 0x0C]) is { Length: >= 4 } chg && chg[3] == 0x01) return new HeadsetStatus(null, true);
        return link.Exchange([0x21, 0xBB, 0x0B]) is { Length: >= 4 } r ? new HeadsetStatus(HeadsetMath.Clamp100(r[3]), false) : null;
    }

    // <cmd> <sub> -> reply <cmd+1> <sub>; acks (FF ..) and notifications (FB ..) are skipped.
    // 52 01 -> [2] == 0 offline; 50 02 -> [2] percent (> 100 is an error), [3] == 1 charging.
    private static HeadsetStatus? CloudAlpha2Wireless(IHidLink link, ushort _)
    {
        if (Alpha2(link, 0x52, 0x01) is not { Length: >= 3 } conn || conn[2] == 0x00) return null;
        if (Alpha2(link, 0x50, 0x02) is not { Length: >= 4 } r || r[2] > 100) return null;
        return new HeadsetStatus(r[2], r[3] == 0x01);
    }

    private static byte[]? Alpha2(IHidLink link, byte cmd, byte sub) =>
        link.Exchange([cmd, sub], r => r.Length >= 2 && r[0] == cmd + 1 && r[1] == sub);
}
