namespace BatteryMonitor.Core.Protocols.Headsets;

/// <summary>
/// Battery read over Logitech's "Centurion" protocol, as used by the PRO X 2 LIGHTSPEED with newer
/// firmware (facts from the public HeadsetControl protocol definition). Only "get" functions are
/// sent: Root.getFeature on the dongle and, through its bridge, on the headset, then the battery
/// state. Every frame is report 51: 51 LEN FLAGS payload, LEN = payload length + 1.
/// </summary>
internal static class LogitechCenturion
{
    public enum Outcome { Reading, Unsupported, Offline }

    private const byte ReportId = 0x51, SoftwareId = 0x01, Root = 0x00;
    private const byte BridgeSend = 0x10, BridgeEvent = 0x10, Rejected = 0xFF;
    private const ushort BridgeFeature = 0x0003, BatteryFeature = 0x0104;

    /// <summary>
    /// <see cref="Outcome.Unsupported"/> when the dongle does not answer Centurion or the headset has no
    /// battery feature (the caller may try the older request); <see cref="Outcome.Offline"/> when the
    /// headset is off, rejected the request or sent no usable reply.
    /// </summary>
    public static (Outcome Outcome, HeadsetStatus? Status) Battery(IHidLink link)
    {
        if (Direct(link, Root, FeatureId(BridgeFeature)) is not { Length: > 0 } bridge || bridge[0] == 0)
            return (Outcome.Unsupported, null);
        var lookup = ThroughBridge(link, bridge[0], Root, FeatureId(BatteryFeature));
        if (lookup.Offline) return (Outcome.Offline, null);
        if (lookup.Data is not { Length: > 0 } found || found[0] == 0) return (Outcome.Unsupported, null);

        var read = ThroughBridge(link, bridge[0], found[0], []);
        if (read.Data is not { Length: > 0 } d || d[0] > 100) return (Outcome.Offline, null);
        byte state = d.Length >= 3 ? d[2] : (byte)0;
        return (Outcome.Reading, new HeadsetStatus(d[0], Charging: state is 1 or 2));
    }

    private static byte[] FeatureId(ushort id) => [(byte)(id >> 8), (byte)id];

    private static byte[] Frame(byte[] payload) => [ReportId, (byte)(payload.Length + 1), 0x00, .. payload];

    /// <summary>The payload of a frame: the bytes after LEN and FLAGS, LEN - 1 of them.</summary>
    private static byte[]? Payload(byte[] frame)
    {
        if (frame.Length < 4 || frame[0] != ReportId || frame[1] <= 1 || frame[1] + 2 > frame.Length) return null;
        return frame[3..(frame[1] + 2)];
    }

    // Direct request: [feature, function 0 | software id, params]; the reply starts with the same feature.
    private static byte[]? Direct(IHidLink link, byte feature, byte[] parameters)
    {
        var reply = link.Exchange(Frame([feature, SoftwareId, .. parameters]), r => Payload(r) is { Length: >= 2 } p && p[0] == feature);
        return reply is not null && Payload(reply) is { } payload ? payload[2..] : null;
    }

    /// <summary>
    /// Bridge request: [bridge, 11, size (12 bits), 00 feature function|1 params]. Skips the ack (software
    /// id 1) and the "headset on" event; stops at a rejection or the "headset off" event.
    /// </summary>
    private static (bool Offline, byte[]? Data) ThroughBridge(IHidLink link, byte bridge, byte feature, byte[] parameters)
    {
        byte[] sub = [0x00, feature, SoftwareId, .. parameters];
        byte[] request = Frame([bridge, BridgeSend | SoftwareId, (byte)((sub.Length >> 8) & 0x0F), (byte)sub.Length, .. sub]);
        var reply = link.Exchange(request, r => Payload(r) is { } p && (IsRejection(p, bridge) || IsOffEvent(p, bridge) || IsReplyFor(p, bridge, feature)));
        if (reply is null || Payload(reply) is not { } p || !IsReplyFor(p, bridge, feature)) return (true, null);
        return p[5] == Rejected ? (true, null) : (false, p[7..]);
    }

    private static bool IsRejection(byte[] p, byte bridge) =>
        p.Length >= 3 && p[0] == Rejected && p[1] == bridge && p[2] == (BridgeSend | SoftwareId);

    private static bool IsOffEvent(byte[] p, byte bridge) => p.Length >= 4 && p[0] == bridge && p[1] == 0x00 && p[3] == 0x00;

    // Message event from the headset (function 1, software id 0) whose sub-message answers our feature.
    private static bool IsReplyFor(byte[] p, byte bridge, byte feature) =>
        p.Length >= 7 && p[0] == bridge && p[1] == BridgeEvent && p[4] == 0x00 &&
        (p[5] == feature || (p[5] == Rejected && p[6] == feature));
}
