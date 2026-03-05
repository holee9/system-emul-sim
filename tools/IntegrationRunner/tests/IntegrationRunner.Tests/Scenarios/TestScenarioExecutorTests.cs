using FluentAssertions;
using IntegrationRunner.Core;
using IntegrationRunner.Core.Models;
using Xunit;

namespace IntegrationRunner.Tests.Scenarios;

/// <summary>
/// Tests for TestScenarioExecutor.
/// TDD: RED-GREEN-REFACTOR cycle.
/// REQ-TOOLS-032: Report pass/fail with metrics.
/// </summary>
public class TestScenarioExecutorTests
{
    [Fact]
    public void Constructor_ShouldCreateExecutor()
    {
        // Arrange & Act
        var executor = new TestScenarioExecutor();

        // Assert
        executor.Should().NotBeNull();
    }

    [Fact]
    public void ExecuteScenario_IT01_ShouldReturnTestResult()
    {
        // Arrange
        var executor = new TestScenarioExecutor();
        var config = GetDefaultConfig();

        // Act
        var result = executor.ExecuteScenario(TestScenario.IT01_SingleFrameMinimum, config);

        // Assert
        result.Should().NotBeNull();
        result.Scenario.Should().Be(TestScenario.IT01_SingleFrameMinimum);
        result.Status.Should().BeOneOf(TestStatus.Passed, TestStatus.Failed);
        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ExecuteScenario_IT02_ShouldReturnTestResult()
    {
        // Arrange
        var executor = new TestScenarioExecutor();
        var config = GetDefaultConfig();

        // Act
        var result = executor.ExecuteScenario(TestScenario.IT02_1000FrameContinuous, config);

        // Assert
        result.Should().NotBeNull();
        result.Scenario.Should().Be(TestScenario.IT02_1000FrameContinuous);
        result.FramesProcessed.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExecuteAllScenarios_ShouldReturnAggregateResults()
    {
        // Arrange
        var executor = new TestScenarioExecutor();
        var config = GetDefaultConfig();

        // Act
        var results = executor.ExecuteAllScenarios(config);

        // Assert
        results.Should().NotBeNull();
        results.TotalTests.Should().BeGreaterThan(0);
        results.TestResults.Should().NotBeEmpty();
        results.TotalExecutionTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExecuteAllScenarios_ShouldIncludeAllScenarios()
    {
        // Arrange
        var executor = new TestScenarioExecutor();
        var config = GetDefaultConfig();

        // Act
        var results = executor.ExecuteAllScenarios(config);

        // Assert
        results.TestResults.Should().HaveCountGreaterOrEqualTo(10); // IT-01 through IT-10
    }

    [Fact]
    public void TestResult_GetSummary_ShouldReturnFormattedString()
    {
        // Arrange
        var result = new TestResult
        {
            Scenario = TestScenario.IT01_SingleFrameMinimum,
            Status = TestStatus.Passed,
            ExecutionTimeMs = 1000,
            FramesProcessed = 1,
            BitErrors = 0,
            FrameDrops = 0,
            ThroughputGbps = 0.5
        };

        // Act
        var summary = result.GetSummary();

        // Assert
        summary.Should().Contain("[PASS]");
        summary.Should().Contain("IT01");
        summary.Should().Contain("Frames: 1");
        summary.Should().Contain("Errors: 0");
    }

    [Fact]
    public void AggregateResults_AllPassed_ShouldReturnTrue()
    {
        // Arrange
        var results = new AggregateResults
        {
            TotalTests = 10,
            PassedTests = 10,
            FailedTests = 0,
            SkippedTests = 0
        };

        // Act & Assert
        results.AllPassed.Should().BeTrue();
        results.PassRate.Should().Be(100.0);
    }

    [Fact]
    public void AggregateResults_WithFailures_ShouldReturnFalse()
    {
        // Arrange
        var results = new AggregateResults
        {
            TotalTests = 10,
            PassedTests = 8,
            FailedTests = 2,
            SkippedTests = 0
        };

        // Act & Assert
        results.AllPassed.Should().BeFalse();
        results.PassRate.Should().Be(80.0);
    }

    private static DetectorConfig GetDefaultConfig()
    {
        return new DetectorConfig
        {
            Panel = new PanelConfig
            {
                Rows = 1024,
                Cols = 1024,
                BitDepth = 14,
                PixelPitchUm = 100.0
            },
            Fpga = new FpgaConfig
            {
                Csi2Lanes = 4,
                Csi2DataRateMbps = 400,
                LineBufferDepth = 2048
            },
            Soc = new SocConfig
            {
                EthernetPort = 8000,
                UdpPort = 8000,
                TcpPort = 8001,
                FrameBufferCount = 4
            },
            Host = new HostConfig
            {
                IpAddress = "127.0.0.1",
                PacketTimeoutMs = 1000,
                ReceiveThreads = 2
            },
            Simulation = new SimulationConfig
            {
                Mode = "fast",
                Seed = 42,
                TestPattern = "counter",
                NoiseStdDev = 0
            }
        };
    }
}
