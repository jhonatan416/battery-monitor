namespace BatteryMonitor.Core.Protocols;

/// <summary>
/// Decides what to show from Windows' Bluetooth battery property. Windows keeps the last value
/// after a device disconnects, so the level only counts while the device is connected.
/// </summary>
public static class BluetoothBattery
{
    /// <param name="batteryProperty">DEVPKEY_Bluetooth_Battery (0..100), or null when absent.</param>
    /// <param name="connected">The device's "connected" property, or null when Windows does not expose it.</param>
    public static int? Percent(byte? batteryProperty, bool? connected)
    {
        if (batteryProperty is not byte level || level > 100) return null;
        if (connected == false) return null; // cached value of a switched-off device
        return level;
    }
}
