using BatteryMonitor.App.Providers;
using BatteryMonitor.App.Settings;
using BatteryMonitor.App.Tray;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Monitoring;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;
using BatteryMonitor.Core.Settings;

namespace BatteryMonitor.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Composition root: the only place that knows every adapter.
        var settings = SettingsStore.Load();
        var bluetooth = new BluetoothBatteryProvider();
        var vendorsWithOwnProvider = new HashSet<ushort> { RazerBatteryProvider.VendorId, LogitechHidpp.VendorId, PlayStationProtocol.SonyVendorId };
        bool ProductHasOwnProvider(ushort vid, ushort pid) =>
            Core.Protocols.Headsets.HeadsetCatalog.Find(vid, pid) is not null || GamepadBatteryProvider.NameFor(vid, pid) is not null;
        IBatteryProvider[] providers = // order matters: Bluetooth runs first so XInput can avoid duplicates
        [
            bluetooth,
            new RazerBatteryProvider(),
            new LogitechBatteryProvider(),
            new PlayStationBatteryProvider(),
            new HeadsetBatteryProvider(),
            new GamepadBatteryProvider(),
            new XInputBatteryProvider(() => bluetooth.LastPollSawGamepad),
            new GenericHidBatteryProvider(vendorsWithOwnProvider, ProductHasOwnProvider),
            new HeadsetControlBatteryProvider(settings.Settings.HeadsetControlPath,
                // Only products another provider really reads: the Logitech HID++ provider covers Logitech
                // headsets, but no provider reads Razer or Sony headsets (e.g. the BlackShark V2 Pro).
                (vid, pid) => vid == LogitechHidpp.VendorId || ProductHasOwnProvider(vid, pid)),
        ];
        var options = new MonitorOptions { LowThreshold = settings.Settings.LowBatteryThreshold };
        var monitor = new BatteryMonitorService(providers, TimeProvider.System, options);

        // `--once`: poll one time and print the results (diagnostics, no tray icon).
        if (args.Contains("--once")) return PrintOnce(monitor, settings);
        // `--diag`: raw Razer protocol exchange, for troubleshooting new devices.
        if (args.Contains("--diag")) { RazerBatteryProvider.Diagnose(Console.Out); return 0; }
        // `--diag-hid`: every HID collection with a standard battery field, Bluetooth ones included.
        if (args.Contains("--diag-hid")) { GenericHidBatteryProvider.Diagnose(Console.Out); return 0; }
        // `--diag-input VID:PID`: raw input reports of every collection of one device (for adding devices).
        int inputAt = Array.IndexOf(args, "--diag-input");
        if (inputAt >= 0) return DumpInputReports(inputAt + 1 < args.Length ? args[inputAt + 1] : "");
        // `--icons <folder>`: save sample tray icons as PNG files, to check how they look.
        int iconsAt = Array.IndexOf(args, "--icons");
        if (iconsAt >= 0) return SaveSampleIcons(iconsAt + 1 < args.Length ? args[iconsAt + 1] : ".");

        using var single = new Mutex(true, @"Local\BatteryMonitor.App", out bool first);
        if (!first) return 0; // already running

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(monitor, settings));
        return 0;
    }

    /// <summary>
    /// Saves sample icons at the common tray sizes, plus sheet.png: every sample enlarged 8x
    /// (nearest neighbour, so each pixel stays visible) on a dark and a light background.
    /// </summary>
    private static int DumpInputReports(string vidPid)
    {
        string[] parts = vidPid.Split(':');
        if (parts.Length != 2 || !ushort.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out ushort vid)
            || !ushort.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out ushort pid))
        {
            Console.WriteLine("usage: --diag-input VID:PID   (hex, e.g. --diag-input 054C:0CE6)");
            return 1;
        }
        var matches = Hid.HidCollection.Enumerate(vid).Where(c => c.ProductId == pid).ToList();
        if (matches.Count == 0)
        {
            Console.WriteLine($"no HID collection with {vid:X4}:{pid:X4}; present HID paths:");
            foreach (string path in Hid.Native.EnumerateHidInterfacePaths()) Console.WriteLine("   " + path);
        }
        foreach (var coll in matches)
        {
            Console.WriteLine(coll.Path);
            using var stream = Hid.HidStream.Open(coll);
            if (stream is null) { Console.WriteLine("   cannot open for input (exclusive or no input reports)"); continue; }
            for (int i = 0; i < 3; i++)
            {
                var report = stream.Read(TimeSpan.FromMilliseconds(500));
                Console.WriteLine(report is null ? "   no report within 500 ms" : "   " + Convert.ToHexString(report));
            }
        }
        return 0;
    }

    private static readonly DeviceKind[] Kinds =
        [DeviceKind.Mouse, DeviceKind.Gamepad, DeviceKind.Headset, DeviceKind.Keyboard, DeviceKind.Other, DeviceKind.Mouse];

    private static int SaveSampleIcons(string folder)
    {
        Directory.CreateDirectory(folder);
        var samples = new (int? Percent, bool Charging)[] { (82, false), (45, false), (8, false), (98, true), (100, false), (null, false) };
        int[] sizes = [16, 32];
        int rows = sizes.Length * 2; // number + picture per size
        const int cell = 32 * 8 + 16;
        using var sheet = new Bitmap(cell * samples.Length, cell * rows * 2);
        using var g = Graphics.FromImage(sheet);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        for (int r = 0; r < rows * 2; r++) // first half on black, second half on a light background
        {
            int size = sizes[(r % rows) / 2];
            g.FillRectangle(r < rows ? Brushes.Black : Brushes.WhiteSmoke, 0, r * cell, sheet.Width, cell);
            for (int c = 0; c < samples.Length; c++)
            {
                var (percent, charging) = samples[c];
                // Row pairs: number frame, then the picture frame of a sample device kind per column.
                var kind = Kinds[c % Kinds.Length];
                using var icon = r % 2 == 0
                    ? BatteryIconRenderer.Render(percent, charging, stale: false, size)
                    : BatteryIconRenderer.RenderPicture(kind, percent, charging, stale: false, size);
                using var bmp = icon.ToBitmap();
                if (r < rows)
                    bmp.Save(Path.Combine(folder, $"icon-{size}px-{(r % 2 == 0 ? "number" : kind.ToString().ToLowerInvariant())}-{(percent?.ToString() ?? "unknown")}{(charging ? "-charging" : "")}.png"));
                g.DrawImage(bmp, c * cell + 8, r * cell + 8, size * 8, size * 8);
            }
        }
        sheet.Save(Path.Combine(folder, "sheet.png"));
        Console.WriteLine($"saved sample icons to {Path.GetFullPath(folder)}");
        return 0;
    }

    private static int PrintOnce(BatteryMonitorService monitor, SettingsLoadResult settings)
    {
        var snap = monitor.Poll();
        foreach (var r in snap.Devices)
        {
            var b = r.Battery;
            string level = b.Percent is int p ? $"{(b.Approximate ? "~" : "")}{p}%" : "?";
            Console.WriteLine($"{b.Name,-28} {b.Kind,-8} {b.Connection,-18} {level,5}{(b.Charging ? " charging" : "")}");
        }
        foreach (var e in snap.ProviderErrors) Console.WriteLine($"error: {e}");
        foreach (var w in settings.Warnings) Console.WriteLine($"settings: {w}");
        if (snap.Devices.Count == 0) Console.WriteLine("no devices found");
        return 0;
    }
}
