namespace BatteryMonitor.Core.Protocols.Headsets;

/// <summary>
/// Sony INZONE dongles (facts from the public HeadsetControl definitions). H5 and H9 II speak a Sony
/// vendor HCI layer on report 02: only a GET of BATTERY_INFO is sent. INZONE Buds need nothing sent:
/// the dongle reports the earbuds by itself. Older INZONE models have no public battery source.
/// </summary>
public static class SonyInzoneHeadsets
{
    public const ushort VendorId = 0x054C;

    private const byte ReportId = 0x02;
    private const byte HciCommand = 0x01, HciEvent = 0x04, SonyEvent = 0xFF;
    private const byte KeyLo = 0x96, KeyHi = 0xC3, PcToReceiver = 0x41, BatteryInfo = 0x04;
    private const byte Get = 0x01, Return = 0x10, NotifyActive = 0xA0;
    private const byte TransactionId = 0x02; // the dongle uses 1 for its own notifications
    private const byte Offline = 0xFF;

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        new("Sony INZONE H5", VendorId, [0x0EBF], 0xFF04, 0x0002, null, (l, _) => BatteryInfoGet(l)),
        new("Sony INZONE H9 II", VendorId, [0x0FA8], 0xFF04, 0x0001, null, (l, _) => BatteryInfoGet(l)),
        new("Sony INZONE Buds", VendorId, [0x0EC2], null, null, null, (l, _) => BudsReport(l)),
    ];

    // Command after the report id: len, 01 00 FC (HCI command FC00), param len, key, address, event, type,
    // transaction id (LE), then a checksum = sum of key..transaction id.
    private static HeadsetStatus? BatteryInfoGet(IHidLink link)
    {
        byte[] get = [ReportId, 12, HciCommand, 0x00, 0xFC, 8, KeyLo, KeyHi, PcToReceiver, BatteryInfo, Get, TransactionId, 0x00, 0];
        get[^1] = Sum(get, 6, 12);
        if (link.Exchange(get, IsBatteryEvent) is not { } r) return null;
        byte charger = r[13], percent = r[14];
        if (percent == Offline || percent > 100) return null;
        return new HeadsetStatus(percent, Charging: charger != 0);
    }

    // Event: 02 len 04 FF plen 00 key(96 C3) address event type tid(LE) payload... checksum (sum of [5..len]).
    private static bool IsBatteryEvent(byte[] r)
    {
        if (r.Length < 16 || r[0] != ReportId || r[1] < 14 || r[1] + 2 > r.Length) return false;
        if (r[2] != HciEvent || r[3] != SonyEvent || r[5] != 0x00 || r[6] != KeyLo || r[7] != KeyHi) return false;
        if (r[8] >> 4 != 0x1 || Sum(r, 5, r[1]) != r[r[1] + 1]) return false; // to the PC, checksum ok
        return r[9] == BatteryInfo && r[10] is Return or NotifyActive && r[11] == TransactionId && r[12] == 0x00;
    }

    // Buds report: 02 12 04 ... [14] right, [16] left, [18] case, [19] = right + left + 117.
    private static HeadsetStatus? BudsReport(IHidLink link)
    {
        var r = link.Listen(x => x.Length >= 20 && x[0] == ReportId && x[1] == 0x12 && x[2] == 0x04
                                 && x[19] == (byte)(x[14] + x[16] + 117));
        if (r is null || r[14] > 100 || r[16] > 100) return null;
        return new HeadsetStatus(Math.Min(r[14], r[16]), Charging: false);
    }

    private static byte Sum(byte[] bytes, int from, int to)
    {
        int sum = 0;
        for (int i = from; i <= to; i++) sum += bytes[i];
        return (byte)sum;
    }
}
