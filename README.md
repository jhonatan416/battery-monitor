# Battery Monitor

A small Windows tray app that shows the battery level of wireless devices: one tray icon per device,
a tooltip with details, and a notification when a device drops below 20 %.

**[Download the latest version](https://github.com/jhonatan416/battery-monitor/releases/latest)**:
a portable zip for Windows 10 2004 or later (x64), no install needed. See [Download](#download).

<img src="docs/tray-icons.png" alt="Tray icons, enlarged: battery percent on top (green 82, yellow 45, red 8, blue 98 while charging, F for full, ? when unknown) and the matching device pictures below (mouse, gamepad, headset, keyboard, battery, mouse)" width="600">

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
| SteelSeries Arctis 1 / 7 / 9 / Pro Wireless / 7+ / Nova 3P / Nova 5 / Nova 7 / Nova Pro Wireless | USB receiver | Vendor status request per family (facts from the public HeadsetControl definitions) | Unverified |
| HyperX Cloud II Wireless / Cloud Flight / Cloud Alpha Wireless / Cloud Alpha 2 Wireless | USB receiver | Vendor status request per family; Cloud Flight level estimated from voltage | Unverified |
| Corsair Void (wireless family) / Virtuoso XT & SE / Void Wireless V2, Virtuoso MAX, HS80 MAX | USB receiver | Vendor status request; Virtuoso and V2 report tenths of a percent | Unverified |
| AirPods / AirPods Pro / AirPods Max / Beats | Bluetooth | The battery advertisement the earbuds broadcast (Bluetooth LE, received passively), in 10 % steps | Unverified; shown only while AirPods/Beats are connected to this PC and Windows reports no level for them. The tray needs a few seconds after start to hear an advert, so `--once` usually shows nothing |
| Sony INZONE H5 / H9 II / INZONE Buds | USB dongle | H5, H9 II: one battery "get" request; Buds: report the dongle sends by itself | Unverified; older INZONE models have no public source |
| Jabra Evolve2 65 Flex / Evolve3 75 / Link 380 / Link 390 | USB or Link dongle | One battery "get" request (Jabra GN protocol) | Unverified |
| Samsung Galaxy Buds+ and later (Live, Pro, Buds2, Buds2 Pro, FE, ...) | Bluetooth | Each poll opens the buds' serial channel, listens up to 3 s for the status message they send by themselves, and closes it; nothing is written | Unverified; skipped when Windows already shows their level. If Samsung's app holds the channel the buds show no level. Original 2019 Galaxy Buds not supported (other framing) |
| JBL, MOONDROP | Bluetooth | Shown only when Windows reports their level (Bluetooth row) | No vendor protocol: no public source |
| Phone (Phone Link) | — | — | Not supported: Phone Link has no public API |
| Steam Controller (original, wired or wireless dongle; 2026 model) | USB / Steam dongle | Battery event the controller sends by itself | Unverified |
| 8BitDo Ultimate 3 / Ultimate 2 Wireless (firmware 1.03+) | USB / receiver | Battery byte of the input report | Unverified; other 8BitDo models need a mode request first and are not read |
| Flydigi Apex / Apex 6 / Vader (V2) | USB receiver | One "get info" request | Unverified |
| GameSir controllers | USB receiver | None: no public battery source | Not supported |
| Any headset HeadsetControl supports | USB | The external [HeadsetControl](https://github.com/Sapd/HeadsetControl) tool, only when `headsetControlPath` is set | Optional, off by default |
| Logitech PRO X 2 LIGHTSPEED | USB receiver | Logitech "Centurion" protocol: feature lookups and one battery "get" through the dongle's bridge; older firmware: one battery "get" request | Unverified |
| ASTRO A50 (Gen 4 on PC mode, Gen 5) | Base station | One battery "get" request; Gen 4 first checks that the headset is linked (the base keeps the last level after it turns off) | Unverified |
| MCHOSE X9 Wireless / V9 Pro | USB dongle | One battery query | Unverified |
| Logitech G435 | USB dongle | — | Not supported: no public read-only battery command |
| SteelSeries Aerox 3 / 5 / 9 Wireless, Prime Wireless, Prime Mini Wireless, Rival 3 Wireless (+ Gen 2), Rival 650 Wireless | USB receiver or cable | One battery "get" request (facts from rivalcfg) | Unverified |
| HyperX Pulsefire Dart | USB receiver or cable | One "heartbeat" request | Unverified; other HyperX mice have no public source |
| Pulsar, Kysona M600, VXE Dragonfly R1 Pro / MAD R, ATK VXE R1 SE | USB dongle or cable | One "power" request (shared protocol of these brands) | Unverified |
| Glorious Model O / O- / D / D- Wireless | USB receiver or cable | One "get" command sent as a feature report, then the feature reply | Unverified; shown as charging while on the cable |
| Corsair wireless mice through a Slipstream receiver (Dark Core RGB PRO / PRO SE, Harpoon RGB Wireless, generic Slipstream receivers) | USB receiver | Bragi "get property" for battery level and status | Unverified; the reference tools first switch the mouse to "software mode", this app does not (read-only), so a mouse that only answers in that mode shows no level. A generic receiver is shown as "Corsair Slipstream device" (it may pair a keyboard). Mice on their cable are not covered |
| Other enthusiast mice and keyboards (LAMZU, Endgame Gear, VAXEE, Finalmouse, ZOWIE, Keychron, ASUS ROG, ...) | — | — | Not supported: no public battery protocol found |
| Razer BlackShark V2 Pro (PlayStation) | USB dongle | Needs the dongle switched to another mode to read the headset | Not supported: breaks the read-only device rule |
| Memzuiox mouse (`248A:8366` receiver) | 2.4 GHz receiver | None found: report 5 is static receiver data | Not supported |
| kofire UG-06 headset (`4C4A:4155`) | USB dongle | Unknown | Not supported yet |

## Download

Get `BatteryMonitor-<version>-win-x64-portable.zip` from the
[Releases](../../releases) page, unzip it anywhere and run `BatteryMonitor.exe`. It needs Windows 10
2004 or later (x64) and nothing else: .NET is included in the exe. The icons appear in the
taskbar's hidden-icons area; right-click one for the menu. The exe is not code-signed, so Windows
SmartScreen may warn the first time ("More info" > "Run anyway").

To keep it running at sign-in, turn on **Start with Windows** in the tray menu after placing the exe
where it will stay (the entry points to that location).

## Build and run

Requires the .NET 10 SDK on Windows 10 2004 (build 19041) or later.

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
| `headsetControlPath` | `""` (off) | HeadsetControl 3.0+ executable: a name on PATH (`"headsetcontrol"`) or a full path |

An invalid value is ignored (its default is used) and reported in a tray notification, and by `--once`.
Unknown settings (such as the old `genericMouseName`) are ignored silently.

HeadsetControl is off by default because it reads every headset it supports, and for the Razer
BlackShark V2 Pro that means switching its dongle mode, which this app never does itself. A headset
another provider read in the last 2 minutes is not shown twice; any other one (an unknown model, or
firmware our own code cannot read) is shown from HeadsetControl.

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
  Protocols/    RazerProtocol, LogitechHidpp, PlayStationProtocol, HidBattery, XInputBattery, GalaxyBuds,
                BluetoothBattery, HidPath (pure decoding)
  Protocols/Headsets/  HeadsetCatalog + SteelSeries, HyperX, Corsair, Sony INZONE, Jabra, Logitech/ASTRO, MCHOSE
                       query sequences over the IHidLink port
  Protocols/Gamepads/  SteamController, EightBitDo, Flydigi (plus HeadsetControlOutput in Protocols/)
  Protocols/Mice/      MiceCatalog: SteelSeries, HyperX, ATK (Pulsar/VXE/Kysona), Glorious query sequences
  Monitoring/   BatteryMonitorService     (merges providers, grace period, low-battery alerts)
  Settings/     AppSettings, SettingsSerializer (validation, never throws)
  Icons/        IconPixels (pixel-font layout), DevicePictures (device pixel art)

BatteryMonitor.App    (Windows adapters + UI)
  Hid/          HID enumeration, feature reports, input/output stream, standard battery fields
  Providers/    Bluetooth, Razer, Logitech, PlayStation, Headset, Gamepad, AppleAudio, GalaxyBuds, XInput, GenericHid,
                HeadsetControl (*BatteryProvider)
  Settings/     SettingsStore (%APPDATA% file), StartupRegistration (HKCU Run key)
  Tray/         TrayApplicationContext, DeviceTrayIcon (two frames), BatteryIconRenderer
  Program.cs    composition root
```

Adding a device means adding one `IBatteryProvider` (plus its protocol decoder and tests in Core)
and registering it in `Program.cs`.

## Releases

Pushing a version tag (`git tag v1.0.0 && git push origin v1.0.0`) runs `.github/workflows/release.yml`:
tests, a self-contained single-file publish, and a GitHub Release with the portable zip.

## License

[MIT](LICENSE).

## Credits

Protocol facts for Razer devices come from the public documentation of the
[OpenRazer](https://github.com/openrazer/openrazer) project; headset facts from
[HeadsetControl](https://github.com/Sapd/HeadsetControl); Steam Controller, 8BitDo and Flydigi facts
from [SDL](https://github.com/libsdl-org/SDL) (zlib license); AirPods advertisement facts from
[OpenPods](https://github.com/adolfintel/OpenPods), [AirStatus](https://github.com/delphiki/AirStatus) and
[furiousMAC/continuity](https://github.com/furiousMAC/continuity); mouse facts from
[rivalcfg](https://github.com/flozz/rivalcfg), [hid-pulsar](https://gitlab.com/NikolasK-source/hid-pulsar),
[madr](https://github.com/dkbednarczyk/madr), [mow](https://github.com/korkje/mow) and public HyperX Pulsefire Dart write-ups. The idea was inspired by
[Wireless-Device-Battery-Monitor](https://github.com/krisgfx9/Wireless-Device-Battery-Monitor).
No code from either project is included.
