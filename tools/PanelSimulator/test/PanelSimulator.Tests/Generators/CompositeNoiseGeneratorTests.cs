using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using PanelSimulator.Generators;

namespace PanelSimulator.Tests.Generators;

/// <summary>
/// Tests for CompositeNoiseGenerator.
/// Validates combined noise model with Poisson, Gaussian, dark current,
/// and flicker noise sources.
/// </summary>
public class CompositeNoiseGeneratorTests
{
    #region Basic Construction

    [Fact]
    public void DefaultConfig_shall_have_expected_defaults()
    {
        var gen = new CompositeNoiseGenerator(42);

        gen.Config.EnablePoissonNoise.Should().BeTrue();
        gen.Config.EnableGaussianNoise.Should().BeTrue();
        gen.Config.EnableDarkCurrent.Should().BeTrue();
        gen.Config.EnableFlickerNoise.Should().BeFalse();
        gen.Config.ReadoutNoiseElectrons.Should().Be(5.0);
        gen.Config.DarkCurrentElectrons.Should().Be(10.0);
        gen.Config.NoiseFloorDN.Should().Be(1.0);
        gen.Config.AdcBits.Should().Be(16);
    }

    [Fact]
    public void Constructor_shall_throw_for_negative_readout_noise()
    {
        var act = () => new CompositeNoiseGenerator(42,
            new CompositeNoiseConfig(ReadoutNoiseElectrons: -1));
        act.Should().Throw<ArgumentException>().WithMessage("*Readout noise*non-negative*");
    }

    [Fact]
    public void Constructor_shall_throw_for_negative_dark_current()
    {
        var act = () => new CompositeNoiseGenerator(42,
            new CompositeNoiseConfig(DarkCurrentElectrons: -1));
        act.Should().Throw<ArgumentException>().WithMessage("*Dark current*non-negative*");
    }

    [Fact]
    public void Constructor_shall_throw_for_negative_flicker_amplitude()
    {
        var act = () => new CompositeNoiseGenerator(42,
            new CompositeNoiseConfig(FlickerNoiseAmplitude: -0.1));
        act.Should().Throw<ArgumentException>().WithMessage("*Flicker*non-negative*");
    }

    [Fact]
    public void Constructor_shall_throw_for_negative_noise_floor()
    {
        var act = () => new CompositeNoiseGenerator(42,
            new CompositeNoiseConfig(NoiseFloorDN: -1));
        act.Should().Throw<ArgumentException>().WithMessage("*Noise floor*non-negative*");
    }

    #endregion

    #region All Sources Enabled vs Disabled

