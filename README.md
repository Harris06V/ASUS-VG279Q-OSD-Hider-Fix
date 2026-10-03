# ASUS VG279Q OSD Disable

## Usage

Double-click **DisableOsd.exe**. It identifies the ASUS VG279Q using its DDC/CI
capabilities, sends standard VCP `0xCA = 1` (disable OSD), and exits after you
dismiss the confirmation. There is no polling, tray process, or automatic startup.
DDC/CI must be enabled and the monitor must be connected and awake.

On this monitor, the command was accepted and the user confirmed that the popup
stopped and remained gone after a power-button off/on test. The monitor does not
advertise `0xCA` or allow reading it back, so command success alone cannot prove
visual success. Persistence across unplugging power or factory reset is untested.
If the popup returns, run **DisableOsd.exe** again.

To request OSD re-enabling, use PowerShell:

```powershell
.\MonitorOsdControl.exe --display=auto --enable
```

`MonitorOsdControl.exe` without arguments prints diagnostic capabilities.
`--display=auto --disable` sends the same command as the double-click launcher.
Auto mode only writes to monitors identifying themselves as `model(VG279Q)`.

## Build

```powershell
.\build.ps1
```

Produces `DisableOsd.exe` and `MonitorOsdControl.exe` using the C# compiler built
into Windows (no installs needed).

## Files

- `OsdLauncher.cs`: double-click launcher and confirmation dialog.
- `MonitorOsdControl.cs`: monitor detection, DDC/CI commands, and diagnostic CLI.
- `build.ps1`: builds both executables.

`DisableOsd.exe` is standalone; the diagnostic executable is only needed for
diagnostics or re-enabling the OSD.
