using System.IO;
using System.Text.Json;

namespace Volt.Utils;

public static class SettingsStore
{
    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public class Settings
    {
        public int FanSpeed { get; set; } = 40;
        public bool AutoFan { get; set; } = false;
    }

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<Settings>(json, Options) ?? new Settings();
            }
        }
        catch
        {
            /* ignorieren und Defaults verwenden */
        }

        return new Settings();
    }

    public static void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(SettingsPath, json);
    }
}