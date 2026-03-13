using Xunit;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// xUnit fact attribute with retry metadata for flaky E2E tests.
/// SPEC-HELP-001: REQ-HELP-056
/// TAG-006: MaxRetries and RetryDelayMs properties; works with E2ETestBase base class.
///
/// Note: This attribute marks tests as retryable. To actually retry,
/// use the RetryHelper.RunWithRetry helper in the test body for critical assertions.
/// A full xUnit SDK-based retry runner requires xunit.extensibility.execution which
/// has API changes between versions; the helper approach is simpler and stable.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RetryFactAttribute : FactAttribute
{
    public int MaxRetries { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 500;
}

/// <summary>
/// Provides retry logic for E2E test assertions that may be flaky due to timing.
/// SPEC-HELP-001: REQ-HELP-056
/// TAG-006: Logs each retry attempt number via E2ELogger AsyncLocal bridge.
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Runs an async action with retry on exception.
    /// Logs each retry attempt via E2ELogger (visible in xUnit output when AsyncLocal bridge is set).
    /// </summary>
    public static async Task RunWithRetryAsync(Func<Task> action, int maxRetries = 2, int retryDelayMs = 500)
    {
        Exception? lastException = null;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                // TAG-006: Log each retry attempt for diagnostics
                System.Diagnostics.Trace.WriteLine(
                    $"[RetryHelper] retry attempt {attempt + 1}/{maxRetries}: {ex.Message}");
                // Also forward via E2ELogger AsyncLocal bridge if available
                try
                {
                    E2ELoggerBridge.WriteRetryLog(attempt + 1, maxRetries, ex.Message);
                }
                catch { /* best effort */ }
                await Task.Delay(retryDelayMs);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }
        if (lastException != null) throw lastException;
    }

    /// <summary>
    /// Runs a synchronous action with retry on exception.
    /// </summary>
    public static void RunWithRetry(Action action, int maxRetries = 2, int retryDelayMs = 500)
    {
        Exception? lastException = null;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                System.Diagnostics.Trace.WriteLine(
                    $"[RetryHelper] retry attempt {attempt + 1}/{maxRetries}: {ex.Message}");
                E2ELoggerBridge.WriteRetryLog(attempt + 1, maxRetries, ex.Message);
                Thread.Sleep(retryDelayMs);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }
        if (lastException != null) throw lastException;
    }
}

/// <summary>
/// Internal bridge that forwards retry log messages to the E2ELogger AsyncLocal output.
/// Avoids tight coupling between RetryHelper and E2ELogger.
/// TAG-006.
/// </summary>
internal static class E2ELoggerBridge
{
    internal static void WriteRetryLog(int attempt, int maxRetries, string message)
    {
        // Access the AsyncLocal ITestOutputHelper via E2ELogger's static accessor
        E2ELogger.WriteRetryAttempt(attempt, maxRetries, message);
    }
}
