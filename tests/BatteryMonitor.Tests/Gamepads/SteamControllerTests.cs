using BatteryMonitor.Core.Protocols.Gamepads;

namespace BatteryMonitor.Tests.Gamepads;

/// <summary>
/// Steam Controller reports (layouts from SDL's controller_structs.h, also handled by the Linux
/// hid-steam driver). Original controller: 64-byte report, version 01 00, type 04 = status event,
/// battery percent at byte 14. 2026 model: input report 0x43, charge state at byte 1, percent at byte 2.
/// </summary>
public class SteamControllerTests
{
    private static byte[] Status(byte level, int prefix = 0, byte type = 0x04)
    {
        var r = new byte[64 + prefix];
        r[prefix] = 0x01;
        r[prefix + 2] = type;
        r[prefix + 3] = 0x0B;
        r[prefix + 12] = 0x10; // voltage, ignored
        r[prefix + 13] = 0x0E;
        r[prefix + 14] = level;
        return r;
    }

    [Theory]
    [InlineData(0)] // as hidapi delivers it
    [InlineData(1)] // as Windows delivers an unnumbered report: leading 0x00
    public void Original_status_event_gives_the_level(int prefix)
    {
        Assert.Equal(new GamepadStatus(64, false), SteamController.Parse(SteamController.WirelessDongle, Status(64, prefix)));
    }

    [Fact]
    public void Original_input_state_report_has_no_battery()
    {
        Assert.Null(SteamController.Parse(SteamController.WirelessDongle, Status(64, 1, type: 0x01)));
    }

    [Fact]
    public void Original_level_above_100_is_ignored()
    {
        Assert.Null(SteamController.Parse(SteamController.Wired, Status(200, 1)));
    }

    [Theory]
    [InlineData((byte)1, (byte)80, 80, false)]  // discharging
    [InlineData((byte)2, (byte)35, 35, true)]   // charging
    [InlineData((byte)4, (byte)99, 100, false)] // charging done
    public void Model_2026_battery_report(byte charge, byte level, int percent, bool charging)
    {
        var report = new byte[64];
        report[0] = 0x43;
        report[1] = charge;
        report[2] = level;

        Assert.Equal(new GamepadStatus(percent, charging), SteamController.Parse(0x1302, report));
    }

    [Fact]
    public void Model_2026_other_reports_are_ignored()
    {
        var report = new byte[64];
        report[0] = 0x42;
        report[2] = 50;

        Assert.Null(SteamController.Parse(0x1304, report));
    }

    [Theory]
    [InlineData((ushort)0x1102, true)]
    [InlineData((ushort)0x1142, true)]
    [InlineData((ushort)0x1302, true)]
    [InlineData((ushort)0x1305, true)]
    [InlineData((ushort)0x1205, false)] // Steam Deck: Windows already shows its battery
    public void Known_products(ushort productId, bool known)
    {
        Assert.Equal(known, SteamController.NameFor(productId) is not null);
    }
}
