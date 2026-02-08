using System.IO;

namespace Volt.Utils;

public static class IniStore
{
    private static readonly string IniPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Volt", "settings.ini");

    public static string? Read(string key)
    {
        if (!File.Exists(IniPath)) return null;
        foreach (var line in File.ReadAllLines(IniPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            var parts = line.Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return parts[1].Trim();
        }
        return null;
    }

    public static void Write(string key, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(IniPath)!);
        var lines = File.Exists(IniPath) ? File.ReadAllLines(IniPath).ToList() : new List<string>();
        bool replaced = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}={value}";
                replaced = true;
                break;
            }
        }
        if (!replaced) lines.Add($"{key}={value}");
        File.WriteAllLines(IniPath, lines);
    }
}