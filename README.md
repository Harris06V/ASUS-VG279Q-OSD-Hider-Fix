# ASUS VG279Q OSD Hider

Workaround for joystick drift, continuously polls the monitor over DDC/CI, which dismisses OSD pop ups almost immediately. Runs in the system tray.

Requires DDC/CI enabled on the monitor (System Setup → DDC/CI → On).

## Build

```powershell
.\build.ps1
```

Produces `OsdHider.exe` using the C# compiler built into Windows (no installs needed).

## Usage

Double-click `OsdHider.exe`. A tray icon ("OSD Hider") appears.

- Right-click the tray icon → **Start with Windows** to toggle startup (on by default)
- Right-click the tray icon → **Exit** to quit
- Only one instance runs at a time
- Reconnects automatically after sleep or monitor power cycles
- If you move the exe, launch it once from the new location to update startup

## Options

| Argument          | Default        | Description                            |
|-------------------|----------------|----------------------------------------|
| `--display=`      | `\\.\DISPLAY2` | Target display                         |
| `--vcp=`          | `0x10`         | VCP code to poll (brightness)          |
| `--interval=`     | `0`            | Delay between polls in ms; raise if unstable |

Arguments are saved in the startup entry, e.g. `OsdHider.exe --display=\\.\DISPLAY1`.

