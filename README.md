# ASUS VG279Q OSD Hider

Workaround for joystick drift, continuously polls the monitor over DDC/CI, which dismisses OSD pop ups almost immediately. Runs in the system tray.

Requires DDC/CI enabled on the monitor (System Setup → DDC/CI → On).

## Usage

```powershell
.\keep_osd_active.ps1              # run now (tray icon: "OSD Hider")
.\keep_osd_active.ps1 -Install     # run at login
.\keep_osd_active.ps1 -Uninstall   # remove from login
```

- Exit: right-click the tray icon → **Exit**
- Only one instance runs at a time
- Reconnects automatically after sleep or monitor power cycles
- Re-run `-Install` if the folder is moved

## Options

| Parameter     | Default        | Description                              |
|---------------|----------------|------------------------------------------|
| `-Display`    | `\\.\DISPLAY2` | Target display                           |
| `-VcpCode`    | `0x10`         | VCP code to poll (brightness)            |
| `-IntervalMs` | `0`            | Delay between polls; raise if unstable   |

Pass options together with `-Install` to save them in the startup shortcut.

