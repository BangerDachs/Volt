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

    public sealed class FanCurvePoint
    {
        public double Temperature { get; set; }
        public double Speed { get; set; }
    }

    public class Settings
    {
        public int FanSpeed { get; set; } = 40;
        public bool AutoFan { get; set; } = false;

        public List<FanCurvePoint> FanCurve { get; set; } =
        [
            new FanCurvePoint { Temperature = 0, Speed = 20 },
            new FanCurvePoint { Temperature = 25, Speed = 35 },
            new FanCurvePoint { Temperature = 50, Speed = 60 },
            new FanCurvePoint { Temperature = 75, Speed = 80 },
            new FanCurvePoint { Temperature = 100, Speed = 100 }
        ];
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