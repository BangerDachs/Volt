using NvAPIWrapper.GPU;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ScottPlot;
using Volt.Utils; // hinzufügen


namespace Volt
{
    public partial class MainWindow : Window
    {
        // imports
        private readonly UDefinition _udef = new();
        private readonly NVOC _nvoc = new();
        private readonly AMD_GPU _AMD = new AMD_GPU(); // Muss noch ausgebaut werden
        private readonly LibreHW _hwinfo = new LibreHW();
        private readonly CancellationTokenSource _cts = new();
        private SettingsStore.Settings _settings = new(); // neu

        // data
        private readonly ObservableCollection<ClockRow> _clockRows = new(); // ObservableCollection für die Anzeige der GPU-Informationen in DataGrid
        private ClockRow _rowGpuTemp = null!;
        private ClockRow _rowGPuHotSpot = null!;
        private ClockRow _rowGPUMemTemp = null!;
        private ClockRow _rowCoreClock = null!;
        private ClockRow _rowMemClock = null!;
        private ClockRow _rowVoltage = null!;
        private ClockRow _rowPower = null!;
        private ClockRow _rowLoad = null!;
        private ClockRow _rowMemory = null!;

        // Array zum Speichern der Werte für Min, Max, AVG, Watt, Voltage werte
        private readonly double[] _minValues = new double[8];
        private readonly double[] _maxValues = new double[8];
        private readonly double[] _avgValues = new double[8];
        private readonly int[] _sampleCounts = new int[8];

        // variables
        //Graph_FanCurve
        private FanCurve? _fanCurveWindow;
        private bool _useFactoryCurve;

        // * * * * *


        public MainWindow()
        {
            InitializeComponent();

            // prüfen ob die Anwendung mit Administratorrechten gestartet wuurde
            if (!IsAdministrator())
            { panel_fanControl.IsEnabled = false; }

            // Diagramm An/Aus 
            //Grid_Background_FanCurve.Visibility = Visibility.Hidden;
            //if (Grid_Background_FanCurve.Visibility == Visibility.Visible)
            //{ show_GPU_FanCurve(); }

            Loaded += (s, e) => Initialize(); // Initialisierung nach dem Laden MainWindow

        }
        // *********************************************************************************************************************************
        private void Initialize()
        {
            _settings = SettingsStore.Load(); // laden

            Array.Fill(_minValues, double.MaxValue);
            Array.Fill(_maxValues, double.MinValue);
            Array.Clear(_avgValues);
            Array.Clear(_sampleCounts);

            lb_driverV.Content = _nvoc.get_DriverVersion();
            InitializeClockRows();

            if (!_nvoc.IsNvidiaAvailable)
            {
                panel_fanControl.IsEnabled = false;
                tb_fanSpeed.Text = "N/A";
                slider_fanSpeed.IsEnabled = false;
                slider_fanSpeed.Value = 0;
            }

            if (_nvoc.IsNvidiaAvailable)
            {
                if (_settings.AutoFan)
                {
                    cb_autoFanSpeed.IsChecked = true;
                    slider_fanSpeed.IsEnabled = false;
                    tb_fanSpeed.IsEnabled = false;
                    ApplyFanCurveIfEnabled();
                }
                else
                {
                    var fanSpeedText = _settings.FanSpeed > 0
                        ? _settings.FanSpeed.ToString()
                        : _nvoc.get_FanSpeed();

                    tb_fanSpeed.Text = fanSpeedText;
                    if (TryParseInt(fanSpeedText) is int fanSpeed)
                    {
                        slider_fanSpeed.Value = fanSpeed;
                    }
                }
            }

            _ = DoWorkAsync(this, _cts.Token);
        }

