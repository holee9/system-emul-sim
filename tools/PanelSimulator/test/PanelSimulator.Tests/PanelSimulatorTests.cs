using System;
using FluentAssertions;
using Xunit;
using Common.Dto.Interfaces;
using Common.Dto.Dtos;
using PanelSimulator.Models;
using Simulator = PanelSimulator.PanelSimulator;

namespace PanelSimulator.Tests;

/// <summary>
/// Tests for PanelSimulator main class.
/// REQ-SIM-001: Implements ISimulator interface.
/// REQ-SIM-002: Loads config from detector_config.yaml.
/// REQ-SIM-010: Generate 2D pixel matrix with configurable resolution and bit depth.
/// </summary>
public class PanelSimulatorTests
{
    [Fact]
    public void PanelSimulator_shall_exist()
    {
        // Arrange & Act
        var simulator = new Simulator();

        // Assert
        simulator.Should().NotBeNull();
    }

    [Fact]
    public void PanelSimulator_shall_implement_ISimulator()
    {
        // Arrange & Act
        var simulator = new Simulator();

        // Assert
        simulator.Should().BeAssignableTo<ISimulator>();
    }

    [Fact]
    public void Initialize_shall_accept_configuration()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 1024,
            Cols = 1024,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.Gaussian,
            NoiseStdDev = 100,
            DefectRate = 0.001,
            Seed = 42
        };

        // Act
        Action act = () => simulator.Initialize(config);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Initialize_shall_set_configuration()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 2048,
            Cols = 2048,
            BitDepth = 16,
            TestPattern = TestPattern.Checkerboard,
            NoiseModel = NoiseModelType.Gaussian,
            NoiseStdDev = 50,
            DefectRate = 0.0001,
            Seed = 123
        };

        // Act
        simulator.Initialize(config);
        var status = simulator.GetStatus();

        // Assert
        status.Should().Contain("2048");
        status.Should().Contain("16");
    }

    [Fact]
    public void Reset_shall_clear_state()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 1024,
            Cols = 1024,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        simulator.Reset();
        var status = simulator.GetStatus();

        // Assert
        status.Should().Contain("Ready");
    }

    [Fact]
    public void Process_shall_generate_FrameData()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 1024,
            Cols = 1024,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var result = simulator.Process(null!);

        // Assert
        result.Should().BeOfType<FrameData>();
        var frameData = result as FrameData;
        frameData.Should().NotBeNull();
        frameData!.Width.Should().Be(1024);
        frameData.Height.Should().Be(1024);
        frameData.Pixels.Length.Should().Be(1024 * 1024);
    }

    [Fact]
    public void Process_shall_generate_frame_with_expected_dimensions()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 512,
            Cols = 768,
            BitDepth = 14,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var result = simulator.Process(null!);

        // Assert
        var frameData = result as FrameData;
        frameData!.Width.Should().Be(768);
        frameData.Height.Should().Be(512);
        frameData.Pixels.Length.Should().Be(512 * 768);
    }

    [Fact]
    public void Process_shall_increment_frame_number()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var frame1 = simulator.Process(null) as FrameData;
        var frame2 = simulator.Process(null) as FrameData;
        var frame3 = simulator.Process(null) as FrameData;

        // Assert
        frame1!.FrameNumber.Should().Be(0);
        frame2!.FrameNumber.Should().Be(1);
        frame3!.FrameNumber.Should().Be(2);
    }

    [Fact]
    public void Process_shall_reset_frame_number_after_Reset()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);
        var frame1 = simulator.Process(null) as FrameData;

        // Act
        simulator.Reset();
        var frame2 = simulator.Process(null) as FrameData;

        // Assert
        frame1!.FrameNumber.Should().Be(0);
        frame2!.FrameNumber.Should().Be(0);
    }

    [Fact]
    public void Process_with_counter_pattern_shall_bypass_noise_and_defects()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.Gaussian,  // Should be bypassed
            NoiseStdDev = 100,
            DefectRate = 0.01,  // Should be bypassed
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var result = simulator.Process(null) as FrameData;

        // Assert
        // REQ-SIM-013: Counter mode bypasses noise and defect injection
        // Check for counter pattern values
        result!.Pixels[0].Should().Be(0);
        result.Pixels[1].Should().Be(1);
        result.Pixels[256].Should().Be(256);  // First pixel of second row
    }

    [Fact]
    public void Process_with_checkerboard_pattern_shall_alternate_max_zero()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 16,
            TestPattern = TestPattern.Checkerboard,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var result = simulator.Process(null) as FrameData;

        // Assert
        // REQ-SIM-014: Checkerboard pattern
        result!.Pixels[0].Should().Be(0);       // Even pixel in row 0
        result.Pixels[1].Should().Be(65535);    // Odd pixel in row 0
        result.Pixels[256].Should().Be(65535);  // Even pixel in row 1 (inverted)
        result.Pixels[257].Should().Be(0);      // Odd pixel in row 1
    }

    [Fact]
    public void Process_shall_clamp_pixels_to_bit_depth()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 14,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var result = simulator.Process(null) as FrameData;

        // Assert
        int maxValue = (1 << 14) - 1;  // 16383
        result!.Pixels.Should().OnlyContain(p => p <= maxValue);
    }

    [Fact]
    public void GetStatus_shall_return_current_state()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 1024,
            Cols = 2048,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.Gaussian,
            NoiseStdDev = 100,
            DefectRate = 0.001,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var status = simulator.GetStatus();

        // Assert
        status.Should().NotBeNullOrEmpty();
        status.Should().Contain("1024");
        status.Should().Contain("2048");
        status.Should().Contain("16");
    }

    [Fact]
    public void Process_without_initialize_shall_throw()
    {
        // Arrange
        var simulator = new Simulator();

        // Act
        Action act = () => simulator.Process(null!);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public void GetStatus_without_initialize_shall_return_not_initialized()
    {
        // Arrange
        var simulator = new Simulator();

        // Act
        var status = simulator.GetStatus();

        // Assert
        status.Should().Contain("Not Initialized");
    }
}
