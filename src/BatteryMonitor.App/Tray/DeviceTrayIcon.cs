using BatteryMonitor.Core.Monitoring;

namespace BatteryMonitor.App.Tray;

/// <summary>
/// One device's tray icon. Keeps both frames (number and device picture) so switching between
/// them is just a handle swap; frames are only redrawn when a new reading arrives.
/// </summary>
internal sealed class DeviceTrayIcon(NotifyIcon notify) : IDisposable
{
    private Icon? _number;
    private Icon? _picture;
    private bool _showingPicture;

    public NotifyIcon Notify { get; } = notify;

    public void Update(DeviceReading reading, string tooltip)
    {
        var b = reading.Battery;
        Icon? oldNumber = _number, oldPicture = _picture;
        _number = BatteryIconRenderer.Render(b.Percent, b.Charging, reading.Stale);
        _picture = BatteryIconRenderer.RenderPicture(b.Kind, b.Percent, b.Charging, reading.Stale);
        Show(_showingPicture); // assign the new frame before freeing the old ones
        oldNumber?.Dispose();
        oldPicture?.Dispose();
        Notify.Text = tooltip;
        Notify.Visible = true;
    }

    public void Show(bool picture)
    {
        _showingPicture = picture;
        Notify.Icon = picture ? _picture : _number;
    }

    public void Dispose()
    {
        Notify.Visible = false;
        Notify.Dispose();
        _number?.Dispose();
        _picture?.Dispose();
    }
}
