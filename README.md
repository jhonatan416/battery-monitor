# Battery Monitor

A small Windows tray app that shows the battery level of wireless devices: one tray icon per device,
a tooltip with details, and a notification when a device drops below 20 %.

Each icon switches every 3 seconds between a picture of the device (mouse, gamepad, headset,
keyboard, or a battery for other devices) and the percent, drawn in a crisp pixel font. Both are
colored by level: green above 50 %, yellow from 21 to 50 %, red at 20 % or less, blue while charging,
gray when unknown, dimmed when the device stopped answering. `F` means full (100 %).

## Supported devices

"Verified" means tested on real hardware; "unverified" means implemented from the official
specification but not yet tested on a real device of that kind.

| Device | Connection | How the level is read | Status |
|---|---|---|---|
| Xbox Wireless Controller (and other Bluetooth devices) | Bluetooth | Windows Bluetooth battery property, only while the device is connected (Windows keeps the last value after it disconnects) | Verified |
| Razer wireless mice (tested target: Viper V3 Pro SE, `1532:00DF`) | Razer receiver | Razer 90-byte feature report (class `07`, command `80`) | Verified |
| Any USB/receiver device with a standard HID battery field, any brand | USB / receiver | HID Usage Tables: Battery Strength (`06:20`) or Battery System (`85:65`/`85:66`) | Unverified (descriptor parsing verified; no owned device has the field) |
| Xbox controllers on a cable or the Xbox Wireless Adapter | XInput | Microsoft XInput battery API, 4 coarse levels shown as "about N %" | Unverified; skipped while a Bluetooth gamepad is shown, to avoid duplicates |
| PlayStation DualShock 4 / DualSense (Edge) | USB or Bluetooth | Battery byte of the input report the controller sends | Unverified |
| Logitech mice, keyboards, headsets (HID++ 2.0) | Unifying / Lightspeed / Bolt receiver, or USB | HID++ Unified Battery (`0x1004`) or Battery Status (`0x1000`); name and type from `0x0005` | Unverified; Bluetooth Logitech devices are covered by the Bluetooth row |
| Memzuiox mouse (`248A:8366` receiver) | 2.4 GHz receiver | None found: report 5 is static receiver data | Not supported |
| kofire UG-06 headset (`4C4A:4155`) | USB dongle | Unknown | Not supported yet |

## Build and run

Requires the .NET 10 SDK on Windows.

```bash
dotnet build -c Release
dotnet test
src/BatteryMonitor.App/bin/Release/net10.0-windows/BatteryMonitor.App.exe
```

Tray menu (right-click any icon): **Refresh now**, **Start with Windows** (per-user, off by default),
**Open settings file**, **Exit**.

## Settings

`%APPDATA%\BatteryMonitor\settings.json` is created with defaults on first run. Restart the app after editing it.

| Setting | Default | Allowed |
|---|---|---|
| `lowBatteryThreshold` | `20` | 1–99 (percent) |
| `pollIntervalSeconds` | `60` | 15–3600 |

An invalid value is ignored (its default is used) and reported in a tray notification, and by `--once`.
Unknown settings (such as the old `genericMouseName`) are ignored silently.

Command-line options:

- `--once`: poll one time, print every device found, and exit (no tray icon).
- `--diag`: print each Razer HID collection and its raw reply to the battery request.
- `--diag-hid`: print every HID collection that declares a standard battery field (Bluetooth ones
  included) with the value read, plus how many collections and value fields were scanned.
- `--diag-input VID:PID`: print the first raw input reports of every collection of one device
  (hex ids, e.g. `--diag-input 054C:0CE6`), to capture bytes when adding a device.
- `--icons <folder>`: save sample tray icons (number and picture frames at 16 and 32 px) as PNG files,
  plus `sheet.png` with all of them enlarged 8x on dark and light backgrounds, and exit.

## Architecture

Hexagonal (ports and adapters):

```
BatteryMonitor.Core   (no Windows dependencies, fully unit tested)
  Domain/       DeviceBattery, DeviceKind
  Ports/        IBatteryProvider          <- one adapter per device family
  Protocols/    RazerProtocol, LogitechHidpp, PlayStationProtocol, HidBattery, XInputBattery,
                BluetoothBattery, HidPath (pure decoding)
  Monitoring/   BatteryMonitorService     (merges providers, grace period, low-battery alerts)
  Settings/     AppSettings, SettingsSerializer (validation, never throws)
  Icons/        IconPixels (pixel-font layout), DevicePictures (device pixel art)

BatteryMonitor.App    (Windows adapters + UI)
  Hid/          HID enumeration, feature reports, input/output stream, standard battery fields
  Providers/    Bluetooth, Razer, Logitech, PlayStation, XInput, GenericHid (*BatteryProvider)
  Settings/     SettingsStore (%APPDATA% file), StartupRegistration (HKCU Run key)
  Tray/         TrayApplicationContext, DeviceTrayIcon (two frames), BatteryIconRenderer
  Program.cs    composition root
```

Adding a device means adding one `IBatteryProvider` (plus its protocol decoder and tests in Core)
and registering it in `Program.cs`.

## Credits

Protocol facts for Razer devices come from the public documentation of the
[OpenRazer](https://github.com/openrazer/openrazer) project. The idea was inspired by
[Wireless-Device-Battery-Monitor](https://github.com/krisgfx9/Wireless-Device-Battery-Monitor).
No code from either project is included.
