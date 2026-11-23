using System;
using System.Threading;
using System.Windows.Forms;

namespace HoverPeDES;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\HoverPeDES.SingleInstance", out bool created);
        if (!created) return;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Settings.Load();
        Application.ThreadException += (_, e) => Log.Write(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write(e.ExceptionObject as Exception);

        try
        {
            Application.Run(new TrayContext());
        }
        catch (Exception ex)
        {
            Log.Write(ex);
            MessageBox.Show(ex.Message, "HoverPeDES", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
