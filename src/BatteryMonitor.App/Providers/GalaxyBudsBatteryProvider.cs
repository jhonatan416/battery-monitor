using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// Galaxy Buds+ and later. Each poll opens the buds' Bluetooth serial channel, listens for the
/// status messages they send by themselves, and closes it again; nothing is ever written. When
/// Samsung's app holds the channel the connection fails and the buds simply show no level. Buds
/// that Windows already reports a level for are skipped.
/// </summary>
/// <param name="namesWithWindowsLevel">Names the Bluetooth provider reported a level for.</param>
public sealed class GalaxyBudsBatteryProvider(Func<IReadOnlyList<string>> namesWithWindowsLevel) : IBatteryProvider
{
    private static readonly TimeSpan ListenFor = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public string Name => "Galaxy Buds";

    // WinRT calls run on the thread pool: blocking on them from the STA thread used by --once could deadlock.
    public IReadOnlyList<DeviceBattery> Poll() => Task.Run(PollAsync).GetAwaiter().GetResult();

    private async Task<IReadOnlyList<DeviceBattery>> PollAsync()
    {
        var result = new List<DeviceBattery>();
        var paired = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true)).AsTask().WaitAsync(Timeout);
        var skip = namesWithWindowsLevel();
        foreach (var info in paired.Where(i => GalaxyBuds.IsSupportedName(i.Name) && !skip.Contains(i.Name, StringComparer.OrdinalIgnoreCase)))
        {
            try
            {
                using var device = await BluetoothDevice.FromIdAsync(info.Id).AsTask().WaitAsync(Timeout);
                if (device?.ConnectionStatus != BluetoothConnectionStatus.Connected) continue;
                if (await ListenAsync(device) is not { } r) continue;
                result.Add(new DeviceBattery($"buds:{device.BluetoothAddress:X12}", info.Name, DeviceKind.Headset, "Bluetooth",
                    r.Percent, r.Charging));
            }
            catch (Exception ex) when (ex is TimeoutException or System.Runtime.InteropServices.COMException or OperationCanceledException)
            {
                // channel held by another app, buds out of range, or Bluetooth off: no level this poll
            }
        }
        return result;
    }

    private static async Task<GalaxyBudsReading?> ListenAsync(BluetoothDevice device)
    {
        if (await FindServiceAsync(device) is not { } service) return null;
        using var socket = new StreamSocket();
        await socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName).AsTask().WaitAsync(Timeout);
        using var reader = new DataReader(socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
        var received = new List<byte>();
        using var cts = new CancellationTokenSource(ListenFor);
        try
        {
            while (true)
            {
                uint n = await reader.LoadAsync(512).AsTask(cts.Token);
                if (n == 0) break; // the buds closed the channel
                var chunk = new byte[n];
                reader.ReadBytes(chunk);
                received.AddRange(chunk);
                if (GalaxyBuds.LatestReading(received.ToArray()) is { } reading) return reading;
            }
        }
        catch (OperationCanceledException) { } // nothing usable within the listening time
        return GalaxyBuds.LatestReading(received.ToArray());
    }

    private static async Task<RfcommDeviceService?> FindServiceAsync(BluetoothDevice device)
    {
        foreach (var uuid in new[] { GalaxyBuds.ServiceUuid, GalaxyBuds.StandardSerialUuid })
        {
            var services = await device.GetRfcommServicesForIdAsync(RfcommServiceId.FromUuid(uuid), BluetoothCacheMode.Cached)
                .AsTask().WaitAsync(Timeout);
            if (services.Services.Count > 0) return services.Services[0];
        }
        return null;
    }
}
