using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Monitoring;

namespace BatteryMonitor.App.Tray;

/// <summary>Owns the tray icons: one per device, refreshed on a timer from a background poll.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly BatteryMonitorService _monitor;
    private readonly Dictionary<string, NotifyIcon> _icons = new();
    private readonly NotifyIcon _placeholder;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly SynchronizationContext _ui;
    private int _polling; // 0 = idle, 1 = a poll is running

    public TrayApplicationContext(BatteryMonitorService monitor)
    {
        _monitor = monitor;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _placeholder = NewIcon("Battery Monitor: looking for devices...", BatteryIconRenderer.Render(null, false, false));
        _placeholder.Visible = true;

        _timer.Interval = (int)Interval.TotalMilliseconds;
        _timer.Tick += (_, _) => PollInBackground();
        _timer.Start();
        PollInBackground();
    }

    private void PollInBackground()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;
        Task.Run(() =>
        {
            try { return _monitor.Poll(); }
            finally { Interlocked.Exchange(ref _polling, 0); }
        }).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully) _ui.Post(_ => Apply(t.Result), null);
        });
    }

    private void Apply(MonitorSnapshot snap)
    {
        var seen = new HashSet<string>();
        foreach (var reading in snap.Devices)
        {
            var b = reading.Battery;
            seen.Add(b.Id);
            if (!_icons.TryGetValue(b.Id, out var icon))
            {
                icon = NewIcon("", null);
                _icons[b.Id] = icon;
            }
            var old = icon.Icon;
            icon.Icon = BatteryIconRenderer.Render(b.Percent, b.Charging, reading.Stale);
            old?.Dispose();
            icon.Text = Tooltip(reading);
            icon.Visible = true;
        }

        foreach (var id in _icons.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _icons[id].Visible = false;
            _icons[id].Dispose();
            _icons.Remove(id);
        }

        _placeholder.Visible = _icons.Count == 0;
        if (snap.ProviderErrors.Count > 0)
            _placeholder.Text = Truncate("Battery Monitor: " + string.Join("; ", snap.ProviderErrors));

        foreach (var low in snap.NewLowBattery)
            if (_icons.TryGetValue(low.Id, out var icon))
                icon.ShowBalloonTip(5000, "Low battery", $"{low.Name} is at {low.Percent}%", ToolTipIcon.Warning);
    }

    private static string Tooltip(DeviceReading r)
    {
        var b = r.Battery;
        string level = b.Percent is int p ? $"{p}%" : "unknown";
        string state = b.Charging ? " (charging)" : r.Stale ? $" (last seen {r.LastSeen.ToLocalTime():HH:mm})" : "";
        return Truncate($"{b.Name}: {level}{state}\n{b.Connection}");
    }

    // NotifyIcon.Text is limited to 127 characters.
    private static string Truncate(string s) => s.Length <= 127 ? s : s[..124] + "...";

    private NotifyIcon NewIcon(string text, Icon? icon)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Refresh now", null, (_, _) => PollInBackground());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return new NotifyIcon { Text = text, Icon = icon, ContextMenuStrip = menu };
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        foreach (var icon in _icons.Values) { icon.Visible = false; icon.Dispose(); }
        _placeholder.Visible = false;
        _placeholder.Dispose();
        base.ExitThreadCore();
    }
}
