using System.Windows;
using Serilog;
using XrayDetector.Gui.Logging;
using XrayDetector.Gui.Services;
using XrayDetector.Gui.Views;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui;

/// <summary>
/// Application entry point for GUI.Application.
/// Unified WPF interface for X-ray Detector Panel System (REQ-TOOLS-040).
/// Uses PipelineDetectorClient to stream real frames from SimulatorPipeline (SPEC-GUI-001 MVP-1).
/// </summary>
public partial class App : Application
{
    private PipelineDetectorClient? _pipelineClient;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize Serilog logging infrastructure (SPEC-HELP-001)
        LoggingBootstrap.Initialize();
        Log.ForContext("SourceContext", LogCategories.App).Information("Application starting up");

        // Configure unhandled exception handling
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Use PipelineDetectorClient — wraps SimulatorPipeline for real frame generation (SPEC-GUI-001 MVP-1)
        _pipelineClient = new PipelineDetectorClient();
        var mainViewModel = new MainViewModel(_pipelineClient);
        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };
        MainWindow = mainWindow;
        mainWindow.Show();

        // Apply initial configuration from SimulatorControlViewModel defaults (1024x1024, 14-bit)
        _pipelineClient.UpdateConfig(mainViewModel.SimulatorControlViewModel.ToDetectorConfig());

        // Auto-connect (always) and start acquisition (non-E2E mode only).
        // E2E mode: Connect so BtnStart is enabled, but skip auto-start so UIAutomation
        // peer registration completes before frame events flood the Dispatcher.
        // PipelineDetectorClient runs acquisition in Task.Run (background), safe with UIAutomation.
        var isE2EMode = Environment.GetEnvironmentVariable("XRAY_E2E_MODE") == "true";
        try
        {
            await _pipelineClient.ConnectAsync("sim", 0);
            if (!isE2EMode)
            {
                await _pipelineClient.StartAcquisitionAsync();
                Log.ForContext("SourceContext", LogCategories.App).Information("Pipeline auto-start completed");
            }
            else
            {
                Log.ForContext("SourceContext", LogCategories.App)
                    .Information("E2E mode: connected, acquisition not auto-started — tests control Start/Stop via UI");
            }
        }
        catch (Exception ex)
        {
            Log.ForContext("SourceContext", LogCategories.App).Warning(ex, "Auto-connect/start failed");
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
