using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HoverPeDES;

/// <summary>
/// Borderless, topmost, non-activating floating window. Depending on the file it hosts an mpv video surface,
/// a text box (text / Office), a picture (camera RAW) or an Edge WebView2 (PDF / SVG).
/// </summary>
internal sealed class PreviewForm : Form
{
    private enum ViewKind { Media, Text, Office, Raw, WebDoc }

    private const int FooterBase = 26;
    private const int HotPause = 1, HotBack = 2, HotFwd = 3, HotMute = 4;
    private const uint ModCtrlAlt = 0x2 | 0x1;   // MOD_CONTROL | MOD_ALT

    private readonly Panel _video = new() { BackColor = Color.Black };
    private readonly PictureBox _pic = new() { SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
    private readonly TextBox _text = new()
    {
        Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, WordWrap = false,
        ScrollBars = ScrollBars.None, TabStop = false, Visible = false, ShortcutsEnabled = false,
        Cursor = Cursors.Default,
    };
    private readonly DocView _doc;
    private readonly Timer _tick = new() { Interval = 200 };
    private Mpv? _mpv;
    private Font? _mono;
    private float _monoScale;
    private string _title = "";
    private string _status = "";
    private double _pos, _dur;
    private bool _sized, _hotkeys, _isAudio, _vizSet;
    private int _ticks, _loadToken;
    private Point _anchor;
    private float _scale = 1f;
    private ViewKind _kind = ViewKind.Media;

    /// <summary>Source currently shown (path or URL), null when hidden.</summary>
    public string? Source { get; private set; }
    /// <summary>"explorer" or "web" - who asked for the preview.</summary>
    public string Origin { get; private set; } = "";
    public string? YtdlPath { get; set; }
    public event Action? MpvMissing;
    public event Action? WebViewMissing;

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
        _pic.BackColor = BackColor;
        Controls.Add(_pic);
        _text.BackColor = BackColor;
        _text.ForeColor = Color.FromArgb(220, 220, 228);
        Controls.Add(_text);
        _doc = new DocView(this);
        _doc.Failed += OnDocFailed;
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
        if (m.Msg == WM_HOTKEY && _mpv != null && _kind == ViewKind.Media)
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

    private static ViewKind Classify(string source, string origin)
    {
        if (origin != "explorer" || !File.Exists(source)) return ViewKind.Media;
        if (MediaTypes.IsVideo(source)) return ViewKind.Media;      // .ts is MPEG-TS video, not TypeScript
        if (MediaTypes.IsRaw(source) || MediaTypes.IsPsd(source)) return ViewKind.Raw;
        if (MediaTypes.IsWebDoc(source)) return ViewKind.WebDoc;
        if (MediaTypes.IsOffice(source)) return ViewKind.Office;
        if (MediaTypes.IsText(source)) return ViewKind.Text;
        return ViewKind.Media;
    }

    public void ShowMedia(string source, string? referrer, string origin, Point cursor)
    {
        var kind = Classify(source, origin);
        if (kind == ViewKind.Media)
        {
            if (!Mpv.IsAvailable()) { MpvMissing?.Invoke(); return; }
            try { EnsureMpv(); }
            catch (Exception ex) { Log.Write(ex); MpvMissing?.Invoke(); return; }
        }

        int token = ++_loadToken;      // invalidates any async load still running for the previous file
        SetPicture(null);
        _doc.Blank();

        Source = source;
        Origin = origin;
        _anchor = cursor;
        _kind = kind;
        _sized = false;
        _ticks = 0;
        _pos = _dur = 0;
        _status = L.T("loading");
        _title = DisplayName(source);
        _isAudio = kind == ViewKind.Media && MediaTypes.IsAudio(source);
        _vizSet = false;
        _video.Visible = kind == ViewKind.Media;
        _text.Visible = kind == ViewKind.Text || kind == ViewKind.Office;
        _text.WordWrap = kind == ViewKind.Office;
        _pic.Visible = kind == ViewKind.Raw;
        _doc.Visible = kind == ViewKind.WebDoc;
        if (_text.Visible) _text.Clear();

        // Move onto the right monitor first so DPI is correct, then size and place.
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, cursor.X, cursor.Y, 10, 10, Native.SWP_NOACTIVATE);
        uint dpi = Native.GetDpiForWindow(Handle);
        _scale = dpi > 0 ? dpi / 96f : 1f;

        int w = (int)(Settings.Current.Width * _scale);
        switch (kind)
        {
            case ViewKind.Media:
                ApplyLayout(w, (int)(w * 9 / 16.0));
                break;
            case ViewKind.Text:
                PrepareText(source);
                ApplyLayout(w, (int)(w * 0.75));
                break;
            case ViewKind.Office:
            {
                EnsureMono();
                var (ow, oh) = FitSize(0.8, 0.8, 1.2);
                ApplyLayout(ow, oh);
                break;
            }
            case ViewKind.WebDoc:
            {
                var (dw, dh) = MediaTypes.IsPdf(source) ? FitSize(1.3, 0.8, 1.2) : FitSize(0.9, 0.7, 1.0);
                ApplyLayout(dw, dh);
                break;
            }
            default:
                ApplyLayout(w, (int)(w * 0.75));
                break;
        }
        Place();

        if (!Visible) Show();
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, Left, Top, Width, Height,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

        if (kind != ViewKind.Media)
        {
            _tick.Stop();
            UnregisterHotkeys();
            try { _mpv?.Stop(); } catch (Exception ex) { Log.Write(ex); }

            switch (kind)
            {
                case ViewKind.Office: _ = LoadOfficeAsync(source, token); break;
                case ViewKind.Raw: _ = LoadRawAsync(source, token); break;
                case ViewKind.WebDoc:
                    _status = Path.GetExtension(source).TrimStart('.').ToUpperInvariant();
                    _doc.Navigate(ViewerUrl(source));
                    break;
            }
            Invalidate();
            return;
        }

        var s = Settings.Current;
        _mpv!.Prop("referrer", referrer ?? "");
        _mpv.Prop("mute", s.Mute ? "yes" : "no");
        _mpv.Prop("lavfi-complex", "");
        _mpv.Prop("start", MediaTypes.IsImage(source) ? "0" : s.StartPercent + "%");
        _mpv.Load(source);

        RegisterHotkeys();
        _tick.Start();
    }

