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
            """{ "lowBatteryThreshold": 15, "POLLINTERVALSECONDS": 120, "genericMouseName": "My Mouse" }""");

        Assert.Equal(new AppSettings { LowBatteryThreshold = 15, PollIntervalSeconds = 120, GenericMouseName = "My Mouse" }, result.Settings);
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

    [Fact]
    public void Blank_mouse_name_falls_back_to_default()
    {
        var result = SettingsSerializer.Parse("""{ "genericMouseName": "  " }""");

        Assert.Equal(Defaults.GenericMouseName, result.Settings.GenericMouseName);
        Assert.Contains(result.Warnings, w => w.Contains("genericMouseName"));
    }

    [Fact]
    public void Serialized_settings_parse_back_to_the_same_values()
    {
        var custom = new AppSettings { LowBatteryThreshold = 25, PollIntervalSeconds = 300, GenericMouseName = "Office mouse" };

        var result = SettingsSerializer.Parse(SettingsSerializer.Serialize(custom));

        Assert.Equal(custom, result.Settings);
        Assert.Empty(result.Warnings);
    }
}
