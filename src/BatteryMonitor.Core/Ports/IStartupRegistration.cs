namespace BatteryMonitor.Core.Ports;

/// <summary>Driven port: where "start with Windows" is stored (the HKCU Run key on Windows).</summary>
public interface IStartupRegistration
{
    /// <summary>Current state. Never throws: an unreadable store reports false.</summary>
    bool IsEnabled();

    /// <summary>Writes the state. Never throws: returns false with a user-readable error instead.</summary>
    bool TrySetEnabled(bool enabled, out string? error);
}
