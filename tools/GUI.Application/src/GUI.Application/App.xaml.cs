using System.Windows;
using XrayDetector.Gui.Views;
using XrayDetector.Gui.ViewModels;
using XrayDetector.Gui.Simulation;

namespace XrayDetector.Gui;

/// <summary>
/// Application entry point for GUI.Application.
/// Unified WPF interface for X-ray Detector Panel System (REQ-TOOLS-040).
/// Runs in simulation mode (SimulatedDetectorClient) when no hardware is present.
/// </summary>
public partial class App : Application
{
    private SimulatedDetectorClient? _simulatedClient;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure unhandled exception handling
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Use SimulatedDetectorClient for demo/development without hardware
        _simulatedClient = new SimulatedDetectorClient();
        var mainViewModel = new MainViewModel(_simulatedClient);
        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };
        MainWindow = mainWindow;
        mainWindow.Show();

        // Auto-connect and start acquisition in simulation mode
        try
        {
            await _simulatedClient.ConnectAsync("sim", 0);
            await _simulatedClient.StartAcquisitionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Auto-start failed: {ex.Message}");
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Log critical exceptions
        System.Diagnostics.Debug.WriteLine($"CRITICAL: {e.ExceptionObject}");
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Log UI thread exceptions
        System.Diagnostics.Debug.WriteLine($"UI Exception: {e.Exception}");

        // Prevent application crash (optional: show error dialog)
        e.Handled = true;
    }
}
