using System.Runtime.InteropServices;
using BatteryMonitor.Core.Protocols;
using Microsoft.Win32.SafeHandles;

namespace BatteryMonitor.App.Hid;

/// <summary>A standard battery field a HID collection declares (see <see cref="HidBattery"/>).</summary>
/// <param name="Feature">True for a feature report, false for an input report.</param>
public sealed record HidBatteryField(
    bool Feature, byte ReportId, ushort UsagePage, ushort Usage, ushort LinkCollection,
    int LogicalMin, int LogicalMax, int ReportLength);

/// <summary>Reads standard HID battery fields. Read-only: only GET_FEATURE / GET_INPUT_REPORT requests.</summary>
public static class HidBatteryReader
{
    /// <summary>Top-level usage of the collection (page, usage), or null if it cannot be read.</summary>
    public static (ushort Page, ushort Usage)? TopLevelUsage(HidCollection coll) =>
        WithCaps(coll, (_, caps) => ((ushort, ushort)?)(caps.UsagePage, caps.Usage));

    public static IReadOnlyList<HidBatteryField> FindBatteryFields(HidCollection coll) =>
        WithCaps(coll, (pp, caps) =>
        {
            var fields = new List<HidBatteryField>();
            fields.AddRange(Fields(pp, Native.HidP_Input, caps.NumberInputValueCaps, caps.InputReportByteLength));
            fields.AddRange(Fields(pp, Native.HidP_Feature, caps.NumberFeatureValueCaps, caps.FeatureReportByteLength));
            return (IReadOnlyList<HidBatteryField>)fields;
        }) ?? [];

    /// <summary>Number of value fields of any usage (input + feature), to show the descriptor was readable.</summary>
    public static int CountValueFields(HidCollection coll) =>
        WithCaps(coll, (pp, caps) => CountCaps(pp, Native.HidP_Input, caps.NumberInputValueCaps) +
                                     CountCaps(pp, Native.HidP_Feature, caps.NumberFeatureValueCaps));

    private static int CountCaps(IntPtr pp, int reportType, ushort count)
    {
        if (count == 0) return 0;
        var caps = new Native.HIDP_VALUE_CAPS[count];
        ushort n = count;
        return Native.HidP_GetValueCaps(reportType, caps, ref n, pp) == Native.HIDP_STATUS_SUCCESS ? n : 0;
    }

    /// <summary>Asks the device for the field's report and returns the level, or null if it gave none.</summary>
    public static int? ReadPercent(HidCollection coll, HidBatteryField field)
    {
        using var h = Native.Open(coll.Path, access: 0);
        if (h.IsInvalid || field.ReportLength == 0) return null;
        var report = new byte[field.ReportLength];
        report[0] = field.ReportId;
        bool got = field.Feature
            ? Native.HidD_GetFeature(h, report, report.Length)
            : Native.HidD_GetInputReport(h, report, report.Length);
        if (!got || !Native.HidD_GetPreparsedData(h, out IntPtr pp)) return null;
        try
        {
            int status = Native.HidP_GetUsageValue(field.Feature ? Native.HidP_Feature : Native.HidP_Input,
                field.UsagePage, field.LinkCollection, field.Usage, out uint value, pp, report, (uint)report.Length);
            return status == Native.HIDP_STATUS_SUCCESS
                ? HidBattery.ToPercent((int)value, field.LogicalMin, field.LogicalMax)
                : null;
        }
        finally { Native.HidD_FreePreparsedData(pp); }
    }

    private static IEnumerable<HidBatteryField> Fields(IntPtr pp, int reportType, ushort count, ushort reportLength)
    {
        if (count == 0) yield break;
        var caps = new Native.HIDP_VALUE_CAPS[count];
        ushort n = count;
        if (Native.HidP_GetValueCaps(reportType, caps, ref n, pp) != Native.HIDP_STATUS_SUCCESS) yield break;
        for (int i = 0; i < n; i++)
        {
            var c = caps[i];
            if (c.IsRange != 0 || !HidBattery.IsBatteryUsage(c.UsagePage, c.UsageMin)) continue;
            yield return new HidBatteryField(reportType == Native.HidP_Feature, c.ReportID, c.UsagePage, c.UsageMin,
                c.LinkCollection, c.LogicalMin, c.LogicalMax, reportLength);
        }
    }

    private static T? WithCaps<T>(HidCollection coll, Func<IntPtr, Native.HIDP_CAPS, T> read)
    {
        using SafeFileHandle h = Native.Open(coll.Path, access: 0);
        if (h.IsInvalid || !Native.HidD_GetPreparsedData(h, out IntPtr pp)) return default;
        try
        {
            return Native.HidP_GetCaps(pp, out var caps) == Native.HIDP_STATUS_SUCCESS ? read(pp, caps) : default;
        }
        finally { Native.HidD_FreePreparsedData(pp); }
    }
}

internal static partial class Native
{
    public const int HidP_Input = 0, HidP_Feature = 2;

    /// <summary>HIDP_VALUE_CAPS (hidpi.h), 72 bytes. For a non-range cap, UsageMin holds the usage.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_VALUE_CAPS
    {
        public ushort UsagePage;
        public byte ReportID, IsAlias;
        public ushort BitField, LinkCollection, LinkUsage, LinkUsagePage;
        public byte IsRange, IsStringRange, IsDesignatorRange, IsAbsolute, HasNull, Reserved;
        public ushort BitSize, ReportCount;
        public ushort Reserved2a, Reserved2b, Reserved2c, Reserved2d, Reserved2e;
        public uint UnitsExp, Units;
        public int LogicalMin, LogicalMax, PhysicalMin, PhysicalMax;
        public ushort UsageMin, UsageMax, StringMin, StringMax, DesignatorMin, DesignatorMax, DataIndexMin, DataIndexMax;
    }

    [DllImport("hid.dll")]
    public static extern int HidP_GetValueCaps(int reportType, [Out] HIDP_VALUE_CAPS[] caps, ref ushort length, IntPtr preparsed);

    [DllImport("hid.dll")]
    public static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage,
        out uint value, IntPtr preparsed, byte[] report, uint reportLength);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetInputReport(SafeFileHandle h, byte[] buffer, int length);
}
