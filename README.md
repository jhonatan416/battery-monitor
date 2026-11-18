# Battery Monitor

A small Windows tray app that shows the battery level of wireless devices: one tray icon per device,
with the percent drawn in the icon, a tooltip with details, and a notification when a device drops below 20 %.

## Supported devices

| Device | Connection | How the level is read | Status |
|---|---|---|---|
| Xbox Wireless Controller (and other Bluetooth devices) | Bluetooth | Windows Bluetooth battery property | Working |
| Razer wireless mice (tested target: Viper V3 Pro SE, `1532:00DF`) | Razer receiver | Razer 90-byte feature report (class `07`, command `80`) | Implemented; not yet verified with the mouse awake |
| Memzuiox mouse (`248A:8366` receiver) | 2.4 GHz receiver | Feature report 5, byte 1 | Working; byte meaning still being confirmed |
| kofire UG-06 headset (`4C4A:4155`) | USB dongle | Unknown | Not supported yet |

## Build and run

Requires the .NET 10 SDK on Windows.

```bash
dotnet build -c Release
dotnet test
src/BatteryMonitor.App/bin/Release/net10.0-windows/BatteryMonitor.App.exe
```

Command-line options:

- `--once`: poll one time, print every device found, and exit (no tray icon).
- `--diag`: print each Razer HID collection and its raw reply to the battery request.

## Architecture

Hexagonal (ports and adapters):

```
BatteryMonitor.Core   (no Windows dependencies, fully unit tested)
  Domain/       DeviceBattery, DeviceKind
  Ports/        IBatteryProvider          <- one adapter per device family
  Protocols/    RazerProtocol, GenericMouseProtocol (pure byte encoding/decoding)
  Monitoring/   BatteryMonitorService     (merges providers, grace period, low-battery alerts)

BatteryMonitor.App    (Windows adapters + UI)
  Hid/          HID enumeration and feature reports (SetupAPI / hid.dll)
  Providers/    BluetoothBatteryProvider, RazerBatteryProvider, GenericMouseBatteryProvider
  Tray/         TrayApplicationContext, BatteryIconRenderer
  Program.cs    composition root
```

Adding a device means adding one `IBatteryProvider` (plus its protocol decoder and tests in Core)
and registering it in `Program.cs`.

## Credits

Protocol facts for Razer devices come from the public documentation of the
[OpenRazer](https://github.com/openrazer/openrazer) project. The idea was inspired by
[Wireless-Device-Battery-Monitor](https://github.com/krisgfx9/Wireless-Device-Battery-Monitor).
No code from either project is included.
