namespace BatteryMonitor.Core.Protocols.Headsets;

/// <summary>
/// Jabra headsets and Link dongles speaking the GN protocol (facts from the public HeadsetControl
/// definitions; the battery command is also used by Gadgetbridge). Frame on report 05:
/// 05 dst src seq (type&lt;&lt;6 | len) cmd sub payload. Only a GET of 12/02 (battery) is sent.
/// </summary>
public static class JabraHeadsets
{
    public const ushort VendorId = 0x0B0E;

    private const byte HeadsetOnUsb = 0x08, HeadsetBehindDongle = 0x04;
    private const byte ReportId = 0x05, Host = 0x00, Sequence = 0x01;
    private const byte TypeGet = 1, TypeResponse = 3, NoPayloadLength = 6;
    private const byte StatusGroup = 0x12, Battery = 0x02, Nack = 0xFE;

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        new("Jabra Evolve2 65 Flex", VendorId, [0x2517, 0x2518, 0x2519], 0xFF00, 0x0001, 3, (l, _) => Query(l, HeadsetOnUsb)),
        new("Jabra Evolve3 75", VendorId, [0x2546, 0x2547, 0x2548], 0xFF00, 0x0005, 3, (l, _) => Query(l, HeadsetOnUsb)),
        new("Jabra Link 380", VendorId, [0x24C7, 0x24C8, 0x24C9, 0x24CA], 0xFF00, 0x0001, 3, (l, _) => Query(l, HeadsetBehindDongle)),
        new("Jabra Link 390", VendorId, [0x2E50, 0x2E51, 0x2E56, 0x2E57], 0xFF00, 0x0005, 3, (l, _) => Query(l, HeadsetBehindDongle)),
    ];

    // Reply: [7] flags (bit 0 charging, bit 1 low, bit 6 fast charging), [8] level %.
    private static HeadsetStatus? Query(IHidLink link, byte address)
    {
        link.Drain(); // a stale reply from the previous poll carries the same sequence number
        byte[] get = [ReportId, address, Host, Sequence, (TypeGet << 6) | NoPayloadLength, StatusGroup, Battery];
        var r = link.Exchange(get, IsResponse);
        if (r is null || r[5] == Nack || (r[4] & 0x3F) - NoPayloadLength < 2) return null;
        return new HeadsetStatus(HeadsetMath.Clamp100(r[8]), Charging: (r[7] & 0x01) != 0);
    }

    private static bool IsResponse(byte[] r) =>
        r.Length >= 9 && r[0] == ReportId && r[3] == Sequence && r[4] >> 6 == TypeResponse;
}