    public void HidePreview()
    {
        if (Source == null && !Visible) return;
        _loadToken++;
        Source = null;
        Origin = "";
        _tick.Stop();
        UnregisterHotkeys();
        try { _mpv?.Stop(); } catch (Exception ex) { Log.Write(ex); }
        _doc.Blank();
        SetPicture(null);
        Hide();
    }

    private void EnsureMpv()
    {
        if (_mpv != null) return;
        _ = Handle;          // make sure the window exists
        _ = _video.Handle;   // and the surface mpv renders into
        _mpv = new Mpv(_video.Handle, YtdlPath);
    }

    private static string ViewerUrl(string path)
    {
        string url = new Uri(path).AbsoluteUri;
        return MediaTypes.IsPdf(path) ? url + "#toolbar=0&navpanes=0&view=FitH" : url;
    }

    private void OnDocFailed()
    {
        _status = L.T("cantPreview");
        Invalidate();
        WebViewMissing?.Invoke();
    }

    private void SetPicture(Bitmap? bmp)
    {
        var old = _pic.Image;
        _pic.Image = bmp;
        old?.Dispose();
    }

    // ---------------------------------------------------------------- async loaders (RAW, Office)

    private async Task LoadRawAsync(string path, int token)
    {
        Bitmap? bmp = null;
        try { bmp = await Task.Run(() => MediaTypes.IsPsd(path) ? PsdPreview.Load(path) : RawPreview.Load(path)); }
        catch (Exception ex) { Log.Write(ex); }

        if (token != _loadToken) { bmp?.Dispose(); return; }
        if (bmp == null) { _status = L.T("cantPreview"); Invalidate(); return; }

        SetPicture(bmp);
        var (w, h) = FitSize(bmp.Height / (double)bmp.Width, 0.7, 1.0);
        ResizeTo(w, h);
        _status = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        Invalidate();
    }

    private async Task LoadOfficeAsync(string path, int token)
    {
        string text = "";
        bool failed = false;
        try { text = await Task.Run(() => OfficeText.Extract(path)); }
        catch (Exception ex) { Log.Write(ex); failed = true; }

        if (token != _loadToken) return;
        if (failed) { _status = L.T("cantPreview"); Invalidate(); return; }

        _text.Text = (string.IsNullOrWhiteSpace(text) ? L.T("noText") : text).Replace("\n", "\r\n");
        try { _status = FormatSize(new FileInfo(path).Length); } catch { _status = ""; }
        Invalidate();
    }

