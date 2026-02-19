using NvAPIWrapper.GPU;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Volt.Utils; // hinzufügen


namespace Volt
{
    public partial class MainWindow : Window
    {
        // imports
        private readonly UDefinition _udef = new();
        private readonly NVOC _nvoc = new();
        //private readonly AMD_GPU _AMD = new AMD_GPU(); // Muss noch ausgebaut werden
        private readonly LibreHW _hwinfo = new LibreHW();
        private readonly GpuMonitor _monitor;
        private readonly FanCurveService _fanCurveService = new();
        private readonly SettingsService _settingsService = new();
        private readonly UpdateService _updateService = new("BangerDachs", "Volt");
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
        private ClockRow _rowMemoryUsage = null!;

        private readonly GpuStats _stats = new(9);
        private readonly Stopwatch _elapsedStopwatch = new();

        // variables
        //Graph_FanCurve
        private FanCurve? _fanCurveWindow;
        private bool _useFactoryCurve;

        // * * * * * *


        public MainWindow()
        {
            InitializeComponent();
            _monitor = new GpuMonitor(_hwinfo, _nvoc);

            // prüfen ob die Anwendung mit Administratorrechten gestartet wuurde
            if (!IsAdministrator())
            { panel_fanControl.IsEnabled = false; }

            // Diagramm An/Aus 
            Grid_Background_FanCurve.Visibility = Visibility.Hidden;
            if (Grid_Background_FanCurve.Visibility == Visibility.Visible)
            { ShowFanCurveDialog(); }

            Loaded += (s, e) => Initialize(); // Initialisierung nach dem Laden MainWindow

        }
        // *********************************************************************************************************************************
        private void Initialize()
        {
            _settings = _settingsService.Load(); // laden

            _stats.Reset();
            _elapsedStopwatch.Restart();
            UpdateElapsedTime();

            var driverVersion = _nvoc.IsNvidiaAvailable
                                ? _nvoc.get_DriverVersion()
                                : _hwinfo.GetAmdDriverVersion() ?? "Driver: N/A";
            lb_driverV.Content = driverVersion;

            InitializeClockRows();

            lbl_version.Content = $"Version {GetAppVersion()}";
            _ = _updateService.CheckForUpdatesAsync(this, _cts.Token);

            if (!_nvoc.IsNvidiaAvailable)
            {
                panel_fanControl.IsEnabled = false;
                panel_fanControl.Visibility = Visibility.Visible;
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
                    _fanCurveService.ApplyIfEnabled(_settings, _nvoc, _useFactoryCurve, value => tb_fanSpeed.Text = value);
                }
                else
                {
                    var fanSpeedText = _settingsService.ResolveFanSpeedText(_settings, _nvoc.get_FanSpeed);

                    tb_fanSpeed.Text = fanSpeedText;
                    if (int.TryParse(fanSpeedText, out var fanSpeed))
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
            _rowMemory = new ClockRow("Memory", "--", "--", "--", "--");
            _rowMemoryUsage = new ClockRow("VRAM Usage", "--", "--", "--", "--");

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
            _clockRows.Add(_rowMemoryUsage);

            dataGrid_clocks.ItemsSource = _clockRows;
        }
        // *********************************************************************************************************************************
        private async Task DoWorkAsync(MainWindow _mw, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var snapshot = await _monitor.CollectSnapshotAsync(_settings, _useFactoryCurve, token).ConfigureAwait(false);

                    await Dispatcher.InvokeAsync(
                        () => ApplySnapshot(snapshot),
                        DispatcherPriority.Background,
                        token);

                    await Task.Delay(UDefinition.cUpdateInterval, token);
                }
            }
            catch (OperationCanceledException)
            { } catch (Exception ex) {
            }
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

            //_rowMemoryUsage.Value = snapshot.MemoryUsageText;
            if (HasValidValue(snapshot.MemoryUsageText)) 
            {
                _rowMemoryUsage.Value = snapshot.MemoryUsageText;
                if (!_clockRows.Contains(_rowMemoryUsage))
                {
                    var insertIndex = _clockRows.IndexOf(_rowMemoryUsage);
                    if (insertIndex < 0)
                    {
                        insertIndex = _clockRows.IndexOf (_rowMemoryUsage);
                    }

                    _clockRows.Insert(insertIndex + 1, _rowMemoryUsage);
                }
            }

            if (_settings.AutoFan && _nvoc.IsNvidiaAvailable)
            {
                tb_fanSpeed.Text = snapshot.FanSpeedText ?? tb_fanSpeed.Text;
                if (snapshot.FanSpeedValue.HasValue)
                {
                    slider_fanSpeed.Value = snapshot.FanSpeedValue.Value;
                }
            }

