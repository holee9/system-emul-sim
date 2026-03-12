using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace XrayDetector.Gui.Logging;

/// <summary>
/// Thread-safe in-memory log sink using ConcurrentQueue.
/// Stores up to 10,000 log events with FIFO overflow behavior.
/// Used for E2E test assertion helpers (SPEC-HELP-001).
/// </summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private const int MaxCapacity = 10_000;

    private readonly ConcurrentQueue<LogEvent> _events = new();

    /// <summary>
    /// Emits a log event to the in-memory queue.
    /// When capacity is reached, the oldest event is discarded (FIFO overflow).
    /// </summary>
    public void Emit(LogEvent logEvent)
    {
        _events.Enqueue(logEvent);

        // Enforce capacity limit - drop oldest when over capacity (one-for-one: enqueue one, dequeue one)
        if (_events.Count > MaxCapacity)
            _events.TryDequeue(out _);
    }

    /// <summary>
    /// Returns stored log events, optionally filtered by category and minimum level.
    /// </summary>
    /// <param name="category">Optional SourceContext category prefix to filter by.</param>
    /// <param name="minLevel">Optional minimum log level filter.</param>
    /// <returns>Filtered list of log events.</returns>
    public IReadOnlyList<LogEvent> GetEvents(string? category = null, LogEventLevel? minLevel = null)
    {
        IEnumerable<LogEvent> query = _events;

        if (category is not null)
        {
            query = query.Where(e =>
                e.Properties.TryGetValue("SourceContext", out var ctx) &&
                ctx is ScalarValue sv &&
                sv.Value is string s &&
                s.StartsWith(category, StringComparison.Ordinal));
        }

        if (minLevel.HasValue)
        {
            query = query.Where(e => e.Level >= minLevel.Value);
        }

        return query.ToList();
    }

    /// <summary>
    /// Clears all stored log events.
    /// </summary>
    public void Clear()
    {
        _events.Clear();
    }
}
