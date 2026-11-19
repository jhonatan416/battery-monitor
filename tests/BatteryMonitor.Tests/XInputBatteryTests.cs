using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

public class XInputBatteryTests
{
    // Values from the Microsoft XInput documentation (XINPUT_BATTERY_INFORMATION).
    private const byte Disconnected = 0x00, Wired = 0x01, Alkaline = 0x02, NiMH = 0x03, Unknown = 0xFF;
    private const byte Empty = 0x00, Low = 0x01, Medium = 0x02, Full = 0x03;

    [Theory]
    [InlineData(Empty, 5)]
    [InlineData(Low, 30)]
    [InlineData(Medium, 65)]
    [InlineData(Full, 100)]
    public void Coarse_levels_map_to_approximate_percents(byte level, int percent)
    {
        var reading = XInputBattery.Decode(Alkaline, level, bluetoothGamepadPresent: false);

        Assert.NotNull(reading);
        Assert.Equal(percent, reading.Value.Percent);
        Assert.True(reading.Value.Approximate);
    }

    [Theory]
    [InlineData(NiMH)]
    [InlineData(Unknown)]
    public void Rechargeable_and_unknown_battery_types_are_reported(byte type)
    {
        Assert.NotNull(XInputBattery.Decode(type, Medium, bluetoothGamepadPresent: false));
    }

    [Theory]
    [InlineData(Disconnected)]
    [InlineData(Wired)] // wired controller: it has no battery
    public void Controllers_without_a_battery_are_not_reported(byte type)
    {
        Assert.Null(XInputBattery.Decode(type, Full, bluetoothGamepadPresent: false));
    }

    [Fact]
    public void Level_outside_the_documented_values_is_not_reported()
    {
        Assert.Null(XInputBattery.Decode(Alkaline, 0x04, bluetoothGamepadPresent: false));
    }

    [Fact]
    public void Battery_controllers_are_skipped_when_a_bluetooth_gamepad_is_already_reported()
    {
        // A Bluetooth Xbox controller is also an XInput slot; the Bluetooth provider gives its exact
        // percent, so XInput must not add a second, coarser icon for the same controller.
        Assert.Null(XInputBattery.Decode(Alkaline, Full, bluetoothGamepadPresent: true));
    }
}
