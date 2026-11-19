using BatteryMonitor.App.Providers;
using BatteryMonitor.App.Settings;
using BatteryMonitor.App.Tray;
using BatteryMonitor.Core.Monitoring;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Settings;

namespace BatteryMonitor.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Composition root: the only place that knows every adapter.
        var settings = SettingsStore.Load();
        IBatteryProvider[] providers =
        [
            new BluetoothBatteryProvider(),
            new RazerBatteryProvider(),
        ];
        var options = new MonitorOptions { LowThreshold = settings.Settings.LowBatteryThreshold };
        var monitor = new BatteryMonitorService(providers, TimeProvider.System, options);

        // `--once`: poll one time and print the results (diagnostics, no tray icon).
        if (args.Contains("--once")) return PrintOnce(monitor, settings);
        // `--diag`: raw Razer protocol exchange, for troubleshooting new devices.
        if (args.Contains("--diag")) { RazerBatteryProvider.Diagnose(Console.Out); return 0; }

        using var single = new Mutex(true, @"Local\BatteryMonitor.App", out bool first);
        if (!first) return 0; // already running

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(monitor, settings));
        return 0;
    }

    private static int PrintOnce(BatteryMonitorService monitor, SettingsLoadResult settings)
    {
        var snap = monitor.Poll();
        foreach (var r in snap.Devices)
        {
            var b = r.Battery;
            Console.WriteLine($"{b.Name,-28} {b.Kind,-8} {b.Connection,-18} {(b.Percent is int p ? $"{p}%" : "?"),5}{(b.Charging ? " charging" : "")}");
        }
        foreach (var e in snap.ProviderErrors) Console.WriteLine($"error: {e}");
        foreach (var w in settings.Warnings) Console.WriteLine($"settings: {w}");
        if (snap.Devices.Count == 0) Console.WriteLine("no devices found");
        return 0;
    }
}
