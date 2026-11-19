using System.Diagnostics;
using BatteryMonitor.App.Settings;
using BatteryMonitor.Core.Monitoring;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Settings;

namespace BatteryMonitor.App.Tray;

/// <summary>
/// Owns the tray icons: one per device, refreshed on a timer from a background poll.
/// Each icon alternates between the device picture and the percent every few seconds.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int FrameSwitchMs = 3000;

    private readonly BatteryMonitorService _monitor;
    private readonly IStartupRegistration _startup = new StartupRegistration();
    private readonly Dictionary<string, DeviceTrayIcon> _icons = new();
    private readonly NotifyIcon _placeholder;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly System.Windows.Forms.Timer _frameTimer = new() { Interval = FrameSwitchMs };
    private bool _showPicture = true; // start with the picture so a new icon is recognisable at once
    private readonly SynchronizationContext _ui;
    private int _polling; // 0 = idle, 1 = a poll is running

    public TrayApplicationContext(BatteryMonitorService monitor, SettingsLoadResult settings)
    {
        _monitor = monitor;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _placeholder = NewIcon("Battery Monitor: looking for devices...", BatteryIconRenderer.Render(null, false, false));
        _placeholder.Visible = true;
        if (settings.Warnings.Count > 0)
            _placeholder.ShowBalloonTip(8000, "Battery Monitor settings", string.Join("\n", settings.Warnings), ToolTipIcon.Warning);

        _timer.Interval = settings.Settings.PollIntervalSeconds * 1000;
        _timer.Tick += (_, _) => PollInBackground();
        _timer.Start();
        _frameTimer.Tick += (_, _) => SwitchFrames();
        _frameTimer.Start();
        PollInBackground();
    }

    private void SwitchFrames()
    {
        _showPicture = !_showPicture;
        foreach (var icon in _icons.Values) icon.Show(_showPicture);
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
                icon = new DeviceTrayIcon(NewIcon("", null));
                icon.Show(_showPicture);
                _icons[b.Id] = icon;
            }
            icon.Update(reading, Tooltip(reading));
        }

        foreach (var id in _icons.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _icons[id].Dispose();
            _icons.Remove(id);
        }

        _placeholder.Visible = _icons.Count == 0;
        if (snap.ProviderErrors.Count > 0)
            _placeholder.Text = Truncate("Battery Monitor: " + string.Join("; ", snap.ProviderErrors));

        foreach (var low in snap.NewLowBattery)
            if (_icons.TryGetValue(low.Id, out var icon))
                icon.Notify.ShowBalloonTip(5000, "Low battery", $"{low.Name} is at {low.Percent}%", ToolTipIcon.Warning);
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
        var startup = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        startup.CheckedChanged += (_, _) => ToggleStartup(startup);
        menu.Opening += (_, _) => startup.Checked = _startup.IsEnabled(); // reflect the real registry state
        menu.Items.Add(startup);
        menu.Items.Add("Open settings file (restart to apply)", null, (_, _) => OpenSettingsFile());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return new NotifyIcon { Text = text, Icon = icon, ContextMenuStrip = menu };
    }

    private void ToggleStartup(ToolStripMenuItem item)
    {
        var result = StartupToggle.Apply(_startup, item.Checked); // no write when the menu is only re-syncing
        if (item.Checked != result.Enabled) item.Checked = result.Enabled;
        if (result.Error is { } error)
            MessageBox.Show($"Could not change the startup setting: {error}", "Battery Monitor");
    }

    private static void OpenSettingsFile()
    {
        // The file is created on first run; open it with the default editor for .json (or Notepad).
        try { Process.Start(new ProcessStartInfo(SettingsStore.FilePath) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { Process.Start("notepad.exe", $"\"{SettingsStore.FilePath}\""); }
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _frameTimer.Stop();
        foreach (var icon in _icons.Values) icon.Dispose();
        _placeholder.Visible = false;
        _placeholder.Dispose();
        base.ExitThreadCore();
    }
}
