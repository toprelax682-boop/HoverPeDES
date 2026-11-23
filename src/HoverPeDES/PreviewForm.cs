using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace HoverPeDES;

/// <summary>Borderless, topmost, non-activating floating window that hosts an mpv video surface.</summary>
internal sealed class PreviewForm : Form
{
    private const int FooterBase = 26;
    private const int HotPause = 1, HotBack = 2, HotFwd = 3, HotMute = 4;
    private const uint ModCtrlAlt = 0x2 | 0x1;   // MOD_CONTROL | MOD_ALT

    private readonly Panel _video = new() { BackColor = Color.Black };
    private readonly Timer _tick = new() { Interval = 200 };
    private Mpv? _mpv;
    private string _title = "";
    private string _status = "";
    private double _pos, _dur;
    private bool _sized;
    private int _ticks;
    private bool _hotkeys;
    private Point _anchor;
    private float _scale = 1f;

    /// <summary>Source currently shown (path or URL), null when hidden.</summary>
    public string? Source { get; private set; }
    /// <summary>"explorer" or "web" - who asked for the preview.</summary>
    public string Origin { get; private set; } = "";
    public string? YtdlPath { get; set; }
    public event Action? MpvMissing;

    public PreviewForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(20, 20, 24);
        DoubleBuffered = true;
        Text = "HoverPeDESPreview";
        Controls.Add(_video);
        Size = new Size(480, 300);
        _tick.Tick += (_, _) => OnTick();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008; // NOACTIVATE | TOOLWINDOW | TOPMOST
            cp.ClassStyle |= 0x00020000;                          // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_MOUSEACTIVATE = 0x21, WM_HOTKEY = 0x312;
        if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)3; return; } // MA_NOACTIVATE
        if (m.Msg == WM_HOTKEY && _mpv != null)
        {
            switch (m.WParam.ToInt32())
            {
                case HotPause: _mpv.TogglePause(); break;
                case HotBack: _mpv.SeekRelative(-5); break;
                case HotFwd: _mpv.SeekRelative(5); break;
                case HotMute: _mpv.ToggleMute(); break;
            }
            return;
        }
        base.WndProc(ref m);
    }

    // ---------------------------------------------------------------- show / hide

    public void ShowMedia(string source, string? referrer, string origin, Point cursor)
    {
        if (!Mpv.IsAvailable()) { MpvMissing?.Invoke(); return; }

        try
        {
            EnsureMpv();
        }
        catch (Exception ex)
        {
            Log.Write(ex);
            MpvMissing?.Invoke();
            return;
        }

        Source = source;
        Origin = origin;
        _anchor = cursor;
        _sized = false;
        _ticks = 0;
        _pos = _dur = 0;
        _status = L.T("loading");
        _title = DisplayName(source);

        // Move onto the right monitor first so DPI is correct, then size and place.
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, cursor.X, cursor.Y, 10, 10,
            Native.SWP_NOACTIVATE);
        uint dpi = Native.GetDpiForWindow(Handle);
        _scale = dpi > 0 ? dpi / 96f : 1f;

        int w = (int)(Settings.Current.Width * _scale);
        ApplyLayout(w, (int)(w * 9 / 16.0));
        Place();

        if (!Visible) Show();
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, Left, Top, Width, Height,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

        var s = Settings.Current;
        _mpv!.Prop("referrer", referrer ?? "");
        _mpv.Prop("mute", s.Mute ? "yes" : "no");
        _mpv.Prop("start", MediaTypes.IsImage(source) ? "0" : s.StartPercent + "%");
        _mpv.Load(source);

        RegisterHotkeys();
        _tick.Start();
    }

    public void HidePreview()
    {
        if (Source == null && !Visible) return;
        Source = null;
        Origin = "";
        _tick.Stop();
        UnregisterHotkeys();
        try { _mpv?.Stop(); } catch (Exception ex) { Log.Write(ex); }
        Hide();
    }

    private void EnsureMpv()
    {
        if (_mpv != null) return;
        _ = Handle;          // make sure the window exists
        _ = _video.Handle;   // and the surface mpv renders into
        _mpv = new Mpv(_video.Handle, YtdlPath);
    }

    // ---------------------------------------------------------------- layout

    private int FooterPx => (int)(FooterBase * _scale);

    private void ApplyLayout(int videoW, int videoH)
    {
        _video.SetBounds(0, 0, videoW, videoH);
        Size = new Size(videoW, videoH + FooterPx);
    }

    private void Place()
    {
        var wa = Screen.FromPoint(_anchor).WorkingArea;
        int off = (int)(28 * _scale);
        int x = _anchor.X + off;
        int y = _anchor.Y + off;
        if (x + Width > wa.Right) x = _anchor.X - off - Width;
        if (y + Height > wa.Bottom) y = wa.Bottom - Height;
        x = Math.Max(wa.Left, Math.Min(x, wa.Right - Width));
        y = Math.Max(wa.Top, y);
        Location = new Point(x, y);
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, x, y, 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    private void FitToVideo(int vw, int vh)
    {
        var wa = Screen.FromPoint(_anchor).WorkingArea;
        double aspect = Math.Clamp((double)vh / vw, 0.35, 1.8);
        int w = (int)(Settings.Current.Width * _scale);
        int h = (int)(w * aspect);
        int maxH = (int)(wa.Height * 0.6) - FooterPx;
        if (h > maxH) { h = maxH; w = (int)(h / aspect); }
        ApplyLayout(w, h);
        Place();
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, Left, Top, Width, Height, Native.SWP_NOACTIVATE);
    }

    // ---------------------------------------------------------------- polling

    private void OnTick()
    {
        if (_mpv == null || Source == null) return;
        _ticks++;
        try
        {
            if (!_sized)
            {
                int vw = (int)_mpv.GetDouble("dwidth");
                int vh = (int)_mpv.GetDouble("dheight");
                if (vw > 0 && vh > 0)
                {
                    _sized = true;
                    _status = "";
                    FitToVideo(vw, vh);
                }
                else if (_ticks > 45)
                {
                    _status = L.T("cantPlay");
                }
            }
            _pos = _mpv.GetDouble("time-pos");
            _dur = _mpv.GetDouble("duration");
        }
        catch (Exception ex) { Log.Write(ex); }
        Invalidate(new Rectangle(0, _video.Height, Width, Height - _video.Height));
    }

    // ---------------------------------------------------------------- painting

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int top = _video.Height;
        int fh = Height - top;

        // progress line
        if (_dur > 0)
        {
            int pw = (int)(Width * Math.Clamp(_pos / _dur, 0, 1));
            using var b = new SolidBrush(Color.FromArgb(255, 99, 102, 241));
            g.FillRectangle(b, 0, top, pw, Math.Max(2, (int)(2 * _scale)));
        }

        using var font = new Font("Segoe UI", 12f * _scale, FontStyle.Regular, GraphicsUnit.Pixel);
        string right = !string.IsNullOrEmpty(_status) ? _status
            : _dur > 0 ? $"{Fmt(_pos)} / {Fmt(_dur)}" : "";
        var rightSize = g.MeasureString(right, font);
        int pad = (int)(8 * _scale);

        using var fg = new SolidBrush(Color.FromArgb(225, 225, 230));
        using var dim = new SolidBrush(Color.FromArgb(160, 160, 170));
        var textY = top + (fh - font.Height) / 2f + 1;

        var nameRect = new RectangleF(pad, textY, Width - rightSize.Width - pad * 3, font.Height + 2);
        using var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(_title, font, fg, nameRect, fmt);
        g.DrawString(right, font, dim, Width - rightSize.Width - pad, textY);
    }

    private static string Fmt(double sec)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, sec));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    private static string DisplayName(string source)
    {
        try
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
            {
                string last = Uri.UnescapeDataString(Path.GetFileName(u.AbsolutePath));
                return string.IsNullOrWhiteSpace(last) ? u.Host : last;
            }
            return Path.GetFileName(source);
        }
        catch { return source; }
    }

    // ---------------------------------------------------------------- hotkeys

    private void RegisterHotkeys()
    {
        if (_hotkeys) return;
        _hotkeys = true;
        Native.RegisterHotKey(Handle, HotPause, ModCtrlAlt, 0x20); // Space
        Native.RegisterHotKey(Handle, HotBack, ModCtrlAlt, 0x25);  // Left
        Native.RegisterHotKey(Handle, HotFwd, ModCtrlAlt, 0x27);   // Right
        Native.RegisterHotKey(Handle, HotMute, ModCtrlAlt, 0x4D);  // M
    }

    private void UnregisterHotkeys()
    {
        if (!_hotkeys) return;
        _hotkeys = false;
        for (int id = HotPause; id <= HotMute; id++) Native.UnregisterHotKey(Handle, id);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tick.Dispose();
            try { _mpv?.Dispose(); } catch { }
            _mpv = null;
        }
        base.Dispose(disposing);
    }
}
