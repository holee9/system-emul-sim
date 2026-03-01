using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using PanelSimulator.Models.Calibration;

namespace PanelSimulator.Tests.Models.Calibration;

/// <summary>
/// Tests for GainOffsetMap.
/// Validates per-pixel gain and offset correction maps for flat-field calibration.
/// </summary>
public class GainOffsetMapTests
{
    #region Flat Map (Identity)

    [Fact]
    public void CreateFlat_shall_produce_gain_of_one_and_offset_of_zero()
    {
        var map = GainOffsetMap.CreateFlat(16, 16);

        for (int r = 0; r < 16; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                map.GetGain(r, c).Should().Be(1.0);
                map.GetOffset(r, c).Should().Be(0.0);
            }
        }
    }

    [Fact]
    public void CreateFlat_shall_have_correct_dimensions()
    {
        var map = GainOffsetMap.CreateFlat(32, 64);

        map.Rows.Should().Be(32);
        map.Cols.Should().Be(64);
    }

    [Fact]
    public void FlatMap_ApplyForward_shall_be_identity()
    {
        var map = GainOffsetMap.CreateFlat(16, 16);
        var frame = CreateUniformFrame(16, 16, 10000);

        var result = map.ApplyForward(frame);

        for (int r = 0; r < 16; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                result[r, c].Should().Be(10000);
            }
        }
    }

    [Fact]
    public void FlatMap_ApplyCorrection_shall_be_identity()
    {
        var map = GainOffsetMap.CreateFlat(16, 16);
        var frame = CreateUniformFrame(16, 16, 10000);

        var result = map.ApplyCorrection(frame);

        for (int r = 0; r < 16; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                result[r, c].Should().Be(10000);
            }
        }
    }

    [Fact]
    public void CreateFlat_shall_throw_for_invalid_dimensions()
    {
        var act1 = () => GainOffsetMap.CreateFlat(0, 16);
        act1.Should().Throw<ArgumentException>().WithMessage("*Rows*positive*");

        var act2 = () => GainOffsetMap.CreateFlat(16, 0);
        act2.Should().Throw<ArgumentException>().WithMessage("*Cols*positive*");
    }

    #endregion

    #region Random Map

    [Fact]
    public void CreateRandom_shall_have_non_uniform_gain_values()
    {
        var map = GainOffsetMap.CreateRandom(64, 64, gainStdDev: 0.05, seed: 42);

        // Collect gain values
        var gains = new double[64 * 64];
        for (int r = 0; r < 64; r++)
        {
            for (int c = 0; c < 64; c++)
            {
                gains[r * 64 + c] = map.GetGain(r, c);
            }
        }

        // Should have variation
        double min = gains.Min();
        double max = gains.Max();
        (max - min).Should().BeGreaterThan(0.01, "random map should have gain variation");

        // Should be centered around 1.0
        double mean = gains.Average();
        mean.Should().BeApproximately(1.0, 0.02);
    }

    [Fact]
    public void CreateRandom_shall_have_non_uniform_offset_values()
    {
        var map = GainOffsetMap.CreateRandom(64, 64, offsetStdDev: 10.0, seed: 42);

        var offsets = new double[64 * 64];
        for (int r = 0; r < 64; r++)
        {
            for (int c = 0; c < 64; c++)
            {
                offsets[r * 64 + c] = map.GetOffset(r, c);
            }
        }

        double min = offsets.Min();
        double max = offsets.Max();
        (max - min).Should().BeGreaterThan(1.0, "random map should have offset variation");

        double mean = offsets.Average();
        mean.Should().BeApproximately(0.0, 2.0);
    }

    [Fact]
    public void CreateRandom_shall_ensure_positive_gains()
    {
        var map = GainOffsetMap.CreateRandom(128, 128, gainStdDev: 0.1, seed: 42);

        for (int r = 0; r < 128; r++)
        {
            for (int c = 0; c < 128; c++)
            {
                map.GetGain(r, c).Should().BeGreaterThan(0,
                    "all gains must be positive to avoid division by zero");
            }
        }
    }

    [Fact]
    public void CreateRandom_shall_be_deterministic_with_seed()
    {
        var map1 = GainOffsetMap.CreateRandom(16, 16, seed: 12345);
        var map2 = GainOffsetMap.CreateRandom(16, 16, seed: 12345);

        for (int r = 0; r < 16; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                map1.GetGain(r, c).Should().Be(map2.GetGain(r, c));
                map1.GetOffset(r, c).Should().Be(map2.GetOffset(r, c));
            }
        }
    }

    [Fact]
    public void CreateRandom_shall_throw_for_invalid_parameters()
    {
        var act1 = () => GainOffsetMap.CreateRandom(0, 16);
        act1.Should().Throw<ArgumentException>().WithMessage("*Rows*positive*");

        var act2 = () => GainOffsetMap.CreateRandom(16, 0);
        act2.Should().Throw<ArgumentException>().WithMessage("*Cols*positive*");

        var act3 = () => GainOffsetMap.CreateRandom(16, 16, gainStdDev: -1);
        act3.Should().Throw<ArgumentException>().WithMessage("*Gain standard deviation*non-negative*");

        var act4 = () => GainOffsetMap.CreateRandom(16, 16, offsetStdDev: -1);
        act4.Should().Throw<ArgumentException>().WithMessage("*Offset standard deviation*non-negative*");
    }

    #endregion

    #region ApplyForward

    [Fact]
    public void ApplyForward_shall_change_pixel_values_with_nonuniform_map()
    {
        var map = GainOffsetMap.CreateRandom(32, 32, gainStdDev: 0.05, offsetStdDev: 10, seed: 42);
        var frame = CreateUniformFrame(32, 32, 10000);

        var result = map.ApplyForward(frame);

        // With non-uniform map, not all pixels should be 10000
        bool anyChanged = false;
        for (int r = 0; r < 32 && !anyChanged; r++)
        {
            for (int c = 0; c < 32 && !anyChanged; c++)
            {
                if (result[r, c] != 10000) anyChanged = true;
            }
        }
        anyChanged.Should().BeTrue("gain/offset should modify pixel values");
    }

    [Fact]
    public void ApplyForward_shall_throw_for_null_frame()
    {
        var map = GainOffsetMap.CreateFlat(16, 16);
        var act = () => map.ApplyForward(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyForward_shall_throw_for_mismatched_dimensions()
    {
        var map = GainOffsetMap.CreateFlat(16, 16);
        var frame = CreateUniformFrame(32, 32, 1000);

        var act = () => map.ApplyForward(frame);
        act.Should().Throw<ArgumentException>().WithMessage("*dimensions*match*");
    }

    [Fact]
    public void ApplyForward_shall_clamp_output_to_ushort_range()
    {
        // Create map with very high gain to trigger clamping
        var gainMap = new double[4, 4];
        var offsetMap = new double[4, 4];
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                gainMap[r, c] = 2.0;
                offsetMap[r, c] = 0;
            }
        }
        var map = new GainOffsetMap(gainMap, offsetMap);
        var frame = CreateUniformFrame(4, 4, 50000);

        var result = map.ApplyForward(frame);

        result[0, 0].Should().Be(65535, "clamped to max ushort");
    }

    #endregion

    #region ApplyCorrection (Inverse)

    [Fact]
    public void ApplyCorrection_shall_approximately_reverse_ApplyForward()
    {
        var map = GainOffsetMap.CreateRandom(32, 32, gainStdDev: 0.03, offsetStdDev: 5, seed: 42);
        var original = CreateUniformFrame(32, 32, 10000);

        var distorted = map.ApplyForward(original);
        var corrected = map.ApplyCorrection(distorted);

        // Due to ushort rounding, allow +/- 2 DN tolerance
        for (int r = 0; r < 32; r++)
        {
            for (int c = 0; c < 32; c++)
            {
                ((int)corrected[r, c]).Should().BeCloseTo(10000, 2,
                    $"correction should reverse forward at ({r},{c})");
            }
        }
    }

    [Fact]
    public void ApplyCorrection_shall_throw_for_null_frame()
    {
        var map = GainOffsetMap.CreateFlat(16, 16);
        var act = () => map.ApplyCorrection(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyCorrection_shall_handle_zero_gain_as_dead_pixel()
    {
        var gainMap = new double[4, 4];
        var offsetMap = new double[4, 4];
        gainMap[0, 0] = 0.0; // Dead pixel
        gainMap[0, 1] = 1.0;
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                if (r == 0 && c == 0) continue;
                gainMap[r, c] = 1.0;
            }
        }
        var map = new GainOffsetMap(gainMap, offsetMap);
        var frame = CreateUniformFrame(4, 4, 5000);

        var result = map.ApplyCorrection(frame);

        result[0, 0].Should().Be(0, "zero-gain pixel should map to 0");
        result[0, 1].Should().Be(5000, "normal pixel should be unchanged");
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_shall_throw_for_null_gainMap()
    {
        var offset = new double[4, 4];
        var act = () => new GainOffsetMap(null!, offset);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_shall_throw_for_null_offsetMap()
    {
        var gain = new double[4, 4];
        var act = () => new GainOffsetMap(gain, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_shall_throw_for_mismatched_dimensions()
    {
        var gain = new double[4, 4];
        var offset = new double[8, 8];
        var act = () => new GainOffsetMap(gain, offset);
        act.Should().Throw<ArgumentException>().WithMessage("*same dimensions*");
    }

    [Fact]
    public void GetGain_shall_throw_for_out_of_range_coordinates()
    {
        var map = GainOffsetMap.CreateFlat(4, 4);

        var act1 = () => map.GetGain(-1, 0);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => map.GetGain(0, 4);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetOffset_shall_throw_for_out_of_range_coordinates()
    {
        var map = GainOffsetMap.CreateFlat(4, 4);

        var act = () => map.GetOffset(4, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
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

    #endregion
}
