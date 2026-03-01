using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using Common.Dto.Dtos;
using PanelSimulator.Models;
using Simulator = PanelSimulator.PanelSimulator;

namespace PanelSimulator.Tests.Generators;

/// <summary>
/// Statistical tests for PanelSimulator pixel output.
/// Validates noise characteristics, defect rates, and determinism.
/// REQ-SIM-011: Noise model with configurable standard deviation.
/// REQ-SIM-012: Defect injection with configurable defect rate.
/// REQ-SIM-013: Counter mode is deterministic and seed-reproducible.
/// </summary>
public class PanelSimulatorStatisticsTests
{
    [Fact]
    public void FlatField_AllPixelsIdentical_WhenNoNoise()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 128,
            Cols = 128,
            BitDepth = 16,
            TestPattern = TestPattern.FlatField,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var result = simulator.Process(null!) as FrameData;

        // Assert
        // FlatFieldPatternGenerator produces (1 << (bitDepth-1)) - 1 = 32767 for 16-bit
        ushort expectedValue = (ushort)((1 << (16 - 1)) - 1); // 32767
        result.Should().NotBeNull();
        result!.Pixels.Should().OnlyContain(p => p == expectedValue,
            "all pixels must equal the flat field base value of 32767 with no noise");
    }

    [Fact]
    public void FlatField_NoiseStatistics_MeanWithinTolerance()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 16,
            TestPattern = TestPattern.FlatField,
            NoiseModel = NoiseModelType.Gaussian,
            NoiseStdDev = 100,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var result = simulator.Process(null!) as FrameData;

        // Assert
        // Mean should remain within 100 of the base value (32767)
        double mean = result!.Pixels.Average(p => (double)p);
        double baseValue = (1 << (16 - 1)) - 1; // 32767
        mean.Should().BeInRange(baseValue - 100, baseValue + 100,
            "pixel mean should be within 100 of the flat field base value 32767 with Gaussian noise stddev=100");
    }

    [Fact]
    public void FlatField_NoiseStatistics_StdDevWithinTolerance()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 16,
            TestPattern = TestPattern.FlatField,
            NoiseModel = NoiseModelType.Gaussian,
            NoiseStdDev = 100,
            DefectRate = 0,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var result = simulator.Process(null!) as FrameData;

        // Assert
        // Computed standard deviation should be within 20% of configured StdDev (80-120)
        double mean = result!.Pixels.Average(p => (double)p);
        double variance = result.Pixels.Average(p => Math.Pow(p - mean, 2));
        double actualStdDev = Math.Sqrt(variance);

        actualStdDev.Should().BeInRange(80, 120,
            "computed standard deviation should be within 20% of configured StdDev (100), i.e. in range [80, 120]");
    }

    [Fact]
    public void Defects_RateMatchesConfiguration()
    {
        // Arrange
        var simulator = new Simulator();
        var config = new PanelConfig
        {
            Rows = 512,
            Cols = 512,
            BitDepth = 16,
            TestPattern = TestPattern.FlatField,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0.01,
            Seed = 42
        };
        simulator.Initialize(config);

        // Act
        var result = simulator.Process(null!) as FrameData;

        // Assert
        // Dead pixels == 0, hot pixels == 65535; defect ratio should be within 50% of target (0.005-0.015)
        int totalPixels = result!.Pixels.Length;
        int defectCount = result.Pixels.Count(p => p == 0 || p == 65535);
        double defectRatio = (double)defectCount / totalPixels;

        defectRatio.Should().BeInRange(0.005, 0.015,
            $"defect ratio {defectRatio:F4} should be within 50% of configured defect rate 0.01 (range 0.005-0.015)");
    }

    [Fact]
    public void Counter_DeterministicOutput_SameSeedSameResult()
    {
        // Arrange
        var config = new PanelConfig
        {
            Rows = 64,
            Cols = 64,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0,
            Seed = 42
        };

        var simulator1 = new Simulator();
        simulator1.Initialize(config);

        var simulator2 = new Simulator();
        simulator2.Initialize(config);

        // Act
        var result1 = simulator1.Process(null!) as FrameData;
        var result2 = simulator2.Process(null!) as FrameData;

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Pixels.Should().BeEquivalentTo(result2!.Pixels,
            "two separate simulator instances with the same seed and Counter pattern must produce identical pixel output");
    }
}
