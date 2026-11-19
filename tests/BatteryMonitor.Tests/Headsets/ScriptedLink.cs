using BatteryMonitor.Core.Protocols.Headsets;

namespace BatteryMonitor.Tests.Headsets;

/// <summary>
/// Fake HID link: replies are scripted per request (hex of the request bytes). Each Exchange
/// walks the queued replies for that request and returns the first one the caller accepts,
/// like the real link skipping unrelated input reports.
/// </summary>
internal sealed class ScriptedLink : IHidLink
{
    private readonly Dictionary<string, Queue<byte[]>> _replies = new();
    public List<string> Sent { get; } = [];

    public ScriptedLink On(byte[] request, params byte[][] replies)
    {
        string key = Convert.ToHexString(request);
        if (!_replies.TryGetValue(key, out var q)) _replies[key] = q = new Queue<byte[]>();
        foreach (var r in replies) q.Enqueue(r);
        return this;
    }

    public byte[]? Exchange(byte[] request, Func<byte[], bool>? isReply = null)
    {
        string key = Convert.ToHexString(request);
        Sent.Add(key);
        if (!_replies.TryGetValue(key, out var q)) return null;
        while (q.Count > 0)
        {
            var r = q.Dequeue();
            if (isReply is null || isReply(r)) return r;
        }
        return null;
    }

    private readonly Queue<byte[]> _unsolicited = new();

    /// <summary>Input reports the device sends without being asked (returned by Listen).</summary>
    public ScriptedLink Unsolicited(params byte[][] reports)
    {
        foreach (var r in reports) _unsolicited.Enqueue(r);
        return this;
    }

    public byte[]? Listen(Func<byte[], bool> isReport)
    {
        while (_unsolicited.Count > 0)
        {
            var r = _unsolicited.Dequeue();
            if (isReport(r)) return r;
        }
        return null;
    }

    public void Drain() { }

    /// <summary>A reply of <paramref name="length"/> bytes with the given values at the given offsets.</summary>
    public static byte[] Reply(int length, params (int Offset, byte Value)[] bytes)
    {
        var r = new byte[length];
        foreach (var (offset, value) in bytes) r[offset] = value;
        return r;
    }
}
