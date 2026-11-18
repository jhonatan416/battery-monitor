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

    /// <summary>
    /// True when the Run entry points at this executable. Never throws: an unreadable key
    /// (Group Policy, security software) reports "off" so the tray menu stays usable.
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value &&
                   string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
