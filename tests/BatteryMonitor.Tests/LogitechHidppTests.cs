using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

/// <summary>Logitech HID++ 2.0 long reports (20 bytes): [0x11, device, feature index, function|swId, params...].</summary>
public class LogitechHidppTests
{
    private const byte Device = 0x01, Feature = 0x06, Function = 0x01;
    private static readonly byte FnSw = (byte)((Function << 4) | LogitechHidpp.SoftwareId);

    private static byte[] Reply(byte device, byte feature, byte fnSw, params byte[] parameters)
    {
        var r = new byte[LogitechHidpp.LongLength];
        r[0] = LogitechHidpp.LongReportId;
        r[1] = device;
        r[2] = feature;
        r[3] = fnSw;
        parameters.CopyTo(r, 4);
        return r;
    }

    [Fact]
    public void Request_is_a_20_byte_long_report_with_our_software_id()
    {
        byte[] req = LogitechHidpp.BuildRequest(Device, LogitechHidpp.RootIndex, 0x00, 0x10, 0x04);

        Assert.Equal(20, req.Length);
        Assert.Equal(new byte[] { 0x11, Device, 0x00, LogitechHidpp.SoftwareId, 0x10, 0x04 }, req[..6]);
        Assert.All(req[6..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void Matching_reply_returns_its_parameters()
    {
        var reply = LogitechHidpp.ParseReply(Reply(Device, Feature, FnSw, 0x4B, 0x08, 0x01), Device, Feature, Function);

        Assert.Equal(HidppStatus.Ok, reply.Status);
        Assert.Equal(new byte[] { 0x4B, 0x08, 0x01 }, reply.Parameters[..3]);
    }

    [Theory]
    [InlineData(0x02, Feature, 0x1B)]                              // another device on the receiver
    [InlineData(Device, 0x07, 0x1B)]                               // another feature
    [InlineData(Device, Feature, 0x10)]                            // a notification (software id 0)
    [InlineData(Device, Feature, 0x1A)]                            // another program's request
    public void Unrelated_reports_are_mismatches(byte device, byte feature, byte fnSw)
    {
        Assert.Equal(HidppStatus.Mismatch,
            LogitechHidpp.ParseReply(Reply(device, feature, fnSw), Device, Feature, Function).Status);
    }

    [Fact]
    public void Hidpp2_error_is_recognised()
    {
        // [0x11, device, 0xFF, feature, function|swId, error code]
        var reply = LogitechHidpp.ParseReply(Reply(Device, 0xFF, Feature, FnSw, 0x05), Device, Feature, Function);

        Assert.Equal(HidppStatus.Error, reply.Status);
    }

    [Fact]
    public void Hidpp1_error_from_the_receiver_is_recognised()
    {
        // Short report [0x10, device, 0x8F, feature, function|swId, error code]: e.g. device off.
        var r = new byte[7] { 0x10, Device, 0x8F, Feature, FnSw, 0x09, 0x00 };

        Assert.Equal(HidppStatus.Error, LogitechHidpp.ParseReply(r, Device, Feature, Function).Status);
    }

    [Theory]
    [InlineData(75, 0x00, 75, false)]  // discharging
    [InlineData(40, 0x01, 40, true)]   // charging
    [InlineData(40, 0x02, 40, true)]   // charging slowly
    [InlineData(99, 0x03, 100, false)] // full
    public void Unified_battery_status(byte stateOfCharge, byte chargingStatus, int percent, bool charging)
    {
        var p = new byte[16];
        p[0] = stateOfCharge;
        p[2] = chargingStatus;

        Assert.Equal(new LogitechBatteryReading(percent, charging), LogitechHidpp.DecodeUnifiedBattery(p));
    }

    [Fact]
    public void Unified_battery_error_gives_no_reading()
    {
        var p = new byte[16];
        p[0] = 50;
        p[2] = 0x04;

        Assert.Null(LogitechHidpp.DecodeUnifiedBattery(p));
    }

    [Theory]
    [InlineData(60, 0x00, 60, false)]  // discharging
    [InlineData(60, 0x01, 60, true)]   // recharging
    [InlineData(90, 0x02, 90, true)]   // almost full
    [InlineData(0, 0x03, 100, false)]  // charged
    [InlineData(30, 0x04, 30, true)]   // recharging slowly
    public void Legacy_battery_status(byte level, byte status, int percent, bool charging)
    {
        var p = new byte[16];
        p[0] = level;
        p[2] = status;

        Assert.Equal(new LogitechBatteryReading(percent, charging), LogitechHidpp.DecodeBatteryStatus(p));
    }

    [Theory]
    [InlineData(0x00)] // level 0 while discharging = unknown
    public void Legacy_battery_unknown_level_gives_no_reading(byte level)
    {
        var p = new byte[16];
        p[0] = level;

        Assert.Null(LogitechHidpp.DecodeBatteryStatus(p));
    }

    [Theory]
    [InlineData(0, DeviceKind.Keyboard)]
    [InlineData(3, DeviceKind.Mouse)]
    [InlineData(4, DeviceKind.Mouse)]  // touchpad
    [InlineData(5, DeviceKind.Mouse)]  // trackball
    [InlineData(8, DeviceKind.Headset)]
    [InlineData(12, DeviceKind.Gamepad)]
    [InlineData(7, DeviceKind.Other)]  // receiver
    public void Device_type_to_kind(byte type, DeviceKind kind)
    {
        Assert.Equal(kind, LogitechHidpp.KindFromDeviceType(type));
    }

    [Theory]
    [InlineData(0xC52B, true)] // Unifying
    [InlineData(0xC539, true)] // Lightspeed
    [InlineData(0xC548, true)] // Bolt
    [InlineData(0xC077, false)] // wired mouse
    public void Receivers_are_recognised(int productId, bool receiver)
    {
        Assert.Equal(receiver, LogitechHidpp.IsReceiver((ushort)productId));
    }

    [Fact]
    public void Receivers_are_asked_about_devices_1_to_6_and_direct_devices_use_index_FF()
    {
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, LogitechHidpp.DeviceIndices(0xC52B));
        Assert.Equal(new byte[] { 0xFF }, LogitechHidpp.DeviceIndices(0xC077));
    }

    [Fact]
    public void Name_chunks_are_joined_up_to_the_announced_length()
    {
        var chunk1 = "G Pro Wireless G"u8.ToArray(); // 16 bytes
        var chunk2 = new byte[16];
        "aming"u8.ToArray().CopyTo(chunk2, 0);

        Assert.Equal("G Pro Wireless Gaming", LogitechHidpp.JoinName(21, [chunk1, chunk2]));
    }
}
