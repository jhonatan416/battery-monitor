using BatteryMonitor.App.Hid;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// Logitech HID++ 2.0 devices: on Unifying / Lightspeed / Bolt receivers (paired slots 1-6) or
/// connected directly by USB. Only "get" functions are sent (read-only); replies are matched by
/// device, feature, function and our software id, so G HUB or other tools can run at the same time.
/// </summary>
public sealed class LogitechBatteryProvider : IBatteryProvider
{
    private const ushort VendorPage = 0xFF00, LongCollection = 0x0002;
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromMilliseconds(80);
    private const int MaxReportsPerRequest = 8;

    public string Name => "Logitech";

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var result = new List<DeviceBattery>();
        foreach (var coll in HidCollection.Enumerate(LogitechHidpp.VendorId))
        {
            if (HidBattery.IsBluetoothPath(coll.Path)) continue; // the Bluetooth provider covers these
            if (HidBatteryReader.TopLevelUsage(coll) != (VendorPage, LongCollection)) continue;
            using var stream = HidStream.Open(coll);
            if (stream is null) continue;
            foreach (byte index in LogitechHidpp.DeviceIndices(coll.ProductId))
                if (ReadDevice(stream, coll, index) is { } device) result.Add(device);
        }
        return result;
    }

    private static DeviceBattery? ReadDevice(HidStream stream, HidCollection coll, byte index)
    {
        var battery = ReadBattery(stream, index);
        if (battery is not { } reading) return null; // empty slot, device off, or no battery feature
        var (name, kind) = ReadIdentity(stream, index);
        bool receiver = LogitechHidpp.IsReceiver(coll.ProductId);
        return new DeviceBattery(
            $"logi:{coll.ProductId:X4}:{index:X2}",
            name ?? coll.ProductName() ?? "Logitech device",
            kind,
            receiver ? "Logitech receiver" : "USB",
            reading.Percent,
            reading.Charging);
    }

    private static LogitechBatteryReading? ReadBattery(HidStream stream, byte index)
    {
        if (FeatureIndex(stream, index, LogitechHidpp.UnifiedBatteryFeature) is byte unified &&
            Request(stream, index, unified, 0x01) is { } status) // get_status
            return LogitechHidpp.DecodeUnifiedBattery(status);
        if (FeatureIndex(stream, index, LogitechHidpp.BatteryStatusFeature) is byte legacy &&
            Request(stream, index, legacy, 0x00) is { } level)  // GetBatteryLevelStatus
            return LogitechHidpp.DecodeBatteryStatus(level);
        return null;
    }

    private static (string? Name, DeviceKind Kind) ReadIdentity(HidStream stream, byte index)
    {
        if (FeatureIndex(stream, index, LogitechHidpp.DeviceNameFeature) is not byte feature) return (null, DeviceKind.Other);
        DeviceKind kind = Request(stream, index, feature, 0x02) is { } type // getDeviceType
            ? LogitechHidpp.KindFromDeviceType(type[0])
            : DeviceKind.Other;
        if (Request(stream, index, feature, 0x00) is not { } count || count[0] == 0) return (null, kind); // getCount
        var chunks = new List<byte[]>();
        for (int offset = 0; offset < count[0]; offset += 16)
        {
            if (Request(stream, index, feature, 0x01, (byte)offset) is not { } chunk) return (null, kind); // getDeviceName
            chunks.Add(chunk);
        }
        return (LogitechHidpp.JoinName(count[0], chunks), kind);
    }

    /// <summary>Root.getFeature: the index of a feature on this device, or null if unsupported.</summary>
    private static byte? FeatureIndex(HidStream stream, byte index, ushort featureId)
    {
        var reply = Request(stream, index, LogitechHidpp.RootIndex, 0x00, (byte)(featureId >> 8), (byte)featureId);
        return reply is { } p && p[0] != 0 ? p[0] : null;
    }

    private static byte[]? Request(HidStream stream, byte index, byte feature, byte function, params byte[] parameters)
    {
        if (!stream.Write(LogitechHidpp.BuildRequest(index, feature, function, parameters))) return null;
        for (int i = 0; i < MaxReportsPerRequest; i++)
        {
            if (stream.Read(ReplyTimeout) is not { } report) return null;
            var reply = LogitechHidpp.ParseReply(report, index, feature, function);
            if (reply.Status == HidppStatus.Ok) return reply.Parameters;
            if (reply.Status == HidppStatus.Error) return null;
        }
        return null;
    }
}
