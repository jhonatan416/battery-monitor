namespace BatteryMonitor.Core.Protocols.Headsets;

/// <summary>
/// Corsair wireless headsets. Facts from the public HeadsetControl device definitions.
/// The Void Wireless V2 "wake" messages are GETs of the firmware and heartbeat properties:
/// they change no setting.
/// </summary>
public static class CorsairHeadsets
{
    public const ushort VendorId = 0x1B1C;
    private const ushort VirtuosoXtWired = 0x0A62;

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        new("Corsair Void", VendorId,
            [0x1B27, 0x0A14, 0x0A16, 0x0A17, 0x0A1D, 0x0A1A, 0x1B2A, 0x1B23, 0x1B29, 0x0A55, 0x0A51, 0x0A52,
             0x0A38, 0x0A4F, 0x0A2B, 0x0A75, 0x0A56],
            0xFFC5, 0x0001, 3, Void),
        new("Corsair Virtuoso", VendorId, [0x0A64, VirtuosoXtWired, 0x0A3E, 0x0A3D], 0xFF42, 0x0001, 3, Virtuoso),
        new("Corsair Void Wireless V2", VendorId, [0x2A08, 0x2A02, 0x0A97], null, null, 4, VoidWirelessV2),
    ];

    // C9 64 -> [4] status (0 disconnected, 1 normal, 2 low, 4/5 charging), [2] level with bit 7 = mic up.
    private static HeadsetStatus? Void(IHidLink link, ushort _)
    {
        if (link.Exchange([0xC9, 0x64]) is not { Length: >= 5 } r) return null;
        int level = HeadsetMath.Clamp100(r[2] & 0x7F);
        return r[4] switch
        {
            0x01 or 0x02 => new HeadsetStatus(level, false),
            0x04 or 0x05 => new HeadsetStatus(level, true),
            _ => null, // disconnected or unknown
        };
    }

    // "Bragi" GET 02 <target> 02 <property> -> 01 <src> 02 <status> <u32 LE value>. Target 09 is the
    // headset behind the receiver, 08 the device itself. Property 0F = battery in tenths, 10 = charging.
    private static HeadsetStatus? Virtuoso(IHidLink link, ushort productId)
    {
        byte[] targets = productId == VirtuosoXtWired ? [0x08, 0x09] : [0x09, 0x08];
        foreach (byte target in targets)
        {
            if (BragiGet(link, target, 0x0F) is not int tenths) continue;
            bool charging = BragiGet(link, target, 0x10) == 1;
            return new HeadsetStatus(HeadsetMath.Clamp100(tenths / 10), charging);
        }
        return null; // neither target answered: headset off
    }

    private static int? BragiGet(IHidLink link, byte target, byte property)
    {
        link.Drain(); // stale reports from before the request
        var r = link.Exchange([0x02, target, 0x02, property], x => x.Length >= 6 && x[0] == 0x01 && x[2] == 0x02);
        if (r is null || r[3] != 0x00) return null; // no reply, or status "no such property"
        return r[4] | (r[5] << 8);
    }

    // Wake: GET receiver firmware (13) and heartbeat (12); then the headset heartbeat must answer.
    // Battery: GET 0F on the headset, value [4] | [5]<<8 in tenths; 0 or > 1000 is bogus, retried up to 3 times.
    private static HeadsetStatus? VoidWirelessV2(IHidLink link, ushort _)
    {
        link.Exchange([0x00, 0x02, 0x08, 0x02, 0x13]);
        link.Exchange([0x00, 0x02, 0x08, 0x02, 0x12]);
        link.Drain();
        if (link.Exchange([0x00, 0x02, 0x09, 0x02, 0x12]) is null) return null; // headset off
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (link.Exchange([0x00, 0x02, 0x09, 0x02, 0x0F]) is not { Length: >= 6 } r) continue;
            int tenths = r[4] | (r[5] << 8);
            if (tenths is >= 1 and <= 1000) return new HeadsetStatus(tenths / 10, false); // charging not reported
        }
        return null;
    }
}
