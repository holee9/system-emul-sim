using Xunit;
using FluentAssertions;
using Common.Dto.Interfaces;
using Common.Dto.Dtos;
using PanelSimulator;
using PanelSimulator.Models;
using FpgaSimulator.Core.Fsm;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-03 through IT-10: Additional integration test scenarios.
/// </summary>
public class IT03_IntegrationScenariosTests
{
    [Fact]
    public void IT03_Pipeline_ShallHandleConfigurationChanges()
    {
        // Arrange - Start with one configuration
        var panelSimulator = new PanelSimulator.PanelSimulator();
        var config1 = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 14,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };
        panelSimulator.Initialize(config1);

        // Act - Generate frame, then change configuration
        var frame1 = panelSimulator.Process(null) as FrameData;
        frame1!.Width.Should().Be(256);

        // Reinitialize with different config
        var config2 = new PanelConfig
        {
            Rows = 512,
            Cols = 512,
            BitDepth = 16,
            TestPattern = TestPattern.Checkerboard,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };
        panelSimulator.Initialize(config2);
        var frame2 = panelSimulator.Process(null) as FrameData;

        // Assert - New configuration should be applied
        frame2!.Width.Should().Be(512);
        frame2.Height.Should().Be(512);
    }

    [Fact]
    public void IT04_Pipeline_ShallSupportMultipleTestPatterns()
    {
        // Arrange
        var patterns = new[] { TestPattern.Counter, TestPattern.Checkerboard };
        var panelSimulator = new PanelSimulator.PanelSimulator();

        foreach (var pattern in patterns)
        {
            // Act
            var config = new PanelConfig
            {
                Rows = 256,
                Cols = 256,
                BitDepth = 16,
                TestPattern = pattern,
                NoiseModel = NoiseModelType.None,
                DefectRate = 0,
                Seed = 42
            };
            panelSimulator.Initialize(config);
            var frame = panelSimulator.Process(null) as FrameData;

            // Assert
            frame.Should().NotBeNull();
            frame!.Pixels.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void IT05_Pipeline_ShallMaintainFrameNumberSequence()
    {
        // Arrange
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

        var panelSimulator = new PanelSimulator.PanelSimulator();
        panelSimulator.Initialize(config);

        // Act - Generate multiple frames
        var frames = new List<FrameData>();
        for (int i = 0; i < 10; i++)
        {
            var frame = panelSimulator.Process(null) as FrameData;
            frames.Add(frame!);
        }

        // Assert - Frame numbers should be sequential
        for (int i = 0; i < frames.Count; i++)
        {
            frames[i].FrameNumber.Should().Be(i);
        }
    }

    [Fact]
    public void IT06_Pipeline_ShallResetToInitialState()
    {
        // Arrange
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

        var panelSimulator = new PanelSimulator.PanelSimulator();
        panelSimulator.Initialize(config);

        // Act - Generate frames, reset, generate more
        var frame1 = panelSimulator.Process(null) as FrameData;
        var frame2 = panelSimulator.Process(null) as FrameData;
        panelSimulator.Reset();
        var frame3 = panelSimulator.Process(null) as FrameData;

        // Assert - After reset, frame number should start from 0
        frame1!.FrameNumber.Should().Be(0);
        frame2!.FrameNumber.Should().Be(1);
        frame3!.FrameNumber.Should().Be(0);
    }

    [Fact]
    public void IT07_Pipeline_ShallSupportDifferentBitDepths()
    {
        // Arrange
        var bitDepths = new[] { 12, 14, 16 };
        var panelSimulator = new PanelSimulator.PanelSimulator();

        foreach (var bitDepth in bitDepths)
        {
            // Act
            var config = new PanelConfig
            {
                Rows = 256,
                Cols = 256,
                BitDepth = bitDepth,
                TestPattern = TestPattern.Counter,
                NoiseModel = NoiseModelType.None,
                DefectRate = 0,
                Seed = 42
            };
            panelSimulator.Initialize(config);
            var frame = panelSimulator.Process(null) as FrameData;

            // Assert - Verify clamping
            int maxValue = (1 << bitDepth) - 1;
            frame!.Pixels.Should().OnlyContain(p => p <= maxValue,
                $"Bit depth {bitDepth} should clamp to {maxValue}");
        }
    }

    [Fact]
    public void IT08_Pipeline_ShallProvideStatusInformation()
    {
        // Arrange
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

        var panelSimulator = new PanelSimulator.PanelSimulator();
        panelSimulator.Initialize(config);

        // Act
        var status = panelSimulator.GetStatus();

        // Assert - Status should contain key information
        status.Should().NotBeNullOrEmpty();
        status.Should().Contain("Ready");
        status.Should().Contain("1024"); // Height
        status.Should().Contain("2048"); // Width
        status.Should().Contain("16");   // Bit depth
    }

    [Fact]
    public void IT09_Pipeline_ShallBeDeterministicWithSameSeed()
    {
        // Arrange - Two simulators, same seed
        var config = new PanelConfig
        {
            Rows = 512,
            Cols = 512,
            BitDepth = 14,
            TestPattern = TestPattern.Checkerboard,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 99999
        };

        var sim1 = new PanelSimulator.PanelSimulator();
        var sim2 = new PanelSimulator.PanelSimulator();
        sim1.Initialize(config);
        sim2.Initialize(config);

        // Act - Generate 10 frames from each
        var frames1 = new List<FrameData>();
        var frames2 = new List<FrameData>();
        for (int i = 0; i < 10; i++)
        {
            frames1.Add(sim1.Process(null) as FrameData);
            frames2.Add(sim2.Process(null) as FrameData);
        }

        // Assert - All frames should be identical
        for (int i = 0; i < 10; i++)
        {
            frames1[i]!.Pixels.Should().Equal(frames2[i]!.Pixels,
                $"Frame {i} should be identical with same seed");
        }
    }

    [Fact]
    public void IT10_Pipeline_ShallHandleMinimumTierConfiguration()
    {
        // Arrange - Minimum tier: 1024x1024, 14-bit, 15fps
        var config = new PanelConfig
        {
            Rows = 1024,
            Cols = 1024,
            BitDepth = 14,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };

        var panelSimulator = new PanelSimulator.PanelSimulator();
        panelSimulator.Initialize(config);

        // Act - Generate frame
        var frame = panelSimulator.Process(null) as FrameData;

        // Assert - Verify minimum tier specs
        frame!.Width.Should().Be(1024);
        frame.Height.Should().Be(1024);
        frame.Pixels.Length.Should().Be(1024 * 1024);

        // Verify 14-bit clamping
        int maxValue = (1 << 14) - 1; // 16383
        frame.Pixels.Should().OnlyContain(p => p <= maxValue);
    }
}
