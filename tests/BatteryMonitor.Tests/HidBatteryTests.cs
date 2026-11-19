using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

public class HidBatteryTests
{
    [Theory]
    [InlineData(50, 0, 100, 50)]
    [InlineData(0, 0, 100, 0)]
    [InlineData(100, 0, 100, 100)]
    [InlineData(255, 0, 255, 100)]
    [InlineData(128, 0, 255, 50)]
    [InlineData(6, 1, 11, 50)] // ranges need not start at zero
    public void Value_scales_from_the_logical_range_to_percent(int value, int min, int max, int percent)
    {
        Assert.Equal(percent, HidBattery.ToPercent(value, min, max));
    }

    [Theory]
    [InlineData(101, 0, 100)]
    [InlineData(-1, 0, 100)]
    [InlineData(5, 10, 10)] // empty range
    [InlineData(5, 10, 0)]  // inverted range
    public void Value_outside_a_valid_range_is_not_a_level(int value, int min, int max)
    {
        Assert.Null(HidBattery.ToPercent(value, min, max));
    }

    [Theory]
    [InlineData(HidBattery.GenericDevicePage, HidBattery.BatteryStrength, true)]
    [InlineData(HidBattery.BatterySystemPage, HidBattery.RemainingCapacity, true)]
    [InlineData(HidBattery.BatterySystemPage, HidBattery.AbsoluteStateOfCharge, true)]
    [InlineData(0x01, 0x30, false)] // Generic Desktop X axis
    [InlineData(0xFF00, 0x20, false)] // vendor page, same usage number
    public void Only_standard_battery_usages_are_recognised(int page, int usage, bool expected)
    {
        Assert.Equal(expected, HidBattery.IsBatteryUsage((ushort)page, (ushort)usage));
    }

    [Theory]
    [InlineData(0x01, 0x02, DeviceKind.Mouse)]
    [InlineData(0x01, 0x06, DeviceKind.Keyboard)]
    [InlineData(0x01, 0x05, DeviceKind.Gamepad)]
    [InlineData(0x01, 0x04, DeviceKind.Gamepad)]
    [InlineData(0x0C, 0x01, DeviceKind.Other)]
    [InlineData(0xFF00, 0x01, DeviceKind.Other)]
    public void Device_kind_comes_from_the_top_level_usage(int page, int usage, DeviceKind kind)
    {
        Assert.Equal(kind, HidBattery.KindFromUsage((ushort)page, (ushort)usage));
    }

    [Theory]
    [InlineData(@"\\?\hid#{00001812-0000-1000-8000-00805f9b34fb}&dev&vid_045e&pid_0b13#a&1#{4d1e55b2-f16f-11cf-88cb-001111000030}", true)]
    [InlineData(@"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002046d_pid&b023#8&1#{4d1e55b2-f16f-11cf-88cb-001111000030}", true)]
    [InlineData(@"\\?\hid#vid_046d&pid_c539&mi_02&col02#8&2#{4d1e55b2-f16f-11cf-88cb-001111000030}", false)]
    public void Bluetooth_hid_paths_are_detected(string path, bool bluetooth)
    {
        // Bluetooth devices are already reported by the Bluetooth provider: skip them to avoid duplicates.
        Assert.Equal(bluetooth, HidBattery.IsBluetoothPath(path));
    }
}
