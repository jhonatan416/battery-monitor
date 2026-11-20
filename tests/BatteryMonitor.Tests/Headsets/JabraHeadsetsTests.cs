using BatteryMonitor.Core.Protocols.Headsets;
using static BatteryMonitor.Tests.Headsets.ScriptedLink;

namespace BatteryMonitor.Tests.Headsets;

/// <summary>
/// Jabra GN protocol facts from the public HeadsetControl definitions (battery command also used by
/// Gadgetbridge): report 05, frame 05 dst src seq (type&lt;&lt;6 | len) cmd sub payload. GET (type 1)
/// of 12/02 HS_BATTERY is answered by a RESPONSE (type 3) with [flags (bit 0 charging)][level %].
/// The headset is at address 08 on USB, 04 behind a Link dongle.
/// </summary>
public class JabraHeadsetsTests
{
    private static HeadsetSpec Spec(ushort pid) => HeadsetCatalog.Find(JabraHeadsets.VendorId, pid)!;

    private static byte[] Get(byte address) => [0x05, address, 0x00, 0x01, 0x46, 0x12, 0x02];

    private static byte[] Response(byte address, byte flags, byte level, byte seq = 0x01, byte cmd = 0x12, int payload = 4) =>
        Reply(64, (0, 0x05), (1, 0x00), (2, address), (3, seq), (4, (byte)(0xC0 | (6 + payload))), (5, cmd), (6, 0x02),
            (7, flags), (8, level));

    [Theory]
    [InlineData((ushort)0x2519, (byte)0x08)] // Evolve2 65 Flex on USB
    [InlineData((ushort)0x2548, (byte)0x08)] // Evolve3 75
    [InlineData((ushort)0x24C7, (byte)0x04)] // Link 380: headset behind the dongle
    [InlineData((ushort)0x2E51, (byte)0x04)] // Link 390
    public void Battery_get_goes_to_the_headset_address(ushort pid, byte address)
    {
        var link = new ScriptedLink().On(Get(address), Response(address, 0x00, 64));

        Assert.Equal(new HeadsetStatus(64, false), Spec(pid).Query(link, pid));
        Assert.Equal([Convert.ToHexString(Get(address))], link.Sent);
    }

    [Fact]
    public void Charging_flag_is_bit_0()
    {
        var link = new ScriptedLink().On(Get(0x04), Response(0x04, 0x43, 30)); // charging + low + fast charging

        Assert.Equal(new HeadsetStatus(30, true), Spec(0x2E50).Query(link, 0x2E50));
    }

    [Fact]
    public void Events_and_stale_replies_are_skipped()
    {
        var evt = Reply(64, (0, 0x05), (3, 0x01), (4, 0x06), (5, 0x12), (6, 0x02)); // type 0 = event
        var link = new ScriptedLink().On(Get(0x04), evt, Response(0x04, 0, 10, seq: 0x07), Response(0x04, 0, 90));

        Assert.Equal(new HeadsetStatus(90, false), Spec(0x2E50).Query(link, 0x2E50));
    }

    [Theory]
    [InlineData((byte)0xFE, 4)] // NACK, e.g. headset offline
    [InlineData((byte)0x12, 1)] // payload too short
    public void Nack_or_short_reply_gives_nothing(byte cmd, int payload)
    {
        var link = new ScriptedLink().On(Get(0x04), Response(0x04, 0, 50, cmd: cmd, payload: payload));

        Assert.Null(Spec(0x2E50).Query(link, 0x2E50));
    }

    [Fact]
    public void Level_is_capped_at_100()
    {
        var link = new ScriptedLink().On(Get(0x08), Response(0x08, 0, 120));

        Assert.Equal(new HeadsetStatus(100, false), Spec(0x2517).Query(link, 0x2517));
    }

    [Theory]
    [InlineData((ushort)0x2517, (ushort)0x0001)]
    [InlineData((ushort)0x2546, (ushort)0x0005)]
    [InlineData((ushort)0x24CA, (ushort)0x0001)]
    [InlineData((ushort)0x2E57, (ushort)0x0005)]
    public void Vendor_collection_usage(ushort pid, ushort usage)
    {
        Assert.Equal(((ushort?)0xFF00, (ushort?)usage), (Spec(pid).UsagePage, Spec(pid).Usage));
    }
}
