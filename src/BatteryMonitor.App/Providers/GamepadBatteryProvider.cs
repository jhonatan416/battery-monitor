using BatteryMonitor.App.Hid;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;
using BatteryMonitor.Core.Protocols.Gamepads;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// Steam Controller, 8BitDo and Flydigi gamepads on USB or their receivers. Steam and 8BitDo send
/// the battery in their input reports, so the provider only listens; Flydigi answers one read-only
/// "get info" request. Bluetooth paths are skipped (the Bluetooth provider reports those).
/// </summary>
public sealed class GamepadBatteryProvider : IBatteryProvider
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ListenFor = TimeSpan.FromSeconds(1);

    public string Name => "Gamepads";

    public static IReadOnlySet<ushort> VendorIds { get; } =
        new HashSet<ushort> { SteamController.VendorId, EightBitDo.VendorId, Flydigi.VendorId };

    public static string? NameFor(ushort vendorId, ushort productId) => vendorId switch
    {
        SteamController.VendorId => SteamController.NameFor(productId),
        EightBitDo.VendorId => EightBitDo.NameFor(productId),
        Flydigi.VendorId => Flydigi.NameFor(productId),
        _ => null,
    };

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var result = new List<DeviceBattery>();
        foreach (ushort vendor in VendorIds)
            foreach (var coll in HidCollection.Enumerate(vendor).Where(c => !HidBattery.IsBluetoothPath(c.Path)))
            {
                if (NameFor(vendor, coll.ProductId) is not { } name) continue;
                // The Steam dongle has one interface per paired controller; other products report once.
                if (vendor != SteamController.VendorId && result.Any(d => d.Id.StartsWith(ProductKey(coll)))) continue;
                if (Read(vendor, coll) is not { } status) continue;
                result.Add(new DeviceBattery($"{ProductKey(coll)}:{coll.Path.GetHashCode():X8}", name, DeviceKind.Gamepad,
                    "USB / receiver", status.Percent, status.Charging));
            }
        return result;
    }

    private static string ProductKey(HidCollection coll) => $"pad:{coll.VendorId:X4}{coll.ProductId:X4}";

    private static GamepadStatus? Read(ushort vendor, HidCollection coll)
    {
        if (vendor == Flydigi.VendorId && HidBatteryReader.TopLevelUsage(coll)?.Page != Flydigi.VendorUsagePage) return null;
        using var stream = HidStream.Open(coll);
        if (stream is null) return null; // e.g. the keyboard/mouse collections Windows keeps exclusive
        if (vendor == Flydigi.VendorId) return Flydigi.Query(new HidLink(stream), coll.ProductId);
        return Listen(stream, report => vendor == SteamController.VendorId
            ? SteamController.Parse(coll.ProductId, report)
            : EightBitDo.Parse(coll.ProductId, report));
    }

    /// <summary>Reads input reports for up to a second; stops early when the device sends nothing (off).</summary>
    private static GamepadStatus? Listen(HidStream stream, Func<byte[], GamepadStatus?> parse)
    {
        var until = DateTime.UtcNow + ListenFor;
        while (DateTime.UtcNow < until)
        {
            if (stream.Read(ReadTimeout) is not { } report) return null;
            if (parse(report) is { } status) return status;
        }
        return null;
    }
}
