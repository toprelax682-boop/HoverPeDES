using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HoverPeDES;

/// <summary>Owns the tray icon and wires the watcher, the local server and the preview window together.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly Control _ui = new();
    private readonly PreviewForm _preview = new();
    private readonly HoverWatcher _watcher = new();
    private readonly LocalServer _server = new();
    private bool _warnedMpv;

    public TrayContext()
    {
        _ = _ui.Handle;       // marshalling target for background threads
        _ = _preview.Handle;

        string ytdl = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
        if (File.Exists(ytdl)) _preview.YtdlPath = ytdl;
        _preview.MpvMissing += OnMpvMissing;

        _tray = new NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = L.T("tip"),
        };
        _tray.DoubleClick += (_, _) => OpenSettings();
        BuildMenu();

        _watcher.Hovered += (path, p) => UI(() => _preview.ShowMedia(path, null, "explorer", p));
        _watcher.Left += () => UI(() => { if (_preview.Origin == "explorer") _preview.HidePreview(); });

        _server.ShowRequested += (url, referrer) => UI(() =>
        {
            Native.GetCursorPos(out var p);
            _preview.ShowMedia(url, referrer, "web", p);
        });
        _server.HideRequested += () => UI(() => { if (_preview.Origin == "web") _preview.HidePreview(); });
        _server.PortBusy += () => UI(() =>
            _tray.ShowBalloonTip(5000, "HoverPeDES", L.T("portBusy"), ToolTipIcon.Warning));

        _server.Start();
        _watcher.Start();

        if (!Mpv.IsAvailable()) OnMpvMissing();

        if (Settings.FirstRun)
        {
            Settings.Current.Save();
            _tray.ShowBalloonTip(6000, "HoverPeDES", L.T("started"), ToolTipIcon.Info);
        }
    }

    private void UI(Action a) => _ui.BeginInvoke(a);

    private static Icon LoadIcon()
    {
        try
        {
            var icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            if (icon != null) return icon;
        }
        catch { }
        return SystemIcons.Application;
    }

    private void BuildMenu()
    {
        _tray.Text = L.T("tip");
        var menu = new ContextMenuStrip();

        var enable = new ToolStripMenuItem(L.T("enable")) { Checked = Settings.Current.Enabled, CheckOnClick = true };
        enable.CheckedChanged += (_, _) =>
        {
            Settings.Current.Enabled = enable.Checked;
            Settings.Current.Save();
            if (!enable.Checked) _preview.HidePreview();
        };
        menu.Items.Add(enable);
        menu.Items.Add(L.T("settings"), null, (_, _) => OpenSettings());
        menu.Items.Add(L.T("extension"), null, (_, _) => InstallExtension());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("log"), null, (_, _) => OpenLog());
        menu.Items.Add(L.T("about"), null, (_, _) => MessageBox.Show(
            string.Format(L.T("aboutBody"), Application.ProductVersion), "HoverPeDES",
            MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("exit"), null, (_, _) => ExitThread());

        var old = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = menu;
        old?.Dispose();
    }

    private void OpenSettings()
    {
        _preview.HidePreview();
        using var f = new SettingsForm();
        if (f.ShowDialog() == DialogResult.OK) BuildMenu();
    }

    private void InstallExtension()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "extension");
        try { Clipboard.SetText(dir); } catch { }
        TryStart("explorer.exe", "\"" + dir + "\"");
        // Open the extensions page in whichever Chromium browser is installed.
        if (!TryStart("msedge.exe", "edge://extensions"))
            TryStart("chrome.exe", "chrome://extensions");
        MessageBox.Show(string.Format(L.T("extBody"), dir), L.T("extTitle"),
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static bool TryStart(string file, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    private static void OpenLog()
    {
        try
        {
            if (!File.Exists(Log.FilePath)) Log.Write("log created");
            Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
        }
        catch { }
    }

    private void OnMpvMissing()
    {
        if (_warnedMpv) return;
        _warnedMpv = true;
        MessageBox.Show(L.T("noMpv"), "HoverPeDES", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected override void ExitThreadCore()
    {
        _watcher.Dispose();
        _server.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _preview.Dispose();
        _ui.Dispose();
        base.ExitThreadCore();
    }
}
