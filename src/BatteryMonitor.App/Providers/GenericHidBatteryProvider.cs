using BatteryMonitor.App.Hid;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// Any USB/receiver device that declares a standard HID battery field (USB-IF HID Usage Tables),
/// whatever the brand. Skips Bluetooth HID (the Bluetooth provider reports those) and vendors that
/// have their own provider.
/// </summary>
/// <param name="vendorsWithOwnProvider">Vendors handled entirely by a dedicated provider.</param>
/// <param name="productHasOwnProvider">Single products handled by a dedicated provider (e.g. headsets).</param>
public sealed class GenericHidBatteryProvider(
    IReadOnlySet<ushort> vendorsWithOwnProvider,
    Func<ushort, ushort, bool> productHasOwnProvider) : IBatteryProvider
{
    public string Name => "Generic HID";

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var result = new List<DeviceBattery>();
        var products = HidCollection.Enumerate()
            .Where(c => !HidBattery.IsBluetoothPath(c.Path) && !vendorsWithOwnProvider.Contains(c.VendorId)
                        && !productHasOwnProvider(c.VendorId, c.ProductId))
            .GroupBy(c => (c.VendorId, c.ProductId));

        foreach (var product in products)
            if (ReadProduct(product.ToList()) is { } device) result.Add(device);
        return result;
    }

    private static DeviceBattery? ReadProduct(IReadOnlyList<HidCollection> collections)
    {
        foreach (var coll in collections)
            foreach (var field in HidBatteryReader.FindBatteryFields(coll))
            {
                if (HidBatteryReader.ReadPercent(coll, field) is not int percent) continue;
                return new DeviceBattery(
                    $"hid:{coll.VendorId:X4}{coll.ProductId:X4}",
                    collections.Select(c => c.ProductName()).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                        ?? $"USB device {coll.VendorId:X4}:{coll.ProductId:X4}",
                    KindOf(collections),
                    "USB / receiver",
                    percent);
            }
        return null;
    }

    private static DeviceKind KindOf(IEnumerable<HidCollection> collections)
    {
        foreach (var coll in collections)
            if (HidBatteryReader.TopLevelUsage(coll) is var (page, usage) &&
                HidBattery.KindFromUsage(page, usage) is var kind && kind != DeviceKind.Other)
                return kind;
        return DeviceKind.Other;
    }

    /// <summary>
    /// Lists every collection that declares a battery field, including Bluetooth ones that the
    /// normal poll skips, with the value read (for `--diag-hid`).
    /// </summary>
    public static void Diagnose(TextWriter output)
    {
        int found = 0, collections = 0, valueFields = 0;
        foreach (var coll in HidCollection.Enumerate())
        {
            collections++;
            valueFields += HidBatteryReader.CountValueFields(coll);
            foreach (var field in HidBatteryReader.FindBatteryFields(coll))
            {
                found++;
                string kind = field.Feature ? "feature" : "input";
                output.WriteLine($"{coll.VendorId:X4}:{coll.ProductId:X4} {(HidBattery.IsBluetoothPath(coll.Path) ? "[bluetooth, skipped]" : "")}");
                output.WriteLine($"   {kind} report {field.ReportId}, usage {field.UsagePage:X2}:{field.Usage:X2}, " +
                                 $"range {field.LogicalMin}..{field.LogicalMax} -> {HidBatteryReader.ReadPercent(coll, field)?.ToString() ?? "no value"}");
            }
        }
        output.WriteLine($"scanned {collections} HID collections, {valueFields} value fields, {found} standard battery fields");
    }
}
