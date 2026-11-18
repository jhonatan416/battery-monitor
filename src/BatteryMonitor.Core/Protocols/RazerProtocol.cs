namespace BatteryMonitor.Core.Protocols;

public readonly record struct RazerCommand(byte Class, byte Id, byte DataSize);

public enum RazerStatus { Ok, Busy, Offline, Unsupported, Mismatch }

public sealed record RazerReply(RazerStatus Status, byte[] Arguments)
{
    public static readonly RazerReply Mismatch = new(RazerStatus.Mismatch, []);
}

/// <summary>
/// Razer's 90-byte feature report, as publicly documented by the OpenRazer project.
/// Buffer layout used here (with the HID report id at index 0, so report byte N is buffer[N + 1]):
///   [0] status, [1] transaction id, [2..3] remaining packets, [4] protocol type, [5] data size,
///   [6] command class, [7] command id, [8..87] arguments, [88] xor checksum of bytes 2..87.
/// Status: 01 busy, 02 ok, 03 failed, 04 timeout (device off), 05 not supported.
/// </summary>
public static class RazerProtocol
{
    public const int ReportLength = 91; // 1 report id byte + 90 byte report

    public static readonly RazerCommand BatteryLevel = new(0x07, 0x80, 0x02);
    public static readonly RazerCommand ChargingStatus = new(0x07, 0x84, 0x02);

    /// <summary>Transaction ids used by Razer wireless mice and dongles, most common first.</summary>
    public static readonly byte[] MouseTransactionIds = [0x1F, 0x3F, 0xFF, 0x08, 0x9F];

    public static byte[] BuildRequest(byte transactionId, RazerCommand cmd)
    {
        var buf = new byte[ReportLength];
        Span<byte> r = buf.AsSpan(1);
        r[1] = transactionId;
        r[5] = cmd.DataSize;
        r[6] = cmd.Class;
        r[7] = cmd.Id;
        r[88] = Checksum(r);
        return buf;
    }

    public static RazerReply ParseReply(ReadOnlySpan<byte> buffer, byte transactionId, RazerCommand cmd)
    {
        if (buffer.Length < ReportLength) return RazerReply.Mismatch;
        ReadOnlySpan<byte> a = buffer.Slice(1, 90);

        // Another program (Synapse) can leave its own reply in the shared mailbox: ignore it.
        bool sameRequest = a[1] == transactionId && a[2] == 0 && a[3] == 0 && a[4] == 0 &&
                           a[5] == cmd.DataSize && a[6] == cmd.Class && a[7] == cmd.Id;
        if (!sameRequest || a[88] != Checksum(a)) return RazerReply.Mismatch;

        return a[0] switch
        {
            0x02 => new RazerReply(RazerStatus.Ok, a.Slice(8, 80).ToArray()),
            0x01 => new RazerReply(RazerStatus.Busy, []),
            0x04 => new RazerReply(RazerStatus.Offline, []),
            0x03 or 0x05 => new RazerReply(RazerStatus.Unsupported, []),
            _ => RazerReply.Mismatch,
        };
    }

    /// <summary>Razer reports the level as 0..255.</summary>
    public static int ToPercent(byte raw) => (raw * 100 + 127) / 255;

    private static byte Checksum(ReadOnlySpan<byte> report)
    {
        byte crc = 0;
        for (int i = 2; i < 88; i++) crc ^= report[i];
        return crc;
    }
}
