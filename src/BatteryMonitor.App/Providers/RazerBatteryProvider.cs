using BatteryMonitor.App.Hid;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.App.Providers;

/// <summary>Razer wireless mice (VID 1532) through their receiver's 90-byte feature report.</summary>
public sealed class RazerBatteryProvider : IBatteryProvider
{
    private const ushort RazerVendorId = 0x1532;
    private readonly Dictionary<ushort, byte> _knownTransactionId = new();

    public string Name => "Razer";

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var result = new List<DeviceBattery>();
        var collections = HidCollection.Enumerate(RazerVendorId)
            .Where(c => c.FeatureReportLength() == RazerProtocol.ReportLength)
            .GroupBy(c => c.ProductId);

        foreach (var product in collections)
        {
            foreach (var coll in product)
            {
                DeviceBattery? device;
                try { device = TryRead(coll); }
                catch (System.ComponentModel.Win32Exception) { continue; } // collection busy/locked: try the next one
                if (device is null) continue;
                result.Add(device);
                break; // one working collection per product is enough
            }
        }
        return result;
    }

    /// <summary>Prints every Razer collection and how it answers the battery request (for `--diag`).</summary>
    public static void Diagnose(TextWriter output)
    {
        foreach (var coll in HidCollection.Enumerate(RazerVendorId))
        {
            int len = coll.FeatureReportLength();
            output.WriteLine($"{coll.Path}  feature={len}");
            if (len != RazerProtocol.ReportLength) continue;
            try
            {
                using var hid = coll.OpenForFeatures();
                foreach (byte tid in RazerProtocol.MouseTransactionIds)
                {
                    byte[] req = RazerProtocol.BuildRequest(tid, RazerProtocol.BatteryLevel);
                    bool sent = hid.SetFeature(req);
                    int err = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
                    Thread.Sleep(15);
                    var buf = new byte[RazerProtocol.ReportLength];
                    bool got = hid.GetFeature(buf);
                    var reply = RazerProtocol.ParseReply(buf, tid, RazerProtocol.BatteryLevel);
                    output.WriteLine($"   tid={tid:X2} set={sent}{(sent ? "" : $"(err {err})")} get={got} status={reply.Status} raw={Convert.ToHexString(buf, 0, 12)}");                }
            }
            catch (Exception ex) { output.WriteLine($"   open failed: {ex.Message}"); }
        }
    }

    private DeviceBattery? TryRead(HidCollection coll)
    {
        using var hid = coll.OpenForFeatures();
        byte[] tids = _knownTransactionId.TryGetValue(coll.ProductId, out byte known)
            ? [known]
            : RazerProtocol.MouseTransactionIds;

        foreach (byte tid in tids)
        {
            var reply = Send(hid, tid, RazerProtocol.BatteryLevel);
            if (reply.Status == RazerStatus.Offline)
                return Device(coll, percent: null, charging: false);
            if (reply.Status != RazerStatus.Ok) continue;

            _knownTransactionId[coll.ProductId] = tid;
            var charging = Send(hid, tid, RazerProtocol.ChargingStatus);
            bool isCharging = charging.Status == RazerStatus.Ok && charging.Arguments[1] == 1;
            return Device(coll, RazerProtocol.ToPercent(reply.Arguments[1]), isCharging);
        }
        return null;
    }

    private static RazerReply Send(HidHandle hid, byte tid, RazerCommand cmd)
    {
        byte[] request = RazerProtocol.BuildRequest(tid, cmd);
        bool resend = true;
        var last = RazerReply.Mismatch;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (resend && !hid.SetFeature(request)) return RazerReply.Mismatch;
            // Short wait: a long one lets Razer Synapse overwrite the shared reply buffer.
            Thread.Sleep(attempt == 0 ? 15 : 40);
            var buffer = new byte[RazerProtocol.ReportLength];
            if (!hid.GetFeature(buffer)) { resend = true; continue; }
            last = RazerProtocol.ParseReply(buffer, tid, cmd);
            switch (last.Status)
            {
                case RazerStatus.Ok:
                case RazerStatus.Unsupported:
                    return last;
                case RazerStatus.Busy:
                    resend = false; // still working on our request: just read again
                    break;
                default:
                    resend = true;  // offline can be transient; mismatch means someone else's reply
                    break;
            }
        }
        return last;
    }

    private static DeviceBattery Device(HidCollection coll, int? percent, bool charging)
    {
        string name = Clean(coll.ProductName()) ?? $"Razer {coll.ProductId:X4}";
        return new DeviceBattery($"razer:{coll.ProductId:X4}", name, DeviceKind.Mouse, "Razer receiver", percent, charging);
    }

    private static string? Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (string suffix in new[] { " HyperSpeed Dongle", " Dongle", " (Wireless)", " (Wired)", " Receiver" })
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return name[..^suffix.Length];
        return name;
    }
}
