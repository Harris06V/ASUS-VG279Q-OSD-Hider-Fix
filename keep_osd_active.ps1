param(
    [string]$Display = "\\.\DISPLAY2",
    [byte]$VcpCode = 0x10,
    [int]$IntervalMs = 0,
    [switch]$Install,
    [switch]$Uninstall
)

$shortcutPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'OSD Hider.lnk'

if ($Install) {
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut($shortcutPath)
    $lnk.TargetPath = (Get-Command powershell.exe).Source
    $lnk.Arguments = "-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Display `"$Display`" -VcpCode $VcpCode -IntervalMs $IntervalMs"
    $lnk.WorkingDirectory = $PSScriptRoot
    $lnk.WindowStyle = 7
    $lnk.Save()
    Write-Host "Added to startup: $shortcutPath"
    exit
}

if ($Uninstall) {
    Remove-Item $shortcutPath -ErrorAction SilentlyContinue
    Write-Host "Removed from startup"
    exit
}

$mutex = New-Object System.Threading.Mutex($false, 'Local\OsdHider')
try { $owned = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $owned = $true }
if (-not $owned) {
    Write-Host "OSD Hider is already running"
    exit
}

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public static class OsdHider {
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PHYSICAL_MONITOR {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte code, IntPtr type, out uint current, out uint maximum);

    [DllImport("dxva2.dll")]
    public static extern bool DestroyPhysicalMonitors(uint count, PHYSICAL_MONITOR[] monitors);

    static volatile bool running;
    static Thread worker;

    public static void Start(string display, byte code, int intervalMs) {
        if (running) return;
        running = true;
        worker = new Thread(() => Run(display, code, intervalMs));
        worker.IsBackground = true;
        worker.Start();
    }

    public static void Stop() {
        running = false;
        if (worker != null) worker.Join(2000);
    }

    static IntPtr FindMonitor(string display) {
        foreach (Screen s in Screen.AllScreens) {
            if (string.Equals(s.DeviceName, display, StringComparison.OrdinalIgnoreCase)) {
                POINT pt;
                pt.X = s.Bounds.Left + s.Bounds.Width / 2;
                pt.Y = s.Bounds.Top + s.Bounds.Height / 2;
                return MonitorFromPoint(pt, 0);
            }
        }
        return IntPtr.Zero;
    }

    static void Run(string display, byte code, int intervalMs) {
        while (running) {
            IntPtr hMon = FindMonitor(display);
            uint count;
            if (hMon == IntPtr.Zero || !GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out count) || count == 0) {
                Thread.Sleep(2000);
                continue;
            }
            PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hMon, count, monitors)) {
                Thread.Sleep(2000);
                continue;
            }
            try {
                int failures = 0;
                // Handles go stale after sleep/resume or monitor power cycles, so reopen after repeated failures.
                while (running && failures < 20) {
                    uint cur, max;
                    if (GetVCPFeatureAndVCPFeatureReply(monitors[0].hPhysicalMonitor, code, IntPtr.Zero, out cur, out max)) {
                        failures = 0;
                    } else {
                        failures++;
                        Thread.Sleep(100);
                    }
                    if (intervalMs > 0) Thread.Sleep(intervalMs);
                }
            } finally {
                DestroyPhysicalMonitors(count, monitors);
            }
        }
    }
}
"@

$tray = New-Object System.Windows.Forms.NotifyIcon
$tray.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Get-Command powershell.exe).Source)
$tray.Text = "OSD Hider ($Display)"
$menu = New-Object System.Windows.Forms.ContextMenuStrip
[void]$menu.Items.Add('Exit', $null, { [System.Windows.Forms.Application]::Exit() })
$tray.ContextMenuStrip = $menu
$tray.Visible = $true

[OsdHider]::Start($Display, $VcpCode, $IntervalMs)
try {
    [System.Windows.Forms.Application]::Run()
}
finally {
    [OsdHider]::Stop()
    $tray.Visible = $false
    $tray.Dispose()
    $mutex.ReleaseMutex()
}
