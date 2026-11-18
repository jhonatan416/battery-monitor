using Microsoft.Win32;

namespace BatteryMonitor.App.Settings;

/// <summary>
/// "Start with Windows" for the current user only (HKCU Run key). No admin rights needed.
/// Off until the user turns it on from the tray menu.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BatteryMonitor";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>True when the Run entry points at this executable.</summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value &&
               string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
