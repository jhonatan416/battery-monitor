using BatteryMonitor.Core.Domain;

namespace BatteryMonitor.Core.Protocols;

/// <summary>
/// Standard HID battery reporting, as defined by the USB-IF HID Usage Tables: any device that
/// declares one of these usages reports its level the same way, whatever the brand.
/// </summary>
public static class HidBattery
{
    public const ushort GenericDevicePage = 0x06;     // Generic Device Controls
    public const ushort BatteryStrength = 0x20;
    public const ushort BatterySystemPage = 0x85;     // Battery System
    public const ushort AbsoluteStateOfCharge = 0x65;
    public const ushort RemainingCapacity = 0x66;

    public static bool IsBatteryUsage(ushort page, ushort usage) =>
        (page == GenericDevicePage && usage == BatteryStrength) ||
        (page == BatterySystemPage && usage is AbsoluteStateOfCharge or RemainingCapacity);

    /// <summary>Scales a raw value from the declared logical range to 0..100, or null if it is outside it.</summary>
    public static int? ToPercent(int value, int logicalMin, int logicalMax)
    {
        if (logicalMax <= logicalMin || value < logicalMin || value > logicalMax) return null;
        long span = (long)logicalMax - logicalMin;
        return (int)(((long)value - logicalMin) * 100 / span);
    }

    /// <summary>Device kind from a collection's top-level usage (Generic Desktop page).</summary>
    public static DeviceKind KindFromUsage(ushort page, ushort usage) => (page, usage) switch
    {
        (0x01, 0x02) => DeviceKind.Mouse,
        (0x01, 0x06) => DeviceKind.Keyboard,
        (0x01, 0x04 or 0x05) => DeviceKind.Gamepad,
        _ => DeviceKind.Other,
    };

    /// <summary>
    /// HID over Bluetooth: HID-over-GATT (service 0x1812) and classic Bluetooth HID (0x1124).
    /// The Bluetooth provider already reports these devices.
    /// </summary>
    public static bool IsBluetoothPath(string path) =>
        path.Contains("{00001812-", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("{00001124-", StringComparison.OrdinalIgnoreCase);
}
