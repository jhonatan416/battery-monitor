using BatteryMonitor.Core.Domain;

namespace BatteryMonitor.Core.Monitoring;

/// <summary>
/// USB products that a dedicated provider reported within a time window. HeadsetControl results are
/// skipped only for these: a headset our own code reads is not shown twice, and one it cannot read
/// (unknown model, newer firmware) still shows up. The window matches the grace period, so a missed
/// poll does not briefly add a HeadsetControl duplicate. Thread-safe.
/// </summary>
public sealed class RecentProducts(TimeProvider clock, TimeSpan window)
{
    private readonly object _gate = new();
    private readonly Dictionary<UsbProduct, DateTimeOffset> _seen = new();

    public void Record(IEnumerable<DeviceBattery> devices)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
            foreach (var d in devices)
                if (d.Usb is UsbProduct usb) _seen[usb] = now;
    }

    public bool Contains(ushort vendorId, ushort productId)
    {
        lock (_gate)
            return _seen.TryGetValue(new UsbProduct(vendorId, productId), out var at) && clock.GetUtcNow() - at <= window;
    }
}
