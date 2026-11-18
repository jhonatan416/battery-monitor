using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace BatteryMonitor.App.Hid;

/// <summary>One HID top-level collection (a "\\?\hid#vid_xxxx&amp;pid_xxxx..." interface).</summary>
public sealed partial record HidCollection(string Path, ushort VendorId, ushort ProductId)
{
    [GeneratedRegex(@"vid_([0-9a-f]{4})&pid_([0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidPid();

    /// <summary>Lists every present HID collection, optionally filtered by vendor id.</summary>
    public static IReadOnlyList<HidCollection> Enumerate(ushort? vendorId = null)
    {
        var result = new List<HidCollection>();
        foreach (string path in Native.EnumerateHidInterfacePaths())
        {
            var m = VidPid().Match(path);
            if (!m.Success) continue;
            ushort vid = Convert.ToUInt16(m.Groups[1].Value, 16);
            ushort pid = Convert.ToUInt16(m.Groups[2].Value, 16);
            if (vendorId is null || vendorId == vid) result.Add(new HidCollection(path, vid, pid));
        }
        return result;
    }

    /// <summary>Feature report length in bytes (including report id), or 0 if it cannot be read.</summary>
    public int FeatureReportLength()
    {
        using var h = Native.Open(Path, access: 0);
        if (h.IsInvalid) return 0;
        if (!Native.HidD_GetPreparsedData(h, out IntPtr pp)) return 0;
        try
        {
            return Native.HidP_GetCaps(pp, out var caps) == Native.HIDP_STATUS_SUCCESS ? caps.FeatureReportByteLength : 0;
        }
        finally { Native.HidD_FreePreparsedData(pp); }
    }

    public string? ProductName()
    {
        using var h = Native.Open(Path, access: 0);
        if (h.IsInvalid) return null;
        var buf = new char[128];
        return Native.HidD_GetProductString(h, buf, buf.Length * 2) ? new string(buf).TrimEnd('\0') : null;
    }

    /// <summary>
    /// Opens the collection for feature reports. Windows keeps mouse/keyboard collections
    /// exclusive, but feature reports still work through a handle opened with no access rights.
    /// </summary>
    public HidHandle OpenForFeatures()
    {
        var h = Native.Open(Path, Native.GENERIC_READ | Native.GENERIC_WRITE);
        if (h.IsInvalid)
        {
            h.Dispose();
            h = Native.Open(Path, access: 0);
        }
        if (h.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot open {Path}");
        return new HidHandle(h);
    }
}

public sealed class HidHandle(SafeFileHandle handle) : IDisposable
{
    /// <summary>buffer[0] must hold the report id; the rest is filled by the device.</summary>
    public bool GetFeature(byte[] buffer) => Native.HidD_GetFeature(handle, buffer, buffer.Length);

    public bool SetFeature(byte[] buffer) => Native.HidD_SetFeature(handle, buffer, buffer.Length);

    public void Dispose() => handle.Dispose();
}

internal static partial class Native
{
    public const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    public const int HIDP_STATUS_SUCCESS = 0x00110000;
    private const uint FILE_SHARE_READ_WRITE = 3, OPEN_EXISTING = 3;
    private const uint DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;

    public static SafeFileHandle Open(string path, uint access) =>
        CreateFileW(path, access, FILE_SHARE_READ_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

    public static IEnumerable<string> EnumerateHidInterfacePaths()
    {
        HidD_GetHidGuid(out Guid hidGuid);
        IntPtr set = SetupDiGetClassDevsW(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == new IntPtr(-1)) yield break;
        try
        {
            var data = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
            for (uint i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref data); i++)
            {
                SetupDiGetDeviceInterfaceDetailW(set, ref data, IntPtr.Zero, 0, out int size, IntPtr.Zero);
                if (size <= 0) continue;
                IntPtr detail = Marshal.AllocHGlobal(size);
                try
                {
                    // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 on x64, 6 on x86.
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (SetupDiGetDeviceInterfaceDetailW(set, ref data, detail, size, out _, IntPtr.Zero))
                        yield return Marshal.PtrToStringUni(detail + 4) ?? "";
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sa, uint disposition, uint flags, IntPtr template);

    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetFeature(SafeFileHandle h, byte[] buffer, int length);
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_SetFeature(SafeFileHandle h, byte[] buffer, int length);
    [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr preparsed);
    [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr preparsed);
    [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr preparsed, out HIDP_CAPS caps);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] public static extern bool HidD_GetProductString(SafeFileHandle h, char[] buffer, int bytes);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid classGuid, uint index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, out int required, IntPtr devInfo);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}
