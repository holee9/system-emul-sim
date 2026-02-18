using System.Windows;

namespace XrayDetector.Gui;

/// <summary>
/// Application entry point for GUI.Application.
/// Unified WPF interface for X-ray Detector Panel System (REQ-TOOLS-040).
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure unhandled exception handling
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
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