            UpdateElapsedTime();

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
                    snapshot.HotspotValue,
                    snapshot.MemoryUsageValue);
            }
        }

        private static bool HasValidValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "--", StringComparison.Ordinal);
        }

        private void UpdateElapsedTime()
        {
            var elapsed = _elapsedStopwatch.Elapsed;
            lbl_elapsedTime.Content = $"Elapsed Time: {elapsed:hh\\:mm\\:ss}";
        }
        // *********************************************************************************************************************************
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts.Cancel();
            _settingsService.UpdateFromUi(_settings, cb_autoFanSpeed.IsChecked, tb_fanSpeed.Text);
            _settingsService.Save(_settings);
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
                _fanCurveService.ApplyIfEnabled(_settings, _nvoc, _useFactoryCurve, value => tb_fanSpeed.Text = value);
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
        private void ShowFanCurveDialog()
        {
            if (_fanCurveService.ShowFanCurveDialog(this, _settings))
            {
                _settingsService.Save(_settings);
                _useFactoryCurve = false;
                _fanCurveService.ApplyIfEnabled(_settings, _nvoc, _useFactoryCurve, value => tb_fanSpeed.Text = value);
            }
        }
        // *********************************************************************************************************************************
        private static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void btn_FanCurve_Click(object sender, RoutedEventArgs e)
        {
            // Graph mit Lüfterkurve öffnen zum anpassen
            ShowFanCurveDialog();
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

        

        private void btn_FanCurveReset_Click(object sender, RoutedEventArgs e)
        {
            _fanCurveService.ResetToDefault(_settings, _nvoc); // Standardkurve erstellen
            _settingsService.Save(_settings); // Änderungen speichern
            cb_autoFanSpeed.IsChecked = true;
            cb_autoFanSpeed_Checked(sender, e); // Auto-Fan aktivieren
            _useFactoryCurve = true;

            if (_settings.AutoFan)
                _fanCurveService.ApplyIfEnabled(_settings, _nvoc, _useFactoryCurve, value => tb_fanSpeed.Text = value);
        }

        private void saveValues(double gpuTempValue, double gpuClockValue, double memoryClockValue, 
                                double gpuVoltCurr, double gpuUsage, double rowPower, 
                                double? memTempValue, double? gpuHotspot, double? memoryUsage)
        {  // Werte in den Arrays einfügen, aktualisieren und Min/Max/Avg berechnen
            _stats.Update(0, gpuTempValue);
            _stats.Update(1, gpuClockValue);
            _stats.Update(2, memoryClockValue);
            _stats.Update(3, gpuVoltCurr);
            _stats.Update(4, gpuUsage);
            _stats.Update(5, rowPower);
            _stats.Update(6, memTempValue); // VRAM-Temperatur hinzufügen
            _stats.Update(7, gpuHotspot);
            _stats.Update(8, memoryUsage);



            _rowGpuTemp.Min = $"{_stats.Min(0):F1}";
            _rowGpuTemp.Max = $"{_stats.Max(0):F1}";
            _rowGpuTemp.Avg = $"{_stats.Avg(0):F1}";

            _rowCoreClock.Min = $"{_stats.Min(1):F1}";
            _rowCoreClock.Max = $"{_stats.Max(1):F1}";
            _rowCoreClock.Avg = $"{_stats.Avg(1):F1}";

            _rowMemClock.Min = $"{_stats.Min(2):F1}";
            _rowMemClock.Max = $"{_stats.Max(2):F1}";
            _rowMemClock.Avg = $"{_stats.Avg(2):F1}";

            _rowVoltage.Min = $"{_stats.Min(3):F3}";
            _rowVoltage.Max = $"{_stats.Max(3):F3}";
            _rowVoltage.Avg = $"{_stats.Avg(3):F3}";

            _rowLoad.Min = $"{_stats.Min(4):F0}";
            _rowLoad.Max = $"{_stats.Max(4):F0}";
            _rowLoad.Avg = $"{_stats.Avg(4):F0}";

            _rowPower.Min = $"{_stats.Min(5):F1}";
            _rowPower.Max = $"{_stats.Max(5):F1}";
            _rowPower.Avg = $"{_stats.Avg(5):F1}";

            if (_stats.HasSamples(6))
            {
                _rowGPUMemTemp.Min = $"{_stats.Min(6):F1}";
                _rowGPUMemTemp.Max = $"{_stats.Max(6):F1}";
                _rowGPUMemTemp.Avg = $"{_stats.Avg(6):F1}";
            }

            if (_stats.HasSamples(7))
            {
                _rowGPuHotSpot.Min = $"{_stats.Min(7):F1}";
                _rowGPuHotSpot.Max = $"{_stats.Max(7):F1}";
                _rowGPuHotSpot.Avg = $"{_stats.Avg(7):F1}";
            }

            if (_stats.HasSamples(8))
            {
                _rowMemoryUsage.Min = $"{_stats.Min(8):F1}";
                _rowMemoryUsage.Max = $"{_stats.Max(8):F1}";
                _rowMemoryUsage.Avg = $"{_stats.Avg(8):F1}";
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Werte zurücksetzen
            _stats.Reset();
            _elapsedStopwatch.Restart();
            UpdateElapsedTime();
        }
        private static string GetAppVersion()
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version is null)
            {
                return "N/A";
            }

            var build = version.Build >= 0 ? version.Build : 0;
            var minor = version.Minor >= 0 ? version.Minor : 0;
            return $"{version.Major}.{minor}.{build}";
        }
    }
}
