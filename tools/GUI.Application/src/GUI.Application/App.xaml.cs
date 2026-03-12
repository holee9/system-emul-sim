using System.Windows;
using Serilog;
using XrayDetector.Gui.Logging;
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

        // Initialize Serilog logging infrastructure (SPEC-HELP-001)
        LoggingBootstrap.Initialize();
        Log.ForContext("SourceContext", LogCategories.App).Information("Application starting up");

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

        // Auto-connect and start acquisition in simulation mode.
        // In E2E test mode, skip acquisition to avoid Dispatcher saturation from the 10fps
        // simulation timer, which defers UIAutomation peer registration and causes test failures.
        var isE2EMode = Environment.GetEnvironmentVariable("XRAY_E2E_MODE") == "true";
        if (!isE2EMode)
        {
            try
            {
                await _simulatedClient.ConnectAsync("sim", 0);
                await _simulatedClient.StartAcquisitionAsync();
                Log.ForContext("SourceContext", LogCategories.App).Information("Simulation auto-start completed");
            }
            catch (Exception ex)
            {
                Log.ForContext("SourceContext", LogCategories.App).Warning(ex, "Auto-start failed");
            }
        }
        else
        {
            Log.ForContext("SourceContext", LogCategories.App).Information("E2E mode: simulation auto-start skipped");
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.ForContext("SourceContext", LogCategories.App).Fatal("Unhandled domain exception: {Exception}", e.ExceptionObject);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.ForContext("SourceContext", LogCategories.App).Error(e.Exception, "Unhandled UI thread exception");

        // Prevent application crash (optional: show error dialog)
        e.Handled = true;
    }
}
