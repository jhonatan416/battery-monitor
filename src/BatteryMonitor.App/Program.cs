using BatteryMonitor.App.Providers;
using BatteryMonitor.App.Tray;
using BatteryMonitor.Core.Monitoring;
using BatteryMonitor.Core.Ports;

namespace BatteryMonitor.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Composition root: the only place that knows every adapter.
        IBatteryProvider[] providers =
        [
            new BluetoothBatteryProvider(),
            new RazerBatteryProvider(),
            new GenericMouseBatteryProvider(),
        ];
        var monitor = new BatteryMonitorService(providers, TimeProvider.System, new MonitorOptions());

        // `--once`: poll one time and print the results (diagnostics, no tray icon).
        if (args.Contains("--once")) return PrintOnce(monitor);
        // `--diag`: raw Razer protocol exchange, for troubleshooting new devices.
        if (args.Contains("--diag")) { RazerBatteryProvider.Diagnose(Console.Out); return 0; }

        using var single = new Mutex(true, @"Local\BatteryMonitor.App", out bool first);
        if (!first) return 0; // already running

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(monitor));
        return 0;
    }

    private static int PrintOnce(BatteryMonitorService monitor)
    {
        var snap = monitor.Poll();
        foreach (var r in snap.Devices)
        {
            var b = r.Battery;
            Console.WriteLine($"{b.Name,-28} {b.Kind,-8} {b.Connection,-18} {(b.Percent is int p ? $"{p}%" : "?"),5}{(b.Charging ? " charging" : "")}");
        }
        foreach (var e in snap.ProviderErrors) Console.WriteLine($"error: {e}");
        if (snap.Devices.Count == 0) Console.WriteLine("no devices found");
        return 0;
    }
}