    // ---------------------------------------------------------------- text preview

    private void EnsureMono()
    {
        if (_mono == null || Math.Abs(_monoScale - _scale) > 0.01f)
        {
            var old = _mono;
            _mono = new Font("Consolas", 12f * _scale, FontStyle.Regular, GraphicsUnit.Pixel);
            _monoScale = _scale;
            _text.Font = _mono;
            old?.Dispose();
        }
        else
        {
            _text.Font = _mono;
        }
    }

    private void PrepareText(string path)
    {
        EnsureMono();

        string content;
        long length = 0;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            length = fs.Length;
            var buf = new byte[(int)Math.Min(length, 48 * 1024)];
            int n = fs.Read(buf, 0, buf.Length);
            content = DecodeText(buf, n);
            if (length > n) content += "\n...";
        }
        catch (Exception ex) { content = ex.Message; }

        content = content.Replace("\0", "").Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
        var lines = content.Split('\n');
        if (lines.Length > 80) content = string.Join("\n", lines, 0, 80) + "\n...";
        _text.Text = content.Replace("\n", "\r\n");
        _status = FormatSize(length);
    }

    private static string DecodeText(byte[] b, int n)
    {
        if (n >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, n - 3);
        if (n >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, n - 2);
        if (n >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, n - 2);
        return Encoding.UTF8.GetString(b, 0, n);
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1048576 ? $"{bytes / 1048576.0:0.#} MB" : bytes >= 1024 ? $"{bytes / 1024.0:0.#} KB" : bytes + " B";

    // ---------------------------------------------------------------- layout

    private int FooterPx => (int)(FooterBase * _scale);

    private void ApplyLayout(int contentW, int contentH)
    {
        _video.SetBounds(0, 0, contentW, contentH);
        _pic.SetBounds(0, 0, contentW, contentH);
        _doc.SetBounds(0, 0, contentW, contentH);
        _text.SetBounds(8, 6, Math.Max(10, contentW - 16), Math.Max(10, contentH - 12));
        Size = new Size(contentW, contentH + FooterPx);
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

    /// <summary>Content size for a given height/width aspect, limited to a share of the screen.</summary>
    private (int w, int h) FitSize(double aspect, double heightFrac, double widthMul)
    {
        var wa = Screen.FromPoint(_anchor).WorkingArea;
        aspect = Math.Clamp(aspect, 0.35, 1.8);
        int w = (int)(Settings.Current.Width * _scale * widthMul);
        int h = (int)(w * aspect);
        int maxH = (int)(wa.Height * heightFrac) - FooterPx;
        if (h > maxH) { h = maxH; w = (int)(h / aspect); }
        int maxW = (int)(wa.Width * 0.9);
        if (w > maxW) { w = maxW; h = (int)(w * aspect); }
        return (Math.Max(w, 80), Math.Max(h, 60));
    }

    private void ResizeTo(int w, int h)
    {
        ApplyLayout(w, h);
        Place();
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, Left, Top, Width, Height, Native.SWP_NOACTIVATE);
    }

    private void FitToVideo(int vw, int vh)
    {
        var (w, h) = FitSize((double)vh / vw, 0.6, 1.0);
        ResizeTo(w, h);
    }

    // ---------------------------------------------------------------- polling

    private void OnTick()
    {
        if (_mpv == null || Source == null || _kind != ViewKind.Media) return;
        _ticks++;
        try
        {
            // Audio without cover art: show a spectrum visualiser instead of a black box.
            if (_isAudio && !_sized && !_vizSet && _ticks >= 3 && _mpv.Get("vid") == "no")
            {
                _mpv.Prop("lavfi-complex", "[aid1]asplit[ao][a];[a]showcqt=s=640x360:fps=30[vo]");
                _vizSet = true;
            }

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
                else if (_isAudio && _mpv.GetDouble("time-pos") > 0)
                {
                    _status = "";   // audio is playing even if no picture / visualiser appeared
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
            _loadToken++;
            _doc.Dispose();
            SetPicture(null);
            _mono?.Dispose();
            try { _mpv?.Dispose(); } catch { }
            _mpv = null;
        }
        base.Dispose(disposing);
    }
}
