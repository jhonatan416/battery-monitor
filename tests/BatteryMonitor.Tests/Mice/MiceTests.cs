using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Protocols.Headsets;
using BatteryMonitor.Core.Protocols.Mice;
using BatteryMonitor.Tests.Headsets;
using static BatteryMonitor.Tests.Headsets.ScriptedLink;

namespace BatteryMonitor.Tests.Mice;

/// <summary>
/// Wireless mice. SteelSeries facts from rivalcfg; HyperX Pulsefire Dart from two public
/// reverse-engineering projects; Pulsar / Kysona / VXE / ATK from hid-pulsar, madr and libatk-rs;
/// Glorious from mow and mxw. Every request is a "get".
/// </summary>
public class MiceTests
{
    private static HeadsetSpec Spec(ushort vid, ushort pid) => MiceCatalog.Find(vid, pid)!;

    // ---- SteelSeries (1038) ----

    [Theory]
    [InlineData((ushort)0x183A, (byte)0x92)] // Aerox 3 on its cable
    [InlineData((ushort)0x1838, (byte)0xD2)] // Aerox 3 through its receiver: command | 0x40
    [InlineData((ushort)0x1840, (byte)0xD2)] // Prime Wireless receiver
    [InlineData((ushort)0x185A, (byte)0x92)] // Aerox 9 wired
    public void Aerox_family_level_is_byte_1_in_steps_of_5(ushort pid, byte command)
    {
        var link = new ScriptedLink().On([0x00, command], Reply(2, (1, 0x11))); // (0x11 - 1) * 5 = 80

        Assert.Equal(new HeadsetStatus(80, false), Spec(0x1038, pid).Query(link, pid));
    }

    [Fact]
    public void Aerox_charging_is_bit_7()
    {
        var link = new ScriptedLink().On([0x00, 0xD2], Reply(2, (1, 0x8B))); // charging, (0x0B - 1) * 5 = 50

        Assert.Equal(new HeadsetStatus(50, true), Spec(0x1038, 0x1852).Query(link, 0x1852));
    }

    [Theory]
    [InlineData((byte)0x00)] // (0 - 1) * 5 < 0
    [InlineData((byte)0x17)] // (23 - 1) * 5 = 110
    public void Aerox_out_of_range_values_are_dropped(byte raw)
    {
        Assert.Null(Spec(0x1038, 0x1838).Query(new ScriptedLink().On([0x00, 0xD2], Reply(2, (1, raw))), 0x1838));
    }

    [Theory]
    [InlineData((ushort)0x1830)] // Rival 3 Wireless
    [InlineData((ushort)0x1872)] // Rival 3 Wireless Gen 2
    [InlineData((ushort)0x1726)] // Rival 650 Wireless receiver
    public void Rival_level_is_byte_0_and_charging_byte_2(ushort pid)
    {
        var link = new ScriptedLink().On([0x00, 0xAA, 0x01], Reply(3, (0, 47), (2, 1)));

        Assert.Equal(new HeadsetStatus(47, true), Spec(0x1038, pid).Query(link, pid));
    }

    [Fact]
    public void SteelSeries_mice_use_interface_3_except_the_Rival_650()
    {
        Assert.Equal(3, Spec(0x1038, 0x1838).Interface);
        Assert.Equal(3, Spec(0x1038, 0x1830).Interface);
        Assert.Equal(0, Spec(0x1038, 0x172B).Interface);
    }

    // ---- HyperX Pulsefire Dart (0951) ----

    [Fact]
    public void Pulsefire_Dart_heartbeat_reply_has_level_and_charging()
    {
        var link = new ScriptedLink().On([0x00, 0x51], Reply(64, (0, 0x02)), Reply(64, (0, 0x51), (4, 77), (5, 1)));

        Assert.Equal(new HeadsetStatus(77, true), Spec(0x0951, 0x16E1).Query(link, 0x16E1));
    }

    [Fact]
    public void Pulsefire_Dart_collections()
    {
        Assert.Equal(((ushort?)0xFF00, 2), (Spec(0x0951, 0x16E1).UsagePage, Spec(0x0951, 0x16E1).Interface)); // receiver
        Assert.Equal(((ushort?)0xFF13, 1), (Spec(0x0951, 0x16E2).UsagePage, Spec(0x0951, 0x16E2).Interface)); // cable
    }

    // ---- Pulsar / Kysona / VXE / ATK (3554, 373B) ----

    // 08 04 00.. checksum: byte 16 = 0x55 - sum(bytes 0..15)
    private static readonly byte[] AtkPower = [0x08, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x49];

