using BatteryMonitor.Core.Settings;

namespace BatteryMonitor.App.Settings;

/// <summary>
/// Settings file at %APPDATA%\BatteryMonitor\settings.json. Created with defaults on first run
/// so the user can find and edit it ("Open settings file" in the tray menu).
/// </summary>
internal static class SettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryMonitor");

    public static string FilePath { get; } = Path.Combine(Folder, "settings.json");

    public static SettingsLoadResult Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var defaults = new AppSettings();
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, SettingsSerializer.Serialize(defaults));
                return new SettingsLoadResult(defaults, []);
            }
            return SettingsSerializer.Parse(File.ReadAllText(FilePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SettingsLoadResult(new AppSettings(), [$"Cannot read {FilePath} ({ex.Message}); using defaults."]);
        }
    }
}
