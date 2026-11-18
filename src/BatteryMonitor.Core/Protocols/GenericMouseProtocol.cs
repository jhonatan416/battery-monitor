namespace BatteryMonitor.Core.Protocols;

/// <summary>
/// Generic 2.4 GHz mouse receiver 248A:8366 (used by the Memzuiox mouse).
/// Feature report 5 on the vendor collection returns 8 bytes; byte 1 looks like the battery percent.
/// ASSUMPTION: confirmed against one reading only (0x58 = 88). A 24 h log is verifying it.
/// </summary>
public static class GenericMouseProtocol
{
    public const ushort VendorId = 0x248A;
    public const ushort ProductId = 0x8366;
    public const byte BatteryReportId = 5;
    public const int ReportLength = 8;

    public static int? ParseBattery(ReadOnlySpan<byte> report)
    {
        if (report.Length < 2) return null;
        byte level = report[1];
        if (level == 0 || level > 100) return null; // 0 = mouse off/asleep, >100 = not a level
        return level;
    }
}
