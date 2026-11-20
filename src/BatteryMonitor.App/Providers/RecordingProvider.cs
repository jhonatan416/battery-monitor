using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Monitoring;
using BatteryMonitor.Core.Ports;

namespace BatteryMonitor.App.Providers;

/// <summary>Passes a provider's results through unchanged and records their USB ids in <see cref="RecentProducts"/>.</summary>
public sealed class RecordingProvider(IBatteryProvider inner, RecentProducts recent) : IBatteryProvider
{
    public string Name => inner.Name;

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var devices = inner.Poll();
        recent.Record(devices);
        return devices;
    }
}
