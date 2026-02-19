using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volt.Utils;

namespace Volt
{
    internal sealed record GpuSnapshot(
        string GpuTempText,
        double? GpuTempValue,
        string HotspotText,
        double? HotspotValue,
        string MemTempText,
        double? MemTempValue,
        string VoltageText,
        double VoltageValue,
        string LoadText,
        double LoadValue,
        string CoreClockText,
        double CoreClockValue,
        string MemClockText,
        double MemClockValue,
        string PowerText,
        double PowerValue,
        string MemoryUsageText,
        double? MemoryUsageValue,
        string? FanSpeedText,
        int? FanSpeedValue);

    internal sealed class GpuMonitor
    {
        private readonly LibreHW _hwinfo;
        private readonly NVOC _nvoc;

        public GpuMonitor(LibreHW hwinfo, NVOC nvoc)
        {
            _hwinfo = hwinfo;
            _nvoc = nvoc;
        }

        public async Task<GpuSnapshot> CollectSnapshotAsync(SettingsStore.Settings settings, bool useFactoryCurve, CancellationToken token)
        {
            await _hwinfo.Read_GPU_InformationAsync().ConfigureAwait(false);

            string gpuTempText = _hwinfo.GPU_coreTemp ?? "N/A";
            double? gpuTempValue = TryParseDoubleWithSuffix(gpuTempText, " °C");

            string hotspotText = _hwinfo.GPU_hotspot ?? "N/A";
            double? hotspotValue = TryParseDoubleWithSuffix(hotspotText, " °C");

            string memTempText = _hwinfo.GPU_memTemp ?? "N/A";
            double? memTempValue = TryParseDoubleWithSuffix(memTempText, " °C");

            string gpuVoltCurrStr = _hwinfo.GPU_voltage ?? "N/A";
            double gpuVoltCurr = TryParseDoubleWithSuffix(gpuVoltCurrStr, " V") ?? 0;

            string gpuUsageStr = _hwinfo.GPU_load ?? _nvoc.get_GPU_usage();
            double gpuUsage = TryParseDoubleWithSuffix(gpuUsageStr, " %") ?? 0;

            string coreClockText = _hwinfo.GPU_freq ?? "N/A";
            double coreClockValue = TryParseDoubleWithSuffix(coreClockText, " Mhz") ?? 0;

            string memClockText = _hwinfo.GPU_memclock ?? "N/A";
            double memClockValue = TryParseDoubleWithSuffix(memClockText, " Mhz") ?? 0;

            var powerText = _hwinfo.GPU_power ?? "N/A";
            double rowPower = TryParseDoubleWithSuffix(powerText, " W") ?? 0;

            string memoryUsageText = _hwinfo.GPU_mem_usage ?? "N/A";
            double? memoryUsageValue = TryParseMemoryUsage(memoryUsageText);

            string? fanSpeedText = null;
            int? fanSpeedValue = null;

            if (settings.AutoFan && _nvoc.IsNvidiaAvailable)
            {
                if (useFactoryCurve)
                {
                    _nvoc.RestoreDefaultFanCurve();
                }
                else if (gpuTempValue.HasValue)
                {
                    int targetSpeed = GetFanSpeedForTemperature(gpuTempValue.Value, settings.FanCurve);
                    _nvoc.set_FanSpeed(targetSpeed);
                }

                fanSpeedText = _nvoc.get_FanSpeed();
                fanSpeedValue = TryParseInt(fanSpeedText);
            }

            return new GpuSnapshot(
                gpuTempText,
                gpuTempValue,
                hotspotText,
                hotspotValue,
                memTempText,
                memTempValue,
                gpuVoltCurrStr,
                gpuVoltCurr,
                gpuUsageStr,
                gpuUsage,
                coreClockText,
                coreClockValue,
                memClockText,
                memClockValue,
                powerText,
                rowPower,
                memoryUsageText,
                memoryUsageValue,
                fanSpeedText,
                fanSpeedValue);
        }

        public static int GetFanSpeedForTemperature(double temperature, List<SettingsStore.FanCurvePoint> curve)
        {
            if (curve.Count == 0) return 0;

            var ordered = curve.OrderBy(p => p.Temperature).ToList();

            if (temperature <= ordered[0].Temperature)
                return ClampSpeed(ordered[0].Speed);

            if (temperature >= ordered[^1].Temperature)
                return ClampSpeed(ordered[^1].Speed);

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var a = ordered[i];
                var b = ordered[i + 1];

                if (temperature >= a.Temperature && temperature <= b.Temperature)
                {
                    double t = (temperature - a.Temperature) / (b.Temperature - a.Temperature);
                    double speed = a.Speed + (b.Speed - a.Speed) * t;
                    return ClampSpeed(speed);
                }
            }

            return ClampSpeed(ordered[^1].Speed);
        }

        private static int ClampSpeed(double speed)
        {
            return (int)Math.Clamp(Math.Round(speed), 0, 100);
        }

        private static double? TryParseDoubleWithSuffix(string? value, string suffix)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value;
            if (!string.IsNullOrEmpty(suffix) && trimmed.EndsWith(suffix, StringComparison.Ordinal))
            {
                trimmed = trimmed[..^suffix.Length];
            }

            return double.TryParse(trimmed, out var result) ? result : null;
        }

        private static double? TryParseMemoryUsage(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            var mbValue = TryParseDoubleWithSuffix(trimmed, " MB");
            if (mbValue.HasValue)
            {
                return mbValue;
            }

            var gbValue = TryParseDoubleWithSuffix(trimmed, " GB");
            return gbValue.HasValue ? gbValue.Value * 1024 : null;
        }

        private static int? TryParseInt(string? value)
        {
            return int.TryParse(value, out var result) ? result : null;
        }
    }
}
