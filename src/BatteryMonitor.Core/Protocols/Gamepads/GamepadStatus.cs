namespace BatteryMonitor.Core.Protocols.Gamepads;

/// <summary>A gamepad's battery level (0..100) and whether it is charging.</summary>
public readonly record struct GamepadStatus(int Percent, bool Charging);
