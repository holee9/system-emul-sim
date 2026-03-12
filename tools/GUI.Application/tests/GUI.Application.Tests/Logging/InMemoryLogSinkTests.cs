using System.Collections.Concurrent;
using Serilog.Events;
using XrayDetector.Gui.Logging;

namespace XrayDetector.Gui.Tests.Logging;

/// <summary>
/// Tests for InMemoryLogSink - thread-safe ConcurrentQueue-based log sink.
/// </summary>
public class InMemoryLogSinkTests
{
    private readonly InMemoryLogSink _sut = new();

    private static LogEvent CreateLogEvent(
        LogEventLevel level = LogEventLevel.Information,
        string messageTemplate = "Test {Category}",
        string category = "XrayDetector.Gui.Test")
    {
        var template = new Serilog.Events.MessageTemplate(
            messageTemplate,
            [new Serilog.Parsing.TextToken("Test "), new Serilog.Parsing.PropertyToken("Category", "{Category}")]);

        var props = new Dictionary<string, LogEventPropertyValue>
        {
            ["Category"] = new ScalarValue(category),
            ["SourceContext"] = new ScalarValue(category)
        };

        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            null,
            template,
            props.Select(kv => new LogEventProperty(kv.Key, kv.Value)));
    }

    [Fact]
    public void Emit_ShouldStoreEvents()
    {
        // Arrange
        var evt = CreateLogEvent();

        // Act
        _sut.Emit(evt);

        // Assert
        _sut.GetEvents().Should().HaveCount(1);
    }

    [Fact]
    public void GetEvents_WithNoFilter_ShouldReturnAllEvents()
    {
        // Arrange
        _sut.Emit(CreateLogEvent(LogEventLevel.Information, "Msg1", "XrayDetector.Gui.UI"));
        _sut.Emit(CreateLogEvent(LogEventLevel.Warning, "Msg2", "XrayDetector.Gui.Pipeline"));
        _sut.Emit(CreateLogEvent(LogEventLevel.Error, "Msg3", "XrayDetector.Gui.App"));

        // Act
        var events = _sut.GetEvents();

        // Assert
        events.Should().HaveCount(3);
    }

    [Fact]
    public void GetEvents_FilterByCategory_ShouldReturnMatchingEvents()
    {
        // Arrange
        _sut.Emit(CreateLogEvent(LogEventLevel.Information, "Msg1", "XrayDetector.Gui.UI"));
        _sut.Emit(CreateLogEvent(LogEventLevel.Warning, "Msg2", "XrayDetector.Gui.Pipeline"));
        _sut.Emit(CreateLogEvent(LogEventLevel.Error, "Msg3", "XrayDetector.Gui.UI"));

        // Act
        var events = _sut.GetEvents(category: "XrayDetector.Gui.UI");

        // Assert
        events.Should().HaveCount(2);
    }

    [Fact]
    public void GetEvents_FilterByMinLevel_ShouldReturnMatchingEvents()
    {
        // Arrange
        _sut.Emit(CreateLogEvent(LogEventLevel.Debug));
        _sut.Emit(CreateLogEvent(LogEventLevel.Information));
        _sut.Emit(CreateLogEvent(LogEventLevel.Warning));
        _sut.Emit(CreateLogEvent(LogEventLevel.Error));

        // Act
        var events = _sut.GetEvents(minLevel: LogEventLevel.Warning);

        // Assert
        events.Should().HaveCount(2);
        events.All(e => e.Level >= LogEventLevel.Warning).Should().BeTrue();
    }

    [Fact]
    public void Clear_ShouldEmptyQueue()
    {
        // Arrange
        _sut.Emit(CreateLogEvent());
        _sut.Emit(CreateLogEvent());
        _sut.GetEvents().Should().HaveCount(2);

        // Act
        _sut.Clear();

        // Assert
        _sut.GetEvents().Should().BeEmpty();
    }

    [Fact]
    public void Emit_WhenAtCapacity_ShouldDropOldestEvents()
    {
        // Arrange - fill to capacity
        const int capacity = 10_000;
        for (int i = 0; i < capacity; i++)
        {
            _sut.Emit(CreateLogEvent(LogEventLevel.Information, $"Message {i}"));
        }

        // Act - add one more
        _sut.Emit(CreateLogEvent(LogEventLevel.Error, "Overflow event"));

        // Assert - should not exceed capacity
        _sut.GetEvents().Should().HaveCount(capacity);
    }

    [Fact]
    public async Task Emit_ConcurrentWriters_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int eventsPerThread = 100;
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int j = 0; j < eventsPerThread; j++)
            {
                _sut.Emit(CreateLogEvent(LogEventLevel.Information, $"Thread {i} Msg {j}"));
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert - all events stored (up to capacity)
        var events = _sut.GetEvents();
        events.Should().HaveCountGreaterThan(0);
        events.Count.Should().BeLessOrEqualTo(10_000);
    }
}
