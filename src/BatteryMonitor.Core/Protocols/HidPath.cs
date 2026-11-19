using System.Globalization;
using System.Text.RegularExpressions;

namespace BatteryMonitor.Core.Protocols;

/// <summary>Vendor and product ids from a HID device interface path.</summary>
public static partial class HidPath
{
    // USB and Bluetooth LE:  ...vid_1532&pid_00df...
    [GeneratedRegex(@"vid_([0-9a-f]{4})&pid_([0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex UsbStyle();

    // Classic Bluetooth HID: ...vid&0002054c_pid&09cc...  ("0002" = vendor id assigned by the USB-IF)
    [GeneratedRegex(@"vid&0002([0-9a-f]{4})_pid&([0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex BluetoothClassicStyle();

    public static (ushort VendorId, ushort ProductId)? TryParseIds(string path)
    {
        var m = UsbStyle().Match(path);
        if (!m.Success) m = BluetoothClassicStyle().Match(path);
        if (!m.Success) return null;
        return (ushort.Parse(m.Groups[1].Value, NumberStyles.HexNumber),
                ushort.Parse(m.Groups[2].Value, NumberStyles.HexNumber));
    }
}
