using BatteryMonitor.Core.Protocols.Headsets;
using static BatteryMonitor.Tests.Headsets.ScriptedLink;

namespace BatteryMonitor.Tests.Headsets;

/// <summary>
/// Sony INZONE facts from the public HeadsetControl definitions. H5 / H9 II: vendor HCI command over
/// report 02 (opcode FC00, key C396, address 41 = PC to receiver), GET (01) of BATTERY_INFO (04);
/// the event reply carries [charger][percent], 0xFF = offline, and a byte-sum checksum.
/// Buds: the dongle sends report 02 12 04 by itself with right [14], left [16], case [18].
/// </summary>
public class SonyInzoneHeadsetsTests
{
    private const ushort H5 = 0x0EBF, H9II = 0x0FA8, Buds = 0x0EC2;
    private static HeadsetSpec Spec(ushort pid) => HeadsetCatalog.Find(SonyInzoneHeadsets.VendorId, pid)!;

    // 02 len=0C | 01 00 FC 08 96 C3 41 04 01 tid=0002 | checksum = sum(96..00) & FF = A1
    private static readonly byte[] BatteryGet = [0x02, 0x0C, 0x01, 0x00, 0xFC, 0x08, 0x96, 0xC3, 0x41, 0x04, 0x01, 0x02, 0x00, 0xA1];

    private static byte[] Event(byte charger, byte percent, byte type = 0x10, byte tid = 0x02, bool badChecksum = false)
    {
        byte[] r = Reply(64, (0, 0x02), (1, 14), (2, 0x04), (3, 0xFF), (4, 0x0A), (5, 0x00), (6, 0x96), (7, 0xC3),
            (8, 0x14), (9, 0x04), (10, type), (11, tid), (12, 0x00), (13, charger), (14, percent));
        int sum = 0;
        for (int i = 5; i <= 14; i++) sum += r[i];
        r[15] = (byte)(sum + (badChecksum ? 1 : 0));
        return r;
    }

    [Theory]
    [InlineData(H5)]
    [InlineData(H9II)]
    public void Battery_info_get_returns_percent_and_charger(ushort pid)
    {
        var link = new ScriptedLink().On(BatteryGet, Event(0x01, 72));

        Assert.Equal(new HeadsetStatus(72, true), Spec(pid).Query(link, pid));
        Assert.Equal([Convert.ToHexString(BatteryGet)], link.Sent);
    }

    [Fact]
    public void Unsolicited_notification_with_our_transaction_id_also_counts()
    {
        var link = new ScriptedLink().On(BatteryGet, Event(0x00, 40, type: 0xA0));

        Assert.Equal(new HeadsetStatus(40, false), Spec(H5).Query(link, H5));
    }

    [Fact]
    public void Replies_with_another_transaction_or_a_bad_checksum_are_skipped()
    {
        var link = new ScriptedLink().On(BatteryGet, Event(0x00, 10, tid: 0x01), Event(0x00, 20, badChecksum: true), Event(0x00, 30));

        Assert.Equal(new HeadsetStatus(30, false), Spec(H5).Query(link, H5));
    }

    [Theory]
    [InlineData((byte)0xFF)] // headset offline
    [InlineData((byte)101)]
    public void Offline_or_invalid_percent_gives_nothing(byte percent)
    {
        Assert.Null(Spec(H5).Query(new ScriptedLink().On(BatteryGet, Event(0x00, percent)), H5));
    }

    [Fact]
    public void No_reply_gives_nothing()
    {
        Assert.Null(Spec(H9II).Query(new ScriptedLink(), H9II));
    }

    private static byte[] BudsReport(byte right, byte left, byte @case, int checksumDelta = 0) =>
        Reply(64, (0, 0x02), (1, 0x12), (2, 0x04), (14, right), (16, left), (18, @case),
            (19, (byte)(right + left + 117 + checksumDelta)));

    [Fact]
    public void Buds_listen_without_sending_and_take_the_lower_earbud()
    {
        var link = new ScriptedLink().Unsolicited(Reply(64, (0, 0x01)), BudsReport(right: 80, left: 65, @case: 90));

        Assert.Equal(new HeadsetStatus(65, false), Spec(Buds).Query(link, Buds));
        Assert.Empty(link.Sent);
    }

    [Fact]
    public void Buds_report_with_a_bad_checksum_is_ignored()
    {
        var link = new ScriptedLink().Unsolicited(BudsReport(80, 65, 90, checksumDelta: 1));

        Assert.Null(Spec(Buds).Query(link, Buds));
    }

    [Fact]
    public void Buds_levels_above_100_are_ignored()
    {
        Assert.Null(Spec(Buds).Query(new ScriptedLink().Unsolicited(BudsReport(150, 65, 90)), Buds));
    }

    [Fact]
    public void Only_the_listening_buds_try_every_collection()
    {
        // A family without usage or interface is opened on every collection of the product.
        var unhinted = HeadsetCatalog.All.Where(s => s.UsagePage is null && s.Interface is null).Select(s => s.Name);

        Assert.Equal(["Sony INZONE Buds"], unhinted);
    }

    [Fact]
    public void Collections()
    {
        Assert.Equal(((ushort?)0xFF04, (ushort?)0x0002), (Spec(H5).UsagePage, Spec(H5).Usage));
        Assert.Equal(((ushort?)0xFF04, (ushort?)0x0001), (Spec(H9II).UsagePage, Spec(H9II).Usage));
        Assert.Equal(((ushort?)null, (ushort?)null, (int?)null), (Spec(Buds).UsagePage, Spec(Buds).Usage, Spec(Buds).Interface));
    }
}
