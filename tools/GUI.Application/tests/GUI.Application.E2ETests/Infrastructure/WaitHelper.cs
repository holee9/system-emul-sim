namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Async polling wait utilities for E2E tests.
/// SPEC-HELP-001: REQ-HELP-051
/// </summary>
public static class WaitHelper
{
    /// <summary>Waits until condition is true or timeout expires.</summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000, int pollIntervalMs = 100)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            await Task.Delay(pollIntervalMs);
        }
        return false;
    }

    /// <summary>Waits for a specified duration.</summary>
    public static Task DelayAsync(int ms = 500) => Task.Delay(ms);
}
