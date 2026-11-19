namespace BatteryMonitor.Core.Protocols;

public enum PlayStationModel { DualShock4, DualSense }

public readonly record struct PlayStationReading(int Percent, bool Charging);

/// <summary>
/// Battery byte of the DualShock 4 and DualSense input reports, from the public report layouts
/// (as documented for the Linux hid-playstation driver). Read-only: the controllers send these
/// reports continuously; nothing is written to them.
/// </summary>
public static class PlayStationProtocol
{
    public const ushort SonyVendorId = 0x054C;

    public static PlayStationModel? ModelFor(ushort productId) => productId switch
    {
        0x05C4 or 0x09CC or 0x0BA0 => PlayStationModel.DualShock4, // v1, v2, wireless adapter
        0x0CE6 or 0x0DF2 => PlayStationModel.DualSense,          // DualSense, DualSense Edge
        _ => null,
    };

    /// <param name="report">Input report including its report id at index 0.</param>
    public static PlayStationReading? Parse(PlayStationModel model, ReadOnlySpan<byte> report)
    {
        int? offset = BatteryOffset(model, report);
        if (offset is not int i || report.Length <= i) return null; // e.g. Bluetooth "simple" report
        byte status = report[i];
        return model == PlayStationModel.DualShock4 ? DualShock4(status) : DualSense(status);
    }

    private static int? BatteryOffset(PlayStationModel model, ReadOnlySpan<byte> report) => (model, report[0]) switch
    {
        (PlayStationModel.DualShock4, 0x01) => 30, // USB
        (PlayStationModel.DualShock4, 0x11) => 32, // Bluetooth
        (PlayStationModel.DualSense, 0x01) => 53,  // USB
        (PlayStationModel.DualSense, 0x31) => 54,  // Bluetooth
        _ => null,
    };

    // Low nibble: level 0..10 (11 = full while on cable). Bit 4: cable plugged in.
    private static PlayStationReading? DualShock4(byte status)
    {
        int level = status & 0x0F;
        bool cable = (status & 0x10) != 0;
        if (!cable) return new PlayStationReading(level < 10 ? level * 10 + 5 : 100, Charging: false);
        return level switch
        {
            < 10 => new PlayStationReading(level * 10 + 5, Charging: true),
            10 => new PlayStationReading(100, Charging: true),
            11 => new PlayStationReading(100, Charging: false), // fully charged
            _ => null, // error states
        };
    }

    // Low nibble: level 0..10. High nibble: 0 discharging, 1 charging, 2 full, others are errors.
    private static PlayStationReading? DualSense(byte status)
    {
        int level = status & 0x0F;
        int percent = Math.Min(level * 10 + 5, 100);
        return (status >> 4) switch
        {
            0x0 => new PlayStationReading(percent, Charging: false),
            0x1 => new PlayStationReading(percent, Charging: true),
            0x2 => new PlayStationReading(100, Charging: false),
            _ => null,
        };
    }
}
