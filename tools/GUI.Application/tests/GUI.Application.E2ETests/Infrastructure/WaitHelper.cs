using FlaUI.Core.AutomationElements;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Async polling wait utilities for E2E tests.
/// SPEC-HELP-001: REQ-HELP-051
/// SPEC-E2E-002: REQ-E2E2-004 (WaitForElementAsync with tree dump on timeout)
/// TAG-003: Added description parameter, progress logging, env-var timeout override.
/// </summary>
public static class WaitHelper
{
    /// <summary>
    /// Reads XRAY_E2E_TIMEOUT_MS to override the default timeout (opt-in).
    /// Returns <paramref name="defaultMs"/> if the env var is absent or invalid.
    /// </summary>
    private static int ResolveTimeout(int defaultMs)
    {
        var raw = Environment.GetEnvironmentVariable("XRAY_E2E_TIMEOUT_MS");
        if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out var overrideMs) && overrideMs > 0)
            return overrideMs;
        return defaultMs;
    }

    /// <summary>
    /// Waits until <paramref name="condition"/> is true or timeout expires.
    /// Logs progress every 10 attempts when <paramref name="description"/> is provided.
    /// </summary>
    public static async Task<bool> WaitUntilAsync(
        Func<bool> condition,
        int timeoutMs = 5000,
        int pollIntervalMs = 100,
        string description = "condition")
    {
        var resolvedTimeout = ResolveTimeout(timeoutMs);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int attempt = 0;
        while (sw.ElapsedMilliseconds < resolvedTimeout)
        {
            if (condition()) return true;
            attempt++;
            if (attempt % 10 == 0)
                System.Diagnostics.Trace.WriteLine(
                    $"[WaitHelper] '{description}' attempt {attempt}, elapsed {sw.ElapsedMilliseconds}ms");
            await Task.Delay(pollIntervalMs);
        }
        System.Diagnostics.Trace.WriteLine(
            $"[WaitHelper] '{description}' timed out after {resolvedTimeout}ms ({attempt} attempts)");
        return false;
    }

    /// <summary>
    /// Waits for an element found by <paramref name="finder"/> to become non-null.
    /// On timeout, dumps the UIAutomation tree via <paramref name="logger"/> if provided.
    /// SPEC-E2E-002: REQ-E2E2-004
    /// TAG-003: Added description parameter for diagnostic logging.
    /// </summary>
    public static async Task<AutomationElement?> WaitForElementAsync(
        AutomationElement root,
        Func<AutomationElement?> finder,
        int timeoutMs = 5000,
        int pollIntervalMs = 100,
        E2ELogger? logger = null,
        string description = "element")
    {
        var resolvedTimeout = ResolveTimeout(timeoutMs);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int attempt = 0;
        while (sw.ElapsedMilliseconds < resolvedTimeout)
        {
            var el = finder();
            if (el != null) return el;
            attempt++;
            if (attempt % 10 == 0)
                logger?.Info($"[WaitHelper] '{description}' attempt {attempt}, elapsed {sw.ElapsedMilliseconds}ms");
            await Task.Delay(pollIntervalMs);
        }
        logger?.Fail($"WaitForElement '{description}' timed out after {resolvedTimeout}ms. Tree dump:\n{TreeDumper.Dump(root)}");
        return null;
    }

    /// <summary>Waits for a specified duration.</summary>
    public static Task DelayAsync(int ms = 500) => Task.Delay(ms);
}
