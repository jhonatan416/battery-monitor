using BatteryMonitor.Core.Settings;

namespace BatteryMonitor.Tests;

public class SettingsSerializerTests
{
    private static readonly AppSettings Defaults = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_file_content_gives_defaults_without_warnings(string? json)
    {
        var result = SettingsSerializer.Parse(json);

        Assert.Equal(Defaults, result.Settings);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Valid_file_is_read_case_insensitively()
    {
        var result = SettingsSerializer.Parse(
            """{ "lowBatteryThreshold": 15, "POLLINTERVALSECONDS": 120 }""");

        Assert.Equal(new AppSettings { LowBatteryThreshold = 15, PollIntervalSeconds = 120 }, result.Settings);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Old_file_with_the_removed_mouse_name_still_loads_without_warnings()
    {
        // Files written before the Memzuiox removal (2026-11-19) still contain genericMouseName.
        var result = SettingsSerializer.Parse(
            """{ "lowBatteryThreshold": 20, "pollIntervalSeconds": 60, "genericMouseName": "Memzuiox Mouse" }""");

        Assert.Equal(new AppSettings(), result.Settings);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Missing_fields_keep_their_defaults()
    {
        var result = SettingsSerializer.Parse("""{ "lowBatteryThreshold": 30 }""");

        Assert.Equal(Defaults with { LowBatteryThreshold = 30 }, result.Settings);
    }

    [Fact]
    public void Malformed_json_gives_defaults_and_a_warning()
    {
        var result = SettingsSerializer.Parse("{ not json");

        Assert.Equal(Defaults, result.Settings);
        Assert.Single(result.Warnings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Out_of_range_threshold_falls_back_to_default(int threshold)
    {
        var result = SettingsSerializer.Parse($$"""{ "lowBatteryThreshold": {{threshold}}, "pollIntervalSeconds": 90 }""");

        Assert.Equal(Defaults.LowBatteryThreshold, result.Settings.LowBatteryThreshold);
        Assert.Equal(90, result.Settings.PollIntervalSeconds); // other valid values are kept
        Assert.Contains(result.Warnings, w => w.Contains("lowBatteryThreshold"));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7200)]
    public void Out_of_range_poll_interval_falls_back_to_default(int seconds)
    {
        var result = SettingsSerializer.Parse($$"""{ "pollIntervalSeconds": {{seconds}} }""");

        Assert.Equal(Defaults.PollIntervalSeconds, result.Settings.PollIntervalSeconds);
        Assert.Contains(result.Warnings, w => w.Contains("pollIntervalSeconds"));
    }

    [Theory]
    [InlineData(AppSettings.MinThreshold)]
    [InlineData(AppSettings.MaxThreshold)]
    public void Threshold_at_the_range_limits_is_accepted(int threshold)
    {
        var result = SettingsSerializer.Parse($$"""{ "lowBatteryThreshold": {{threshold}} }""");

        Assert.Equal(threshold, result.Settings.LowBatteryThreshold);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData(AppSettings.MinPollSeconds)]
    [InlineData(AppSettings.MaxPollSeconds)]
    public void Poll_interval_at_the_range_limits_is_accepted(int seconds)
    {
        var result = SettingsSerializer.Parse($$"""{ "pollIntervalSeconds": {{seconds}} }""");

        Assert.Equal(seconds, result.Settings.PollIntervalSeconds);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Serialized_settings_parse_back_to_the_same_values()
    {
        var custom = new AppSettings { LowBatteryThreshold = 25, PollIntervalSeconds = 300, HeadsetControlPath = @"C:\Tools\headsetcontrol.exe" };

        var result = SettingsSerializer.Parse(SettingsSerializer.Serialize(custom));

        Assert.Equal(custom, result.Settings);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void HeadsetControl_is_off_by_default()
    {
        Assert.Equal("", Defaults.HeadsetControlPath);
    }

    [Fact]
    public void HeadsetControl_path_is_read_and_trimmed()
    {
        var result = SettingsSerializer.Parse("""{ "headsetControlPath": "  headsetcontrol  " }""");

        Assert.Equal("headsetcontrol", result.Settings.HeadsetControlPath);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    public void HeadsetControl_path_that_is_not_text_is_ignored_with_a_warning(string value)
    {
        var result = SettingsSerializer.Parse($$"""{ "headsetControlPath": {{value}} }""");

        Assert.Equal("", result.Settings.HeadsetControlPath);
        Assert.Contains("headsetControlPath", Assert.Single(result.Warnings));
    }

    [Fact]
    public void Existing_numeric_settings_are_unchanged_by_the_new_path_setting()
    {
        var result = SettingsSerializer.Parse("""{ "lowBatteryThreshold": 0, "headsetControlPath": "hc" }""");

        Assert.Equal(Defaults.LowBatteryThreshold, result.Settings.LowBatteryThreshold);
        Assert.Equal("hc", result.Settings.HeadsetControlPath);
        Assert.Single(result.Warnings);
    }
}
