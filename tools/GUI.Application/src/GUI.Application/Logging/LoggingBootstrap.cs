using Serilog;

namespace XrayDetector.Gui.Logging;

/// <summary>
/// Initializes Serilog logging infrastructure for GUI.Application (SPEC-HELP-001).
/// Call Initialize() at application startup in App.xaml.cs OnStartup.
/// </summary>
public static class LoggingBootstrap
{
    /// <summary>
    /// Gets the InMemoryLogSink instance when E2E mode is active, null otherwise.
    /// Used by LogAssertions for test verification.
    /// </summary>
    public static InMemoryLogSink? InMemorySink { get; private set; }

    /// <summary>
    /// Initializes Serilog with file rolling sink and optional in-memory sink for E2E.
    /// Reads XRAY_E2E_MODE environment variable to auto-detect E2E mode.
    /// </summary>
    /// <param name="isE2EMode">
    /// When true, adds WriteTo.Sink&lt;InMemoryLogSink&gt;() for test assertions.
    /// If null, reads from XRAY_E2E_MODE environment variable.
    /// </param>
    public static void Initialize(bool? isE2EMode = null)
    {
        bool e2eMode = isE2EMode ?? IsE2EModeFromEnvironment();

        var config = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName()
            .WriteTo.Async(a => a.File(
                path: "logs/app.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] {SourceContext} {Message:lj}{NewLine}{Exception}"));

        if (e2eMode)
        {
            InMemorySink = new InMemoryLogSink();
            config.WriteTo.Sink(InMemorySink);
        }

        Log.Logger = config.CreateLogger();
        Log.Information("Logging initialized. E2E mode: {E2EMode}", e2eMode);
    }

    private static bool IsE2EModeFromEnvironment()
    {
        var envValue = Environment.GetEnvironmentVariable("XRAY_E2E_MODE");
        return string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase);
    }
}
