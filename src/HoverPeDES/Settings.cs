using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace HoverPeDES;

internal sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public bool Mute { get; set; } = true;
    public bool PreviewImages { get; set; } = true;
    public bool ExtensionEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string Language { get; set; } = "auto";   // auto | en | vi
    public int DelayMs { get; set; } = 350;
    public int Width { get; set; } = 480;
    public int StartPercent { get; set; } = 20;
    public int Volume { get; set; } = 60;

    public static Settings Current { get; private set; } = new Settings();
    public static bool FirstRun { get; private set; }

    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HoverPeDES");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static void Load()
    {
        try
        {
            FirstRun = !File.Exists(FilePath);
            if (!FirstRun)
                Current = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception ex) { Log.Write(ex); Current = new Settings(); }
        Current.Clamp();
    }

    public void Clamp()
    {
        DelayMs = Math.Clamp(DelayMs, 100, 3000);
        Width = Math.Clamp(Width, 240, 1280);
        StartPercent = Math.Clamp(StartPercent, 0, 90);
        Volume = Math.Clamp(Volume, 0, 100);
    }

    public void Save()
    {
        try
        {
            Clamp();
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            ApplyAutostart();
        }
        catch (Exception ex) { Log.Write(ex); }
    }

    private void ApplyAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (StartWithWindows)
                key.SetValue("HoverPeDES", "\"" + Environment.ProcessPath + "\"");
            else
                key.DeleteValue("HoverPeDES", false);
        }
        catch (Exception ex) { Log.Write(ex); }
    }
}
