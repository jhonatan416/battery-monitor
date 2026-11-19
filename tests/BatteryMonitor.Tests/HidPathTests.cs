using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

public class HidPathTests
{
    [Theory]
    // USB / receiver
    [InlineData(@"\\?\hid#vid_1532&pid_00df&mi_00#8&30674f22&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x1532, 0x00DF)]
    // Bluetooth LE (HID over GATT), format of the owner's Xbox controller HID node
    [InlineData(@"\\?\hid#{00001812-0000-1000-8000-00805f9b34fb}&dev&vid_045e&pid_0b13&rev_0509&f46ad78d0c30&ig_00#c&1d21ec5c&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x045E, 0x0B13)]
    // Classic Bluetooth HID: vendor id source "0002" + vendor id (e.g. a DualShock 4)
    [InlineData(@"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&09cc#8&1#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x054C, 0x09CC)]
    // A real vendor id that starts with 02 must not lose its digits
    [InlineData(@"\\?\hid#vid_0201&pid_1234#8&1#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x0201, 0x1234)]
    public void Vendor_and_product_ids_are_parsed_from_usb_and_bluetooth_paths(string path, int vid, int pid)
    {
        Assert.Equal(((ushort)vid, (ushort)pid), HidPath.TryParseIds(path));
    }

    [Fact]
    public void Path_without_ids_gives_null()
    {
        Assert.Null(HidPath.TryParseIds(@"\\?\hid#convertedDevice&col01#5&1#{4d1e55b2-f16f-11cf-88cb-001111000030}"));
    }
}
