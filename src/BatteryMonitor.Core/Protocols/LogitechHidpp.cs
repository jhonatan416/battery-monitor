using System.Text;
using BatteryMonitor.Core.Domain;

namespace BatteryMonitor.Core.Protocols;

public enum HidppStatus { Ok, Error, Mismatch }

public sealed record HidppReply(HidppStatus Status, byte[] Parameters)
{
    public static readonly HidppReply Mismatch = new(HidppStatus.Mismatch, []);
    public static readonly HidppReply Failed = new(HidppStatus.Error, []);
}

public readonly record struct LogitechBatteryReading(int Percent, bool Charging);

/// <summary>
/// Logitech HID++ 2.0, as documented by Logitech and the Solaar project. Requests are long reports
/// [0x11, device index, feature index, function &lt;&lt; 4 | software id, parameters...]; replies echo
/// the first four bytes. Only "get" functions are used (read-only).
/// </summary>
public static class LogitechHidpp
{
    public const ushort VendorId = 0x046D;
    public const byte LongReportId = 0x11, ShortReportId = 0x10;
    public const int LongLength = 20;
    public const byte SoftwareId = 0x0B; // non-zero, so replies are told apart from notifications
    public const byte RootIndex = 0x00;  // the Root feature is always at index 0
    public const byte DirectDevice = 0xFF;

    public const ushort UnifiedBatteryFeature = 0x1004;
    public const ushort BatteryStatusFeature = 0x1000;
    public const ushort DeviceNameFeature = 0x0005;

    private static readonly HashSet<ushort> Receivers =
    [
        0xC52B, 0xC532,                                         // Unifying
        0xC539, 0xC53A, 0xC53D, 0xC53F, 0xC541, 0xC545, 0xC547, 0xC54D, // Lightspeed
        0xC548,                                                 // Bolt
    ];

    public static bool IsReceiver(ushort productId) => Receivers.Contains(productId);

    /// <summary>Paired-device slots to ask on a receiver; a directly connected device is index 0xFF.</summary>
    public static byte[] DeviceIndices(ushort productId) => IsReceiver(productId) ? [1, 2, 3, 4, 5, 6] : [DirectDevice];

    public static byte[] BuildRequest(byte deviceIndex, byte featureIndex, byte function, params byte[] parameters)
    {
        var r = new byte[LongLength];
        r[0] = LongReportId;
        r[1] = deviceIndex;
        r[2] = featureIndex;
        r[3] = (byte)((function << 4) | SoftwareId);
        parameters.CopyTo(r, 4);
        return r;
    }

    public static HidppReply ParseReply(ReadOnlySpan<byte> report, byte deviceIndex, byte featureIndex, byte function)
    {
        if (report.Length < 7 || report[1] != deviceIndex) return HidppReply.Mismatch;
        byte fnSw = (byte)((function << 4) | SoftwareId);

        // HID++ 2.0 error: [id, device, 0xFF, feature, fn|sw, code]; HID++ 1.0 error: [0x10, device, 0x8F, ...].
        if (report[2] is 0xFF or 0x8F)
            return report[3] == featureIndex && report[4] == fnSw ? HidppReply.Failed : HidppReply.Mismatch;

        if (report[0] != LongReportId || report.Length < LongLength) return HidppReply.Mismatch;
        if (report[2] != featureIndex || report[3] != fnSw) return HidppReply.Mismatch;
        return new HidppReply(HidppStatus.Ok, report[4..LongLength].ToArray());
    }

    /// <summary>Feature 0x1004 get_status: [0] state of charge %, [2] charging status.</summary>
    public static LogitechBatteryReading? DecodeUnifiedBattery(ReadOnlySpan<byte> p) => p[2] switch
    {
        0x00 => new LogitechBatteryReading(Clamp(p[0]), Charging: false),
        0x01 or 0x02 => new LogitechBatteryReading(Clamp(p[0]), Charging: true),
        0x03 => new LogitechBatteryReading(100, Charging: false), // full
        _ => null,                                                // error states
    };

    /// <summary>Feature 0x1000 GetBatteryLevelStatus: [0] level % (0 = unknown), [2] status.</summary>
    public static LogitechBatteryReading? DecodeBatteryStatus(ReadOnlySpan<byte> p) => p[2] switch
    {
        0x03 => new LogitechBatteryReading(100, Charging: false), // charged
        0x00 when p[0] > 0 => new LogitechBatteryReading(Clamp(p[0]), Charging: false),
        0x01 or 0x02 or 0x04 when p[0] > 0 => new LogitechBatteryReading(Clamp(p[0]), Charging: true),
        _ => null,
    };

    /// <summary>Feature 0x0005 getDeviceType values.</summary>
    public static DeviceKind KindFromDeviceType(byte type) => type switch
    {
        0 => DeviceKind.Keyboard,
        3 or 4 or 5 => DeviceKind.Mouse, // mouse, touchpad, trackball
        8 => DeviceKind.Headset,
        11 or 12 => DeviceKind.Gamepad,  // joystick, gamepad
        _ => DeviceKind.Other,
    };

    /// <summary>Feature 0x0005 getDeviceName returns the name in 16-byte chunks.</summary>
    public static string JoinName(int length, IEnumerable<byte[]> chunks)
    {
        byte[] all = chunks.SelectMany(c => c).Take(length).ToArray();
        return Encoding.UTF8.GetString(all).TrimEnd('\0').Trim();
    }

    private static int Clamp(byte percent) => Math.Min((int)percent, 100);
}
