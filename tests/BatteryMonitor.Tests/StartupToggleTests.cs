using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Settings;

namespace BatteryMonitor.Tests;

public class StartupToggleTests
{
    private readonly FakeRegistration _reg = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Requesting_the_current_state_does_not_write(bool state)
    {
        // The tray menu re-syncs its checkbox on every open; that must never write the registry.
        _reg.Enabled = state;

        var result = StartupToggle.Apply(_reg, requested: state);

        Assert.Equal(new ToggleResult(state, null), result);
        Assert.Equal(0, _reg.Writes);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Requesting_a_different_state_writes_it(bool current, bool requested)
    {
        _reg.Enabled = current;

        var result = StartupToggle.Apply(_reg, requested);

        Assert.Equal(new ToggleResult(requested, null), result);
        Assert.Equal(1, _reg.Writes);
    }

    [Fact]
    public void Failed_write_reports_the_real_state_and_the_error()
    {
        _reg.Enabled = false;
        _reg.FailWith = "Access denied";

        var result = StartupToggle.Apply(_reg, requested: true);

        Assert.Equal(new ToggleResult(false, "Access denied"), result);
    }

    private sealed class FakeRegistration : IStartupRegistration
    {
        public bool Enabled { get; set; }
        public string? FailWith { get; set; }
        public int Writes { get; private set; }

        public bool IsEnabled() => Enabled;

        public bool TrySetEnabled(bool enabled, out string? error)
        {
            Writes++;
            error = FailWith;
            if (FailWith is not null) return false;
            Enabled = enabled;
            return true;
        }
    }
}
