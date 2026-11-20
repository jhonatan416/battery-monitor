using BatteryMonitor.Core.Protocols.Headsets;
using static BatteryMonitor.Tests.Headsets.ScriptedLink;

namespace BatteryMonitor.Tests.Headsets;

/// <summary>
/// Logitech PRO X 2 LIGHTSPEED (older-firmware battery request), ASTRO A50 Gen 4 and Gen 5, and
/// MCHOSE X9 / V9 Pro. Facts from the public HeadsetControl definitions (PRO X 2 also from the
/// hid-logitech-headset kernel patch). Battery "get" requests only.
/// </summary>
public class MoreHeadsetsTests
{
    private static HeadsetSpec Spec(ushort vid, ushort pid) => HeadsetCatalog.Find(vid, pid)!;

    // ---- PRO X 2 LIGHTSPEED (046D:0AF7) ----

    private static readonly byte[] ProX2Get = [0x51, 0x08, 0x00, 0x03, 0x1A, 0x00, 0x03, 0x00, 0x04, 0x0A];

    [Theory]
    [InlineData((byte)0x02, true)]
    [InlineData((byte)0x01, false)]
    public void Pro_x2_level_is_byte_10_and_charging_byte_12(byte state, bool charging)
    {
        var other = Reply(64, (0, 0x51), (1, 0x0B), (8, 0x05)); // another feature's reply
        var link = new ScriptedLink().On(ProX2Get, other, Reply(64, (0, 0x51), (1, 0x0B), (8, 0x04), (10, 88), (12, state)));

        Assert.Equal(new HeadsetStatus(88, charging), Spec(0x046D, 0x0AF7).Query(link, 0x0AF7));
    }

    [Fact]
    public void Pro_x2_level_above_100_is_ignored()
    {
        var link = new ScriptedLink().On(ProX2Get, Reply(64, (0, 0x51), (1, 0x0B), (8, 0x04), (10, 120)));

        Assert.Null(Spec(0x046D, 0x0AF7).Query(link, 0x0AF7));
    }

    // ---- ASTRO A50 Gen 5 (046D:0B1C): 02 0C LEN 00 CMD HANDLE payload ----

    private static readonly byte[] A50Get = [0x02, 0x0C, 0x03, 0x00, 0x06, 0x0C];

    [Fact]
    public void Astro_a50_skips_pushes_and_bare_acks()
    {
        var volumePush = Reply(64, (0, 0x02), (1, 0x0C), (2, 0x06), (4, 0x08), (6, 40));
        var bareAck = Reply(64, (0, 0x02), (1, 0x0C), (2, 0x03), (4, 0x06));
        var battery = Reply(64, (0, 0x02), (1, 0x0C), (2, 0x06), (4, 0x06), (6, 73), (8, 0x01));
        var link = new ScriptedLink().On(A50Get, volumePush, bareAck, battery);

        Assert.Equal(new HeadsetStatus(73, true), Spec(0x046D, 0x0B1C).Query(link, 0x0B1C));
    }

    // ---- ASTRO A50 Gen 4 (9886:002C): 02 CMD -> 02 STATUS LEN payload ----

    [Fact]
    public void Astro_a50_gen4_reads_the_battery_only_while_the_headset_is_linked()
    {
        var link = new ScriptedLink()
            .On([0x02, 0x54], Reply(64, (0, 0x02), (1, 0x02), (2, 1), (3, 0x02)))  // linked
            .On([0x02, 0x7C], Reply(64, (0, 0x02), (1, 0x02), (2, 1), (3, 0xB2))); // charging, 50 %

        Assert.Equal(new HeadsetStatus(50, true), Spec(0x9886, 0x002C).Query(link, 0x002C));
        Assert.Equal(["0254", "027C"], link.Sent);
    }

    [Fact]
    public void Astro_a50_gen4_headset_off_gives_nothing_and_skips_the_stale_level()
    {
        var link = new ScriptedLink()
            .On([0x02, 0x54], Reply(64, (0, 0x02), (1, 0x02), (2, 1), (3, 0x00)))
            .On([0x02, 0x7C], Reply(64, (0, 0x02), (1, 0x02), (2, 1), (3, 0x40)));

        Assert.Null(Spec(0x9886, 0x002C).Query(link, 0x002C));
        Assert.Equal(["0254"], link.Sent);
    }

    [Fact]
    public void Astro_a50_gen4_error_status_gives_nothing()
    {
        var link = new ScriptedLink()
            .On([0x02, 0x54], Reply(64, (0, 0x02), (1, 0x02), (2, 1), (3, 0x02)))
            .On([0x02, 0x7C], Reply(64, (0, 0x02), (1, 0x01), (2, 1), (3, 0x32)));

        Assert.Null(Spec(0x9886, 0x002C).Query(link, 0x002C));
    }

    // ---- MCHOSE (3837:6045 X9, 291D:385D V9 Pro): 55 65 01 -> 55 65 VV 02 ----

    [Theory]
    [InlineData((ushort)0x3837, (ushort)0x6045)]
    [InlineData((ushort)0x291D, (ushort)0x385D)]
    public void Mchose_battery_field_is_02(ushort vid, ushort pid)
    {
        var heartbeat = Reply(64, (0, 0x55), (1, 0x65), (2, 0x10), (3, 0x07));
        var link = new ScriptedLink().On([0x55, 0x65, 0x01], heartbeat, Reply(64, (0, 0x55), (1, 0x65), (2, 50), (3, 0x02)));

        Assert.Equal(new HeadsetStatus(50, false), Spec(vid, pid).Query(link, pid));
    }

    [Fact]
    public void Collections()
    {
        Assert.Equal(((ushort?)0xFFA0, (ushort?)0x0001), (Spec(0x046D, 0x0AF7).UsagePage, Spec(0x046D, 0x0AF7).Usage));
        Assert.Equal(((ushort?)0xFF32, (ushort?)0x0074), (Spec(0x046D, 0x0B1C).UsagePage, Spec(0x046D, 0x0B1C).Usage));
        Assert.Equal(((ushort?)0xFF32, (ushort?)0x0074), (Spec(0x9886, 0x002C).UsagePage, Spec(0x9886, 0x002C).Usage));
        Assert.Equal(((ushort?)0xFF90, (ushort?)0x0001), (Spec(0x3837, 0x6045).UsagePage, Spec(0x3837, 0x6045).Usage));
    }
}
