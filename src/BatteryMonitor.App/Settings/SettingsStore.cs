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

    public static SettingsLoadResult Load() => File.Exists(FilePath) ? Read() : CreateDefaults();

    private static SettingsLoadResult Read()
    {
        try { return SettingsSerializer.Parse(File.ReadAllText(FilePath)); }
        catch (Exception ex) when (IsFileError(ex))
        {
            return new SettingsLoadResult(new AppSettings(), [$"Cannot read {FilePath} ({ex.Message}); using defaults."]);
        }
    }

    private static SettingsLoadResult CreateDefaults()
    {
        var defaults = new AppSettings();
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, SettingsSerializer.Serialize(defaults));
            return new SettingsLoadResult(defaults, []);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            // Defaults still apply; the user just has no file to edit until this is fixed.
            return new SettingsLoadResult(defaults, [$"Cannot create {FilePath} ({ex.Message}); using defaults."]);
        }
    }

    private static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException;
}
