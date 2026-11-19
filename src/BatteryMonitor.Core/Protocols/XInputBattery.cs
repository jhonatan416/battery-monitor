namespace BatteryMonitor.Core.Protocols;

public readonly record struct XInputReading(int Percent, bool Approximate);

/// <summary>
/// Decodes XINPUT_BATTERY_INFORMATION (Microsoft XInput documentation). XInput only reports
/// four coarse levels, so the percent is approximate.
/// </summary>
public static class XInputBattery
{
    private const byte TypeDisconnected = 0x00, TypeWired = 0x01;

    // Midpoint-ish values so the tray color matches the level (red / yellow / green).
    private static readonly int[] LevelPercents = [5, 30, 65, 100]; // empty, low, medium, full

    /// <param name="bluetoothGamepadPresent">
    /// A Bluetooth Xbox controller is also visible as an XInput slot. The Bluetooth provider reports
    /// its exact percent, so battery slots are skipped to avoid a duplicate, coarser icon.
    /// </param>
    public static XInputReading? Decode(byte batteryType, byte batteryLevel, bool bluetoothGamepadPresent)
    {
        if (batteryType is TypeDisconnected or TypeWired) return null; // no battery to report
        if (bluetoothGamepadPresent) return null;
        if (batteryLevel >= LevelPercents.Length) return null;
        return new XInputReading(LevelPercents[batteryLevel], Approximate: true);
    }
}
