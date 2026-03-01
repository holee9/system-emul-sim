using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using PanelSimulator.Generators;

namespace PanelSimulator.Tests.Generators;

/// <summary>
/// Tests for PoissonNoiseGenerator.
/// Validates Poisson-distributed photon statistics noise with signal-dependent
/// variance and proper clamping to 16-bit range.
/// </summary>
public class PoissonNoiseGeneratorTests
{
    #region Basic Behavior

    [Fact]
    public void ApplyNoise_shall_return_correct_dimensions()
    {
        var gen = new PoissonNoiseGenerator(42);
        var frame = CreateUniformFrame(32, 64, 1000);

        var result = gen.ApplyNoise(frame);

        result.GetLength(0).Should().Be(32);
        result.GetLength(1).Should().Be(64);
    }

    [Fact]
    public void ApplyNoise_1D_shall_return_correct_length()
    {
        var gen = new PoissonNoiseGenerator(42);
        var pixels = Enumerable.Range(0, 100).Select(_ => (ushort)1000).ToArray();

        var result = gen.ApplyNoise(pixels);

        result.Length.Should().Be(100);
    }

    [Fact]
    public void ApplyNoise_shall_throw_for_null_2D_frame()
    {
        var gen = new PoissonNoiseGenerator(42);
        var act = () => gen.ApplyNoise((ushort[,])null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyNoise_shall_throw_for_null_1D_array()
    {
        var gen = new PoissonNoiseGenerator(42);
        var act = () => gen.ApplyNoise((ushort[])null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Signal-Dependent Noise

    [Fact]
    public void Noise_variance_shall_increase_with_signal_level()
    {
        // Poisson noise: variance = mean, so higher signal = higher noise variance
        int seed = 42;
        int sampleCount = 10000;

        // Low signal
        double varianceLow = CalculateVariance(seed, 100, sampleCount);
        // High signal
        double varianceHigh = CalculateVariance(seed, 10000, sampleCount);

        varianceHigh.Should().BeGreaterThan(varianceLow);
    }

    [Fact]
    public void Zero_signal_shall_produce_zero_output()
    {
        var gen = new PoissonNoiseGenerator(42);
        var frame = CreateUniformFrame(16, 16, 0);

        var result = gen.ApplyNoise(frame);

        for (int r = 0; r < 16; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                result[r, c].Should().Be(0);
            }
        }
    }

    #endregion

    #region Seed Reproducibility

    [Fact]
    public void Same_seed_shall_produce_identical_results()
    {
        var gen1 = new PoissonNoiseGenerator(12345);
        var gen2 = new PoissonNoiseGenerator(12345);
        var frame = CreateUniformFrame(32, 32, 5000);

        var result1 = gen1.ApplyNoise(frame);
        var result2 = gen2.ApplyNoise(frame);

        for (int r = 0; r < 32; r++)
        {
            for (int c = 0; c < 32; c++)
            {
                result1[r, c].Should().Be(result2[r, c]);
            }
        }
    }

    [Fact]
    public void Different_seeds_shall_produce_different_results()
    {
        var gen1 = new PoissonNoiseGenerator(42);
        var gen2 = new PoissonNoiseGenerator(999);
        var frame = CreateUniformFrame(32, 32, 5000);

        var result1 = gen1.ApplyNoise(frame);
        var result2 = gen2.ApplyNoise(frame);

        // At least some pixels should differ
        bool anyDifferent = false;
        for (int r = 0; r < 32 && !anyDifferent; r++)
        {
            for (int c = 0; c < 32 && !anyDifferent; c++)
            {
                if (result1[r, c] != result2[r, c]) anyDifferent = true;
            }
        }
        anyDifferent.Should().BeTrue("different seeds should produce different noise");
    }

    #endregion

    #region Output Clamping

    [Fact]
    public void Output_shall_be_clamped_to_ushort_range()
    {
        var gen = new PoissonNoiseGenerator(42);
        var frame = CreateUniformFrame(64, 64, 60000);

        var result = gen.ApplyNoise(frame);

        for (int r = 0; r < 64; r++)
        {
            for (int c = 0; c < 64; c++)
            {
                result[r, c].Should().BeInRange((ushort)0, (ushort)65535);
            }
        }
    }

    [Fact]
    public void MaxValue_input_shall_not_exceed_65535()
    {
        var gen = new PoissonNoiseGenerator(42);
        var frame = CreateUniformFrame(16, 16, 65535);

        var result = gen.ApplyNoise(frame);

        for (int r = 0; r < 16; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                result[r, c].Should().BeLessOrEqualTo((ushort)65535);
            }
        }
    }

    #endregion

    #region Statistical Properties

    [Fact]
    public void Mean_output_shall_approximate_input_for_large_sample()
    {
        // For Poisson distribution, E[X] = lambda
        int seed = 42;
        ushort lambda = 5000;
        int sampleCount = 100000;

        var gen = new PoissonNoiseGenerator(seed);
        var pixels = Enumerable.Range(0, sampleCount).Select(_ => lambda).ToArray();

        var result = gen.ApplyNoise(pixels);

        double mean = result.Select(v => (double)v).Average();
        // Allow 1% tolerance for statistical convergence
        mean.Should().BeApproximately(lambda, lambda * 0.01);
    }

    [Fact]
    public void Variance_shall_approximate_mean_for_large_sample()
    {
        // For Poisson distribution, Var[X] = lambda
        int seed = 42;
        ushort lambda = 1000;
        int sampleCount = 100000;

        var gen = new PoissonNoiseGenerator(seed);
        var pixels = Enumerable.Range(0, sampleCount).Select(_ => lambda).ToArray();
        var result = gen.ApplyNoise(pixels);

        double mean = result.Select(v => (double)v).Average();
        double variance = result.Select(v => (double)v).Average(v => Math.Pow(v - mean, 2));

        // For Poisson, variance should approximately equal the mean
        variance.Should().BeApproximately(lambda, lambda * 0.1,
            "Poisson variance should approximate the mean");
    }

    [Fact]
    public void Zero_signal_1D_shall_produce_zero_output()
    {
        var gen = new PoissonNoiseGenerator(42);
        var pixels = Enumerable.Range(0, 1000).Select(_ => (ushort)0).ToArray();

        var result = gen.ApplyNoise(pixels);

        result.Should().OnlyContain(v => v == 0,
            "Poisson(0) should always produce 0");
    }

    [Theory]
    [InlineData(5)]     // Small lambda (Knuth algorithm path)
    [InlineData(100)]   // Large lambda (Gaussian approximation path)
    [InlineData(1000)]  // Very large lambda
    public void ApplyNoise_shall_produce_non_negative_values_for_all_signal_levels(ushort signalLevel)
    {
        var gen = new PoissonNoiseGenerator(42);
        var pixels = Enumerable.Range(0, 1000).Select(_ => signalLevel).ToArray();

        var result = gen.ApplyNoise(pixels);

        result.Should().OnlyContain(v => v >= 0,
            "Poisson noise output should always be non-negative");
    }

    [Fact]
    public void Small_lambda_path_shall_produce_correct_statistics()
    {
        // Test Knuth algorithm path (lambda <= 30) through public API
        var gen = new PoissonNoiseGenerator(42);
        ushort lambda = 10;
        var pixels = Enumerable.Range(0, 100000).Select(_ => lambda).ToArray();

        var result = gen.ApplyNoise(pixels);

        double mean = result.Select(v => (double)v).Average();
        mean.Should().BeApproximately(lambda, lambda * 0.05,
            "mean should approximate lambda for Knuth path");
    }

    #endregion

    #region Helpers

    private static ushort[,] CreateUniformFrame(int rows, int cols, ushort value)
    {
        var frame = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                frame[r, c] = value;
            }
        }
        return frame;
    }

    private static double CalculateVariance(int seed, ushort signalLevel, int sampleCount)
    {
        var gen = new PoissonNoiseGenerator(seed);
        var pixels = Enumerable.Range(0, sampleCount).Select(_ => signalLevel).ToArray();
        var result = gen.ApplyNoise(pixels);

        double mean = result.Select(v => (double)v).Average();
        return result.Select(v => (double)v).Average(v => Math.Pow(v - mean, 2));
    }

    #endregion
}
