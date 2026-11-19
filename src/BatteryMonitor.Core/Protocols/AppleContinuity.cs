namespace BatteryMonitor.Core.Protocols;

/// <summary>An AirPods / Beats battery reading: the lower earbud (or the single battery), in 10 % steps.</summary>
public readonly record struct AppleAudioReading(string Model, int Percent, bool Charging);

/// <summary>
/// The "proximity pairing" Bluetooth LE advertisement AirPods and Beats send by themselves (Apple
/// manufacturer data, company 0x004C), as documented by OpenPods, AirStatus and the furiousMAC
/// continuity dissector. Receiving it is passive: nothing is sent to the earbuds.
/// </summary>
public static class AppleContinuity
{
    public const ushort AppleCompanyId = 0x004C;

    private const int Length = 27;
    private const byte ProximityPairing = 0x07, PayloadLength = 0x19;
    private const int Absent = 15;

    /// <param name="data">Manufacturer data after the company id, starting at the type byte.</param>
    public static AppleAudioReading? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != Length || data[0] != ProximityPairing || data[1] != PayloadLength) return null;
        var (name, single) = Model(data[3], data[4]);
        bool flipped = (data[5] & 0x20) == 0;
        int low = data[6] & 0x0F, high = data[6] >> 4, chargingBits = data[7] >> 4;
        if (single) return Reading(name, [low], (chargingBits & 0x1) != 0);

        int left = flipped ? high : low, right = flipped ? low : high;
        bool leftCharging = (chargingBits & (flipped ? 0x2 : 0x1)) != 0;
        bool rightCharging = (chargingBits & (flipped ? 0x1 : 0x2)) != 0;
        return Reading(name, [left, right], leftCharging || rightCharging);
    }

    /// <summary>True for a paired device name that belongs to AirPods or Beats.</summary>
    public static bool IsAppleAudioName(string name) =>
        name.Contains("airpods", StringComparison.OrdinalIgnoreCase) || name.Contains("beats", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Advertisements carry no owner, so one is only used while this PC has AirPods/Beats connected
    /// and Windows does not already report their level.
    /// </summary>
    /// <param name="connectedNames">Names of the connected Bluetooth devices.</param>
    /// <param name="namesWithWindowsLevel">Names Windows already reports a battery level for.</param>
    public static bool NeedsAdvertisement(IEnumerable<string> connectedNames, IEnumerable<string> namesWithWindowsLevel) =>
        connectedNames.Any(IsAppleAudioName) && !namesWithWindowsLevel.Any(IsAppleAudioName);

    private static AppleAudioReading? Reading(string name, int[] levels, bool charging)
    {
        var present = levels.Where(l => l != Absent).ToList();
        if (present.Count == 0 || present.Any(l => l > 10)) return null;
        int lowest = present.Min();
        return new AppleAudioReading(name, lowest == 10 ? 100 : lowest * 10 + 5, charging);
    }

    // Two-byte model id; some models are only known by the low nibble of the first byte (OpenPods order).
    private static (string Name, bool Single) Model(byte b1, byte b2) => ((b1 << 8) | b2, b1 & 0x0F) switch
    {
        (0x0220, _) => ("AirPods", false),
        (0x0F20, _) => ("AirPods (2nd generation)", false),
        (0x1320, _) => ("AirPods (3rd generation)", false),
        (0x0E20, _) => ("AirPods Pro", false),
        (0x1420 or 0x2420, _) => ("AirPods Pro (2nd generation)", false),
        (0x2720, _) => ("AirPods Pro (3rd generation)", false),
        (_, 0xA) => ("AirPods Max", true),
        (_, 0xB) => ("Powerbeats Pro", false),
        (0x0520, _) => ("BeatsX", true),
        (0x1020, _) => ("Beats Flex", true),
        (0x0620, _) => ("Beats Solo3", true),
        (_, 0x9) => ("Beats Studio3", true),
        (0x0320, _) => ("Powerbeats3", true),
        _ => ("AirPods", false),
    };
}
