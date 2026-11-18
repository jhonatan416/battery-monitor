using BatteryMonitor.Core.Domain;

namespace BatteryMonitor.Core.Ports;

/// <summary>
/// Driven port: one adapter per device family (Bluetooth, Razer, ...).
/// Poll is blocking and is always called from a background thread.
/// </summary>
public interface IBatteryProvider
{
    string Name { get; }

    /// <summary>Returns every device this provider can currently see. Never returns null.</summary>
    IReadOnlyList<DeviceBattery> Poll();
}
