using System.Runtime.InteropServices;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// Xbox controllers through Microsoft's XInput API (cable or Xbox Wireless Adapter).
/// Coarse levels only; skipped while a Bluetooth gamepad is reported, to avoid duplicates.
/// </summary>
public sealed class XInputBatteryProvider(Func<bool> bluetoothGamepadPresent) : IBatteryProvider
{
    private const int MaxControllers = 4; // XUSER_MAX_COUNT
    private const byte BatteryDevTypeGamepad = 0x00;
    private const uint ErrorSuccess = 0;

    public string Name => "XInput";

    public IReadOnlyList<DeviceBattery> Poll()
    {
        bool bluetooth = bluetoothGamepadPresent();
        var result = new List<DeviceBattery>();
        for (uint slot = 0; slot < MaxControllers; slot++)
        {
            if (XInputGetBatteryInformation(slot, BatteryDevTypeGamepad, out var info) != ErrorSuccess) continue;
            if (XInputBattery.Decode(info.BatteryType, info.BatteryLevel, bluetooth) is not { } reading) continue;
            result.Add(new DeviceBattery($"xinput:{slot}", $"Xbox Controller {slot + 1}", DeviceKind.Gamepad,
                "Xbox Wireless Adapter / USB", reading.Percent, Approximate: reading.Approximate));
        }
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_BATTERY_INFORMATION { public byte BatteryType; public byte BatteryLevel; }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetBatteryInformation(uint userIndex, byte devType, out XINPUT_BATTERY_INFORMATION info);
}
