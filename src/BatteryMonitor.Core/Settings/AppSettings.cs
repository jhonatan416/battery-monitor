namespace BatteryMonitor.Core.Settings;

/// <summary>User settings, stored as JSON. Every property has a safe default.</summary>
public sealed record AppSettings
{
    public const int MinThreshold = 1, MaxThreshold = 99;
    public const int MinPollSeconds = 15, MaxPollSeconds = 3600;

    /// <summary>Alert when a device drops below this percent (1..99).</summary>
    public int LowBatteryThreshold { get; init; } = 20;

    /// <summary>Seconds between polls (15..3600).</summary>
    public int PollIntervalSeconds { get; init; } = 60;

    /// <summary>
    /// HeadsetControl executable (a name on PATH or a full path). Empty means off: the tool reads
    /// every headset it supports, including one whose read switches its dongle mode.
    /// </summary>
    public string HeadsetControlPath { get; init; } = "";
}
