using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

public class BluetoothBatteryTests
{
    [Fact]
    public void Connected_device_reports_its_level()
    {
        Assert.Equal(82, BluetoothBattery.Percent(batteryProperty: 82, connected: true));
    }

    [Fact]
    public void Disconnected_device_does_not_report_its_cached_level()
    {
        // Observed 2026-11-19: Xbox controller switched off, Windows still holds battery = 82.
        Assert.Null(BluetoothBattery.Percent(batteryProperty: 82, connected: false));
    }

    [Fact]
    public void Missing_connection_state_keeps_the_previous_behaviour()
    {
        // Older drivers may not expose the connected property: trust the battery value as before.
        Assert.Equal(60, BluetoothBattery.Percent(batteryProperty: 60, connected: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(101)]
    [InlineData(255)]
    public void Missing_or_invalid_battery_value_is_not_a_level(int? battery)
    {
        Assert.Null(BluetoothBattery.Percent(batteryProperty: (byte?)battery, connected: true));
    }
}
