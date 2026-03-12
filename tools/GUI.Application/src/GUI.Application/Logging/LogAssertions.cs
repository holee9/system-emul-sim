using Serilog.Events;

namespace XrayDetector.Gui.Logging;

/// <summary>
/// E2E test assertion helpers for log verification (SPEC-HELP-001).
/// Requires E2E mode to be active via LoggingBootstrap.Initialize(isE2EMode: true).
/// </summary>
public static class LogAssertions
{
    /// <summary>
    /// Asserts that no Error or Fatal level events exist in the in-memory log.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when E2E mode is not active or errors are found.</exception>
    public static void ShouldHaveNoErrors()
    {
        var sink = GetSinkOrThrow();
        var errors = sink.GetEvents(minLevel: LogEventLevel.Error);

        if (errors.Count > 0)
        {
            var messages = string.Join(Environment.NewLine,
                errors.Select(e => $"[{e.Level}] {e.RenderMessage()}"));

            throw new InvalidOperationException(
                $"Expected no Error/Fatal log events, but found {errors.Count}:{Environment.NewLine}{messages}");
        }
    }

    /// <summary>
    /// Polls the in-memory log until an event matching the specified substring is found,
    /// or the timeout elapses.
    /// </summary>
    /// <param name="contains">Substring to search for in rendered log messages.</param>
    /// <param name="timeoutMs">Polling timeout in milliseconds (default: 5000).</param>
    /// <returns>True if found within timeout, false otherwise.</returns>
    public static bool ShouldEventuallyLog(string contains, int timeoutMs = 5_000)
    {
        var sink = GetSinkOrThrow();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            var found = sink.GetEvents()
                .Any(e => e.RenderMessage().Contains(contains, StringComparison.OrdinalIgnoreCase));

            if (found)
                return true;

            Thread.Sleep(50);
        }

        return false;
    }

    private static InMemoryLogSink GetSinkOrThrow()
    {
        return LoggingBootstrap.InMemorySink
            ?? throw new InvalidOperationException(
                "E2E mode is not active. Call LoggingBootstrap.Initialize(isE2EMode: true) before using LogAssertions.");
    }
}
