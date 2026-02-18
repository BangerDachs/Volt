using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using System.Management;

namespace Volt
{
    class LibreHW
    {
        private readonly Computer _computer;
        private IHardware? _gpu;
        private ISensor? _voltageSensor;
        private ISensor? _currentSensor;
        private ISensor? _powerSensor;
        private ISensor? _memClockSensor;
        private ISensor? _coreClockSensor;
        private ISensor? _coreTempSensor;
        private ISensor? _hotspotSensor;
        private ISensor? _memTempSensor;
        private ISensor? _loadSensor;
        private ISensor? _memUsageSensor;
        private bool _sensorsInitialized;
        public string GPU_voltage;
        public string GPU_current;
        public string GPU_power;
        public string GPU_memclock;
        public string GPU_coreTemp;
        public string GPU_hotspot;
        public string GPU_memTemp;
        public string GPU_load;
        public string GPU_freq;
        public string GPU_mem_usage;
        

        public LibreHW()
        {
            _computer = new Computer
            {
                IsGpuEnabled = true
            };
            _computer.Open();

        }
        // Voltage = 0,     Current = 1,        Power = 2,          Clock = 3,      Temperature = 4,    Load = 5,
        // Frequency = 6,   Fan = 7,            Flow = 8,           Control = 9,    Level = 10,         Factor = 11,
        // Data = 12,       SmallData = 13,     Throughput = 14,    TimeSpan = 15,  Timing = 16,
        // Energy = 17,     Noise = 18,         Conductivity = 19,  Humidity = 20
        public Task Read_GPU_InformationAsync()
        {
            EnsureSensorsInitialized();

            if (_gpu == null)
            {
                return Task.CompletedTask;
            }

            _gpu.Update();

            GPU_voltage = FormatSensor(_voltageSensor, "{0:F2} V") ?? GPU_voltage;
            GPU_current = FormatSensor(_currentSensor, "{0:F2}") ?? GPU_current;
            GPU_power = FormatSensor(_powerSensor, "{0:F2} W") ?? GPU_power;
            GPU_memclock = FormatSensor(_memClockSensor, "{0:F2} Mhz") ?? GPU_memclock;
            GPU_freq = FormatSensor(_coreClockSensor, "{0:F2} Mhz") ?? GPU_freq;
            GPU_coreTemp = FormatSensor(_coreTempSensor, "{0:F0} °C") ?? GPU_coreTemp;
            GPU_hotspot = FormatSensor(_hotspotSensor, "{0:F0} °C") ?? GPU_hotspot;
            GPU_memTemp = FormatSensor(_memTempSensor, "{0:F0} °C") ?? GPU_memTemp;
            GPU_load = FormatSensor(_loadSensor, "{0:F0} %") ?? GPU_load;
            GPU_mem_usage = FormatSensor(_memUsageSensor, "{0:F0} MB") ?? GPU_mem_usage;

            return Task.CompletedTask;
        }

        public string? GetAmdDriverVersion()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT DriverVersion, AdapterCompatibility FROM Win32_VideoController");

                foreach (ManagementObject result in searcher.Get())
                {
                    var compatibility = result["AdapterCompatibility"]?.ToString();
                    if (compatibility is null)
                    {
                        continue;
                    }

                    if (compatibility.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase)
                        || compatibility.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Driver Version AMD: " + result["DriverVersion"]?.ToString();
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private void EnsureSensorsInitialized()
        {
            if (_sensorsInitialized)
            {
                return;
            }

            var gpus = _computer.Hardware
                .Where(hardware => hardware.HardwareType is HardwareType.GpuNvidia
                    or HardwareType.GpuAmd
                    or HardwareType.GpuIntel)
                .ToList();


            // Priorität auf PCIe-GPUs, dann nach Hersteller
            _gpu = gpus.FirstOrDefault(hardware => IsPciGpu(hardware) && hardware.HardwareType == HardwareType.GpuNvidia)
                ?? gpus.FirstOrDefault(hardware => IsPciGpu(hardware) && hardware.HardwareType == HardwareType.GpuAmd)
                ?? gpus.FirstOrDefault(hardware => IsPciGpu(hardware) && hardware.HardwareType == HardwareType.GpuIntel)
                ?? gpus.FirstOrDefault(hardware => hardware.HardwareType == HardwareType.GpuNvidia)
                ?? gpus.FirstOrDefault(hardware => hardware.HardwareType == HardwareType.GpuAmd)
                ?? gpus.FirstOrDefault(hardware => hardware.HardwareType == HardwareType.GpuIntel);

            if (_gpu == null)
            {
                _sensorsInitialized = true;
                return;
            }

            _gpu.Update();

            foreach (var sensor in _gpu.Sensors)
            {
                switch (sensor.SensorType)
                {
                    case SensorType.Voltage:
                        _voltageSensor ??= sensor;
                        break;
                    case SensorType.Current:
                        _currentSensor ??= sensor;
                        break;
                    case SensorType.Power:
                        _powerSensor ??= sensor;
                        break;
                    case SensorType.Clock:
                    case SensorType.Frequency:
                        if ((sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase)
                            || sensor.Name.Contains("VRAM", StringComparison.OrdinalIgnoreCase))
                            && _memClockSensor == null)
                        {
                            _memClockSensor = sensor;
                        }
                        else if (_coreClockSensor == null)
                        {
                            _coreClockSensor = sensor;
                        }
                        break;
                    case SensorType.Temperature:
                        if (sensor.Name.Contains("hot spot", StringComparison.OrdinalIgnoreCase))
                        {
                            _hotspotSensor ??= sensor;
                        }
                        else if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                            || sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                        {
                            _coreTempSensor ??= sensor;
                        }

                        if (sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase)
                            || sensor.Name.Contains("VRAM", StringComparison.OrdinalIgnoreCase))
                        {
                            _memTempSensor ??= sensor;
                        }
                        break;
                    case SensorType.Load:
                        _loadSensor ??= sensor;
                        break;
                    case SensorType.SmallData:
                        if (sensor.Name.Equals("D3D Dedicated Memory Used", StringComparison.OrdinalIgnoreCase))
                        {
                            _memUsageSensor ??= sensor;
                        }
                        break;
                }
            }

            _sensorsInitialized = true;
        }

        private static bool IsPciGpu(IHardware hardware)
        {
            return hardware.Parent is not null
                && hardware.Parent.HardwareType != HardwareType.Cpu;
        }

        private static string? FormatSensor(ISensor? sensor, string format)
        {
            if (sensor?.Value is null)
            {
                return null;
            }

            return string.Format(format, sensor.Value.Value);
        }
    }
}
