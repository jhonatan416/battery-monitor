using BatteryMonitor.Core.Protocols.Headsets;
using static BatteryMonitor.Tests.Headsets.ScriptedLink;

namespace BatteryMonitor.Tests.Headsets;

/// <summary>Battery facts from the public HeadsetControl Corsair device definitions.</summary>
public class CorsairHeadsetsTests
{
    private static HeadsetSpec Spec(ushort pid) => HeadsetCatalog.Find(CorsairHeadsets.VendorId, pid)!;

    [Theory]
    [InlineData(0x01, 0x41, 65, false)] // normal
    [InlineData(0x02, 0x0F, 15, false)] // low
    [InlineData(0x04, 0x32, 50, true)]  // charging
    [InlineData(0x05, 0x64, 100, true)] // charging
    [InlineData(0x01, 0xC1, 65, false)] // bit 7 is the mic-up flag, not part of the level
    public void Void_status_byte_4_and_level_byte_2(byte status, byte level, int percent, bool charging)
    {
        var link = new ScriptedLink().On([0xC9, 0x64], Reply(5, (2, level), (4, status)));

        Assert.Equal(new HeadsetStatus(percent, charging), Spec(0x0A14).Query(link, 0x0A14));
    }

    [Theory]
    [InlineData(0x00)] // disconnected
    [InlineData(0x03)] // unknown status
    public void Void_disconnected_or_unknown_status_gives_no_reading(byte status)
    {
        var link = new ScriptedLink().On([0xC9, 0x64], Reply(5, (2, 50), (4, status)));

        Assert.Null(Spec(0x0A14).Query(link, 0x0A14));
    }

    // Bragi GET reply: 01 <src> 02 <status> <u32 LE value>
    private static byte[] Bragi(int value, byte status = 0x00) =>
        Reply(64, (0, 0x01), (2, 0x02), (3, status), (4, (byte)value), (5, (byte)(value >> 8)));

    [Fact]
    public void Virtuoso_wireless_asks_the_headset_behind_the_receiver_in_tenths_of_a_percent()
    {
        var link = new ScriptedLink()
            .On([0x02, 0x09, 0x02, 0x0F], Bragi(734))
            .On([0x02, 0x09, 0x02, 0x10], Bragi(1));

        Assert.Equal(new HeadsetStatus(73, true), Spec(0x0A64).Query(link, 0x0A64));
    }

    [Fact]
    public void Virtuoso_falls_back_to_the_other_target_and_skips_volume_events()
    {
        var volumeEvent = Reply(64, (0, 0x0E));
        var link = new ScriptedLink()
            .On([0x02, 0x09, 0x02, 0x0F], Bragi(0, status: 0x05)) // no such property on target 09
            .On([0x02, 0x08, 0x02, 0x0F], volumeEvent, Bragi(500))
            .On([0x02, 0x08, 0x02, 0x10], Bragi(0));

        Assert.Equal(new HeadsetStatus(50, false), Spec(0x0A3E).Query(link, 0x0A3E));
    }

    [Fact]
    public void Virtuoso_with_no_answer_on_either_target_is_off()
    {
        Assert.Null(Spec(0x0A64).Query(new ScriptedLink(), 0x0A64));
    }

    [Fact]
    public void Void_v2_wireless_retries_bogus_values_up_to_three_times()
    {
        var link = new ScriptedLink()
            .On([0x00, 0x02, 0x09, 0x02, 0x12], Reply(64, (0, 0x01)))           // headset heartbeat answered
            .On([0x00, 0x02, 0x09, 0x02, 0x0F], Reply(64, (4, 0x00), (5, 0x00)),  // 0: bogus
                                                Reply(64, (4, 0xA0), (5, 0x86)),  // 34464: bogus (> 1000)
                                                Reply(64, (4, 0x84), (5, 0x03))); // 900 -> 90 %

        Assert.Equal(new HeadsetStatus(90, false), Spec(0x2A08).Query(link, 0x2A08));
    }

    [Fact]
    public void Void_v2_wireless_without_a_heartbeat_reply_is_off()
    {
        var link = new ScriptedLink().On([0x00, 0x02, 0x09, 0x02, 0x0F], Reply(64, (4, 0x84), (5, 0x03)));

        Assert.Null(Spec(0x2A08).Query(link, 0x2A08));
    }

    [Fact]
    public void Catalog_never_lists_a_product_twice()
    {
        var ids = HeadsetCatalog.All.SelectMany(s => s.ProductIds.Select(p => (s.VendorId, p))).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
