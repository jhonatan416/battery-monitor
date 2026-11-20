using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

/// <summary>
/// Galaxy Buds+ and later over their Bluetooth serial channel (facts from GalaxyBudsClient and
/// Gadgetbridge). Frame: FD, header (little endian: size in the low 10 bits = id + payload + CRC),
/// id, payload, CRC-16 (XMODEM: poly 1021, init 0) of id + payload with the low byte first, DD.
/// 60 STATUS_UPDATED: [1] left %, [2] right %, [7] charging bits (10 left, 04 right).
/// 61 EXTENDED_STATUS_UPDATED: [2] left %, [3] right %. The buds send both by themselves.
/// </summary>
public class GalaxyBudsTests
{
    // Real 61 message quoted in GalaxyBudsClient's Crc16.cs: id + payload, then CRC 0F F3.
    private static readonly byte[] CapturedExtendedStatus =
        [0xFD, 0x11, 0x00, 0x61, 0x02, 0x00, 0x4B, 0x5F, 0x01, 0x00, 0x00, 0x00, 0x01, 0x05, 0x00, 0x02, 0x00, 0x13, 0x0F, 0xF3, 0xDD];

    private static byte[] Message(byte id, params byte[] payload)
    {
        byte[] body = [id, .. payload];
        ushort crc = GalaxyBuds.Crc16(body);
        int size = body.Length + 2;
        return [0xFD, (byte)size, (byte)(size >> 8), .. body, (byte)crc, (byte)(crc >> 8), 0xDD];
    }

    [Fact]
    public void Crc_matches_the_captured_message()
    {
        Assert.Equal(0xF30F, GalaxyBuds.Crc16(CapturedExtendedStatus[3..^3]));
    }

    [Fact]
    public void Captured_extended_status_gives_the_lower_earbud()
    {
        Assert.Equal(new GalaxyBudsReading(75, false), GalaxyBuds.LatestReading(CapturedExtendedStatus));
    }

    [Theory]
    [InlineData((byte)0x00, false)]
    [InlineData((byte)0x10, true)]  // left charging
    [InlineData((byte)0x04, true)]  // right charging
    [InlineData((byte)0x01, false)] // only the case charges
    public void Status_update_with_charging_bits(byte chargingBits, bool charging)
    {
        var msg = Message(0x60, 0x05, 62, 80, 0x01, 0x00, 0x22, 90, chargingBits);

        Assert.Equal(new GalaxyBudsReading(62, charging), GalaxyBuds.LatestReading(msg));
    }

    [Fact]
    public void The_last_message_in_the_stream_wins()
    {
        byte[] stream = [.. Message(0x61, 0x02, 0x00, 50, 60, 0, 0, 0, 70), .. Message(0x99, 1, 2, 3), .. Message(0x60, 0x05, 48, 49, 1, 0, 0, 70, 0)];

        Assert.Equal(new GalaxyBudsReading(48, false), GalaxyBuds.LatestReading(stream));
    }

    [Fact]
    public void Earbud_reporting_0_is_ignored_when_the_other_has_a_level()
    {
        Assert.Equal(new GalaxyBudsReading(80, false), GalaxyBuds.LatestReading(Message(0x60, 0x05, 0, 80, 0, 0, 0, 90, 0)));
        Assert.Null(GalaxyBuds.LatestReading(Message(0x60, 0x05, 0, 0, 0, 0, 0, 90, 0)));
    }

    [Fact]
    public void Bad_crc_bad_end_byte_or_cut_frames_are_ignored()
    {
        var good = Message(0x60, 0x05, 62, 80, 1, 0, 0, 90, 0);
        var badCrc = (byte[])good.Clone();
        badCrc[^2] ^= 0x01;
        var badEnd = (byte[])good.Clone();
        badEnd[^1] = 0x00;

        Assert.Null(GalaxyBuds.LatestReading(badCrc));
        Assert.Null(GalaxyBuds.LatestReading(badEnd));
        Assert.Null(GalaxyBuds.LatestReading(good[..^4]));
    }

    [Fact]
    public void Garbage_before_a_frame_is_skipped()
    {
        byte[] stream = [0x00, 0xFD, 0x7F, 0x12, .. Message(0x60, 0x05, 33, 34, 1, 0, 0, 0, 0)];

        Assert.Equal(new GalaxyBudsReading(33, false), GalaxyBuds.LatestReading(stream));
    }

    [Fact]
    public void Levels_above_100_are_ignored()
    {
        Assert.Null(GalaxyBuds.LatestReading(Message(0x60, 0x05, 120, 130, 1, 0, 0, 0, 0)));
    }

    [Theory]
    [InlineData("Galaxy Buds2 Pro (A1B2)", true)]
    [InlineData("Galaxy Buds FE", true)]
    [InlineData("Galaxy Buds Live", true)]
    [InlineData("Galaxy Buds (1234)", false)] // original 2019 Buds use another framing, not supported
    [InlineData("AirPods Pro", false)]
    public void Supported_names(string name, bool supported)
    {
        Assert.Equal(supported, GalaxyBuds.IsSupportedName(name));
    }
}
