using LibreHardwareMonitor;
using LibreHardwareMonitor.Hardware;

namespace Volt
{
    class LibreHW
    {
        private readonly Computer _computer;
        public string GPU_power;
        public string GPU_load;
        public string GPU_mem_usage;



        public LibreHW()
        {
            _computer = new Computer
            {
                IsGpuEnabled = true
            };
            _computer.Open();

        }

        public async Task Read_GPU_InformationAsync()
        {
            await Task.Run(() =>
            {
                foreach (var gpu in _computer.Hardware)
                {
                    if (gpu.HardwareType == HardwareType.GpuNvidia)
                    {
                        gpu.Update();

                        foreach (var sensor in gpu.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Power)
                            {
                                GPU_power = $"{sensor.Value:F2} W";
                            }
                            else if (sensor.SensorType == SensorType.SmallData)
                            {
                                GPU_mem_usage = $"{sensor.Value:F0} MB";
                            }
                        }
                    }
                }
            });
        }



        public void Get_Read_GPU_Information()
        {
            foreach (var gpu in _computer.Hardware)
            {
                if (gpu.HardwareType == HardwareType.GpuNvidia)
                {
                    gpu.Update();

                    foreach (var sensor in gpu.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Power)
                        { GPU_power = ($"{sensor.Value:F2} W");
                            break;
                        }
                        
                        if (sensor.SensorType == SensorType.SmallData)
                        { GPU_mem_usage = ($"{sensor.Value:F0} MB");
                            break;
                        }
                    }
                }
            }
        }



    }
}