        private void InitializeClockRows()
        {
            _rowGpuTemp = new ClockRow("GPU Temperature", "--", "--", "--", "--");
            _rowGPuHotSpot = new ClockRow("Hotspot Temperature", "--", "--", "--", "--");
            _rowGPUMemTemp = new ClockRow("VRam Temperature", "--", "--", "--", "--");
            _rowCoreClock = new ClockRow("Core Clock", "--", "--", "--", "--");
            _rowMemClock = new ClockRow("Memory Clock", "--", "--", "--", "--");
            _rowVoltage = new ClockRow("Curr. Voltage", "--", "--", "--", "--");
            _rowPower = new ClockRow("Curr. Power", "--", "--", "--", "--");
            _rowLoad = new ClockRow("Curr. Load", "--", "--", "--", "--");
            _rowMemory = new ClockRow("Curr. Memory", "--", "--", "--", "--");

            _clockRows.Clear();
            _clockRows.Add(_rowGpuTemp);
            if (_rowGPuHotSpot.Value != "--") 
            {// Nur hinzufügen, wenn tatsächlich ein Wert für Hotspot-Temperatur vorhanden ist
                _clockRows.Add(_rowGPuHotSpot); 
            }
            if (_rowGPUMemTemp.Value != "--")
            {// Nur hinzufügen, wenn tatsächlich ein Wert für VRam-Temperatur vorhanden ist
                _clockRows.Add(_rowGPUMemTemp);
            }
            _clockRows.Add(_rowCoreClock);
            _clockRows.Add(_rowMemClock);
            _clockRows.Add(_rowVoltage);
            _clockRows.Add(_rowPower);
            _clockRows.Add(_rowLoad);
            _clockRows.Add(_rowMemory);

            dataGrid_clocks.ItemsSource = _clockRows;
        }
        // *********************************************************************************************************************************
        private async Task DoWorkAsync(MainWindow _mw, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var snapshot = await CollectSnapshotAsync(token).ConfigureAwait(false);

                    await Dispatcher.InvokeAsync(
                        () => ApplySnapshot(snapshot),
                        DispatcherPriority.Background,
                        token);

                    await Task.Delay(UDefinition.cUpdateInterval, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private sealed class ClockRow : INotifyPropertyChanged
        {
            private string _value;
            private string _avg;
            private string _min;
            private string _max;
            

            public string Name { get; }
            public string Value
            {
                get => _value;
                set
                {
                    if (_value == value)
                        return;

                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
            public string Avg
            {
                get => _avg;
                set
                {
                    if (_avg == value)
                        return;
                    _avg = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Avg)));
                }
            }
            public string Min
            {
                get => _min;
                set
                {
                    if (_min == value)
                        return;
                    _min = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Min)));
                }
            }
            public string Max
            {
                get => _max;
                set
                {
                    if (_max == value)
                        return;
                    _max = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Max)));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public ClockRow(string name, string value, string avg, string min, string max)
            {
                Name = name;
                _value = value;
                _avg = avg;
                _min = min;
                _max = max;
            }
        }

        private sealed record GpuSnapshot(
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
            string? FanSpeedText,
            int? FanSpeedValue);

        private async Task<GpuSnapshot> CollectSnapshotAsync(CancellationToken token)
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

            string? fanSpeedText = null;
            int? fanSpeedValue = null;

            if (_settings.AutoFan && _nvoc.IsNvidiaAvailable)
            {
                if (_useFactoryCurve)
                {
                    _nvoc.RestoreDefaultFanCurve();
                }
                else if (gpuTempValue.HasValue)
                {
                    int targetSpeed = GetFanSpeedForTemperature(gpuTempValue.Value, _settings.FanCurve);
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
                fanSpeedText,
                fanSpeedValue);
        }

        private void ApplySnapshot(GpuSnapshot snapshot)
        {
            _rowGpuTemp.Value = snapshot.GpuTempValue.HasValue
                ? $"{snapshot.GpuTempValue.Value:F1} °C"
                : snapshot.GpuTempText;

            _rowVoltage.Value = snapshot.VoltageText;
            _rowLoad.Value = snapshot.LoadText;
            _rowCoreClock.Value = snapshot.CoreClockText;
            _rowMemClock.Value = snapshot.MemClockText;
            _rowPower.Value = snapshot.PowerText;
            _rowMemory.Value = snapshot.MemoryUsageText;

            if (HasValidValue(snapshot.HotspotText))
            {
                _rowGPuHotSpot.Value = snapshot.HotspotText;
                if (!_clockRows.Contains(_rowGPuHotSpot))
                {
                    var insertIndex = _clockRows.IndexOf(_rowGpuTemp);
                    _clockRows.Insert(insertIndex + 1, _rowGPuHotSpot);
                }
            }
            else if (_clockRows.Contains(_rowGPuHotSpot))
            {
                _clockRows.Remove(_rowGPuHotSpot);
            }

            if (HasValidValue(snapshot.MemTempText))
            {
                _rowGPUMemTemp.Value = snapshot.MemTempText;
                if (!_clockRows.Contains(_rowGPUMemTemp))
                {
                    var insertIndex = _clockRows.IndexOf(_rowGPuHotSpot);
                    if (insertIndex < 0)
                    {
                        insertIndex = _clockRows.IndexOf(_rowGpuTemp);
                    }

                    _clockRows.Insert(insertIndex + 1, _rowGPUMemTemp);
                }
            }
            else if (_clockRows.Contains(_rowGPUMemTemp))
            {
                _clockRows.Remove(_rowGPUMemTemp);
            }

            

            if (_settings.AutoFan && _nvoc.IsNvidiaAvailable)
            {
                tb_fanSpeed.Text = snapshot.FanSpeedText ?? tb_fanSpeed.Text;
                if (snapshot.FanSpeedValue.HasValue)
                {
                    slider_fanSpeed.Value = snapshot.FanSpeedValue.Value;
                }
            }

            if (snapshot.GpuTempValue.HasValue)
            {
                saveValues(
                    snapshot.GpuTempValue.Value,
                    snapshot.CoreClockValue,
                    snapshot.MemClockValue,
                    snapshot.VoltageValue,
                    snapshot.LoadValue,
                    snapshot.PowerValue,
                    snapshot.MemTempValue,
                    snapshot.HotspotValue);
            }
        }

        private static bool HasValidValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "--", StringComparison.Ordinal);
        }
        // *********************************************************************************************************************************
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts.Cancel();
            //_settings.AutoFan = cb_autoFanSpeed.IsChecked == true;
            //_settings.FanSpeed = int.TryParse(tb_fanSpeed.Text, out var fs) ? fs : _settings.FanSpeed;
            saveSettingsInJSON();
            SettingsStore.Save(_settings);
        }
        // *********************************************************************************************************************************
        private void slider_fanSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (cb_autoFanSpeed.IsChecked == true)
                return;

            _useFactoryCurve = false;

            int newSpeed = (int)slider_fanSpeed.Value;
            tb_fanSpeed.Text = newSpeed.ToString();
            _nvoc.set_FanSpeed(newSpeed);
        }
        // *********************************************************************************************************************************
        private void cb_autoFanSpeed_Checked(object sender, RoutedEventArgs e)
        {
            if (cb_autoFanSpeed.IsChecked == true)
            {
                _settings.AutoFan = true;
                slider_fanSpeed.IsEnabled = false;
                tb_fanSpeed.IsEnabled = false;
                cb_autoFanSpeed.IsChecked = true;
                ApplyFanCurveIfEnabled();
            }
        }
        // *********************************************************************************************************************************
        private void cb_autoFanSpeed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (cb_autoFanSpeed.IsChecked == false)
            {
                _settings.AutoFan = false;
                slider_fanSpeed.IsEnabled = true;
                tb_fanSpeed.IsEnabled = true;
                

                if (!_useFactoryCurve)
                    _nvoc.set_FanSpeed((int)slider_fanSpeed.Value);
            }
        }
        // *********************************************************************************************************************************
        private void btn_FanSpeed_ok_Click(object sender, RoutedEventArgs e)
        {
            _nvoc.set_FanSpeed(int.Parse(tb_fanSpeed.Text));
            slider_fanSpeed.Value = int.Parse(tb_fanSpeed.Text);
        }
        // *********************************************************************************************************************************
        // Beenden der Anwendung
        private void CloseApplication(object sender, RoutedEventArgs e)
        {
            Window_Closing(sender, new System.ComponentModel.CancelEventArgs());
            Application.Current.Shutdown();
            GC.Collect();
        }
        // *********************************************************************************************************************************
        // Maximieren der Anwendung
        private void Maximize_Window(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }
        // *********************************************************************************************************************************
        // Fenster minimieren
        private void Minimize_Window(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        // *********************************************************************************************************************************
        // Fenster bewegen per Drag
        private void panel_driverV_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
        // *********************************************************************************************************************************
        private void topStatusBar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Maximize_Window(sender, e);
        }
        // *********************************************************************************************************************************
        private void show_GPU_FanCurve()
        {
            var initialPoints = ToCoordinates(_settings.FanCurve);
            var dialog = new FanCurve(initialPoints)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true)
            {
                _settings.FanCurve = ToFanCurvePoints(dialog.Points);
                SettingsStore.Save(_settings);
                _useFactoryCurve = false;
                ApplyFanCurveIfEnabled();
            }
        }
        // *********************************************************************************************************************************
        private static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private string saveSettingsInJSON()
        {
            _settings.AutoFan = cb_autoFanSpeed.IsChecked == true;
            _settings.FanSpeed = int.TryParse(tb_fanSpeed.Text, out var fs) ? fs : _settings.FanSpeed;
            return _settings?.ToString() ?? string.Empty;
        }

        private void btn_FanCurve_Click(object sender, RoutedEventArgs e)
        {
            // Graph mit Lüfterkurve öffnen zum anpassen
            show_GPU_FanCurve();
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

        private static int GetFanSpeedForTemperature(double temperature, List<SettingsStore.FanCurvePoint> curve)
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

        private static int? TryParseInt(string? value)
        {
            return int.TryParse(value, out var result) ? result : null;
        }

        private void ApplyFanCurveIfEnabled()
        { // Nur anwenden wenn Auto-Fan aktiviert ist und eine Kurve definiert ist
            if (!_settings.AutoFan || _settings.FanCurve.Count == 0)
                return;

            if (_useFactoryCurve)
            {
                _nvoc.RestoreDefaultFanCurve();
                tb_fanSpeed.Text = _nvoc.get_FanSpeed();
                return;
            }

            if (!double.TryParse(_nvoc.get_GPUCoreTemperature(), out var temp))
                return;

            int targetSpeed = GetFanSpeedForTemperature(temp, _settings.FanCurve);
            _nvoc.set_FanSpeed(targetSpeed);
            tb_fanSpeed.Text = targetSpeed.ToString();
        }

        private void btn_FanCurveReset_Click(object sender, RoutedEventArgs e)
        {
            _settings.FanCurve = CreateDefaultFanCurve(); // Standardkurve erstellen
            SettingsStore.Save(_settings); // Änderungen speichern
            cb_autoFanSpeed.IsChecked = true;
            cb_autoFanSpeed_Checked(sender, e); // Auto-Fan aktivieren
            _useFactoryCurve = true;
            _nvoc.RestoreDefaultFanCurve(); // Standard-Lüfterkurve wiederherstellen

            if (_settings.AutoFan)
                ApplyFanCurveIfEnabled();
        }

        private static List<SettingsStore.FanCurvePoint> CreateDefaultFanCurve() // Standard-Lüfterkurve mit 5 Punkten erstellen
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

        private void saveValues(double gpuTempValue, double gpuClockValue, double memoryClockValue, double gpuVoltCurr, double gpuUsage, double rowPower, double? memTempValue, double? gpuHotspot)
        {  // Werte in den Arrays einfügen, aktualisieren und Min/Max/Avg berechnen
            UpdateStats(0, gpuTempValue);
            UpdateStats(1, gpuClockValue);
            UpdateStats(2, memoryClockValue);
            UpdateStats(3, gpuVoltCurr);
            UpdateStats(4, gpuUsage);
            UpdateStats(5, rowPower);
            UpdateStats(6, memTempValue); // VRAM-Temperatur hinzufügen
            UpdateStats(7, gpuHotspot);



            _rowGpuTemp.Min = $"{_minValues[0]:F1}";
            _rowGpuTemp.Max = $"{_maxValues[0]:F1}";
            _rowGpuTemp.Avg = $"{_avgValues[0]:F1}";

            _rowCoreClock.Min = $"{_minValues[1]:F1}";
            _rowCoreClock.Max = $"{_maxValues[1]:F1}";
            _rowCoreClock.Avg = $"{_avgValues[1]:F1}";

            _rowMemClock.Min = $"{_minValues[2]:F1}";
            _rowMemClock.Max = $"{_maxValues[2]:F1}";
            _rowMemClock.Avg = $"{_avgValues[2]:F1}";

            _rowVoltage.Min = $"{_minValues[3]:F3}";
            _rowVoltage.Max = $"{_maxValues[3]:F3}";
            _rowVoltage.Avg = $"{_avgValues[3]:F3}";

            _rowLoad.Min = $"{_minValues[4]:F0}";
            _rowLoad.Max = $"{_maxValues[4]:F0}";
            _rowLoad.Avg = $"{_avgValues[4]:F0}";

            _rowPower.Min = $"{_minValues[5]:F1}";
            _rowPower.Max = $"{_maxValues[5]:F1}";
            _rowPower.Avg = $"{_avgValues[5]:F1}";

            if (_sampleCounts[6] > 0)
            {
                _rowGPUMemTemp.Min = $"{_minValues[6]:F1}";
                _rowGPUMemTemp.Max = $"{_maxValues[6]:F1}";
                _rowGPUMemTemp.Avg = $"{_avgValues[6]:F1}";
            }

            if (_sampleCounts[7] > 0)
            {
                _rowGPuHotSpot.Min = $"{_minValues[7]:F1}";
                _rowGPuHotSpot.Max = $"{_maxValues[7]:F1}";
                _rowGPuHotSpot.Avg = $"{_avgValues[7]:F1}";
            }
        }

        private void UpdateStats(int index, double? value)
        {  // Min/Max/Avg für den angegebenen Index aktualisieren
            if (!value.HasValue)
                return;

            _sampleCounts[index]++;
            var current = value.Value;
            _minValues[index] = Math.Min(_minValues[index], current);
            _maxValues[index] = Math.Max(_maxValues[index], current);
            _avgValues[index] = ((_avgValues[index] * (_sampleCounts[index] - 1)) + current) / _sampleCounts[index];
        }
    }
}
