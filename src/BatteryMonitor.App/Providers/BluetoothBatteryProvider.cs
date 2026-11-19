using System.Runtime.InteropServices;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// Reads the battery level Windows itself keeps for paired Bluetooth devices
/// (DEVPKEY_Bluetooth_Battery). Covers the Xbox controller and most Bluetooth mice/headsets.
/// </summary>
public sealed class BluetoothBatteryProvider : IBatteryProvider
{
    public string Name => "Bluetooth";

    /// <summary>True when the latest poll reported a gamepad (used by XInput to avoid duplicates).</summary>
    public bool LastPollSawGamepad { get; private set; }

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var result = new List<DeviceBattery>();
        foreach (string enumerator in new[] { "BTHLE", "BTHENUM" })
            foreach (var (instanceId, name, level) in Native.DevicesWithBattery(enumerator))
                result.Add(new DeviceBattery($"bt:{instanceId}", name, GuessKind(name), "Bluetooth", level));
        LastPollSawGamepad = result.Any(d => d.Kind == DeviceKind.Gamepad);
        return result;
    }

    internal static DeviceKind GuessKind(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("controller") || n.Contains("gamepad") || n.Contains("dualsense")) return DeviceKind.Gamepad;
        if (n.Contains("mouse")) return DeviceKind.Mouse;
        if (n.Contains("keyboard")) return DeviceKind.Keyboard;
        if (n.Contains("headset") || n.Contains("headphone") || n.Contains("buds") || n.Contains("airpods")) return DeviceKind.Headset;
        return DeviceKind.Other;
    }

    private static class Native
    {
        private const uint DIGCF_PRESENT = 0x2, DIGCF_ALLCLASSES = 0x4;

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY { public Guid fmtid; public uint pid; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA { public int cbSize; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }

        private static readonly DEVPROPKEY BatteryKey = new() { fmtid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), pid = 2 };
        private static readonly DEVPROPKEY FriendlyNameKey = new() { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };

        public static IEnumerable<(string InstanceId, string Name, int Level)> DevicesWithBattery(string enumerator)
        {
            IntPtr set = SetupDiGetClassDevsW(IntPtr.Zero, enumerator, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
            if (set == new IntPtr(-1)) yield break;
            try
            {
                var info = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>() };
                for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref info); i++)
                {
                    string id = InstanceId(set, ref info);
                    // Only the device node itself (BTHLE\DEV_..., BTHENUM\DEV_...), not its service children.
                    if (!id.StartsWith(enumerator + "\\DEV_", StringComparison.OrdinalIgnoreCase)) continue;
                    byte[]? battery = Property(set, ref info, BatteryKey);
                    if (battery is not { Length: > 0 } || battery[0] > 100) continue;
                    string name = Utf16(Property(set, ref info, FriendlyNameKey)) ?? id;
                    yield return (id, name, battery[0]);
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }

        private static string InstanceId(IntPtr set, ref SP_DEVINFO_DATA info)
        {
            var buf = new char[512];
            return SetupDiGetDeviceInstanceIdW(set, ref info, buf, buf.Length, out _) ? new string(buf).TrimEnd('\0') : "";
        }

        private static byte[]? Property(IntPtr set, ref SP_DEVINFO_DATA info, DEVPROPKEY key)
        {
            var buf = new byte[512];
            return SetupDiGetDevicePropertyW(set, ref info, ref key, out _, buf, buf.Length, out int size, 0) ? buf[..size] : null;
        }

        private static string? Utf16(byte[]? bytes) =>
            bytes is null ? null : System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SP_DEVINFO_DATA info);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SP_DEVINFO_DATA info, char[] buffer, int size, out int required);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref SP_DEVINFO_DATA info, ref DEVPROPKEY key,
            out uint type, byte[] buffer, int size, out int required, uint flags);
        [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    }
}
