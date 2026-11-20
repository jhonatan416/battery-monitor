using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Protocols.Headsets;

namespace BatteryMonitor.Core.Protocols.Mice;

/// <summary>HyperX Pulsefire Dart (facts from two public reverse-engineering projects): "get heartbeat" 51.</summary>
public static class HyperXMice
{
    public const ushort VendorId = 0x0951;

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        new("HyperX Pulsefire Dart", VendorId, [0x16E1], 0xFF00, null, 2, Heartbeat, DeviceKind.Mouse), // receiver
        new("HyperX Pulsefire Dart", VendorId, [0x16E2], 0xFF13, null, 1, Heartbeat, DeviceKind.Mouse), // cable
    ];

    // 51 -> reply starting 51: [4] percent, [5] == 1 charging.
    private static HeadsetStatus? Heartbeat(IHidLink link, ushort _)
    {
        if (link.Exchange([0x00, 0x51], r => r.Length >= 6 && r[0] == 0x51) is not { } r || r[4] > 100) return null;
        return new HeadsetStatus(r[4], r[5] == 1);
    }
}

/// <summary>
/// Pulsar, Kysona, VXE and ATK mice: one shared protocol (facts from hid-pulsar, madr and libatk-rs).
/// 17-byte report 08: 08 cmd ... checksum, where byte 16 = 0x55 - sum of bytes 0..15. Command 04
/// (power) answers [6] percent, [7] 1 = on USB (charging).
/// </summary>
public static class AtkMice
{
    public const ushort PulsarVendorId = 0x3554, AtkVendorId = 0x373B;
    private const byte ReportId = 0x08, Power = 0x04, Magic = 0x55;
    private const int Length = 17;

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        Mouse("Pulsar mouse", PulsarVendorId, [0xF507, 0xF508, 0xF509]),
        Mouse("Kysona M600", PulsarVendorId, [0xF57D, 0xF57C, 0xF5D5]),
        Mouse("VXE Dragonfly R1 Pro", PulsarVendorId, [0xF58C, 0xF58A]),
        Mouse("ATK VXE R1 SE", PulsarVendorId, [0xF58F]),
        Mouse("ATK VXE R1 SE", AtkVendorId, [0x1085]),
        Mouse("VXE MAD R", AtkVendorId, [0x103F, 0x1040]),
    ];

    private static HeadsetSpec Mouse(string name, ushort vid, ushort[] pids) =>
        new(name, vid, pids, null, null, 1, (l, _) => Query(l), DeviceKind.Mouse);

    private static HeadsetStatus? Query(IHidLink link)
    {
        var request = new byte[Length];
        request[0] = ReportId;
        request[1] = Power;
        request[^1] = Checksum(request);
        var r = link.Exchange(request, x => x.Length >= Length && x[0] == ReportId && x[1] == Power && x[Length - 1] == Checksum(x));
        if (r is null || r[6] > 100 || r[7] > 1) return null;
        return new HeadsetStatus(r[6], Charging: r[7] == 1);
    }

    private static byte Checksum(byte[] frame)
    {
        byte sum = 0;
        for (int i = 0; i < Length - 1; i++) sum += frame[i];
        return (byte)(Magic - sum);
    }
}

/// <summary>
/// Glorious Model O / O- / D / D- Wireless (facts from mow and mxw): a feature report carrying the
/// "get" command 83, then the feature reply: [1] A1 = awake, [6] echoes 83, [8] percent. No
/// charging flag: on its cable (wired id) the mouse charges.
/// </summary>
public static class GloriousMice
{
    public const ushort VendorId = 0x258A;
    private const byte Get = 0x83, Awake = 0xA1;
    private static readonly HashSet<ushort> Wired = [0x2011, 0x2012, 0x2014];

    public static IEnumerable<HeadsetSpec> Specs =>
    [
        Mouse("Glorious Model O Wireless", [0x2022, 0x2011]),
        Mouse("Glorious Model D Wireless", [0x2023, 0x2012]),
        Mouse("Glorious Model O- Wireless", [0x2024, 0x2013]), // sources disagree which of the two is wired
        Mouse("Glorious Model D- Wireless", [0x2025, 0x2014]),
    ];

    private static HeadsetSpec Mouse(string name, ushort[] pids) =>
        new(name, VendorId, pids, null, null, 2, Query, DeviceKind.Mouse);

    private static HeadsetStatus? Query(IHidLink link, ushort productId)
    {
        var request = new byte[65];
        request[3] = 0x02;
        request[4] = 0x02;
        request[6] = Get;
        if (link.FeatureExchange(request) is not { Length: >= 9 } r || r[6] != Get || r[1] != Awake || r[8] > 100) return null;
        return new HeadsetStatus(Math.Max((int)r[8], 1), Charging: Wired.Contains(productId)); // the tools show 0 as 1
    }
}

/// <summary>Every supported wireless mouse family with a vendor battery protocol.</summary>
public static class MiceCatalog
{
    public static IReadOnlyList<HeadsetSpec> All { get; } =
        [.. SteelSeriesMice.Specs, .. HyperXMice.Specs, .. AtkMice.Specs, .. GloriousMice.Specs, .. CorsairMice.Specs];

    public static IReadOnlySet<ushort> VendorIds { get; } = All.Select(s => s.VendorId).ToHashSet();

    public static HeadsetSpec? Find(ushort vendorId, ushort productId) =>
        All.FirstOrDefault(s => s.VendorId == vendorId && s.ProductIds.Contains(productId));
}
