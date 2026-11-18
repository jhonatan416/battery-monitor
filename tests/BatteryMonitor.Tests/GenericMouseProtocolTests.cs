using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

public class GenericMouseProtocolTests
{
    [Fact]
    public void Report_5_captured_from_the_memzuiox_receiver_decodes_to_88_percent()
    {
        // Captured 2026-11-17 from 248A:8366 COL04, feature report 5.
        byte[] report = [0x04, 0x58, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00];

        Assert.Equal(88, GenericMouseProtocol.ParseBattery(report));
    }

    [Fact]
    public void Value_above_100_is_not_a_battery_level()
    {
        byte[] report = [0x04, 0xC8, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00];

        Assert.Null(GenericMouseProtocol.ParseBattery(report));
    }

    [Fact]
    public void Short_report_is_ignored()
    {
        Assert.Null(GenericMouseProtocol.ParseBattery([0x04]));
    }

    [Fact]
    public void All_zero_report_means_mouse_is_off()
    {
        // The receiver answers with zeros while the mouse is switched off or asleep.
        Assert.Null(GenericMouseProtocol.ParseBattery(new byte[8]));
    }
}
