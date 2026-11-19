using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

/// <summary>
/// Layouts from the public DualShock 4 / DualSense input report documentation (also used by the
/// Linux hid-playstation driver): the battery byte holds the level (0..10) in its low nibble and
/// the power state in its high nibble.
/// </summary>
public class PlayStationProtocolTests
{
    private static byte[] Report(byte id, int length, int offset, byte status)
    {
        var r = new byte[length];
        r[0] = id;
        r[offset] = status;
        return r;
    }

    // DualShock 4: USB report 0x01 byte 30, Bluetooth report 0x11 byte 32; bit 4 = cable plugged in.
    [Theory]
    [InlineData(0x07, 75, false)]  // on battery, level 7 -> 75 %
    [InlineData(0x0A, 100, false)] // on battery, full
    [InlineData(0x00, 5, false)]   // on battery, empty
    [InlineData(0x15, 55, true)]   // cable, level 5 -> charging 55 %
    [InlineData(0x1A, 100, true)]  // cable, level 10 -> still charging
    [InlineData(0x1B, 100, false)] // cable, level 11 -> fully charged
    public void DualShock4_usb_battery(byte status, int percent, bool charging)
    {
        var reading = PlayStationProtocol.Parse(PlayStationModel.DualShock4, Report(0x01, 64, 30, status));

        Assert.Equal(new PlayStationReading(percent, charging), reading);
    }

    [Fact]
    public void DualShock4_bluetooth_battery_is_two_bytes_later()
    {
        var reading = PlayStationProtocol.Parse(PlayStationModel.DualShock4, Report(0x11, 78, 32, 0x07));

        Assert.Equal(new PlayStationReading(75, false), reading);
    }

    // DualSense: USB report 0x01 byte 53, Bluetooth report 0x31 byte 54; high nibble 0 discharging,
    // 1 charging, 2 full, others are errors.
    [Theory]
    [InlineData(0x07, 75, false)]
    [InlineData(0x0A, 100, false)]
    [InlineData(0x15, 55, true)]
    [InlineData(0x2A, 100, false)]
    public void DualSense_usb_battery(byte status, int percent, bool charging)
    {
        var reading = PlayStationProtocol.Parse(PlayStationModel.DualSense, Report(0x01, 64, 53, status));

        Assert.Equal(new PlayStationReading(percent, charging), reading);
    }

    [Fact]
    public void DualSense_bluetooth_battery_is_one_byte_later()
    {
        var reading = PlayStationProtocol.Parse(PlayStationModel.DualSense, Report(0x31, 78, 54, 0x07));

        Assert.Equal(new PlayStationReading(75, false), reading);
    }

    [Theory]
    [InlineData(0xA5)] // voltage error
    [InlineData(0xF5)] // charging error
    public void DualSense_error_states_give_no_reading(byte status)
    {
        Assert.Null(PlayStationProtocol.Parse(PlayStationModel.DualSense, Report(0x01, 64, 53, status)));
    }

    [Fact]
    public void DualShock4_cable_error_levels_give_no_reading()
    {
        Assert.Null(PlayStationProtocol.Parse(PlayStationModel.DualShock4, Report(0x01, 64, 30, 0x1E)));
    }

    [Fact]
    public void DualSense_bluetooth_simple_report_has_no_battery()
    {
        // Over Bluetooth the controller sends a short report 0x01 until the full mode is enabled.
        Assert.Null(PlayStationProtocol.Parse(PlayStationModel.DualSense, Report(0x01, 10, 5, 0x07)));
    }

    [Theory]
    [InlineData(0x05C4, PlayStationModel.DualShock4)]
    [InlineData(0x09CC, PlayStationModel.DualShock4)]
    [InlineData(0x0BA0, PlayStationModel.DualShock4)] // wireless adapter
    [InlineData(0x0CE6, PlayStationModel.DualSense)]
    [InlineData(0x0DF2, PlayStationModel.DualSense)]  // DualSense Edge
    public void Known_product_ids(int productId, PlayStationModel model)
    {
        Assert.Equal(model, PlayStationProtocol.ModelFor((ushort)productId));
    }

    [Fact]
    public void Unknown_product_id_is_not_a_controller()
    {
        Assert.Null(PlayStationProtocol.ModelFor(0x1234));
    }
}
