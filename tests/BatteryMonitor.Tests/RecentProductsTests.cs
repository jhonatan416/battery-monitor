using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Monitoring;

namespace BatteryMonitor.Tests;

/// <summary>
/// USB products the dedicated providers reported recently. HeadsetControl results are skipped only
/// for these, so a headset our own code cannot read (e.g. newer firmware) still shows up through
/// HeadsetControl, without showing twice when our code does read it.
/// </summary>
public class RecentProductsTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static DeviceBattery Device(ushort vid, ushort pid) =>
        new($"x:{vid:X4}{pid:X4}", "X", DeviceKind.Headset, "USB", 50, Usb: new UsbProduct(vid, pid));

    [Fact]
    public void Reported_product_is_known_and_others_are_not()
    {
        var recent = new RecentProducts(new Clock(), TimeSpan.FromMinutes(2));

        recent.Record([Device(0x1038, 0x12AD)]);

        Assert.True(recent.Contains(0x1038, 0x12AD));
        Assert.False(recent.Contains(0x1038, 0x12AE));
        Assert.False(recent.Contains(0x046D, 0x12AD));
    }

    [Fact]
    public void Product_stays_known_for_the_window_like_the_grace_period()
    {
        var clock = new Clock();
        var recent = new RecentProducts(clock, TimeSpan.FromMinutes(2));
        recent.Record([Device(0x1038, 0x12AD)]);

        clock.Now += TimeSpan.FromMinutes(2);
        Assert.True(recent.Contains(0x1038, 0x12AD)); // one missed poll does not let HeadsetControl show a duplicate

        clock.Now += TimeSpan.FromSeconds(1);
        Assert.False(recent.Contains(0x1038, 0x12AD));
    }

    [Fact]
    public void Devices_without_a_usb_id_are_ignored()
    {
        var recent = new RecentProducts(new Clock(), TimeSpan.FromMinutes(2));

        recent.Record([new DeviceBattery("bt:1", "Xbox Wireless Controller", DeviceKind.Gamepad, "Bluetooth", 80)]);

        Assert.False(recent.Contains(0, 0));
    }

    [Fact]
    public void Usb_id_is_optional_on_existing_devices()
    {
        Assert.Null(new DeviceBattery("bt:1", "X", DeviceKind.Other, "Bluetooth", 10).Usb);
    }
}
