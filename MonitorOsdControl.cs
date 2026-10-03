using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// One-shot DDC/CI diagnostics and standard OSD control. No polling or startup entry.
static class MonitorOsdControl
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PhysicalMonitor
    {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MonitorInfo
    {
        public int Size;
        public int Left, Top, Right, Bottom;
        public int WorkLeft, WorkTop, WorkRight, WorkBottom;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("dxva2.dll", SetLastError = true)] static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
    [DllImport("dxva2.dll", SetLastError = true)] static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll")] static extern bool DestroyPhysicalMonitors(uint count, PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll", SetLastError = true)] static extern bool GetCapabilitiesStringLength(IntPtr monitor, out uint length);
    [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Ansi)] static extern bool CapabilitiesRequestAndCapabilitiesReply(IntPtr monitor, StringBuilder text, uint length);
    [DllImport("dxva2.dll", SetLastError = true)] static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr monitor, byte code, out uint type, out uint current, out uint maximum);
    [DllImport("dxva2.dll", SetLastError = true)] static extern bool SetVCPFeature(IntPtr monitor, byte code, uint value);

    public static int Main(string[] args)
    {
        string target = null;
        uint? value = null;
        foreach (string arg in args)
        {
            if (arg.StartsWith("--display=")) target = arg.Substring(10);
            else if (arg == "--disable") value = 1;
            else if (arg == "--enable") value = 2;
            else { Console.Error.WriteLine("Usage: MonitorOsdControl.exe [--display=\\\\.\\DISPLAY2] [--disable|--enable]"); return 2; }
        }
        if (value.HasValue && target == null) { Console.Error.WriteLine("A specific --display is required for writes."); return 2; }
        bool found = false;
        bool success = true;
        MonitorCallback callback = delegate(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data)
        {
            MonitorInfo info = new MonitorInfo();
            info.Size = Marshal.SizeOf(typeof(MonitorInfo));
            if (!GetMonitorInfo(monitor, ref info)) { success = false; return true; }
            bool auto = string.Equals(target, "auto", StringComparison.OrdinalIgnoreCase);
            if (target != null && !auto && !string.Equals(target, info.Device, StringComparison.OrdinalIgnoreCase)) return true;
            if (!auto) found = true;
            Console.WriteLine("Display: " + info.Device);
            uint count;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out count) || count == 0) { Console.WriteLine("No DDC/CI handles."); success = false; return true; }
            PhysicalMonitor[] physical = new PhysicalMonitor[count];
            if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, physical)) { success = false; return true; }
            try
            {
                foreach (PhysicalMonitor p in physical)
                {
                    if (auto)
                    {
                        uint size;
                        if (!GetCapabilitiesStringLength(p.Handle, out size) || size == 0 || size >= 65536) continue;
                        StringBuilder identity = new StringBuilder((int)size);
                        if (!CapabilitiesRequestAndCapabilitiesReply(p.Handle, identity, size)) continue;
                        if (identity.ToString().IndexOf("model(VG279Q)", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        found = true;
                    }
                    Console.WriteLine("Monitor: " + p.Description);
                    if (!value.HasValue)
                    {
                        uint length;
                        if (GetCapabilitiesStringLength(p.Handle, out length) && length > 0 && length < 65536)
                        {
                            StringBuilder caps = new StringBuilder((int)length);
                            if (CapabilitiesRequestAndCapabilitiesReply(p.Handle, caps, length)) Console.WriteLine("Capabilities: " + caps);
                            else Console.WriteLine("Capabilities unavailable: " + Marshal.GetLastWin32Error());
                        }
                        else Console.WriteLine("Capabilities length unavailable: " + Marshal.GetLastWin32Error());
                    }
                    uint type, current, maximum;
                    bool readable = GetVCPFeatureAndVCPFeatureReply(p.Handle, 0xCA, out type, out current, out maximum);
                    Console.WriteLine(readable ? "OSD 0xCA: current=" + current + " maximum=" + maximum : "OSD 0xCA read unsupported/failed: " + Marshal.GetLastWin32Error());
                    if (value.HasValue)
                    {
                        // MCCS 0xCA: 1 = OSD disabled, 2 = OSD enabled.
                        bool written = SetVCPFeature(p.Handle, 0xCA, value.Value);
                        Console.WriteLine(written ? "OSD write accepted: " + value.Value : "OSD write failed: " + Marshal.GetLastWin32Error());
                        success &= written;
                        Thread.Sleep(250);
                        if (GetVCPFeatureAndVCPFeatureReply(p.Handle, 0xCA, out type, out current, out maximum))
                        {
                            Console.WriteLine("OSD readback: " + current);
                            success &= current == value.Value;
                        }
                        else Console.WriteLine("Readback unavailable; visual confirmation required.");
                    }
                }
            }
            finally { DestroyPhysicalMonitors(count, physical); }
            return true;
        };
        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero)) success = false;
        if (!found) Console.Error.WriteLine("Target display not found.");
        return found && success ? 0 : 1;
    }
}
