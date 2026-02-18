using System.Windows;
using System.Windows.Threading;

namespace ParameterExtractor.Wpf;

/// <summary>
/// Application entry point for ParameterExtractor WPF application.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure exception handling
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"An unhandled exception occurred:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        if (e.IsTerminating)
        {
            // Log the error before termination
            System.Diagnostics.Debug.WriteLine($"Application terminating due to unhandled exception: {e.ExceptionObject}");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe operation has been cancelled.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
