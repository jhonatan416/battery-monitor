using BatteryMonitor.Core.Ports;

namespace BatteryMonitor.Core.Settings;

/// <param name="Enabled">The state actually stored after the request.</param>
/// <param name="Error">Why the request failed, or null.</param>
public sealed record ToggleResult(bool Enabled, string? Error);

/// <summary>Decides when the "Start with Windows" menu item really needs to write.</summary>
public static class StartupToggle
{
    public static ToggleResult Apply(IStartupRegistration registration, bool requested)
    {
        bool current = registration.IsEnabled();
        if (current == requested) return new ToggleResult(current, null); // menu re-sync: nothing to write

        bool written = registration.TrySetEnabled(requested, out string? error);
        return new ToggleResult(registration.IsEnabled(), written ? null : error);
    }
}
