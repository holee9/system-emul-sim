using FlaUI.Core.AutomationElements;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Async polling wait utilities for E2E tests.
/// SPEC-HELP-001: REQ-HELP-051
/// SPEC-E2E-002: REQ-E2E2-004 (WaitForElementAsync with tree dump on timeout)
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

    /// <summary>
    /// Waits for an element found by <paramref name="finder"/> to become non-null.
    /// On timeout, dumps the UIAutomation tree via <paramref name="logger"/> if provided.
    /// SPEC-E2E-002: REQ-E2E2-004
    /// </summary>
    public static async Task<AutomationElement?> WaitForElementAsync(
        AutomationElement root,
        Func<AutomationElement?> finder,
        int timeoutMs = 5000,
        int pollIntervalMs = 100,
        E2ELogger? logger = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var el = finder();
            if (el != null) return el;
            await Task.Delay(pollIntervalMs);
        }
        logger?.Fail($"WaitForElement timed out after {timeoutMs}ms. Tree dump:\n{TreeDumper.Dump(root)}");
        return null;
    }

    /// <summary>Waits for a specified duration.</summary>
    public static Task DelayAsync(int ms = 500) => Task.Delay(ms);
}
