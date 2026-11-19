using BatteryMonitor.Core.Protocols.Headsets;

namespace BatteryMonitor.App.Hid;

/// <summary>
/// <see cref="IHidLink"/> over a <see cref="HidStream"/>, using the hidapi byte convention the
/// protocol facts are written in: Windows prefixes every input report with its report id, so the
/// 0x00 of unnumbered reports is stripped before the protocol sees the reply.
/// </summary>
public sealed class HidLink(HidStream stream) : IHidLink
{
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan ListenFor = TimeSpan.FromSeconds(2);
    private const int MaxReportsPerExchange = 8, MaxDrained = 32;

    public byte[]? Exchange(byte[] request, Func<byte[], bool>? isReply = null)
    {
        if (!stream.Write(request)) return null;
        for (int i = 0; i < MaxReportsPerExchange; i++)
        {
            if (stream.Read(ReplyTimeout) is not { } raw) return null;
            byte[] report = raw.Length > 0 && raw[0] == 0x00 ? raw[1..] : raw;
            if (isReply is null || isReply(report)) return report;
        }
        return null;
    }

    public byte[]? Listen(Func<byte[], bool> isReport)
    {
        var until = DateTime.UtcNow + ListenFor;
        while (DateTime.UtcNow < until)
        {
            if (stream.Read(ReplyTimeout) is not { } raw) continue; // quiet for a moment; keep waiting
            byte[] report = raw.Length > 0 && raw[0] == 0x00 ? raw[1..] : raw;
            if (isReport(report)) return report;
        }
        return null;
    }

    public void Drain()
    {
        for (int i = 0; i < MaxDrained && stream.Read(DrainTimeout) is not null; i++) { }
    }
}
