namespace BatteryMonitor.Core.Domain;

public enum DeviceKind { Other, Mouse, Headset, Keyboard, Gamepad }

/// <summary>One device's battery state as seen by a single poll.</summary>
/// <param name="Id">Stable identity across polls (for example "razer:00DF").</param>
/// <param name="Name">Human readable name.</param>
/// <param name="Kind">Device category, used for the icon and tooltip.</param>
/// <param name="Connection">How it is connected, for example "Bluetooth" or "2.4 GHz receiver".</param>
/// <param name="Percent">0..100, or null when the device answered without a level (asleep, off).</param>
/// <param name="Charging">True while the device reports it is charging.</param>
/// <param name="Approximate">True when the device only reports coarse levels (e.g. XInput empty/low/medium/full).</param>
public sealed record DeviceBattery(
    string Id,
    string Name,
    DeviceKind Kind,
    string Connection,
    int? Percent,
    bool Charging = false,
    bool Approximate = false);
