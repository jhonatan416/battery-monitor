using BatteryMonitor.Core.Protocols.Headsets;
using static BatteryMonitor.Tests.Headsets.ScriptedLink;

namespace BatteryMonitor.Tests.Headsets;

/// <summary>Battery facts from the public HeadsetControl SteelSeries device definitions.</summary>
public class SteelSeriesHeadsetsTests
{
    private static HeadsetSpec Spec(ushort pid) => HeadsetCatalog.Find(SteelSeriesHeadsets.VendorId, pid)!;

    [Fact]
    public void Arctis_1_reports_byte_3_and_byte_2_value_1_means_off()
    {
        var link = new ScriptedLink().On([0x06, 0x12], Reply(8, (3, 75)));
        Assert.Equal(new HeadsetStatus(75, false), Spec(0x12B3).Query(link, 0x12B3));

        var off = new ScriptedLink().On([0x06, 0x12], Reply(8, (2, 0x01), (3, 75)));
        Assert.Null(Spec(0x12B3).Query(off, 0x12B3));
    }

    [Fact]
    public void Arctis_7_reports_byte_2()
    {
        var link = new ScriptedLink().On([0x06, 0x18], Reply(8, (2, 60)));

        Assert.Equal(new HeadsetStatus(60, false), Spec(0x1260).Query(link, 0x1260));
    }

    [Theory]
    [InlineData(0x64, 0)]
    [InlineData(0x9A, 100)]
    [InlineData(0x7F, 50)]
    [InlineData(0xC0, 100)] // above the range is clamped
    public void Arctis_9_maps_0x64_to_0x9A_onto_0_to_100(byte raw, int percent)
    {
        var link = new ScriptedLink().On([0x00, 0x20], Reply(12, (3, raw)));

        Assert.Equal(percent, Spec(0x12C2).Query(link, 0x12C2)?.Percent);
    }

    [Fact]
    public void Arctis_9_byte_4_value_1_means_charging()
    {
        var link = new ScriptedLink().On([0x00, 0x20], Reply(12, (3, 0x9A), (4, 0x01)));

        Assert.True(Spec(0x12C2).Query(link, 0x12C2)?.Charging);
    }

    [Fact]
    public void Arctis_Pro_Wireless_asks_status_then_level()
    {
        var link = new ScriptedLink()
            .On([0x41, 0xAA], Reply(2, (0, 0x04)))  // online
            .On([0x40, 0xAA], Reply(1, (0, 0x03))); // level 3 of 4

        Assert.Equal(new HeadsetStatus(75, false), Spec(0x1290).Query(link, 0x1290));
    }

    [Fact]
    public void Arctis_Pro_Wireless_offline_stops_after_the_status()
    {
        var link = new ScriptedLink().On([0x41, 0xAA], Reply(2, (0, 0x02)));

        Assert.Null(Spec(0x1290).Query(link, 0x1290));
        Assert.DoesNotContain("40AA", link.Sent);
    }

    [Theory]
    [InlineData(0x08, 4, 50, false)] // online, 4 of 8
    [InlineData(0x02, 8, 100, true)] // charging on cable
    public void Nova_Pro_Wireless_status_byte_15_and_level_byte_6_of_8(byte status, byte level, int percent, bool charging)
    {
        var link = new ScriptedLink().On([0x06, 0xB0], Reply(32, (6, level), (15, status)));

        Assert.Equal(new HeadsetStatus(percent, charging), Spec(0x12E0).Query(link, 0x12E0));
    }

    [Fact]
    public void Nova_Pro_Wireless_status_1_means_off()
    {
        var link = new ScriptedLink().On([0x06, 0xB0], Reply(32, (6, 8), (15, 0x01)));

        Assert.Null(Spec(0x12E0).Query(link, 0x12E0));
    }

    [Fact]
    public void Arctis_7_plus_maps_0_to_4_and_byte_3_is_charging()
    {
        var link = new ScriptedLink().On([0x00, 0xB0], Reply(16, (2, 3), (3, 0x01)));

        Assert.Equal(new HeadsetStatus(75, true), Spec(0x220E).Query(link, 0x220E));
    }

    [Fact]
    public void Nova_3P_offline_is_byte_1_value_2_and_level_is_byte_3()
    {
        var on = new ScriptedLink().On([0x00, 0xB0], Reply(16, (3, 42), (4, 0x01)));
        Assert.Equal(new HeadsetStatus(42, true), Spec(0x2269).Query(on, 0x2269));

        var off = new ScriptedLink().On([0x00, 0xB0], Reply(16, (1, 0x02), (3, 42)));
        Assert.Null(Spec(0x2269).Query(off, 0x2269));
    }

    [Fact]
    public void Nova_5_needs_at_least_16_bytes()
    {
        var link = new ScriptedLink().On([0x00, 0xB0], Reply(8, (3, 42)));

        Assert.Null(Spec(0x2232).Query(link, 0x2232));
    }

    [Theory]
    [InlineData(0x2202, 2, 50)]  // older firmware: 0..4 steps
    [InlineData(0x22A1, 63, 63)] // newer firmware: percent
    public void Nova_7_encoding_depends_on_the_product_id(int pid, byte raw, int percent)
    {
        var link = new ScriptedLink().On([0x00, 0xB0], Reply(16, (2, raw), (3, 0x03)));

        Assert.Equal(new HeadsetStatus(percent, false), Spec((ushort)pid).Query(link, (ushort)pid));
    }

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x02)]
    public void Nova_7_byte_3_values_1_and_2_mean_charging(byte state)
    {
        var link = new ScriptedLink().On([0x00, 0xB0], Reply(16, (2, 4), (3, state)));

        Assert.True(Spec(0x2202).Query(link, 0x2202)?.Charging);
    }

    [Fact]
    public void Nova_7_byte_3_value_0_means_off()
    {
        var link = new ScriptedLink().On([0x00, 0xB0], Reply(16, (2, 4), (3, 0x00)));

        Assert.Null(Spec(0x2202).Query(link, 0x2202));
    }

    [Fact]
    public void No_reply_means_no_reading()
    {
        Assert.Null(Spec(0x1260).Query(new ScriptedLink(), 0x1260));
    }
}
