using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

/// <summary>
/// JSON written by `headsetcontrol -b -o json` (HeadsetControl 3.0+): a top-level object with a
/// "devices" array; each device has "device", "id_vendor"/"id_product" as "0x1b1c" strings and an
/// optional "battery" object with "status" and "level" (-1 when unknown).
/// </summary>
public class HeadsetControlOutputTests
{
    private static string Output(string devices) =>
        $$"""{"name":"HeadsetControl","version":"4.3.0","device_count":1,"devices":[{{devices}}]}""";

    private static string Device(string battery, string name = "Corsair VOID Pro", string vid = "0x1b1c", string pid = "0x1b27") =>
        $$"""{"status":"success","device":"{{name}}","id_vendor":"{{vid}}","id_product":"{{pid}}","battery":{{battery}}}""";

    [Fact]
    public void Available_battery_gives_name_ids_and_level()
    {
        var devices = HeadsetControlOutput.Parse(Output(Device("""{"status":"BATTERY_AVAILABLE","level":85}""")));

        Assert.Equal(new HeadsetControlDevice("Corsair VOID Pro", 0x1B1C, 0x1B27, 85, false), Assert.Single(devices));
    }

    [Fact]
    public void Charging_keeps_the_level_when_reported()
    {
        var d = Assert.Single(HeadsetControlOutput.Parse(Output(Device("""{"status":"BATTERY_CHARGING","level":40}"""))));

        Assert.Equal(40, d.Percent);
        Assert.True(d.Charging);
    }

    [Fact]
    public void Charging_without_a_level_has_no_percent()
    {
        var d = Assert.Single(HeadsetControlOutput.Parse(Output(Device("""{"status":"BATTERY_CHARGING","level":-1}"""))));

        Assert.Null(d.Percent);
        Assert.True(d.Charging);
    }

    [Theory]
    [InlineData("""{"status":"BATTERY_UNAVAILABLE","level":-1}""")] // headset off
    [InlineData("""{"status":"BATTERY_ERROR","level":-1}""")]
    [InlineData("""{"status":"BATTERY_TIMEOUT","level":-1}""")]
    [InlineData("""{"status":"BATTERY_AVAILABLE","level":-1}""")]
    [InlineData("""{"status":"BATTERY_AVAILABLE","level":101}""")]
    [InlineData("""{"status":"BATTERY_AVAILABLE","level":"85"}""")]
    public void Devices_without_a_usable_battery_are_skipped(string battery)
    {
        Assert.Empty(HeadsetControlOutput.Parse(Output(Device(battery))));
    }

    [Fact]
    public void Device_without_battery_object_is_skipped()
    {
        string json = Output("""{"status":"success","device":"X","id_vendor":"0x1b1c","id_product":"0x1b27"}""");

        Assert.Empty(HeadsetControlOutput.Parse(json));
    }

    [Fact]
    public void No_devices_gives_an_empty_list()
    {
        Assert.Empty(HeadsetControlOutput.Parse("""{"name":"HeadsetControl","device_count":0,"devices":[]}"""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"devices":"nope"}""")]
    [InlineData("""{"devices":[42]}""")]
    public void Unexpected_output_gives_an_empty_list(string? json)
    {
        Assert.Empty(HeadsetControlOutput.Parse(json));
    }

    [Fact]
    public void Device_with_bad_ids_is_skipped_but_others_are_kept()
    {
        string json = Output(Device("""{"status":"BATTERY_AVAILABLE","level":50}""", vid: "zz") + "," +
                             Device("""{"status":"BATTERY_AVAILABLE","level":60}""", name: "Arctis 7", vid: "0x1038", pid: "0x12ad"));

        Assert.Equal(new HeadsetControlDevice("Arctis 7", 0x1038, 0x12AD, 60, false), Assert.Single(HeadsetControlOutput.Parse(json)));
    }

    [Fact]
    public void Command_reads_battery_as_json()
    {
        Assert.Equal("-b -o json", HeadsetControlOutput.Arguments);
    }
}
