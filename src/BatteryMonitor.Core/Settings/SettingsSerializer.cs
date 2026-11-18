using System.Text.Json;

namespace BatteryMonitor.Core.Settings;

/// <param name="Settings">Valid settings: any invalid value is replaced by its default.</param>
/// <param name="Warnings">One message per value that was ignored, for showing to the user.</param>
public sealed record SettingsLoadResult(AppSettings Settings, IReadOnlyList<string> Warnings);

/// <summary>
/// Reads and writes the settings file. Parsing never throws: a broken or partly invalid file
/// keeps every valid value and falls back to defaults for the rest.
/// </summary>
public static class SettingsSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, WriteOptions);

    public static SettingsLoadResult Parse(string? json)
    {
        var defaults = new AppSettings();
        if (string.IsNullOrWhiteSpace(json)) return new SettingsLoadResult(defaults, []);

        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement; }
        catch (JsonException ex) { return new SettingsLoadResult(defaults, [$"Settings file is not valid JSON ({ex.Message}); using defaults."]); }
        if (root.ValueKind != JsonValueKind.Object)
            return new SettingsLoadResult(defaults, ["Settings file must contain a JSON object; using defaults."]);

        var warnings = new List<string>();
        var settings = defaults with
        {
            LowBatteryThreshold = ReadInt(root, "lowBatteryThreshold", defaults.LowBatteryThreshold,
                AppSettings.MinThreshold, AppSettings.MaxThreshold, warnings),
            PollIntervalSeconds = ReadInt(root, "pollIntervalSeconds", defaults.PollIntervalSeconds,
                AppSettings.MinPollSeconds, AppSettings.MaxPollSeconds, warnings),
            GenericMouseName = ReadName(root, "genericMouseName", defaults.GenericMouseName, warnings),
        };
        return new SettingsLoadResult(settings, warnings);
    }

    private static int ReadInt(JsonElement root, string name, int fallback, int min, int max, List<string> warnings)
    {
        if (!TryGet(root, name, out var value)) return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n) && n >= min && n <= max) return n;
        warnings.Add($"{name} must be a whole number from {min} to {max}; using {fallback}.");
        return fallback;
    }

    private static string ReadName(JsonElement root, string name, string fallback, List<string> warnings)
    {
        if (!TryGet(root, name, out var value)) return fallback;
        if (value.ValueKind == JsonValueKind.String && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text))
            return text.Trim();
        warnings.Add($"{name} must be a non-empty text; using \"{fallback}\".");
        return fallback;
    }

    private static bool TryGet(JsonElement root, string name, out JsonElement value)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            value = prop.Value;
            return true;
        }
        value = default;
        return false;
    }
}
