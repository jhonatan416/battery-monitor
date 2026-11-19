using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;
using Windows.Devices.Bluetooth.Advertisement;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// AirPods and Beats, from the Bluetooth LE advertisements they send by themselves. Scanning is
/// passive (no scan requests, nothing sent to the earbuds). Adverts carry no owner, so a reading is
/// shown only while this PC has AirPods/Beats connected (see <see cref="AppleContinuity.NeedsAdvertisement"/>),
/// from the strongest nearby advert of the last few seconds.
/// </summary>
/// <param name="connectedNames">Connected Bluetooth device names (from the Bluetooth provider's latest poll).</param>
/// <param name="namesWithWindowsLevel">Names Windows already reports a level for.</param>
public sealed class AppleAudioBatteryProvider(
    Func<IReadOnlyList<string>> connectedNames,
    Func<IReadOnlyList<string>> namesWithWindowsLevel) : IBatteryProvider
{
    private const short NearbyDbm = -60; // OpenPods' threshold: an advert this strong comes from close by
    private static readonly TimeSpan Recent = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly List<(DateTime At, short Rssi, AppleAudioReading Reading)> _seen = [];
    private BluetoothLEAdvertisementWatcher? _watcher;

    public string Name => "AirPods / Beats";

    public IReadOnlyList<DeviceBattery> Poll()
    {
        EnsureWatching();
        AppleAudioReading? best;
        lock (_gate)
        {
            _seen.RemoveAll(s => DateTime.UtcNow - s.At > Recent);
            best = _seen.Count == 0 ? null : _seen.MaxBy(s => s.Rssi).Reading;
        }
        if (best is not { } r || !AppleContinuity.NeedsAdvertisement(connectedNames(), namesWithWindowsLevel())) return [];
        return [new DeviceBattery("apple:audio", r.Model, DeviceKind.Headset, "Bluetooth (advert)", r.Percent, r.Charging,
            Approximate: true)];
    }

    /// <summary>Starts the watcher, or restarts it after Bluetooth was turned off and on.</summary>
    private void EnsureWatching()
    {
        if (_watcher?.Status is BluetoothLEAdvertisementWatcherStatus.Started or BluetoothLEAdvertisementWatcherStatus.Stopping) return;
        _watcher ??= CreateWatcher();
        try { _watcher.Start(); } // with Bluetooth off the status becomes Aborted; the next poll tries again
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
    }

    private BluetoothLEAdvertisementWatcher CreateWatcher()
    {
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Passive };
        watcher.Received += (_, args) =>
        {
            if (args.RawSignalStrengthInDBm < NearbyDbm) return;
            foreach (var data in args.Advertisement.GetManufacturerDataByCompanyId(AppleContinuity.AppleCompanyId))
            {
                var bytes = new byte[data.Data.Length];
                using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(data.Data)) reader.ReadBytes(bytes);
                if (AppleContinuity.Parse(bytes) is not { } reading) continue;
                lock (_gate) _seen.Add((DateTime.UtcNow, args.RawSignalStrengthInDBm, reading));
            }
        };
        return watcher;
    }
}
