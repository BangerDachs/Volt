using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ScottPlot;
using Volt.Utils;

namespace Volt
{
    internal sealed class FanCurveService
    {
        public bool ShowFanCurveDialog(Window owner, SettingsStore.Settings settings)
        {
            var initialPoints = ToCoordinates(settings.FanCurve);
            var dialog = new FanCurve(initialPoints)
            {
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() != true)
                return false;

            settings.FanCurve = ToFanCurvePoints(dialog.Points);
            return true;
        }

        public void ApplyIfEnabled(SettingsStore.Settings settings, NVOC nvoc, bool useFactoryCurve, Action<string>? updateFanSpeedText)
        {
            if (!settings.AutoFan || settings.FanCurve.Count == 0)
                return;

            if (useFactoryCurve)
            {
                nvoc.RestoreDefaultFanCurve();
                updateFanSpeedText?.Invoke(nvoc.get_FanSpeed());
                return;
            }

            if (!double.TryParse(nvoc.get_GPUCoreTemperature(), out var temp))
                return;

            int targetSpeed = GpuMonitor.GetFanSpeedForTemperature(temp, settings.FanCurve);
            nvoc.set_FanSpeed(targetSpeed);
            updateFanSpeedText?.Invoke(targetSpeed.ToString());
        }

        public void ResetToDefault(SettingsStore.Settings settings, NVOC nvoc)
        {
            settings.FanCurve = CreateDefaultFanCurve();
            nvoc.RestoreDefaultFanCurve();
        }

        private static List<Coordinates> ToCoordinates(List<SettingsStore.FanCurvePoint>? points)
        {
            if (points == null || points.Count == 0)
                return new();

            return points.Select(p => new Coordinates(p.Temperature, p.Speed)).ToList();
        }

        private static List<SettingsStore.FanCurvePoint> ToFanCurvePoints(IReadOnlyList<Coordinates> points)
        {
            return points.Select(p => new SettingsStore.FanCurvePoint
            {
                Temperature = p.X,
                Speed = p.Y
            }).ToList();
        }

        private static List<SettingsStore.FanCurvePoint> CreateDefaultFanCurve()
        {
            return new List<SettingsStore.FanCurvePoint>
            {
                new() { Temperature = 0, Speed = 20 },
                new() { Temperature = 25, Speed = 35 },
                new() { Temperature = 50, Speed = 60 },
                new() { Temperature = 75, Speed = 80 },
                new() { Temperature = 100, Speed = 100 }
            };
        }
    }
}
