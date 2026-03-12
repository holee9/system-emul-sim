using Xunit;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// xUnit fact attribute with retry metadata for flaky E2E tests.
/// SPEC-HELP-001: REQ-HELP-056
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
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Runs an async action with retry on exception.
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
