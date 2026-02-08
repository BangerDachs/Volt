using NvAPIWrapper.GPU;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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

        // variables
        //Graph_FanCurve


        // * * * * *


        public MainWindow()
        {
            InitializeComponent();

            // prüfen ob die Anwendung mit Administratorrechten gestartet wuurde
            if (!IsAdministrator())
            { panel_fanControl.IsEnabled = false; }

            // Diagramm An/Aus 
            Grid_Background_FanCurve.Visibility = Visibility.Hidden;
            if (Grid_Background_FanCurve.Visibility == Visibility.Visible)
            { show_GPU_FanCurve(); }

            Loaded += (s, e) => Initialize();

        }
        // *********************************************************************************************************************************
        private void Initialize()
        {
            _settings = SettingsStore.Load(); // laden

            lb_driverV.Content = _nvoc.get_DriverVersion();

            if (_settings.AutoFan)
            {
                cb_autoFanSpeed.IsChecked = true;
                slider_fanSpeed.IsEnabled = false;
                tb_fanSpeed.IsEnabled = false;
                _nvoc.RestoreDefaultFanCurve();
            }
            else
            {
                var fanSpeedText = _settings.FanSpeed > 0
                    ? _settings.FanSpeed.ToString()
                    : _nvoc.get_FanSpeed();

                tb_fanSpeed.Text = fanSpeedText;
                slider_fanSpeed.Value = int.Parse(fanSpeedText);
            }

            // Starte den asynchronen Task
            _ = DoWorkAsync(this, _cts.Token);

        }
        // *********************************************************************************************************************************
        private async Task DoWorkAsync(MainWindow _mw, CancellationToken token)
        {
            while (!token.IsCancellationRequested) // Schleife mit Abbruchtoken
            {
                // Abruf von GPU-Daten und Anzeige in UI-Komponenten
                string gpuTemp = _mw._nvoc.get_GPUCoreTemperature();

                _mw.lb_gpu_temp_sensor.Content = ($"{gpuTemp:F1} °C");

                string[] clocks = _mw._nvoc.get_ClockSpeed();
                _mw.lb_gpu_frequency.Content = ($"{clocks[0]:F1} mhz");
                _mw.lb_mem_frequency.Content = ($"{clocks[1]:F1} mhz");


                string gpuVoltCurr = _mw._nvoc.get_Voltage();
                _mw.lb_voltage_curr_voltage.Content = gpuVoltCurr;

                string gpuUsage = _nvoc.get_GPU_usage();
                _mw.lb_load_curr_load.Content = gpuUsage;



                //_mw._hwinfo.Get_Read_GPU_Information();
                _hwinfo.Read_GPU_InformationAsync();
                _mw.lb_power_curr_power.Content = _hwinfo.GPU_power;
                //_mw.lb_load_curr_load.Content = _hwinfo.GPU_load;
                _mw.lb_memory_curr_memory.Content = _hwinfo.GPU_mem_usage;

                string fanSpeed = _mw._nvoc.get_FanSpeed();
                _mw.tb_fanSpeed.Text = fanSpeed;

                await Task.Delay(UDefinition.cUpdateInterval); // Intervall 500ms
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
            int newSpeed = (int)slider_fanSpeed.Value;
            tb_fanSpeed.Text = newSpeed.ToString();
            _nvoc.set_FanSpeed(newSpeed);
        }
        // *********************************************************************************************************************************
        private void cb_autoFanSpeed_Checked(object sender, RoutedEventArgs e)
        {
            if (cb_autoFanSpeed.IsChecked == true)
            {
                slider_fanSpeed.IsEnabled = false;
                tb_fanSpeed.IsEnabled = false;
                _nvoc.RestoreDefaultFanCurve(); // auf Standard-Lüfterkurve zurücksetzen
            }
        }
        // *********************************************************************************************************************************
        private void cb_autoFanSpeed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (cb_autoFanSpeed.IsChecked == false)
            {
                slider_fanSpeed.IsEnabled = true;
                tb_fanSpeed.IsEnabled = true;
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
            // Beispielwerte zum Testen:
            double[] dataX = { 1, 2, 3, 4, 5, };
            double[] dataY = { 1, 2, 4, 8, 16 };
            Graph_FanCurve.Plot.Add.Scatter(dataX, dataY);
            Graph_FanCurve.Refresh();
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

    }
}
