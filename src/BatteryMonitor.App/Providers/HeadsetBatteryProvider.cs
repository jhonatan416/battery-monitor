using BatteryMonitor.App.Hid;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;
using BatteryMonitor.Core.Protocols.Headsets;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// Device families with a vendor battery query: headsets (<see cref="HeadsetCatalog"/>: SteelSeries,
/// HyperX, Corsair, Sony INZONE, Jabra) or mice (<see cref="Core.Protocols.Mice.MiceCatalog"/>). Picks
/// the collection each family uses and runs its battery query through a <see cref="HidLink"/>.
/// </summary>
/// <param name="name">Provider name, e.g. "Headsets".</param>
/// <param name="idPrefix">Device id prefix, e.g. "headset".</param>
/// <param name="specs">The families this provider reads.</param>
public sealed class HeadsetBatteryProvider(string name, string idPrefix, IReadOnlyList<HeadsetSpec> specs) : IBatteryProvider
{
    public HeadsetBatteryProvider() : this("Headsets", "headset", HeadsetCatalog.All) { }

    public string Name => name;

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var result = new List<DeviceBattery>();
        foreach (ushort vendor in specs.Select(s => s.VendorId).Distinct())
            foreach (var product in HidCollection.Enumerate(vendor).GroupBy(c => c.ProductId))
            {
                if (specs.FirstOrDefault(s => s.VendorId == vendor && s.ProductIds.Contains(product.Key)) is not { } spec) continue;
                if (Read(spec, product.Key, product) is not { } status) continue;
                result.Add(new DeviceBattery($"{idPrefix}:{vendor:X4}{product.Key:X4}", spec.Name, spec.Kind,
                    "USB receiver", status.Percent, status.Charging));
            }
        return result;
    }

    private static HeadsetStatus? Read(HeadsetSpec spec, ushort productId, IEnumerable<HidCollection> collections)
    {
        foreach (var coll in collections.Where(c => !HidBattery.IsBluetoothPath(c.Path) && Matches(spec, c)))
        {
            using var stream = HidStream.Open(coll, featuresOnly: true); // some mice answer on feature reports only
            if (stream is null) continue;
            if (spec.Query(new HidLink(stream), productId) is { } status) return status;
        }
        return null;
    }

    /// <summary>
    /// Usage page:usage when the family declares it, else the USB interface number; a family that
    /// declares neither (it only listens) tries every collection.
    /// </summary>
    private static bool Matches(HeadsetSpec spec, HidCollection coll)
    {
        if (spec.UsagePage is ushort page && spec.Usage is ushort usage)
            return HidBatteryReader.TopLevelUsage(coll) == (page, usage);
        if (spec.Interface is not int iface) return true;
        string path = coll.Path.ToLowerInvariant();
        return path.Contains($"&mi_{iface:x2}") || (iface == 0 && !path.Contains("&mi_")); // single-interface device
    }
}
