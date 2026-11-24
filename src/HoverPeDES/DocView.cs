using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HoverPeDES;

/// <summary>
/// Lazily-created Microsoft Edge WebView2 surface used to show PDF and SVG files.
/// If the WebView2 runtime is missing, <see cref="Failed"/> fires and the caller shows a hint instead.
/// </summary>
internal sealed class DocView : IDisposable
{
    private readonly Control _parent;
    private WebView2? _wv;
    private bool _initStarted, _ready, _failed, _visible;
    private string? _pending;
    private Rectangle _bounds;

    public event Action? Failed;

    public DocView(Control parent) => _parent = parent;

    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            if (_wv != null) _wv.Visible = value;
        }
    }

    public void SetBounds(int x, int y, int w, int h)
    {
        _bounds = new Rectangle(x, y, w, h);
        if (_wv != null) _wv.Bounds = _bounds;
    }

    /// <summary>Clears the page so the previous document never flashes when the window is reused.</summary>
    public void Blank()
    {
        _pending = null;
        try
        {
            if (_ready && _wv?.CoreWebView2 != null) _wv.CoreWebView2.Navigate("about:blank");
        }
        catch (Exception ex) { Log.Write(ex); }
    }

    public async void Navigate(string url)
    {
        if (_failed) { Failed?.Invoke(); return; }
        _pending = url;
        try
        {
            if (_wv == null)
            {
                _wv = new WebView2
                {
                    DefaultBackgroundColor = Color.FromArgb(20, 20, 24),
                    Bounds = _bounds,
                    Visible = _visible,
                };
                _parent.Controls.Add(_wv);
            }

            var wv = _wv;
            if (!_ready)
            {
                if (_initStarted) return;   // the pending URL is opened when initialisation finishes
                _initStarted = true;
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Settings.Dir, "webview2"));
                await wv.EnsureCoreWebView2Async(env);

                var st = wv.CoreWebView2.Settings;
                st.AreDefaultContextMenusEnabled = false;
                st.AreDevToolsEnabled = false;
                st.IsStatusBarEnabled = false;
                st.IsZoomControlEnabled = false;
                st.AreBrowserAcceleratorKeysEnabled = false;
                _ready = true;
            }

            if (_pending != null) wv.CoreWebView2.Navigate(_pending);
        }
        catch (Exception ex)
        {
            Log.Write(ex);
            _failed = true;
            Failed?.Invoke();
        }
    }

    public void Dispose()
    {
        try { _wv?.Dispose(); } catch { }
        _wv = null;
    }
}
