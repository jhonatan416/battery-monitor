using BatteryMonitor.Core.Protocols.Headsets;
using static BatteryMonitor.Tests.Headsets.ScriptedLink;

namespace BatteryMonitor.Tests.Headsets;

/// <summary>Battery facts from the public HeadsetControl HyperX device definitions.</summary>
public class HyperXHeadsetsTests
{
    private static HeadsetSpec Spec(ushort vid, ushort pid) => HeadsetCatalog.Find(vid, pid)!;

    // Cloud II Wireless (03F0:0696): requests 06 FF BB <cmd>, replies echo the 4-byte header.
    private static byte[] CloudII(byte cmd, params (int, byte)[] bytes) =>
        Reply(20, [(0, 0x06), (1, 0xFF), (2, 0xBB), (3, cmd), .. bytes]);

    [Fact]
    public void Cloud_II_wireless_level_is_byte_7_and_charging_byte_4()
    {
        var link = new ScriptedLink()
            .On([0x06, 0xFF, 0xBB, 0x02], CloudII(0x02, (7, 64)))
            .On([0x06, 0xFF, 0xBB, 0x03], CloudII(0x03, (4, 0x01)));

        Assert.Equal(new HeadsetStatus(64, true), Spec(0x03F0, 0x0696).Query(link, 0x0696));
    }

    [Fact]
    public void Cloud_II_wireless_ignores_replies_with_another_header()
    {
        var link = new ScriptedLink()
            .On([0x06, 0xFF, 0xBB, 0x02], CloudII(0x09, (7, 99)), CloudII(0x02, (7, 64)))
            .On([0x06, 0xFF, 0xBB, 0x03], CloudII(0x03));

        Assert.Equal(64, Spec(0x03F0, 0x0696).Query(link, 0x0696)?.Percent);
    }

    [Theory]
    [InlineData(3700, 16)]
    [InlineData(3800, 55)]
    [InlineData(3976, 100)]
    [InlineData(3000, 4)] // at or below 3648 mV: 0.00125 * mV
    public void Cloud_Flight_estimates_level_from_voltage(int millivolts, int percent)
    {
        var link = new ScriptedLink().On([0x21, 0xFF, 0x05],
            Reply(20, (3, (byte)(millivolts >> 8)), (4, (byte)millivolts)));

        Assert.Equal(new HeadsetStatus(percent, false), Spec(0x0951, 0x16C4).Query(link, 0x16C4));
    }

    [Fact]
    public void Cloud_Flight_above_4107_mV_is_charging_without_a_level()
    {
        var link = new ScriptedLink().On([0x21, 0xFF, 0x05], Reply(20, (3, 0x10), (4, 0x20))); // 4128 mV

        Assert.Equal(new HeadsetStatus(null, true), Spec(0x0951, 0x1723).Query(link, 0x1723));
    }

    [Fact]
    public void Cloud_Alpha_wireless_checks_connection_then_charging_then_level()
    {
        var link = new ScriptedLink()
            .On([0x21, 0xBB, 0x03], Reply(31, (3, 0x02)))  // connected
            .On([0x21, 0xBB, 0x0C], Reply(31, (3, 0x00)))  // not charging
            .On([0x21, 0xBB, 0x0B], Reply(31, (3, 70)));

        Assert.Equal(new HeadsetStatus(70, false), Spec(0x03F0, 0x098D).Query(link, 0x098D));
    }

    [Fact]
    public void Cloud_Alpha_wireless_offline_and_charging()
    {
        var off = new ScriptedLink().On([0x21, 0xBB, 0x03], Reply(31, (3, 0x01)));
        Assert.Null(Spec(0x03F0, 0x098D).Query(off, 0x098D));

        var charging = new ScriptedLink()
            .On([0x21, 0xBB, 0x03], Reply(31, (3, 0x02)))
            .On([0x21, 0xBB, 0x0C], Reply(31, (3, 0x01)));
        Assert.Equal(new HeadsetStatus(null, true), Spec(0x03F0, 0x098D).Query(charging, 0x098D));
    }

    [Fact]
    public void Cloud_Alpha_2_wireless_skips_unrelated_reports()
    {
        var link = new ScriptedLink()
            .On([0x52, 0x01], Reply(64, (0, 0xFB), (1, 0x01)), Reply(64, (0, 0x53), (1, 0x01), (2, 0x01)))
            .On([0x50, 0x02], Reply(64, (0, 0xFF), (1, 0x01)), Reply(64, (0, 0x51), (1, 0x02), (2, 88), (3, 0x01)));

        Assert.Equal(new HeadsetStatus(88, true), Spec(0x03F0, 0x08BE).Query(link, 0x08BE));
    }

    [Fact]
    public void Cloud_Alpha_2_wireless_offline_and_invalid_level()
    {
        var off = new ScriptedLink().On([0x52, 0x01], Reply(64, (0, 0x53), (1, 0x01), (2, 0x00)));
        Assert.Null(Spec(0x03F0, 0x08BE).Query(off, 0x08BE));

        var bad = new ScriptedLink()
            .On([0x52, 0x01], Reply(64, (0, 0x53), (1, 0x01), (2, 0x01)))
            .On([0x50, 0x02], Reply(64, (0, 0x51), (1, 0x02), (2, 150)));
        Assert.Null(Spec(0x03F0, 0x08BE).Query(bad, 0x08BE));
    }
}
