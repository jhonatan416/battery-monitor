using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Monitoring;
using BatteryMonitor.Core.Ports;

namespace BatteryMonitor.Tests;

public class BatteryMonitorServiceTests
{
    private readonly ManualClock _clock = new();
    private readonly FakeProvider _mice = new("mice");
    private readonly FakeProvider _pads = new("pads");

    private BatteryMonitorService Service() =>
        new([_mice, _pads], _clock, new MonitorOptions { GracePeriod = TimeSpan.FromMinutes(2), LowThreshold = 20 });

    private static DeviceBattery Mouse(int? pct, bool charging = false) =>
        new("mouse", "Mouse", DeviceKind.Mouse, "2.4 GHz receiver", pct, charging);

    private static DeviceBattery Pad(int? pct) =>
        new("pad", "Pad", DeviceKind.Gamepad, "Bluetooth", pct);

    [Fact]
    public void Devices_from_all_providers_are_reported()
    {
        _mice.Next = [Mouse(88)];
        _pads.Next = [Pad(83)];

        var snap = Service().Poll();

        Assert.Equal(["mouse", "pad"], snap.Devices.Select(d => d.Battery.Id).Order());
        Assert.All(snap.Devices, d => Assert.False(d.Stale));
    }

    [Fact]
    public void A_failing_provider_does_not_hide_the_others()
    {
        _mice.Throws = true;
        _pads.Next = [Pad(83)];

        var snap = Service().Poll();

        Assert.Equal("pad", Assert.Single(snap.Devices).Battery.Id);
        Assert.Contains(snap.ProviderErrors, e => e.Contains("mice"));
    }

    [Fact]
    public void Missing_device_keeps_last_value_during_grace_period_then_disappears()
    {
        var svc = Service();
        _mice.Next = [Mouse(88)];
        svc.Poll();

        _mice.Next = [];
        _clock.Advance(TimeSpan.FromMinutes(1));
        var during = Assert.Single(svc.Poll().Devices);
        Assert.Equal(88, during.Battery.Percent);
        Assert.True(during.Stale);

        _clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Empty(svc.Poll().Devices);
    }

    [Fact]
    public void Unknown_level_keeps_last_known_percent()
    {
        var svc = Service();
        _mice.Next = [Mouse(88)];
        svc.Poll();

        _mice.Next = [Mouse(null)];
        var d = Assert.Single(svc.Poll().Devices);

        Assert.Equal(88, d.Battery.Percent);
        Assert.True(d.Stale);
    }

    [Fact]
    public void Device_without_level_on_first_poll_is_not_shown()
    {
        // A sleeping mouse can answer with no level before we ever read one: nothing to show yet.
        _mice.Next = [Mouse(null)];
        _pads.Next = [Pad(83)];

        var snap = Service().Poll();

        Assert.Equal("pad", Assert.Single(snap.Devices).Battery.Id);
        Assert.Empty(snap.NewLowBattery);
    }

    [Fact]
    public void Low_battery_alert_fires_once_when_crossing_threshold()
    {
        var svc = Service();
        _mice.Next = [Mouse(25)];
        Assert.Empty(svc.Poll().NewLowBattery);

        _mice.Next = [Mouse(19)];
        Assert.Equal("mouse", Assert.Single(svc.Poll().NewLowBattery).Id);

        _mice.Next = [Mouse(18)];
        Assert.Empty(svc.Poll().NewLowBattery);
    }

    [Fact]
    public void Low_battery_alert_rearms_after_charging()
    {
        var svc = Service();
        _mice.Next = [Mouse(15)];
        Assert.Single(svc.Poll().NewLowBattery);

        _mice.Next = [Mouse(16, charging: true)];
        Assert.Empty(svc.Poll().NewLowBattery);

        _mice.Next = [Mouse(14)];
        Assert.Single(svc.Poll().NewLowBattery);
    }

    [Fact]
    public void Charging_device_below_threshold_does_not_alert()
    {
        _mice.Next = [Mouse(10, charging: true)];

        Assert.Empty(Service().Poll().NewLowBattery);
    }

    private sealed class FakeProvider(string name) : IBatteryProvider
    {
        public IReadOnlyList<DeviceBattery> Next { get; set; } = [];
        public bool Throws { get; set; }
        public string Name => name;
        public IReadOnlyList<DeviceBattery> Poll() =>
            Throws ? throw new InvalidOperationException("device error") : Next;
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
