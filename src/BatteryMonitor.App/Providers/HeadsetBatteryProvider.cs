using BatteryMonitor.App.Hid;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;
using BatteryMonitor.Core.Protocols.Headsets;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// SteelSeries, HyperX and Corsair wireless headsets (see <see cref="HeadsetCatalog"/>). Picks the
/// collection each family uses and runs its battery query through a <see cref="HidLink"/>.
/// </summary>
public sealed class HeadsetBatteryProvider : IBatteryProvider
{
    public string Name => "Headsets";

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var result = new List<DeviceBattery>();
        foreach (ushort vendor in HeadsetCatalog.VendorIds)
            foreach (var product in HidCollection.Enumerate(vendor).GroupBy(c => c.ProductId))
            {
                if (HeadsetCatalog.Find(vendor, product.Key) is not { } spec) continue;
                if (Read(spec, product.Key, product) is not { } status) continue;
                result.Add(new DeviceBattery($"headset:{vendor:X4}{product.Key:X4}", spec.Name, DeviceKind.Headset,
                    "USB receiver", status.Percent, status.Charging));
            }
        return result;
    }

    private static HeadsetStatus? Read(HeadsetSpec spec, ushort productId, IEnumerable<HidCollection> collections)
    {
        foreach (var coll in collections.Where(c => !HidBattery.IsBluetoothPath(c.Path) && Matches(spec, c)))
        {
            using var stream = HidStream.Open(coll);
            if (stream is null) continue;
            if (spec.Query(new HidLink(stream), productId) is { } status) return status;
        }
        return null;
    }

    /// <summary>Usage page:usage when the family declares it, else the USB interface number.</summary>
    private static bool Matches(HeadsetSpec spec, HidCollection coll)
    {
        if (spec.UsagePage is ushort page && spec.Usage is ushort usage)
            return HidBatteryReader.TopLevelUsage(coll) == (page, usage);
        if (spec.Interface is not int iface) return false;
        string path = coll.Path.ToLowerInvariant();
        return path.Contains($"&mi_{iface:x2}") || (iface == 0 && !path.Contains("&mi_")); // single-interface device
    }
}
