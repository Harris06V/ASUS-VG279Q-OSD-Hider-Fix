using System;
using System.Windows.Forms;

static class OsdLauncher
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        try
        {
            int result = MonitorOsdControl.Main(new string[] { "--display=auto", "--disable" });
            MessageBox.Show(result == 0
                ? "OSD-disable command sent to your ASUS VG279Q.\n\nThis program now exits; no polling runs in the background.\nIf the menu ever returns, run DisableOsd.exe again."
                : "Could not send the OSD-disable command to an ASUS VG279Q.\n\nCheck that the monitor is connected, awake, and DDC/CI is enabled.",
                "ASUS OSD Disable", MessageBoxButtons.OK,
                result == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ASUS OSD Disable", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
