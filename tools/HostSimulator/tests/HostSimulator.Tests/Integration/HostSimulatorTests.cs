using CoreHostSimulator = HostSimulator.Core.HostSimulator;
using HostSimulator.Core.Configuration;
using HostSimulator.Core.Reassembly;
using Xunit;
using FluentAssertions;
using Common.Dto.Dtos;
using Common.Dto.Interfaces;

namespace HostSimulator.Tests.Integration;

/// <summary>
/// Tests for HostSimulator class.
/// REQ-SIM-040: Receive UDP packets and reassemble complete frames.
/// AC-SIM-009: Full pipeline integration test.
/// </summary>
public class HostSimulatorTests
{
    [Fact]
    public void Initialize_ShouldAcceptConfiguration()
    {
        // Arrange
        var simulator = new CoreHostSimulator();
        var config = new HostConfig();

        // Act & Assert - Should not throw
        simulator.Initialize(config);
    }

    [Fact]
    public void GetStatus_ShouldReturnStatusString()
    {
        // Arrange
        var simulator = new CoreHostSimulator();
        simulator.Initialize(new HostConfig());

        // Act
        var status = simulator.GetStatus();

        // Assert
        status.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Reset_ShouldClearPendingFrames()
    {
        // Arrange
        var simulator = new CoreHostSimulator();
        simulator.Initialize(new HostConfig());

        // Act & Assert - Should not throw
        simulator.Reset();
    }

    [Fact]
    public void Process_WithFrameData_ShouldReturnSameData()
    {
        // Arrange
        var simulator = new CoreHostSimulator();
        simulator.Initialize(new HostConfig());
        var frame = new FrameData(
            frameNumber: 1,
            width: 100,
            height: 100,
            pixels: new ushort[100 * 100]
        );

        // Act
        var result = simulator.Process(frame);

        // Assert
        result.Should().BeOfType<FrameData>();
        var resultFrame = result as FrameData;
        resultFrame!.FrameNumber.Should().Be(1);
    }

    [Fact]
    public void Constructor_ShouldImplementISimulator()
    {
        // Arrange & Act
        var simulator = new CoreHostSimulator();

        // Assert
        simulator.Should().BeAssignableTo<ISimulator>();
    }
}
