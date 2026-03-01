using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using PanelSimulator.Models.Calibration;

namespace PanelSimulator.Tests.Models.Calibration;

/// <summary>
/// Tests for CalibrationFrameGenerator.
/// Validates dark frame, flat field frame, bias frame, and averaged dark frame generation
/// with realistic noise characteristics.
/// </summary>
public class CalibrationFrameGeneratorTests
{
    private const int TestRows = 64;
    private const int TestCols = 64;

    #region Construction

    [Fact]
    public void DefaultConfig_shall_have_expected_values()
    {
        var gen = new CalibrationFrameGenerator();

        gen.Config.Rows.Should().Be(256);
        gen.Config.Cols.Should().Be(256);
        gen.Config.AdcBits.Should().Be(16);
        gen.Config.FlatFieldSignalDN.Should().Be(32768);
    }

    [Fact]
    public void Constructor_shall_accept_null_config()
    {
        var gen = new CalibrationFrameGenerator(null);
        gen.Config.Rows.Should().Be(256);
    }

    [Fact]
    public void Constructor_shall_throw_for_invalid_rows()
    {
        var act = () => new CalibrationFrameGenerator(new CalibrationConfig(Rows: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Rows*positive*");
    }

    [Fact]
    public void Constructor_shall_throw_for_invalid_cols()
    {
        var act = () => new CalibrationFrameGenerator(new CalibrationConfig(Cols: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Cols*positive*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Constructor_shall_throw_for_invalid_adc_bits(int bits)
    {
        var act = () => new CalibrationFrameGenerator(new CalibrationConfig(AdcBits: bits));
        act.Should().Throw<ArgumentException>().WithMessage("*ADC bits*");
    }

    #endregion

    #region Dark Frame

    [Fact]
    public void GenerateDarkFrame_shall_return_correct_dimensions()
    {
        var config = new CalibrationConfig(Rows: TestRows, Cols: TestCols);
        var gen = new CalibrationFrameGenerator(config);

        var frame = gen.GenerateDarkFrame();

        frame.GetLength(0).Should().Be(TestRows);
        frame.GetLength(1).Should().Be(TestCols);
    }

    [Fact]
    public void GenerateDarkFrame_shall_have_low_mean_value()
    {
        // Dark frame has no X-ray signal, only dark current + noise
        var config = new CalibrationConfig(
            Rows: TestRows, Cols: TestCols,
            DarkCurrentElectrons: 10,
            ReadoutNoiseElectrons: 5);
        var gen = new CalibrationFrameGenerator(config);

        var frame = gen.GenerateDarkFrame();

        double mean = Flatten(frame).Average();
        // Dark frame mean should be very low (dark current only)
        mean.Should().BeLessThan(100, "dark frame should have low signal level");
    }

    [Fact]
    public void GenerateDarkFrame_shall_have_noise_variation()
    {
        var config = new CalibrationConfig(
            Rows: TestRows, Cols: TestCols,
            ReadoutNoiseElectrons: 10);
        var gen = new CalibrationFrameGenerator(config);

        var frame = gen.GenerateDarkFrame();

        // Check that not all pixels are identical (noise should add variation)
        var values = Flatten(frame);
        double stdDev = CalculateStdDev(values);
        stdDev.Should().BeGreaterThan(0, "dark frame should have noise variation");
    }

    [Fact]
    public void GenerateDarkFrame_shall_be_deterministic_with_seed()
    {
        var config = new CalibrationConfig(Rows: TestRows, Cols: TestCols);
        var gen = new CalibrationFrameGenerator(config);

        var frame1 = gen.GenerateDarkFrame(seed: 12345);
        var frame2 = gen.GenerateDarkFrame(seed: 12345);

        for (int r = 0; r < TestRows; r++)
        {
            for (int c = 0; c < TestCols; c++)
            {
                frame1[r, c].Should().Be(frame2[r, c]);
            }
        }
    }

    [Fact]
    public void GenerateDarkFrame_shall_be_much_lower_than_flat_field()
    {
        var config = new CalibrationConfig(
            Rows: TestRows, Cols: TestCols,
            FlatFieldSignalDN: 32768);
        var gen = new CalibrationFrameGenerator(config);

        var darkFrame = gen.GenerateDarkFrame(seed: 42);
        var flatFrame = gen.GenerateFlatFieldFrame(seed: 42);

        double darkMean = Flatten(darkFrame).Average();
        double flatMean = Flatten(flatFrame).Average();

        flatMean.Should().BeGreaterThan(darkMean * 10,
            "flat field should be much brighter than dark frame");
    }

    #endregion

    #region Flat Field Frame

    [Fact]
    public void GenerateFlatFieldFrame_shall_return_correct_dimensions()
    {
        var config = new CalibrationConfig(Rows: TestRows, Cols: TestCols);
        var gen = new CalibrationFrameGenerator(config);

        var frame = gen.GenerateFlatFieldFrame();

        frame.GetLength(0).Should().Be(TestRows);
        frame.GetLength(1).Should().Be(TestCols);
    }

    [Fact]
    public void GenerateFlatFieldFrame_shall_have_mean_near_configured_signal()
    {
        ushort targetSignal = 20000;
        var config = new CalibrationConfig(
            Rows: TestRows, Cols: TestCols,
            FlatFieldSignalDN: targetSignal,
            ReadoutNoiseElectrons: 2,
            DarkCurrentElectrons: 1);
        var gen = new CalibrationFrameGenerator(config);

        var frame = gen.GenerateFlatFieldFrame();

        double mean = Flatten(frame).Average();
        // Allow 5% tolerance due to noise and dark current
        mean.Should().BeApproximately(targetSignal, targetSignal * 0.05);
    }

    [Fact]
    public void GenerateFlatFieldFrame_shall_have_variation_with_random_map()
    {
        var config = new CalibrationConfig(
            Rows: TestRows, Cols: TestCols,
            FlatFieldSignalDN: 20000);
        var gen = new CalibrationFrameGenerator(config);
        var map = GainOffsetMap.CreateRandom(TestRows, TestCols, gainStdDev: 0.05, seed: 42);

        var frame = gen.GenerateFlatFieldFrame(map, seed: 42);

        var values = Flatten(frame);
        double stdDev = CalculateStdDev(values);
        stdDev.Should().BeGreaterThan(100,
            "flat field with non-uniform gain should have significant variation");
    }

    [Fact]
    public void GenerateFlatFieldFrame_with_flat_map_shall_be_relatively_uniform()
    {
        var config = new CalibrationConfig(
            Rows: TestRows, Cols: TestCols,
            FlatFieldSignalDN: 20000,
            ReadoutNoiseElectrons: 1,
            DarkCurrentElectrons: 1);
        var gen = new CalibrationFrameGenerator(config);

        var frame = gen.GenerateFlatFieldFrame();

        var values = Flatten(frame);
        double stdDev = CalculateStdDev(values);
        double mean = values.Average();
        double coeffOfVariation = stdDev / mean;

        // With flat map, CV should be small (dominated by noise only)
        coeffOfVariation.Should().BeLessThan(0.05,
            "flat map flat field should be relatively uniform");
    }

    #endregion

    #region Bias Frame

    [Fact]
    public void GenerateBiasFrame_shall_return_correct_dimensions()
    {
        var config = new CalibrationConfig(Rows: TestRows, Cols: TestCols);
        var gen = new CalibrationFrameGenerator(config);

        var frame = gen.GenerateBiasFrame();

        frame.GetLength(0).Should().Be(TestRows);
        frame.GetLength(1).Should().Be(TestCols);
    }

    [Fact]
    public void GenerateBiasFrame_shall_have_very_low_values()
    {
        // Bias = offset + readout noise only (no signal, no dark current)
        var config = new CalibrationConfig(
            Rows: TestRows, Cols: TestCols,
            ReadoutNoiseElectrons: 5);
        var gen = new CalibrationFrameGenerator(config);

        var frame = gen.GenerateBiasFrame();

        double mean = Flatten(frame).Average();
        // Bias frame should have very low mean (electronic offset only)
        mean.Should().BeLessThan(50, "bias frame should have very low signal");
    }

    [Fact]
    public void GenerateBiasFrame_shall_be_lower_than_dark_frame()
    {
        var config = new CalibrationConfig(
            Rows: TestRows, Cols: TestCols,
            DarkCurrentElectrons: 100, // Significant dark current
            ReadoutNoiseElectrons: 5);
        var gen = new CalibrationFrameGenerator(config);

        var biasFrame = gen.GenerateBiasFrame(seed: 42);
        var darkFrame = gen.GenerateDarkFrame(seed: 42);

        double biasMean = Flatten(biasFrame).Average();
        double darkMean = Flatten(darkFrame).Average();

        // Dark frame includes dark current, bias does not
        darkMean.Should().BeGreaterOrEqualTo(biasMean,
            "dark frame includes dark current that bias frame lacks");
    }

    [Fact]
    public void GenerateBiasFrame_shall_be_deterministic_with_seed()
    {
        var config = new CalibrationConfig(Rows: TestRows, Cols: TestCols);
        var gen = new CalibrationFrameGenerator(config);

        var frame1 = gen.GenerateBiasFrame(seed: 99);
        var frame2 = gen.GenerateBiasFrame(seed: 99);

        for (int r = 0; r < TestRows; r++)
        {
            for (int c = 0; c < TestCols; c++)
            {
                frame1[r, c].Should().Be(frame2[r, c]);
            }
        }
    }

    #endregion

    #region Averaged Dark Frame

    [Fact]
    public void GenerateAveragedDarkFrame_shall_return_correct_dimensions()
    {
        var config = new CalibrationConfig(Rows: TestRows, Cols: TestCols);
        var gen = new CalibrationFrameGenerator(config);

        var frame = gen.GenerateAveragedDarkFrame(count: 4);

        frame.GetLength(0).Should().Be(TestRows);
        frame.GetLength(1).Should().Be(TestCols);
    }

    [Fact]
    public void GenerateAveragedDarkFrame_shall_reduce_noise()
    {
        // Averaging N frames should reduce noise by sqrt(N)
        var config = new CalibrationConfig(
            Rows: TestRows, Cols: TestCols,
            ReadoutNoiseElectrons: 20,
            DarkCurrentElectrons: 10);
        var gen = new CalibrationFrameGenerator(config);

        var singleFrame = gen.GenerateDarkFrame(seed: 42);
        var averagedFrame = gen.GenerateAveragedDarkFrame(count: 16, baseSeed: 42);

        double singleStdDev = CalculateStdDev(Flatten(singleFrame));
        double averagedStdDev = CalculateStdDev(Flatten(averagedFrame));

        // Averaging 16 frames should reduce noise by ~4x
        averagedStdDev.Should().BeLessThan(singleStdDev,
            "averaged dark frame should have less noise than single frame");
    }

    [Fact]
    public void GenerateAveragedDarkFrame_shall_throw_for_zero_count()
    {
        var config = new CalibrationConfig(Rows: TestRows, Cols: TestCols);
        var gen = new CalibrationFrameGenerator(config);

        var act = () => gen.GenerateAveragedDarkFrame(count: 0);
        act.Should().Throw<ArgumentException>().WithMessage("*Count*at least 1*");
    }

    [Fact]
    public void GenerateAveragedDarkFrame_with_count_one_shall_equal_single_frame()
    {
        var config = new CalibrationConfig(Rows: TestRows, Cols: TestCols);
        var gen = new CalibrationFrameGenerator(config);

        var single = gen.GenerateDarkFrame(seed: 42);
        var averaged = gen.GenerateAveragedDarkFrame(count: 1, baseSeed: 42);

        for (int r = 0; r < TestRows; r++)
        {
            for (int c = 0; c < TestCols; c++)
            {
                averaged[r, c].Should().Be(single[r, c]);
            }
        }
    }

    #endregion

    #region Map Dimension Validation

    [Fact]
    public void GenerateDarkFrame_shall_throw_for_mismatched_map()
    {
        var config = new CalibrationConfig(Rows: 32, Cols: 32);
        var gen = new CalibrationFrameGenerator(config);
        var map = GainOffsetMap.CreateFlat(64, 64);

        var act = () => gen.GenerateDarkFrame(map);
        act.Should().Throw<ArgumentException>().WithMessage("*dimensions*match*");
    }

    [Fact]
    public void GenerateFlatFieldFrame_shall_throw_for_mismatched_map()
    {
        var config = new CalibrationConfig(Rows: 32, Cols: 32);
        var gen = new CalibrationFrameGenerator(config);
        var map = GainOffsetMap.CreateFlat(64, 64);

        var act = () => gen.GenerateFlatFieldFrame(map);
        act.Should().Throw<ArgumentException>().WithMessage("*dimensions*match*");
    }

    [Fact]
    public void GenerateBiasFrame_shall_throw_for_mismatched_map()
    {
        var config = new CalibrationConfig(Rows: 32, Cols: 32);
        var gen = new CalibrationFrameGenerator(config);
        var map = GainOffsetMap.CreateFlat(64, 64);

        var act = () => gen.GenerateBiasFrame(map);
        act.Should().Throw<ArgumentException>().WithMessage("*dimensions*match*");
    }

    #endregion

    #region Helpers

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

    private static double CalculateStdDev(double[] values)
    {
        double mean = values.Average();
        double variance = values.Average(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(variance);
    }

    #endregion
}
