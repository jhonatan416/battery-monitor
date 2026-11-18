using BatteryMonitor.App.Hid;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.App.Providers;

/// <summary>Memzuiox mouse on the generic 248A:8366 receiver (read-only GET_FEATURE of report 5).</summary>
public sealed class GenericMouseBatteryProvider(string displayName = "Memzuiox Mouse") : IBatteryProvider
{
    public string Name => "Generic 2.4 GHz mouse";

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var coll = HidCollection.Enumerate(GenericMouseProtocol.VendorId)
            .Where(c => c.ProductId == GenericMouseProtocol.ProductId)
            .FirstOrDefault(c => c.FeatureReportLength() == GenericMouseProtocol.ReportLength);
        if (coll is null) return [];

        using var hid = coll.OpenForFeatures();
        var buffer = new byte[GenericMouseProtocol.ReportLength];
        buffer[0] = GenericMouseProtocol.BatteryReportId;
        int? percent = hid.GetFeature(buffer) ? GenericMouseProtocol.ParseBattery(buffer) : null;

        return [new DeviceBattery("mouse:248A8366", displayName, DeviceKind.Mouse, "2.4 GHz receiver", percent)];
    }
}
