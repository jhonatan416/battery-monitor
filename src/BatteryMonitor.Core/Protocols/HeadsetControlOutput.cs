using System.Globalization;
using System.Text.Json;

namespace BatteryMonitor.Core.Protocols;

/// <summary>A headset reported by HeadsetControl. A null Percent with Charging true means "charging, level not reported".</summary>
public sealed record HeadsetControlDevice(string Name, ushort VendorId, ushort ProductId, int? Percent, bool Charging);

/// <summary>
/// Output of the HeadsetControl command-line tool (https://github.com/Sapd/HeadsetControl, 3.0+)
/// in JSON mode: { "devices": [ { "device", "id_vendor": "0x1b1c", "id_product", "battery":
/// { "status": "BATTERY_AVAILABLE" | "BATTERY_CHARGING" | ..., "level" } } ] }. Parsing never throws.
/// </summary>
public static class HeadsetControlOutput
{
    public const string Arguments = "-b -o json";

    public static IReadOnlyList<HeadsetControlDevice> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array)
                return [];
            var result = new List<HeadsetControlDevice>();
            foreach (var device in devices.EnumerateArray())
                if (ReadDevice(device) is { } d) result.Add(d);
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static HeadsetControlDevice? ReadDevice(JsonElement device)
    {
        if (device.ValueKind != JsonValueKind.Object) return null;
        if (Text(device, "device") is not { } name || Hex(device, "id_vendor") is not ushort vid ||
            Hex(device, "id_product") is not ushort pid) return null;
        if (!device.TryGetProperty("battery", out var battery) || battery.ValueKind != JsonValueKind.Object) return null;
        int? level = battery.TryGetProperty("level", out var l) && l.ValueKind == JsonValueKind.Number &&
                     l.TryGetInt32(out int n) && n is >= 0 and <= 100 ? n : null;
        return Text(battery, "status") switch
        {
            "BATTERY_AVAILABLE" when level is not null => new HeadsetControlDevice(name, vid, pid, level, false),
            "BATTERY_CHARGING" => new HeadsetControlDevice(name, vid, pid, level, true),
            _ => null, // headset off, read error or timeout
        };
    }

    private static string? Text(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static ushort? Hex(JsonElement obj, string name) =>
        Text(obj, name) is { } s && s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
        ushort.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort id) ? id : null;
}
