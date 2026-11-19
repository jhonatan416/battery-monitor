using BatteryMonitor.Core.Protocols.Gamepads;

namespace BatteryMonitor.Tests.Gamepads;

/// <summary>
/// 8BitDo input reports (layout from SDL's HIDAPI 8BitDo driver): byte 14 holds the level in
/// percent (bits 0-6) and a charging flag (bit 7); report id at byte 0.
/// </summary>
public class EightBitDoTests
{
    private static byte[] Report(byte id, int length, byte battery)
    {
        var r = new byte[length];
        r[0] = id;
        if (length > 14) r[14] = battery;
        return r;
    }

    [Theory]
    [InlineData((byte)0x04, (byte)72, 72, false)]   // enhanced USB report
    [InlineData((byte)0x03, (byte)0x9E, 30, true)]  // standard report, charging, 30 %
    [InlineData((byte)0x04, (byte)0xE4, 100, false)] // charging flag with 100 % means charged
    [InlineData((byte)0x04, (byte)0, 0, false)]
    public void Ultimate_3_reads_byte_14(byte id, byte battery, int percent, bool charging)
    {
        var status = EightBitDo.Parse(EightBitDo.Ultimate3, Report(id, 64, battery));

        Assert.Equal(new GamepadStatus(percent, charging), status);
    }

    [Fact]
    public void Ultimate_2_wireless_needs_the_long_report_of_firmware_1_03()
    {
        Assert.Equal(new GamepadStatus(55, false), EightBitDo.Parse(EightBitDo.Ultimate2Wireless, Report(0x04, 34, 55)));
        Assert.Null(EightBitDo.Parse(EightBitDo.Ultimate2Wireless, Report(0x04, 33, 55)));
    }

    [Theory]
    [InlineData((ushort)0x301B)] // Ultimate 2C: no battery in its report
    [InlineData((ushort)0x6009)] // Pro 3: needs a feature request first, not supported
    public void Models_without_a_passive_battery_byte_are_not_read(ushort productId)
    {
        Assert.Null(EightBitDo.NameFor(productId));
        Assert.Null(EightBitDo.Parse(productId, Report(0x04, 64, 50)));
    }

    [Theory]
    [InlineData((byte)0x02)] // unknown report id
    [InlineData((byte)0x00)]
    public void Other_reports_are_ignored(byte id)
    {
        Assert.Null(EightBitDo.Parse(EightBitDo.Ultimate3, Report(id, 64, 50)));
    }

    [Fact]
    public void Short_or_out_of_range_reports_are_ignored()
    {
        Assert.Null(EightBitDo.Parse(EightBitDo.Ultimate3, Report(0x04, 9, 0)));
        Assert.Null(EightBitDo.Parse(EightBitDo.Ultimate3, Report(0x04, 64, 0x7F))); // 127 %
    }

    [Fact]
    public void Names()
    {
        Assert.Equal("8BitDo Ultimate 3", EightBitDo.NameFor(EightBitDo.Ultimate3));
        Assert.Equal("8BitDo Ultimate 2 Wireless", EightBitDo.NameFor(EightBitDo.Ultimate2Wireless));
    }
}
