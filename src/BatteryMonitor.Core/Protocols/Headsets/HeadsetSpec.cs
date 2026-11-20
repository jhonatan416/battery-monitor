namespace BatteryMonitor.Core.Protocols.Headsets;

/// <summary>A headset's battery state. A null Percent with Charging true means "charging, level not reported".</summary>
public readonly record struct HeadsetStatus(int? Percent, bool Charging);

/// <summary>
/// Driven port to one HID collection of a headset. Byte layout follows the hidapi convention:
/// requests start with the report id (0x00 when the device does not number its reports) and
/// replies from unnumbered reports come without that leading 0x00.
/// </summary>
public interface IHidLink
{
    /// <summary>
    /// Sends <paramref name="request"/> as an output report (padded by the link) and returns the first
    /// input report <paramref name="isReply"/> accepts (any report when null), or null on timeout.
    /// </summary>
    byte[]? Exchange(byte[] request, Func<byte[], bool>? isReply = null);

    /// <summary>
    /// Reads the input reports the device sends by itself, without sending anything, and returns
    /// the first one <paramref name="isReport"/> accepts, or null when none arrives in time.
    /// </summary>
    byte[]? Listen(Func<byte[], bool> isReport);

    /// <summary>
    /// Sends <paramref name="request"/> (a "get" command) as a feature report (padded by the link),
    /// then reads back the feature report with the same id. Null if either step fails.
    /// </summary>
    byte[]? FeatureExchange(byte[] request);

    /// <summary>Discards input reports that are already waiting.</summary>
    void Drain();
}

/// <summary>
/// One device family (headsets, and since slice 6 also mice): which USB ids and HID collection it
/// uses, and how to query its battery. The collection is chosen by usage page:usage when both are
/// known, else by USB interface number.
/// </summary>
public sealed record HeadsetSpec(
    string Name,
    ushort VendorId,
    IReadOnlyList<ushort> ProductIds,
    ushort? UsagePage,
    ushort? Usage,
    int? Interface,
    Func<IHidLink, ushort, HeadsetStatus?> Query,
    Domain.DeviceKind Kind = Domain.DeviceKind.Headset);

internal static class HeadsetMath
{
    /// <summary>Linear map of a clamped raw value onto 0..100 (integer arithmetic).</summary>
    public static int Map(int value, int inMin, int inMax)
    {
        int v = Math.Clamp(value, inMin, inMax);
        return (v - inMin) * 100 / (inMax - inMin);
    }

    public static int Clamp100(int value) => Math.Clamp(value, 0, 100);
}