    private static byte[] AtkReply(byte percent, byte usb, byte cmd = 0x04, int checksumDelta = 0)
    {
        var r = Reply(17, (0, 0x08), (1, cmd), (6, percent), (7, usb), (8, 0x0F), (9, 0xA0));
        byte sum = 0;
        for (int i = 0; i < 16; i++) sum += r[i];
        r[16] = (byte)(0x55 - sum + checksumDelta);
        return r;
    }

    [Theory]
    [InlineData((ushort)0x3554, (ushort)0xF508)] // Pulsar 1 kHz dongle
    [InlineData((ushort)0x3554, (ushort)0xF57C)] // Kysona M600 dongle
    [InlineData((ushort)0x3554, (ushort)0xF58A)] // VXE Dragonfly R1 Pro dongle
    [InlineData((ushort)0x373B, (ushort)0x1085)] // ATK VXE R1 SE dongle
    [InlineData((ushort)0x373B, (ushort)0x1040)] // VXE MAD R dongle
    public void Atk_power_command_gives_level_and_usb_flag(ushort vid, ushort pid)
    {
        var link = new ScriptedLink().On(AtkPower, AtkReply(10, 0x00, cmd: 0x0A), AtkReply(63, 0x01)); // event 0A is skipped

        Assert.Equal(new HeadsetStatus(63, true), Spec(vid, pid).Query(link, pid));
        Assert.Equal(1, Spec(vid, pid).Interface);
    }

    [Fact]
    public void Atk_reply_with_a_bad_checksum_or_out_of_range_values_is_ignored()
    {
        Assert.Null(Spec(0x3554, 0xF508).Query(new ScriptedLink().On(AtkPower, AtkReply(50, 0, checksumDelta: 1)), 0xF508));
        Assert.Null(Spec(0x3554, 0xF508).Query(new ScriptedLink().On(AtkPower, AtkReply(101, 0)), 0xF508));
        Assert.Null(Spec(0x3554, 0xF508).Query(new ScriptedLink().On(AtkPower, AtkReply(50, 2)), 0xF508));
    }

    // ---- Glorious (258A): feature SET of a "get 83" command, then feature GET ----

    private static byte[] GloriousGet()
    {
        var r = new byte[65];
        r[3] = 0x02;
        r[4] = 0x02;
        r[6] = 0x83;
        return r;
    }

    private static byte[] GloriousReply(byte state, byte percent, byte echo = 0x83) =>
        Reply(65, (1, state), (6, echo), (8, percent));

    [Theory]
    [InlineData((ushort)0x2022, false)] // Model O Wireless through its receiver
    [InlineData((ushort)0x2011, true)]  // on its cable: charging
    [InlineData((ushort)0x2023, false)] // Model D
    [InlineData((ushort)0x2024, false)] // Model O- (the two sources disagree which id is wired)
    [InlineData((ushort)0x2025, false)] // Model D-
    public void Glorious_awake_reply_gives_level(ushort pid, bool wired)
    {
        var link = new ScriptedLink().OnFeature(GloriousGet(), GloriousReply(0xA1, 66));

        Assert.Equal(new HeadsetStatus(66, wired), Spec(0x258A, pid).Query(link, pid));
        Assert.Equal(["F:" + Convert.ToHexString(GloriousGet())], link.Sent);
    }

    [Fact]
    public void Glorious_zero_is_shown_as_1_like_the_reference_tools()
    {
        var link = new ScriptedLink().OnFeature(GloriousGet(), GloriousReply(0xA1, 0));

        Assert.Equal(1, Spec(0x258A, 0x2022).Query(link, 0x2022)!.Value.Percent);
    }

    [Theory]
    [InlineData((byte)0xA4, (byte)0x83)] // asleep
    [InlineData((byte)0xA3, (byte)0x83)] // waking up
    [InlineData((byte)0xA1, (byte)0x00)] // reply to another command
    public void Glorious_asleep_or_unrelated_reply_gives_nothing(byte state, byte echo)
    {
        var link = new ScriptedLink().OnFeature(GloriousGet(), GloriousReply(state, 66, echo));

        Assert.Null(Spec(0x258A, 0x2022).Query(link, 0x2022));
    }

    // ---- catalog ----

    [Fact]
    public void Every_mouse_is_a_mouse_and_has_a_collection_hint()
    {
        Assert.All(MiceCatalog.All, s =>
        {
            Assert.Equal(DeviceKind.Mouse, s.Kind);
            Assert.True(s.UsagePage is not null || s.Interface is not null, s.Name);
        });
    }

    [Fact]
    public void Headsets_stay_headsets()
    {
        Assert.All(HeadsetCatalog.All, s => Assert.Equal(DeviceKind.Headset, s.Kind));
    }
}
