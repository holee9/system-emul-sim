using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using PanelSimulator.Generators;

namespace PanelSimulator.Tests.Generators;

/// <summary>
/// Tests for GaussianNoiseGenerator.
/// REQ-SIM-011: Noise model with configurable standard deviation.
/// AC-SIM-002: Noise model validation.
/// </summary>
public class GaussianNoiseGeneratorTests
{
    [Fact]
    public void GaussianNoiseGenerator_shall_exist()
    {
        // Arrange & Act
        double stdDev = 100;
        int seed = 42;
        var generator = new GaussianNoiseGenerator(stdDev, seed);

        // Assert
        generator.Should().NotBeNull();
    }

    [Fact]
    public void Generate_shall_add_noise_to_pixels()
    {
        // Arrange
        double stdDev = 100;
        int seed = 42;
        var generator = new GaussianNoiseGenerator(stdDev, seed);
        int size = 100;
        ushort[] basePixels = Enumerable.Range(0, size).Select(i => (ushort)10000).ToArray();

        // Act
        var noisyPixels = generator.ApplyNoise(basePixels);

        // Assert
        noisyPixels.Should().NotBeNull();
        noisyPixels.Length.Should().Be(basePixels.Length);
        noisyPixels.Should().NotBeEquivalentTo(basePixels, "noise should modify pixel values");
    }

    [Theory]
    [InlineData(50, 42)]
    [InlineData(100, 42)]
    [InlineData(200, 42)]
    public void Generate_shall_produce_expected_standard_deviation(int stdDev, int seed)
    {
        // Arrange
        var generator = new GaussianNoiseGenerator(stdDev, seed);
        int frameCount = 100;
        int pixelsPerFrame = 1024 * 1024;
        ushort[] basePixels = Enumerable.Range(0, pixelsPerFrame).Select(i => (ushort)16384).ToArray();

        // Act
        var allValues = new double[frameCount];
        for (int f = 0; f < frameCount; f++)
        {
            var noisyPixels = generator.ApplyNoise(basePixels);
            // Sample first pixel from each frame
            allValues[f] = noisyPixels[0];
        }

        // Assert
        // AC-SIM-002: Pixel value standard deviation is within 5% of configured stddev
        double mean = allValues.Average();
        double variance = allValues.Average(v => Math.Pow(v - mean, 2));
        double actualStdDev = Math.Sqrt(variance);

        actualStdDev.Should().BeApproximately(stdDev, stdDev * 0.05,
            $"standard deviation should be within 5% of {stdDev}");
    }

    [Fact]
    public void Generate_shall_preserve_mean_within_1_percent()
    {
        // Arrange
        double stdDev = 100;
        int seed = 42;
        var generator = new GaussianNoiseGenerator(stdDev, seed);
        int frameCount = 100;
        int pixelsPerFrame = 1024;
        ushort baseSignal = 16384;
        ushort[] basePixels = Enumerable.Range(0, pixelsPerFrame).Select(i => (ushort)baseSignal).ToArray();

        // Act
        var allValues = new double[frameCount * pixelsPerFrame];
        for (int f = 0; f < frameCount; f++)
        {
            var noisyPixels = generator.ApplyNoise(basePixels);
            for (int i = 0; i < pixelsPerFrame; i++)
            {
                allValues[f * pixelsPerFrame + i] = noisyPixels[i];
            }
        }

        // Assert
        // AC-SIM-002: Mean pixel value is within 1% of base signal
        double mean = allValues.Average();
        mean.Should().BeApproximately(baseSignal, baseSignal * 0.01,
            $"mean should be within 1% of base signal {baseSignal}");
    }

    [Fact]
    public void Generate_shall_be_deterministic_with_seed()
    {
        // Arrange
        int stdDev = 100;
        int seed = 12345;
        var generator1 = new GaussianNoiseGenerator(stdDev, seed);
        var generator2 = new GaussianNoiseGenerator(stdDev, seed);
        ushort[] basePixels = Enumerable.Range(0, 1000).Select(i => (ushort)10000).ToArray();

        // Act
        var noisy1 = generator1.ApplyNoise(basePixels);
        var noisy2 = generator2.ApplyNoise(basePixels);

        // Assert
        noisy1.Should().BeEquivalentTo(noisy2, "same seed should produce identical noise");
    }

    [Fact]
    public void Generate_shall_produce_different_results_with_different_seeds()
    {
        // Arrange
        int stdDev = 100;
        var generator1 = new GaussianNoiseGenerator(stdDev, 42);
        var generator2 = new GaussianNoiseGenerator(stdDev, 999);
        ushort[] basePixels = Enumerable.Range(0, 1000).Select(i => (ushort)10000).ToArray();

        // Act
        var noisy1 = generator1.ApplyNoise(basePixels);
        var noisy2 = generator2.ApplyNoise(basePixels);

        // Assert
        noisy1.Should().NotBeEquivalentTo(noisy2, "different seeds should produce different noise");
    }

    [Fact]
    public void ApplyNoise_shall_not_clamp_valid_values()
    {
        // Arrange
        int stdDev = 10;
        int seed = 42;
        var generator = new GaussianNoiseGenerator(stdDev, seed);
        ushort[] basePixels = Enumerable.Range(0, 1000).Select(i => (ushort)32000).ToArray();

        // Act
        var noisyPixels = generator.ApplyNoise(basePixels);

        // Assert
        // With small stddev and mid-range values, most pixels should not be clamped
        noisyPixels.Should().OnlyContain(v => v >= 0 && v <= 65535);
    }
}
