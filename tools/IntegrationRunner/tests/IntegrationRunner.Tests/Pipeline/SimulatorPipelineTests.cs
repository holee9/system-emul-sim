using FluentAssertions;
using IntegrationRunner.Core;
using IntegrationRunner.Core.Models;
using Xunit;

namespace IntegrationRunner.Tests.Pipeline;

/// <summary>
/// Tests for SimulatorPipeline.
/// TDD: RED-GREEN-REFACTOR cycle.
/// REQ-TOOLS-031: Instantiate all required simulators, connect in pipeline order.
/// </summary>
public class SimulatorPipelineTests
{
    [Fact]
    public void Constructor_ShouldCreatePipeline()
    {
        // Arrange & Act
        var pipeline = new SimulatorPipeline();

        // Assert
        pipeline.Should().NotBeNull();
        pipeline.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void Initialize_WithValidConfig_ShouldSetInitialized()
    {
        // Arrange
        var pipeline = new SimulatorPipeline();
        var config = GetDefaultConfig();

        // Act
        pipeline.Initialize(config);

        // Assert
        pipeline.IsInitialized.Should().BeTrue();
        pipeline.Config.Should().Be(config);
    }

    [Fact]
    public void Initialize_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange
        var pipeline = new SimulatorPipeline();

        // Act
        var act = () => pipeline.Initialize(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProcessFrame_WhenNotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pipeline = new SimulatorPipeline();

        // Act
        var act = () => pipeline.ProcessFrame();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public void ProcessFrame_AfterInitialization_ShouldReturnFrameData()
    {
        // Arrange
        var pipeline = new SimulatorPipeline();
        var config = GetDefaultConfig();
        pipeline.Initialize(config);

        // Act
        var result = pipeline.ProcessFrame();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void ProcessFrames_WithValidCount_ShouldReturnCorrectNumberOfFrames()
    {
        // Arrange
        var pipeline = new SimulatorPipeline();
        var config = GetDefaultConfig();
        pipeline.Initialize(config);
        const int frameCount = 10;

        // Act
        var frames = pipeline.ProcessFrames(frameCount);

        // Assert
        frames.Should().HaveCount(frameCount);
    }

    [Fact]
    public void Reset_AfterInitialization_ShouldClearState()
    {
        // Arrange
        var pipeline = new SimulatorPipeline();
        var config = GetDefaultConfig();
        pipeline.Initialize(config);
        pipeline.ProcessFrame();

        // Act
        pipeline.Reset();

        // Assert - Should not throw and pipeline should still be usable
        var result = pipeline.ProcessFrame();
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetPipelineStatus_AfterInitialization_ShouldReturnStatusString()
    {
        // Arrange
        var pipeline = new SimulatorPipeline();
        var config = GetDefaultConfig();
        pipeline.Initialize(config);

        // Act
        var status = pipeline.GetPipelineStatus();

        // Assert
        status.Should().NotBeNullOrEmpty();
        status.Should().Contain("Panel");
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
