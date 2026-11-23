using System;
using System.IO;

namespace HoverPeDES;

internal static class Log
{
    private static readonly object Gate = new();
    public static string FilePath => Path.Combine(Settings.Dir, "log.txt");

    public static void Write(Exception? ex) => Write(ex?.ToString() ?? "unknown error");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Settings.Dir);
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch { /* never throw from logging */ }
    }
}
