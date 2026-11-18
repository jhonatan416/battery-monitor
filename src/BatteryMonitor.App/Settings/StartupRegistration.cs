using System.Security;
using BatteryMonitor.Core.Ports;
using Microsoft.Win32;

namespace BatteryMonitor.App.Settings;

/// <summary>
/// "Start with Windows" for the current user only (HKCU Run key). No admin rights needed.
/// Off until the user turns it on from the tray menu. Never throws (see <see cref="IStartupRegistration"/>).
/// </summary>
internal sealed class StartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BatteryMonitor";

    /// <summary>Quoted path of this executable, or null when Windows cannot tell us where it is.</summary>
    private static string? Command =>
        string.IsNullOrEmpty(Environment.ProcessPath) ? null : $"\"{Environment.ProcessPath}\"";

    public bool IsEnabled()
    {
        if (Command is not { } command) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value &&
                   string.Equals(value, command, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (IsRegistryError(ex))
        {
            return false; // unreadable key (Group Policy, security software): report off, keep the menu usable
        }
    }

    public bool TrySetEnabled(bool enabled, out string? error)
    {
        if (!enabled) return TryWrite(key => key.DeleteValue(ValueName, throwOnMissingValue: false), out error);
        if (Command is not { } command)
        {
            error = "the app's own location is unknown, so Windows could not start it";
            return false; // never write an empty Run value
        }
        return TryWrite(key => key.SetValue(ValueName, command), out error);
    }

    private static bool TryWrite(Action<RegistryKey> write, out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            write(key);
            return true;
        }
        catch (Exception ex) when (IsRegistryError(ex))
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool IsRegistryError(Exception ex) =>
        ex is SecurityException or UnauthorizedAccessException or IOException;
}
