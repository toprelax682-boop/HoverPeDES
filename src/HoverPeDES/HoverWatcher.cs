using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Automation;

namespace HoverPeDES;

/// <summary>
/// Polls the cursor and, when it rests on a media file in Explorer or on the Desktop,
/// resolves the file path using UI Automation + the Shell.Application COM object.
/// Runs on its own STA thread so slow cross-process calls never block the UI.
/// </summary>
internal sealed class HoverWatcher : IDisposable
{
    public event Action<string, Point>? Hovered;
    public event Action? Left;

    private readonly Thread _thread;
    private volatile bool _run = true;
    private string? _current;
    private const string Keep = "\0keep";

    /// <summary>Handle of the preview window; hovering/clicking it keeps it open so documents can be scrolled.</summary>
    public IntPtr PreviewHwnd { get; set; }
    private object? _shellObj;

    public HoverWatcher()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "HoverPeDES.Watcher" };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    public void Start() => _thread.Start();

    private void Loop()
    {
        var sw = Stopwatch.StartNew();
        Point last = default;
        long lastMove = 0;
        bool resolved = true;

        while (_run)
        {
            Thread.Sleep(50);

            if (!Settings.Current.Enabled)
            {
                Clear();
                resolved = true;
                continue;
            }

            Native.GetCursorPos(out var p);
            if (Math.Abs(p.X - last.X) > 3 || Math.Abs(p.Y - last.Y) > 3)
            {
                last = p;
                lastMove = sw.ElapsedMilliseconds;
                resolved = false;
            }

            // A click means "open it": hide and stay quiet until the mouse moves again.
            if ((Native.GetAsyncKeyState(0x01) & 0x8000) != 0)
            {
                if (OverPreview(p)) continue;
                Clear();
                resolved = true;
                continue;
            }

            if (resolved) continue;

            long need = _current == null ? Settings.Current.DelayMs : 120;
            if (sw.ElapsedMilliseconds - lastMove < need) continue;
            resolved = true;

            string? path = null;
            try { path = Resolve(p); }
            catch (Exception ex) { Log.Write(ex); }

            if (path == Keep) continue;

            if (path == null)
            {
                Clear();
            }
            else if (!string.Equals(path, _current, StringComparison.OrdinalIgnoreCase))
            {
                _current = path;
                Hovered?.Invoke(path, p);
            }
        }
    }

    private void Clear()
    {
        if (_current == null) return;
        _current = null;
        Left?.Invoke();
    }

    private bool OverPreview(Point p)
    {
        if (PreviewHwnd == IntPtr.Zero) return false;
        IntPtr h = Native.WindowFromPoint(p);
        return h != IntPtr.Zero && Native.GetAncestor(h, 2) == PreviewHwnd;
    }

    // ---------------------------------------------------------------- resolution

    private string? Resolve(Point p)
    {
        IntPtr hwnd = Native.WindowFromPoint(p);
        if (hwnd == IntPtr.Zero) return null;
        IntPtr root = Native.GetAncestor(hwnd, 2 /* GA_ROOT */);
        if (root == IntPtr.Zero) return null;

        if (PreviewHwnd != IntPtr.Zero && root == PreviewHwnd) return Keep;

        string cls = Native.ClassName(root);
        bool explorer = cls == "CabinetWClass";
        bool desktop = cls == "Progman" || cls == "WorkerW";
        if (!explorer && !desktop) return null;

        string? name = ItemNameAt(p);
        if (string.IsNullOrWhiteSpace(name)) return null;

        foreach (var folder in explorer ? ExplorerFolders(root) : DesktopFolders())
        {
            var file = FindInFolder(folder, name);
            if (file != null && MediaTypes.IsPreviewable(file)) return file;
        }
        return null;
    }

    private static string? ItemNameAt(Point p)
    {
        AutomationElement? el;
        try { el = AutomationElement.FromPoint(new System.Windows.Point(p.X, p.Y)); }
        catch { return null; }

        for (int i = 0; i < 5 && el != null; i++)
        {
            var ct = el.Current.ControlType;
            if (ct == ControlType.ListItem || ct == ControlType.DataItem)
                return el.Current.Name;
            try { el = TreeWalker.ControlViewWalker.GetParent(el); }
            catch { return null; }
        }
        return null;
    }

    private IEnumerable<string> ExplorerFolders(IntPtr root)
    {
        var result = new List<string>();
        try
        {
            _shellObj ??= Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            dynamic shell = _shellObj!;
            int target = root.ToInt32();
            foreach (dynamic w in shell.Windows())
            {
                try
                {
                    if ((int)w.HWND != target) continue;
                    string path = w.Document.Folder.Self.Path;
                    if (Directory.Exists(path)) result.Add(path);
                }
                catch { /* some shell windows are not folders */ }
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex);
            _shellObj = null;
        }
        return result;
    }

    private static IEnumerable<string> DesktopFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }

    /// <summary>Explorer may hide extensions, so match with or without one.</summary>
    private static string? FindInFolder(string folder, string name)
    {
        try
        {
            string exact = Path.Combine(folder, name);
            if (File.Exists(exact)) return exact;

            if (name.IndexOfAny(new[] { '*', '?', '<', '>', '|', '"' }) >= 0) return null;
            foreach (var f in Directory.EnumerateFiles(folder, name + ".*"))
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(f), name, StringComparison.OrdinalIgnoreCase))
                    return f;
            }
        }
        catch { /* access denied, network folder gone, etc. */ }
        return null;
    }

    public void Dispose() => _run = false;
}
