using System;
using System.Runtime.InteropServices;

namespace HoverPeDES;

/// <summary>Minimal libmpv wrapper (libmpv-2.dll, client API).</summary>
internal sealed class Mpv : IDisposable
{
    private const string Dll = "libmpv-2.dll";
    private const CallingConvention Cc = CallingConvention.Cdecl;

    [DllImport(Dll, CallingConvention = Cc)] private static extern ulong mpv_client_api_version();
    [DllImport(Dll, CallingConvention = Cc)] private static extern IntPtr mpv_create();
    [DllImport(Dll, CallingConvention = Cc)] private static extern int mpv_initialize(IntPtr ctx);
    [DllImport(Dll, CallingConvention = Cc)] private static extern void mpv_terminate_destroy(IntPtr ctx);
    [DllImport(Dll, CallingConvention = Cc)] private static extern int mpv_command(IntPtr ctx, IntPtr args);
    [DllImport(Dll, CallingConvention = Cc)] private static extern void mpv_free(IntPtr data);

    [DllImport(Dll, CallingConvention = Cc)]
    private static extern int mpv_set_option_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(Dll, CallingConvention = Cc)]
    private static extern int mpv_set_property_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(Dll, CallingConvention = Cc)]
    private static extern IntPtr mpv_get_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    private IntPtr _ctx;

    /// <summary>True when libmpv-2.dll can be loaded.</summary>
    public static bool IsAvailable()
    {
        try { return mpv_client_api_version() != 0; }
        catch (DllNotFoundException) { return false; }
        catch (BadImageFormatException) { return false; }
    }

    public Mpv(IntPtr windowHandle, string? ytdlPath)
    {
        _ctx = mpv_create();
        if (_ctx == IntPtr.Zero) throw new InvalidOperationException("mpv_create failed");

        var s = Settings.Current;
        Opt("wid", windowHandle.ToInt64().ToString());
        Opt("vo", "gpu");
        Opt("gpu-context", "d3d11");
        Opt("hwdec", "auto-safe");
        Opt("osc", "no");
        Opt("osd-level", "0");
        Opt("input-default-bindings", "no");
        Opt("input-vo-keyboard", "no");
        Opt("input-cursor", "no");
        Opt("cursor-autohide", "no");
        Opt("terminal", "no");
        Opt("msg-level", "all=no");
        Opt("idle", "yes");
        Opt("force-window", "yes");
        Opt("keep-open", "no");
        Opt("loop-file", "inf");
        Opt("audio-display", "embedded-first");
        Opt("image-display-duration", "inf");
        Opt("hr-seek", "no");               // fast keyframe seeking
        Opt("cache", "yes");
        Opt("demuxer-max-bytes", "48MiB");
        Opt("demuxer-readahead-secs", "6");
        Opt("network-timeout", "12");
        Opt("save-position-on-quit", "no");
        Opt("resume-playback", "no");
        Opt("mute", s.Mute ? "yes" : "no");
        Opt("volume", s.Volume.ToString());
        Opt("ytdl", "yes");
        Opt("ytdl-format", "bv*[height<=720]+ba/b[height<=720]/b");
        if (!string.IsNullOrEmpty(ytdlPath))
            Opt("script-opts", "ytdl_hook-ytdl_path=" + ytdlPath);

        int rc = mpv_initialize(_ctx);
        if (rc < 0) throw new InvalidOperationException("mpv_initialize failed: " + rc);
    }

    private void Opt(string name, string value)
    {
        int rc = mpv_set_option_string(_ctx, name, value);
        if (rc < 0) Log.Write($"mpv option {name}={value} rejected ({rc})");
    }

    public void Prop(string name, string value) => mpv_set_property_string(_ctx, name, value);

    public string? Get(string name)
    {
        IntPtr p = mpv_get_property_string(_ctx, name);
        if (p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(p); }
        finally { mpv_free(p); }
    }

    public double GetDouble(string name) =>
        double.TryParse(Get(name), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;

    public int Command(params string[] args)
    {
        IntPtr arr = Marshal.AllocHGlobal(IntPtr.Size * (args.Length + 1));
        var strings = new IntPtr[args.Length];
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                strings[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
                Marshal.WriteIntPtr(arr, i * IntPtr.Size, strings[i]);
            }
            Marshal.WriteIntPtr(arr, args.Length * IntPtr.Size, IntPtr.Zero);
            return mpv_command(_ctx, arr);
        }
        finally
        {
            foreach (var p in strings) if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p);
            Marshal.FreeHGlobal(arr);
        }
    }

    public void Load(string source) => Command("loadfile", source, "replace");
    public void Stop() => Command("stop");
    public void TogglePause() => Command("cycle", "pause");
    public void ToggleMute() => Command("cycle", "mute");
    public void SeekRelative(double seconds) =>
        Command("seek", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "relative", "keyframes");

    public void Dispose()
    {
        if (_ctx != IntPtr.Zero)
        {
            mpv_terminate_destroy(_ctx);
            _ctx = IntPtr.Zero;
        }
    }
}
