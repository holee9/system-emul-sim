using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Tests.Unit;

/// <summary>
/// Unit tests for E2ELogger AsyncLocal ITestOutputHelper bridge.
/// TAG-002: Tests must pass without desktop/GUI.
/// </summary>
public sealed class E2ELoggerBridgeTests
{
    [Fact]
    public void Log_WhenTestOutputSet_WritesToTestOutput()
    {
        var collector = new TestOutputCollector();
        E2ELogger.SetTestOutput(collector);
        try
        {
            using var logger = new E2ELogger();
            logger.Info("bridge-test-message");

            collector.Lines.Should().ContainMatch("*bridge-test-message*");
        }
        finally
        {
            E2ELogger.ClearTestOutput();
        }
    }

    [Fact]
    public void Log_WhenNoOutputSet_DoesNotThrow()
    {
        E2ELogger.ClearTestOutput();
        var act = () =>
        {
            using var logger = new E2ELogger();
            logger.Info("no-output-message");
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void ClearTestOutput_AfterSet_StopsWriting()
    {
        var collector = new TestOutputCollector();
        E2ELogger.SetTestOutput(collector);
        E2ELogger.ClearTestOutput();

        using var logger = new E2ELogger();
        logger.Info("should-not-appear");

        collector.Lines.Should().BeEmpty("output was cleared before logging");
    }

    /// <summary>Test double for ITestOutputHelper that collects written lines.</summary>
    private sealed class TestOutputCollector : ITestOutputHelper
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
