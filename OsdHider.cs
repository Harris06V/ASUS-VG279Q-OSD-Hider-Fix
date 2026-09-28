using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("OSD Hider")]
[assembly: System.Reflection.AssemblyProduct("OSD Hider")]

static class Program
{
    const string AppName = "OSD Hider";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppKey = @"Software\OsdHider";

    [STAThread]
    static void Main(string[] args)
    {
        string display = @"\\.\DISPLAY2";
        byte vcpCode = 0x10;
        int intervalMs = 0;
        foreach (string arg in args)
        {
            if (arg.StartsWith("--display=", StringComparison.OrdinalIgnoreCase)) display = arg.Substring(10);
            else if (arg.StartsWith("--vcp=", StringComparison.OrdinalIgnoreCase)) vcpCode = Convert.ToByte(arg.Substring(6), 16);
            else if (arg.StartsWith("--interval=", StringComparison.OrdinalIgnoreCase)) intervalMs = int.Parse(arg.Substring(11));
        }

        bool created;
        using (Mutex mutex = new Mutex(true, @"Local\OsdHider", out created))
        {
            if (!created) return;

            string command = "\"" + Application.ExecutablePath + "\"";
            foreach (string arg in args) command += " \"" + arg + "\"";
            InitStartup(command);

            Application.EnableVisualStyles();

            ToolStripMenuItem startupItem = new ToolStripMenuItem("Start with Windows");
            startupItem.Checked = StartupEnabled();
            startupItem.Click += delegate
            {
                SetStartup(!startupItem.Checked, command);
                startupItem.Checked = StartupEnabled();
            };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { Application.Exit(); });

            NotifyIcon tray = new NotifyIcon();
            tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            tray.Text = AppName + " (" + display + ")";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;

            Poller.Start(display, vcpCode, intervalMs);
            Application.Run();
            Poller.Stop();

            tray.Visible = false;
            tray.Dispose();
        }
    }

    static bool StartupEnabled()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
            return key != null && key.GetValue(AppName) != null;
    }

    static void SetStartup(bool enable, string command)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (enable) key.SetValue(AppName, command);
            else key.DeleteValue(AppName, false);
        }
        Registry.CurrentUser.CreateSubKey(AppKey).Dispose();
    }

    // Enable startup on first run; afterwards refresh the path so moving the exe keeps working.
    static void InitStartup(string command)
    {
        bool firstRun;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppKey))
            firstRun = key == null;
        if (firstRun || StartupEnabled()) SetStartup(true, command);
    }
}

static class Poller
{
    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte code, IntPtr type, out uint current, out uint maximum);

    [DllImport("dxva2.dll")]
    static extern bool DestroyPhysicalMonitors(uint count, PHYSICAL_MONITOR[] monitors);

    static volatile bool running;
    static Thread worker;

    public static void Start(string display, byte code, int intervalMs)
    {
        if (running) return;
        running = true;
        worker = new Thread(() => Run(display, code, intervalMs));
        worker.IsBackground = true;
        worker.Start();
    }

    public static void Stop()
    {
        running = false;
        if (worker != null) worker.Join(2000);
    }

    static IntPtr FindMonitor(string display)
    {
        foreach (Screen s in Screen.AllScreens)
        {
            if (string.Equals(s.DeviceName, display, StringComparison.OrdinalIgnoreCase))
            {
                POINT pt;
                pt.X = s.Bounds.Left + s.Bounds.Width / 2;
                pt.Y = s.Bounds.Top + s.Bounds.Height / 2;
                return MonitorFromPoint(pt, 0);
            }
        }
        return IntPtr.Zero;
    }

    static void Run(string display, byte code, int intervalMs)
    {
        while (running)
        {
            IntPtr hMon = FindMonitor(display);
            uint count;
            if (hMon == IntPtr.Zero || !GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out count) || count == 0)
            {
                Thread.Sleep(2000);
                continue;
            }
            PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hMon, count, monitors))
            {
                Thread.Sleep(2000);
                continue;
            }
            try
            {
                int failures = 0;
                // Handles go stale after sleep/resume or monitor power cycles, so reopen after repeated failures.
                while (running && failures < 20)
                {
                    uint cur, max;
                    if (GetVCPFeatureAndVCPFeatureReply(monitors[0].hPhysicalMonitor, code, IntPtr.Zero, out cur, out max))
                    {
                        failures = 0;
                    }
                    else
                    {
                        failures++;
                        Thread.Sleep(100);
                    }
                    if (intervalMs > 0) Thread.Sleep(intervalMs);
                }
            }
            finally
            {
                DestroyPhysicalMonitors(count, monitors);
            }
        }
    }
}
