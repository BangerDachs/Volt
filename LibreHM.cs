using LibreHardwareMonitor;
using LibreHardwareMonitor.Hardware;
using NvAPIWrapper.Native.GPU;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Volt
{
    class LibreHW
    {
        private readonly Computer _computer;
        public string GPU_voltage;
        public string GPU_current;
        public string GPU_power;
        public string GPU_memclock;
        public string GPU_coreTemp;
        public string GPU_hotspot;
        public string GPU_memTemp;
        public string GPU_load;
        public string GPU_freq;
        public string GPU_fan;
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
        public async Task Read_GPU_InformationAsync()
        {
            await Task.Run(() =>
            {
                foreach (var gpu in _computer.Hardware)
                {
                    if (gpu.HardwareType != HardwareType.GpuNvidia
                        && gpu.HardwareType != HardwareType.GpuAmd
                        && gpu.HardwareType != HardwareType.GpuIntel)
                    {
                        continue;
                    }

                    gpu.Update();

                    foreach (var sensor in gpu.Sensors)
                    {
                        if (!sensor.Value.HasValue)
                        {
                            continue;
                        }

                        switch (sensor.SensorType)
                        {
                            case SensorType.Voltage: //0
                                GPU_voltage = $"{sensor.Value:F2} V";
                                break;
                            case SensorType.Current: //1
                                GPU_current = $"{sensor.Value:F2}";
                                break;
                            case SensorType.Power: //2
                                GPU_power = $"{sensor.Value:F2} W";
                                break;
                            case SensorType.Clock: //3
                                if (sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase)
                                    || sensor.Name.Contains("VRAM", StringComparison.OrdinalIgnoreCase))
                                {
                                    GPU_memclock = $"{sensor.Value:F2} Mhz";
                                }
                                else
                                {
                                    GPU_freq = $"{sensor.Value:F2} Mhz";
                                }
                                break;
                            case SensorType.Temperature: //4
                                if (sensor.Name.Contains("hot spot", StringComparison.OrdinalIgnoreCase))
                                {
                                    GPU_hotspot = $"{sensor.Value:F0} °C";
                                }
                                if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                                    || sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                                {
                                    GPU_coreTemp = $"{sensor.Value:F0} °C";
                                }
                                if (sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase)
                                    || sensor.Name.Contains("VRAM", StringComparison.OrdinalIgnoreCase))
                                {
                                    GPU_memTemp = $"{sensor.Value:F0} °C";
                                }
                                break;
                            case SensorType.Load: //5
                                GPU_load = $"{sensor.Value:F0} %";
                                break;
                            case SensorType.Frequency: //6
                                if (sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase)
                                    || sensor.Name.Contains("VRAM", StringComparison.OrdinalIgnoreCase))
                                {
                                    GPU_memclock = $"{sensor.Value:F2} Mhz";
                                }
                                else
                                {
                                    GPU_freq = $"{sensor.Value:F2} Mhz";
                                }
                                break;
                            case SensorType.SmallData: //13
                                GPU_mem_usage = $"{sensor.Value:F0} MB";
                                break;
                   



                        }
                    }
                }
            });
        }


    }
}
