using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Protocols.Headsets;
using BatteryMonitor.Core.Protocols.Mice;
using BatteryMonitor.Tests.Headsets;
using static BatteryMonitor.Tests.Headsets.ScriptedLink;

namespace BatteryMonitor.Tests.Mice;

/// <summary>
/// Corsair Slipstream dongles (facts from ckb-next and OpenLinkHub): Bragi "get property" 08|child 02
/// prop, sent as an unnumbered output report on interface 1; the only paired device is child 1 (09).
/// Reply: [1] 02, [2] error (0 = ok), [3..5] value (little endian). Property 0F = level in tenths of a
/// percent, 10 = status (1 charging, 2 discharging, 3 charged). No "software mode" switch is sent.
/// </summary>
public class CorsairMiceTests
{
    private static HeadsetSpec Spec(ushort pid) => MiceCatalog.Find(CorsairMice.VendorId, pid)!;

    private static readonly byte[] GetLevel = [0x00, 0x09, 0x02, 0x0F];
    private static readonly byte[] GetStatus = [0x00, 0x09, 0x02, 0x10];

    private static byte[] Value(int value, byte error = 0x00) =>
        Reply(64, (0, 0x09), (1, 0x02), (2, error), (3, (byte)value), (4, (byte)(value >> 8)), (5, (byte)(value >> 16)));

    [Theory]
    [InlineData(2, false)] // discharging
    [InlineData(1, true)]  // charging
    [InlineData(3, false)] // charged
    public void Level_in_tenths_and_status(int status, bool charging)
    {
        var link = new ScriptedLink().On(GetLevel, Value(735)).On(GetStatus, Value(status));

        Assert.Equal(new HeadsetStatus(73, charging), Spec(0x1BDC).Query(link, 0x1BDC));
        Assert.Equal(["0009020F", "00090210"], link.Sent);
    }

    [Fact]
    public void Only_get_requests_are_sent()
    {
        var link = new ScriptedLink().On(GetLevel, Value(500)).On(GetStatus, Value(2));

        Spec(0x2B1B).Query(link, 0x2B1B);

        Assert.All(link.Sent, request => Assert.Equal("02", request.Substring(4, 2))); // GET, never SET (01)
    }

    [Theory]
    [InlineData(0x05)] // error code, e.g. not supported or no device paired
    [InlineData(0x09)]
    public void Error_code_gives_nothing(byte error)
    {
        var link = new ScriptedLink().On(GetLevel, Value(500, error));

        Assert.Null(Spec(0x1BA6).Query(link, 0x1BA6));
    }

    [Fact]
    public void Zero_level_gives_nothing_like_the_reference_tools()
    {
        var link = new ScriptedLink().On(GetLevel, Value(0)).On(GetStatus, Value(2));

        Assert.Null(Spec(0x2B00).Query(link, 0x2B00));
    }

    [Fact]
    public void Level_is_capped_at_100_and_unknown_status_means_not_charging()
    {
        var link = new ScriptedLink().On(GetLevel, Value(1005)).On(GetStatus, Value(7, error: 0x05));

        Assert.Equal(new HeadsetStatus(100, false), Spec(0x1B7F).Query(link, 0x1B7F));
    }

    [Fact]
    public void Other_reports_are_skipped()
    {
        var other = Reply(64, (0, 0x09), (1, 0x12)); // another command's reply
        var link = new ScriptedLink().On(GetLevel, other, Value(420)).On(GetStatus, Value(2));

        Assert.Equal(new HeadsetStatus(42, false), Spec(0x1B65).Query(link, 0x1B65));
    }

    [Theory]
    [InlineData((ushort)0x1B7F, "Corsair Dark Core RGB PRO SE", DeviceKind.Mouse)]
    [InlineData((ushort)0x1B81, "Corsair Dark Core RGB PRO", DeviceKind.Mouse)]
    [InlineData((ushort)0x1B65, "Corsair Harpoon RGB Wireless", DeviceKind.Mouse)]
    [InlineData((ushort)0x1BDC, "Corsair Slipstream device", DeviceKind.Other)] // generic dongle: mouse or keyboard
    [InlineData((ushort)0x1BA6, "Corsair Slipstream device", DeviceKind.Other)]
    [InlineData((ushort)0x2B00, "Corsair Slipstream device", DeviceKind.Other)]
    [InlineData((ushort)0x2B1B, "Corsair Slipstream device", DeviceKind.Other)]
    public void Dongles(ushort pid, string name, DeviceKind kind)
    {
        Assert.Equal((name, kind, (int?)1), (Spec(pid).Name, Spec(pid).Kind, Spec(pid).Interface));
    }

    [Fact]
    public void Dongles_do_not_clash_with_corsair_headsets()
    {
        foreach (var spec in MiceCatalog.All.Where(s => s.VendorId == CorsairMice.VendorId))
            foreach (ushort pid in spec.ProductIds)
                Assert.Null(HeadsetCatalog.Find(CorsairMice.VendorId, pid));
    }
}
