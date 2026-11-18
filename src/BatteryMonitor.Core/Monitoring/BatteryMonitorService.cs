using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;

namespace BatteryMonitor.Core.Monitoring;

public sealed class MonitorOptions
{
    /// <summary>How long to keep showing the last value of a device that stops answering.</summary>
    public TimeSpan GracePeriod { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Alert when a device drops below this percent.</summary>
    public int LowThreshold { get; init; } = 20;

    /// <summary>Points above the threshold a device must recover before alerting again.</summary>
    public int Hysteresis { get; init; } = 5;
}

/// <param name="Battery">Latest known state.</param>
/// <param name="LastSeen">When a level was last read from the device.</param>
/// <param name="Stale">True when this is a remembered value, not a fresh reading.</param>
public sealed record DeviceReading(DeviceBattery Battery, DateTimeOffset LastSeen, bool Stale);

public sealed record MonitorSnapshot(
    IReadOnlyList<DeviceReading> Devices,
    IReadOnlyList<DeviceBattery> NewLowBattery,
    IReadOnlyList<string> ProviderErrors);

/// <summary>
/// Application service: polls every provider, smooths over devices that briefly stop answering,
/// and decides when to raise a low-battery alert. Not thread safe: call Poll from one thread.
/// </summary>
public sealed class BatteryMonitorService(
    IEnumerable<IBatteryProvider> providers,
    TimeProvider clock,
    MonitorOptions options)
{
    private readonly IReadOnlyList<IBatteryProvider> _providers = providers.ToList();
    private readonly Dictionary<string, Tracked> _tracked = new();

    private sealed class Tracked(DeviceBattery last, DateTimeOffset lastSeen)
    {
        public DeviceBattery Last { get; set; } = last;
        public DateTimeOffset LastSeen { get; set; } = lastSeen;
        public bool Alerted { get; set; }
    }

    public MonitorSnapshot Poll()
    {
        DateTimeOffset now = clock.GetUtcNow();
        var errors = new List<string>();
        var reported = new List<DeviceBattery>();

        foreach (var provider in _providers)
        {
            try { reported.AddRange(provider.Poll()); }
            catch (Exception ex) { errors.Add($"{provider.Name}: {ex.Message}"); }
        }

        var readings = new List<DeviceReading>();
        var alerts = new List<DeviceBattery>();
        var fresh = new HashSet<string>();

        foreach (var device in reported)
        {
            if (device.Percent is not int percent) continue; // no level this time: handled below
            if (!fresh.Add(device.Id)) continue; // first provider wins on duplicate ids
            if (!_tracked.TryGetValue(device.Id, out var t))
                _tracked[device.Id] = t = new Tracked(device, now);
            t.Last = device;
            t.LastSeen = now;
            if (UpdateAlert(t, percent, device.Charging)) alerts.Add(device);
            readings.Add(new DeviceReading(device, now, Stale: false));
        }

        // Devices that did not give a level this time: remember them for the grace period.
        foreach (var (id, t) in _tracked.ToList())
        {
            if (fresh.Contains(id)) continue;
            if (now - t.LastSeen > options.GracePeriod) { _tracked.Remove(id); continue; }
            readings.Add(new DeviceReading(t.Last, t.LastSeen, Stale: true));
        }

        return new MonitorSnapshot(
            readings.OrderBy(r => r.Battery.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            alerts,
            errors);
    }

    private bool UpdateAlert(Tracked t, int pct, bool charging)
    {
        if (charging || pct >= options.LowThreshold + options.Hysteresis)
        {
            t.Alerted = false;
            return false;
        }
        if (pct < options.LowThreshold && !t.Alerted)
        {
            t.Alerted = true;
            return true;
        }
        return false;
    }
}