    [Fact]
    public void AllDisabled_shall_preserve_original_values()
    {
        var config = new CompositeNoiseConfig(
            EnablePoissonNoise: false,
            EnableGaussianNoise: false,
            EnableDarkCurrent: false,
            EnableFlickerNoise: false,
            NoiseFloorDN: 0);
        var gen = new CompositeNoiseGenerator(42, config);
        var frame = CreateUniformFrame(16, 16, 10000);

        var result = gen.ApplyNoise(frame);

        for (int r = 0; r < 16; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                result[r, c].Should().Be(10000);
            }
        }
    }

    [Fact]
    public void AllEnabled_shall_modify_pixel_values()
    {
        var config = new CompositeNoiseConfig(
            EnablePoissonNoise: true,
            EnableGaussianNoise: true,
            EnableDarkCurrent: true,
            EnableFlickerNoise: true);
        var gen = new CompositeNoiseGenerator(42, config);
        var frame = CreateUniformFrame(32, 32, 10000);

        var result = gen.ApplyNoise(frame);

        // With noise enabled, not all pixels should remain at 10000
        bool anyChanged = false;
        for (int r = 0; r < 32 && !anyChanged; r++)
        {
            for (int c = 0; c < 32 && !anyChanged; c++)
            {
                if (result[r, c] != 10000) anyChanged = true;
            }
        }
        anyChanged.Should().BeTrue("noise should modify pixel values");
    }

    [Fact]
    public void PoissonOnly_shall_add_signal_dependent_noise()
    {
        var config = new CompositeNoiseConfig(
            EnablePoissonNoise: true,
            EnableGaussianNoise: false,
            EnableDarkCurrent: false,
            EnableFlickerNoise: false,
            NoiseFloorDN: 0);
        var gen = new CompositeNoiseGenerator(42, config);
        var frame = CreateUniformFrame(64, 64, 5000);

        var result = gen.ApplyNoise(frame);

        // Should have variation around 5000
        var values = Flatten(result);
        double mean = values.Average();
        mean.Should().BeApproximately(5000, 500, "mean should be near input signal");
    }

    [Fact]
    public void GaussianOnly_shall_add_signal_independent_noise()
    {
        var config = new CompositeNoiseConfig(
            EnablePoissonNoise: false,
            EnableGaussianNoise: true,
            EnableDarkCurrent: false,
            EnableFlickerNoise: false,
            ReadoutNoiseElectrons: 100, // Large readout noise
            NoiseFloorDN: 0);
        var gen = new CompositeNoiseGenerator(42, config);
        var frame = CreateUniformFrame(64, 64, 10000);

        var result = gen.ApplyNoise(frame);

        bool anyChanged = false;
        for (int r = 0; r < 64 && !anyChanged; r++)
        {
            for (int c = 0; c < 64 && !anyChanged; c++)
            {
                if (result[r, c] != 10000) anyChanged = true;
            }
        }
        anyChanged.Should().BeTrue("Gaussian noise should modify pixel values");
    }

    #endregion

    #region Noise Floor

    [Fact]
    public void NoiseFloor_shall_enforce_minimum_value_for_positive_pixels()
    {
        var config = new CompositeNoiseConfig(
            EnablePoissonNoise: false,
            EnableGaussianNoise: false,
            EnableDarkCurrent: false,
            EnableFlickerNoise: false,
            NoiseFloorDN: 100);
        var gen = new CompositeNoiseGenerator(42, config);

        // Create frame with small values that are above zero but below noise floor
        var frame = new ushort[4, 4];
        frame[0, 0] = 50; // Below noise floor, positive -> should be raised to 100
        frame[0, 1] = 0;  // Zero -> should stay zero (noise floor only applies to positive)
        frame[0, 2] = 200; // Above noise floor -> unchanged

        var result = gen.ApplyNoise(frame);

        result[0, 0].Should().Be(100, "values below noise floor should be raised");
        result[0, 1].Should().Be(0, "zero values should remain zero");
        result[0, 2].Should().Be(200, "values above noise floor should be unchanged");
    }

    #endregion

    #region Reproducibility

    [Fact]
    public void Same_seed_shall_produce_identical_output()
    {
        var config = new CompositeNoiseConfig();
        var gen1 = new CompositeNoiseGenerator(42, config);
        var gen2 = new CompositeNoiseGenerator(42, config);
        var frame = CreateUniformFrame(32, 32, 8000);

        var result1 = gen1.ApplyNoise(frame, frameNumber: 0);
        var result2 = gen2.ApplyNoise(frame, frameNumber: 0);

        for (int r = 0; r < 32; r++)
        {
            for (int c = 0; c < 32; c++)
            {
                result1[r, c].Should().Be(result2[r, c]);
            }
        }
    }

    [Fact]
    public void Different_frame_numbers_shall_produce_different_noise()
    {
        var gen = new CompositeNoiseGenerator(42);
        var frame = CreateUniformFrame(32, 32, 8000);

        var result0 = gen.ApplyNoise(frame, frameNumber: 0);
        var result1 = gen.ApplyNoise(frame, frameNumber: 1);

        bool anyDifferent = false;
        for (int r = 0; r < 32 && !anyDifferent; r++)
        {
            for (int c = 0; c < 32 && !anyDifferent; c++)
            {
                if (result0[r, c] != result1[r, c]) anyDifferent = true;
            }
        }
        anyDifferent.Should().BeTrue("different frame numbers should produce different noise");
    }

    #endregion

    #region Frame Dimensions

    [Theory]
    [InlineData(16, 16)]
    [InlineData(64, 128)]
    [InlineData(256, 256)]
    public void ApplyNoise_shall_preserve_frame_dimensions(int rows, int cols)
    {
        var gen = new CompositeNoiseGenerator(42);
        var frame = CreateUniformFrame(rows, cols, 5000);

        var result = gen.ApplyNoise(frame);

        result.GetLength(0).Should().Be(rows);
        result.GetLength(1).Should().Be(cols);
    }

    [Fact]
    public void ApplyNoise_shall_throw_for_null_frame()
    {
        var gen = new CompositeNoiseGenerator(42);
        var act = () => gen.ApplyNoise(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyNoise_shall_clamp_output_to_ushort_range()
    {
        var gen = new CompositeNoiseGenerator(42);
        var frame = CreateUniformFrame(32, 32, 60000);

        var result = gen.ApplyNoise(frame);

        for (int r = 0; r < 32; r++)
        {
            for (int c = 0; c < 32; c++)
            {
                result[r, c].Should().BeInRange((ushort)0, (ushort)65535);
            }
        }
    }

    #endregion

    #region Dark Current Effect

    [Fact]
    public void DarkCurrent_shall_increase_mean_signal()
    {
        // Without dark current
        var configNoDark = new CompositeNoiseConfig(
            EnablePoissonNoise: false,
            EnableGaussianNoise: false,
            EnableDarkCurrent: false,
            EnableFlickerNoise: false,
            NoiseFloorDN: 0);
        var genNoDark = new CompositeNoiseGenerator(42, configNoDark);

        // With dark current
        var configDark = new CompositeNoiseConfig(
            EnablePoissonNoise: false,
            EnableGaussianNoise: false,
            EnableDarkCurrent: true,
            EnableFlickerNoise: false,
            DarkCurrentElectrons: 1000,
            NoiseFloorDN: 0);
        var genDark = new CompositeNoiseGenerator(42, configDark);

        var frame = CreateUniformFrame(32, 32, 5000);

        var resultNoDark = genNoDark.ApplyNoise(frame);
        var resultDark = genDark.ApplyNoise(frame);

        double meanNoDark = Flatten(resultNoDark).Average();
        double meanDark = Flatten(resultDark).Average();

        meanDark.Should().BeGreaterThan(meanNoDark,
            "dark current should increase average signal level");
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

    private static double[] Flatten(ushort[,] frame)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        var result = new double[rows * cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result[r * cols + c] = frame[r, c];
            }
        }
        return result;
    }

    #endregion
}
