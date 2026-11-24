using System;
using System.Collections.Generic;
using System.IO;

namespace HoverPeDES;

/// <summary>Which file extensions HoverPeDES previews, and how.</summary>
internal static class MediaTypes
{
    private static HashSet<string> Set(params string[] items) => new(items, StringComparer.OrdinalIgnoreCase);

    // Everything below is decoded by mpv / FFmpeg.
    private static readonly HashSet<string> Video = Set(
        ".mp4", ".m4v", ".mkv", ".avi", ".mov", ".qt", ".wmv", ".asf", ".flv", ".f4v", ".webm", ".ogv", ".ogm",
        ".ts", ".m2ts", ".mts", ".m2t", ".tp", ".trp", ".mpg", ".mpeg", ".mpe", ".mpv", ".m1v", ".m2v", ".vob", ".evo",
        ".3gp", ".3g2", ".rm", ".rmvb", ".divx", ".xvid", ".mxf", ".gxf", ".dv", ".y4m", ".amv", ".nsv", ".nut",
        ".ivf", ".bik", ".roq", ".usm", ".wtv", ".dvr-ms", ".mod", ".tod", ".rec", ".ismv", ".mjpeg", ".mjpg",
        ".h264", ".264", ".h265", ".265", ".hevc", ".avs", ".swf", ".m3u8", ".mpd");

    private static readonly HashSet<string> Audio = Set(
        ".mp3", ".m4a", ".m4b", ".aac", ".flac", ".wav", ".wave", ".ogg", ".oga", ".opus", ".spx", ".wma", ".aiff",
        ".aif", ".aifc", ".ape", ".alac", ".mka", ".mpc", ".wv", ".tta", ".ac3", ".eac3", ".dts", ".amr", ".caf",
        ".weba", ".dsf", ".dff", ".mp2", ".mp1", ".au", ".snd", ".ra");

    private static readonly HashSet<string> Image = Set(
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".apng", ".gif", ".webp", ".bmp", ".dib", ".avif", ".jxl",
        ".tif", ".tiff", ".heic", ".heif", ".ico", ".tga", ".pcx", ".pgm", ".ppm", ".pnm", ".pbm", ".exr", ".hdr",
        ".dds", ".jp2", ".j2k", ".qoi");

    // Photoshop: read directly (merged image or embedded thumbnail) so multi-GB files work.
    private static readonly HashSet<string> Psd = Set(".psd", ".psb");

    // Shown as plain text (first ~48 KB / 80 lines).
    private static readonly HashSet<string> Text = Set(
        ".txt", ".md", ".markdown", ".log", ".csv", ".tsv", ".json", ".jsonl", ".xml", ".yml", ".yaml", ".ini",
        ".cfg", ".conf", ".toml", ".env", ".html", ".htm", ".css", ".scss", ".js", ".mjs", ".jsx", ".tsx",
        ".py", ".java", ".kt", ".c", ".h", ".cpp", ".hpp", ".cc", ".cs", ".go", ".rs", ".php", ".rb", ".swift",
        ".lua", ".pl", ".sh", ".bash", ".bat", ".cmd", ".ps1", ".sql", ".srt", ".vtt", ".ass", ".ssa", ".lrc",
        ".nfo", ".tex", ".gitignore", ".gradle", ".vue", ".dart", ".r");

    // Opened with the built-in Edge WebView2 viewer.
    private static readonly HashSet<string> WebDoc = Set(".pdf", ".svg");

    // Office Open XML + OpenDocument: text is extracted directly from the zip.
    private static readonly HashSet<string> Office = Set(
        ".docx", ".docm", ".xlsx", ".xlsm", ".pptx", ".pptm", ".odt", ".ods", ".odp");

    // Camera RAW: the embedded full-size JPEG preview is shown.
    private static readonly HashSet<string> Raw = Set(
        ".cr2", ".cr3", ".crw", ".nef", ".nrw", ".arw", ".srf", ".sr2", ".dng", ".orf", ".rw2", ".raf", ".pef",
        ".srw", ".x3f", ".3fr", ".erf", ".kdc", ".dcr", ".mef", ".mos", ".raw", ".rwl", ".iiq", ".gpr");

    private static string _extraRaw = "";
    private static HashSet<string> _extra = Set();

    /// <summary>Extension of a local path or of the path part of an http(s) URL.</summary>
    private static string ExtOf(string s)
    {
        try
        {
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return Uri.TryCreate(s, UriKind.Absolute, out var u) ? Path.GetExtension(u.AbsolutePath) : "";
            return Path.GetExtension(s);
        }
        catch { return ""; }
    }

    private static bool IsExtra(string ext)
    {
        string raw = Settings.Current.ExtraExtensions ?? "";
        if (!string.Equals(raw, _extraRaw, StringComparison.Ordinal))
        {
            var set = Set();
            foreach (var part in raw.Split(new[] { ',', ';', ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var e = part.Trim();
                if (e.Length == 0) continue;
                set.Add(e.StartsWith(".") ? e : "." + e);
            }
            _extra = set;
            _extraRaw = raw;
        }
        return _extra.Contains(ext);
    }

    public static bool IsVideo(string path) => Video.Contains(ExtOf(path));
    public static bool IsAudio(string path) => Audio.Contains(ExtOf(path));
    public static bool IsImage(string path) => Image.Contains(ExtOf(path));
    public static bool IsText(string path) => Text.Contains(ExtOf(path));
    public static bool IsWebDoc(string path) => WebDoc.Contains(ExtOf(path));
    public static bool IsPdf(string path) => string.Equals(ExtOf(path), ".pdf", StringComparison.OrdinalIgnoreCase);
    public static bool IsOffice(string path) => Office.Contains(ExtOf(path));
    public static bool IsRaw(string path) => Raw.Contains(ExtOf(path));
    public static bool IsPsd(string path) => Psd.Contains(ExtOf(path));

    /// <summary>True when HoverPeDES should preview this local file.</summary>
    public static bool IsPreviewable(string path)
    {
        var s = Settings.Current;
        string ext = ExtOf(path);
        return Video.Contains(ext)
            || IsExtra(ext)
            || (s.PreviewAudio && Audio.Contains(ext))
            || (s.PreviewImages && (Image.Contains(ext) || Psd.Contains(ext)))
            || (s.PreviewText && Text.Contains(ext))
            || (s.PreviewDocuments && (WebDoc.Contains(ext) || Office.Contains(ext)))
            || (s.PreviewRaw && Raw.Contains(ext));
    }
}
