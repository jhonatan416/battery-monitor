namespace BatteryMonitor.Core.Protocols;

/// <summary>A Galaxy Buds reading: the lower earbud's level and whether an earbud charges.</summary>
public readonly record struct GalaxyBudsReading(int Percent, bool Charging);

/// <summary>
/// Galaxy Buds+ and later, from the status messages they send by themselves on their Bluetooth
/// serial channel (facts from GalaxyBudsClient and Gadgetbridge). Nothing is sent to the buds.
/// Frame: FD, little-endian header (size = low 10 bits, counting id + payload + CRC), id, payload,
/// CRC-16/XMODEM of id + payload (low byte first), DD. The original 2019 Buds use another framing.
/// </summary>
public static class GalaxyBuds
{
    /// <summary>Serial service of Buds+ and later, and the standard serial port service as a fallback.</summary>
    public static readonly Guid ServiceUuid = new("2e73a4ad-332d-41fc-90e2-16bef06523f2");
    public static readonly Guid StandardSerialUuid = new("00001101-0000-1000-8000-00805F9B34FB");

    private const byte Start = 0xFD, End = 0xDD, StatusUpdated = 0x60, ExtendedStatusUpdated = 0x61;
    private const int SizeMask = 0x3FF;

    /// <summary>True for Buds+ and later ("Galaxy Buds2 Pro (A1B2)"); false for the original "Galaxy Buds (A1B2)".</summary>
    public static bool IsSupportedName(string name)
    {
        int at = name.IndexOf("Galaxy Buds", StringComparison.OrdinalIgnoreCase);
        if (at < 0) return false;
        string rest = name[(at + "Galaxy Buds".Length)..].Trim();
        return rest.Length > 0 && rest[0] != '(';
    }

    /// <summary>The reading of the last valid status message in <paramref name="stream"/>, or null.</summary>
    public static GalaxyBudsReading? LatestReading(ReadOnlySpan<byte> stream)
    {
        GalaxyBudsReading? latest = null;
        for (int i = 0; i < stream.Length; i++)
        {
            if (stream[i] != Start || Frame(stream[i..]) is not { } frame) continue;
            if (Decode(frame.Id, frame.Payload) is { } reading) latest = reading;
            i += frame.Length - 1;
        }
        return latest;
    }

    private static (byte Id, byte[] Payload, int Length)? Frame(ReadOnlySpan<byte> s)
    {
        if (s.Length < 7) return null;
        int size = (s[1] | (s[2] << 8)) & SizeMask; // id + payload + 2 CRC bytes
        int length = 3 + size + 1;
        if (size < 3 || s.Length < length || s[length - 1] != End) return null;
        var body = s.Slice(3, size - 2); // id + payload
        ushort crc = (ushort)(s[3 + size - 2] | (s[3 + size - 1] << 8));
        if (Crc16(body) != crc) return null;
        return (body[0], body[1..].ToArray(), length);
    }

    private static GalaxyBudsReading? Decode(byte id, byte[] p) => id switch
    {
        StatusUpdated when p.Length >= 3 => Reading(p[1], p[2], p.Length >= 8 && (p[7] & 0x14) != 0), // 10 left, 04 right
        ExtendedStatusUpdated when p.Length >= 4 => Reading(p[2], p[3], false),
        _ => null,
    };

    // An earbud that is not connected reports 0: use the other one.
    private static GalaxyBudsReading? Reading(int left, int right, bool charging)
    {
        if (left > 100 || right > 100) return null;
        int[] levels = [.. new[] { left, right }.Where(l => l > 0)];
        return levels.Length == 0 ? null : new GalaxyBudsReading(levels.Min(), charging);
    }

    /// <summary>CRC-16/XMODEM: polynomial 0x1021, initial value 0, no reflection.</summary>
    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        int crc = 0;
        foreach (byte b in data)
        {
            crc ^= b << 8;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1;
        }
        return (ushort)crc;
    }
}
