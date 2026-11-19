using System.ComponentModel;
using System.Diagnostics;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// Headsets read by the external HeadsetControl tool, when the user enables it with the
/// headsetControlPath setting. Runs `headsetcontrol -b -o json` once per poll. Products another
/// provider already reads are skipped, so no headset appears twice.
/// </summary>
/// <param name="executable">Name on PATH or full path; empty disables the provider.</param>
/// <param name="productHasOwnProvider">Products handled by a dedicated provider.</param>
public sealed class HeadsetControlBatteryProvider(string executable, Func<ushort, ushort, bool> productHasOwnProvider)
    : IBatteryProvider
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public string Name => "HeadsetControl";

    public IReadOnlyList<DeviceBattery> Poll()
    {
        if (string.IsNullOrWhiteSpace(executable)) return [];
        return HeadsetControlOutput.Parse(Run())
            .Where(d => !productHasOwnProvider(d.VendorId, d.ProductId))
            .Select(d => new DeviceBattery($"hc:{d.VendorId:X4}{d.ProductId:X4}", d.Name, DeviceKind.Headset,
                "USB (HeadsetControl)", d.Percent, d.Charging))
            .ToList();
    }

    /// <summary>Standard output of the tool. Throws when it cannot start or hangs (reported as a provider error).</summary>
    private string Run()
    {
        var info = new ProcessStartInfo(executable, HeadsetControlOutput.Arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process process;
        try { process = Process.Start(info) ?? throw new InvalidOperationException($"could not start {executable}"); }
        catch (Win32Exception ex) { throw new InvalidOperationException($"could not start {executable}: {ex.Message}", ex); }
        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync(); // drained so the tool never blocks on a full pipe
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"{executable} did not finish within {Timeout.TotalSeconds:0} s");
            }
            return output.GetAwaiter().GetResult(); // exit code 1 just means "no headsets"
        }
    }
}
