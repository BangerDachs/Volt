// Nur NvAPIWrapper
using NvAPIWrapper;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native;               // für NvAPIStatus
using NvAPIWrapper.Native.GPU;           // für GPU und PhysicalGPU
using NvAPIWrapper.Native.GPU.Structures;
using NvAPIWrapper.Native.Exceptions;    // für NvAPIException
using System.Linq;
using NvAPIWrapper.Native.General;

namespace Volt
{
    public class NVOC
    {
        private PhysicalGPU? gpu = PhysicalGPU.GetPhysicalGPUs().FirstOrDefault();

        // *********************************************************************************************************************************
        // Treiber Version
        public string get_DriverVersion()
        { return DriverVersion(); }
        private string DriverVersion()
        { return "Driver Version: " + (NVIDIA.DriverVersion / 100.0).ToString("F2"); } // Gibt Nummer in format XXX.XX zurück

        // *********************************************************************************************************************************
        // Fancontrol
        public string get_FanSpeed()
        { return GetFanSpeed(); }
        private string GetFanSpeed()
        {
            if (gpu != null)
            {
                var fanSpeed = gpu?.CoolerInformation?.CurrentFanSpeedLevel;
                return fanSpeed?.ToString() ?? "N/A";
            }
            return "N/A";
        }
        // *********************************************************************************************************************************
        // Setze Lüftergeschwindigkeit
        public void set_FanSpeed(int newSpeed)
        { Set_FanSpeed(newSpeed); }
        private void Set_FanSpeed(int newSpeed)
        {
            if (gpu != null)
            {
                var coolers = gpu?.CoolerInformation?.Coolers;
                var fanSpeed = gpu?.CoolerInformation?.CurrentFanSpeedLevel;

                if (coolers == null || fanSpeed == null)
                { return; }

                if (fanSpeed != newSpeed)
                {
                    try
                    {
                        foreach (var cooler in coolers)
                        { gpu?.CoolerInformation?.SetCoolerSettings(cooler.CoolerId, newSpeed); } // Setze die Lüftergeschwindigkeit
                    }
                    catch (Exception ex)
                    { Console.WriteLine($"Fehler beim Setzen der Lüftergeschwindigkeit: {ex.Message}"); }

                }
            }
        }
        // *********************************************************************************************************************************
        //Fan Curve
        public void set_SetFan(int[] newCurve)
        { SetFan(newCurve); }

        private void SetFan(int[] fanCurve)
        {
            if (gpu != null)
            {
                var coolers = gpu?.CoolerInformation?.Coolers;
                var fanSpeed = gpu?.CoolerInformation?.CurrentFanSpeedLevel;

                if (coolers == null || fanSpeed == null)
                { return; }

                if (fanCurve.Length != 21)
                { return; }

                try
                {
                    for (int i = 0; i < 21; i++)
                    {
                        foreach (var cooler in coolers)
                        { gpu?.CoolerInformation?.SetCoolerSettings(cooler.CoolerId, fanCurve[i]); } // Setze die Lüftergeschwindigkeit
                    }
                }
                catch (Exception ex)
                { Console.WriteLine($"Fehler beim Setzen der Lüftergeschwindigkeit: {ex.Message}"); }
            }
        }
        // *********************************************************************************************************************************
        // Setze die Lüfterkurve der Aktuellen GPU
        public void set_FanCurveValues(PhysicalGPU gpu)
        { SetFanCurveValues(gpu); }
        private double[] SetFanCurveValues(PhysicalGPU gpu)
        {
            if (gpu == null)
                return new double[0];

            return [];
        }
        // *********************************************************************************************************************************
        // Temperatur
        public string get_GPUCoreTemperature()
        { return GPUCoreTemperature(); }
        private string GPUCoreTemperature() //Temperatur auslesen
        {
            if (gpu != null)
            {
                var temperature = gpu?.ThermalInformation?.ThermalSensors;
                if (temperature != null)
                {
                    foreach (var sensor in temperature)
                    {
                        return sensor.CurrentTemperature.ToString();
                    }
                }
                return "N/A1";
            }
            return "N/A2";
        }
        // *********************************************************************************************************************************
        // Clock Speed
        public string[] get_ClockSpeed()
        { return GetClockSpeed(); }
        private string[] GetClockSpeed()
        {
            if (gpu != null)
            {
                var gpu_clocks = gpu?.CurrentClockFrequencies.Clocks;
                if (gpu_clocks != null)
                {
                    string[] clk = new string[gpu_clocks.Count];
                    int i = 0;
                    foreach (var clock in gpu_clocks)
                    {
                        clk[i++] = (clock.Value.Frequency / 1_000).ToString();
                    }
                    return clk; // Rückgabe des Arrays
                }
                return new string[] { "N/A_1" }; // Rückgabe eines Arrays mit einer Standard-Nachricht
            }
            return new string[] { "N/A_2" }; // Rückgabe eines Arrays mit einer Standard-Nachricht
        }
        // *********************************************************************************************************************************
        // Ausgabe Aktuelle GPU Spannung
        public string get_Voltage()
        { return GetVoltage(gpu); }

        private string GetVoltage(PhysicalGPU gpu)
        {
            if (gpu == null)
            { return "0.0"; }

            var voltageStatus = GPUApi.GetCurrentVoltage(gpu.Handle);
            if (voltageStatus.ValueInMicroVolt != 0)
            {
                string voltage = (voltageStatus.ValueInMicroVolt / 1_000_000.0).ToString("F3") + " V";
                return voltage;
            }
            return "N/A";
        }
        // *********************************************************************************************************************************
        // Ausgabe Aktueller Verbrauch Watt
        public string get_PowerConsuption()
        { return PowerConsuption(gpu); }

        private string PowerConsuption(PhysicalGPU gpu)
        {
            if (gpu == null)
            {
                return "0.0";
            }
            var result3 = GPUApi.GetDynamicPerformanceStatesInfoEx(gpu.Handle);
            //result3.GPU.Percentage GPU Auslastung
            var result = gpu?.PowerTopologyInformation;
            // result alle informationen zur karte

            var result0 = GPUApi.GetAllClockFrequencies(gpu.Handle);
            // ALle Taktraten

            return "N/A";
        }
        // *********************************************************************************************************************************
        public string get_GPU_usage()
        { return GPU_usage(gpu); }
        private string GPU_usage(PhysicalGPU gpu)
        {
            if (gpu == null)
            { return "N/A"; }
            var result = GPUApi.GetDynamicPerformanceStatesInfoEx(gpu.Handle);
            uint usage = result.GPU.Percentage;

            return ($"{usage:F0} %");
        }

    }

}
