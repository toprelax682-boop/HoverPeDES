using System;
using System.Collections.Generic;
using System.IO;

namespace HoverPeDES;

internal static class MediaTypes
{
    private static readonly HashSet<string> Video = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".f4v", ".webm", ".ts", ".m2ts", ".mts",
        ".mpg", ".mpeg", ".mpe", ".vob", ".3gp", ".3g2", ".rm", ".rmvb", ".ogv", ".ogm", ".asf", ".divx",
        ".xvid", ".mxf", ".dv", ".y4m", ".amv", ".qt", ".m3u8"
    };

    private static readonly HashSet<string> Image = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".avif", ".jxl", ".tif", ".tiff", ".heic", ".heif"
    };

    public static bool IsVideo(string path) => Video.Contains(Path.GetExtension(path));
    public static bool IsImage(string path) => Image.Contains(Path.GetExtension(path));

    public static bool IsPreviewable(string path) =>
        IsVideo(path) || (Settings.Current.PreviewImages && IsImage(path));
}
