using NvAPIWrapper.GPU;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ScottPlot;
using Volt.Utils; // hinzufügen


namespace Volt
{
    public partial class MainWindow : Window
    {
        // imports
        private readonly UDefinition _udef = new();
        private readonly NVOC _nvoc = new();
        private readonly LibreHW _hwinfo = new LibreHW();
        private readonly CancellationTokenSource _cts = new();
        private SettingsStore.Settings _settings = new(); // neu

        private readonly ObservableCollection<ClockRow> _clockRows = new();
        private ClockRow _rowGpuTemp = null!;
        private ClockRow _rowCoreClock = null!;
        private ClockRow _rowMemClock = null!;
        private ClockRow _rowVoltage = null!;
        private ClockRow _rowPower = null!;
        private ClockRow _rowLoad = null!;
        private ClockRow _rowMemory = null!;

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

            Loaded += (s, e) => Initialize();

        }
        // *********************************************************************************************************************************
        private void Initialize()
        {
            _settings = SettingsStore.Load(); // laden

            lb_driverV.Content = _nvoc.get_DriverVersion();
            InitializeClockRows();

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
                slider_fanSpeed.Value = int.Parse(fanSpeedText);
            }

            _ = DoWorkAsync(this, _cts.Token);
        }

        private void InitializeClockRows()
        {
            _rowGpuTemp = new ClockRow("GPU Temperature", "--");
            _rowCoreClock = new ClockRow("Core Clock", "--");
            _rowMemClock = new ClockRow("Memory Clock", "--");
            _rowVoltage = new ClockRow("Curr. Voltage", "--");
            _rowPower = new ClockRow("Curr. Power", "--");
            _rowLoad = new ClockRow("Curr. Load", "--");
            _rowMemory = new ClockRow("Curr. Memory", "--");

            _clockRows.Clear();
            _clockRows.Add(_rowGpuTemp);
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
            while (!token.IsCancellationRequested)
            {
                string gpuTempText = _mw._nvoc.get_GPUCoreTemperature();
                double? gpuTempValue = double.TryParse(gpuTempText, out var tempValue) ? tempValue : null;

                _mw._rowGpuTemp.Value = gpuTempValue.HasValue
                    ? $"{tempValue:F1} °C"
                    : $"{gpuTempText} °C";

                string[] clocks = _mw._nvoc.get_ClockSpeed();
                _mw._rowCoreClock.Value = $"{clocks[0]:F1} mhz";
                _mw._rowMemClock.Value = $"{clocks[1]:F1} mhz";

                string gpuVoltCurr = _mw._nvoc.get_Voltage();
                _mw._rowVoltage.Value = gpuVoltCurr;

                string gpuUsage = _nvoc.get_GPU_usage();
                _mw._rowLoad.Value = gpuUsage;

                _hwinfo.Read_GPU_InformationAsync();
                _mw._rowPower.Value = _hwinfo.GPU_power;
                _mw._rowMemory.Value = _hwinfo.GPU_mem_usage;

                if (_mw._settings.AutoFan)
                {
                    if (_mw._useFactoryCurve)
                    {
                        _mw._nvoc.RestoreDefaultFanCurve();
                        _mw.tb_fanSpeed.Text = _mw._nvoc.get_FanSpeed();
                    }
                    else if (gpuTempValue.HasValue)
                    {
                        int targetSpeed = GetFanSpeedForTemperature(gpuTempValue.Value, _mw._settings.FanCurve);
                        _mw._nvoc.set_FanSpeed(targetSpeed);
                        _mw.tb_fanSpeed.Text = targetSpeed.ToString();
                    }
                    // Synchronisieren des Sliders mit der aktuellen Lüftergeschwindigkeit wenn auf auto
                    //_mw.slider_fanSpeed.Value = int.Parse(_mw.tb_fanSpeed.Text) ?? string.Empty; ; 
                    _mw.slider_fanSpeed.Value = _mw._nvoc.get_FanSpeed() is string fanSpeedStr && int.TryParse(fanSpeedStr, out var fanSpeed) ? fanSpeed : _mw.slider_fanSpeed.Value;
                }

                await Task.Delay(UDefinition.cUpdateInterval);
            }
        }

        private sealed class ClockRow : INotifyPropertyChanged
        {
            private string _value;

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

            public event PropertyChangedEventHandler? PropertyChanged;

            public ClockRow(string name, string value)
            {
                Name = name;
                _value = value;
            }
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
