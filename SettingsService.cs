using Volt.Utils;

namespace Volt
{
    internal sealed class SettingsService
    {
        public SettingsStore.Settings Load() => SettingsStore.Load();

        public void Save(SettingsStore.Settings settings) => SettingsStore.Save(settings);

        public void UpdateFromUi(SettingsStore.Settings settings, bool? autoFanChecked, string fanSpeedText)
        {
            settings.AutoFan = autoFanChecked == true;
            if (int.TryParse(fanSpeedText, out var fs))
            {
                settings.FanSpeed = fs;
            }
        }

        public string ResolveFanSpeedText(SettingsStore.Settings settings, Func<string> fallback)
        {
            return settings.FanSpeed > 0
                ? settings.FanSpeed.ToString()
                : fallback();
        }
    }
}
