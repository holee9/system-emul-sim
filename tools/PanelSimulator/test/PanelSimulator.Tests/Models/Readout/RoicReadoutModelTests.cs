using System;
using FluentAssertions;
using Xunit;
using PanelSimulator.Models.Readout;

namespace PanelSimulator.Tests.Models.Readout;

/// <summary>
/// Tests for RoicReadoutModel.
/// Validates ROIC row-by-row readout behavior including timing metadata,
/// pixel preservation, and frame dimension handling.
/// </summary>
public class RoicReadoutModelTests
{
    #region Config Defaults

    [Fact]
    public void DefaultConfig_shall_have_expected_values()
    {
        var model = new RoicReadoutModel();

        model.Config.SettleTimeUs.Should().Be(5.0);
        model.Config.AdcConversionTimeUs.Should().Be(2.0);
        model.Config.AdcBits.Should().Be(16);
    }

    [Fact]
    public void Constructor_shall_accept_null_config()
    {
        var model = new RoicReadoutModel(null);
        model.Config.SettleTimeUs.Should().Be(5.0);
    }

    [Fact]
    public void Constructor_shall_accept_custom_config()
    {
        var config = new RoicReadoutConfig(SettleTimeUs: 3.0, AdcConversionTimeUs: 1.5, AdcBits: 14);
        var model = new RoicReadoutModel(config);

        model.Config.SettleTimeUs.Should().Be(3.0);
        model.Config.AdcConversionTimeUs.Should().Be(1.5);
        model.Config.AdcBits.Should().Be(14);
    }

    #endregion

    #region Validation

    [Fact]
    public void Constructor_shall_throw_for_negative_settle_time()
    {
        var act = () => new RoicReadoutModel(new RoicReadoutConfig(SettleTimeUs: -1.0));
        act.Should().Throw<ArgumentException>().WithMessage("*Settle time*non-negative*");
    }

    [Fact]
    public void Constructor_shall_throw_for_negative_adc_conversion_time()
    {
        var act = () => new RoicReadoutModel(new RoicReadoutConfig(AdcConversionTimeUs: -0.5));
        act.Should().Throw<ArgumentException>().WithMessage("*ADC conversion time*non-negative*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Constructor_shall_throw_for_invalid_adc_bits(int bits)
    {
        var act = () => new RoicReadoutModel(new RoicReadoutConfig(AdcBits: bits));
        act.Should().Throw<ArgumentException>().WithMessage("*ADC bits*between 1 and 16*");
    }

    [Fact]
    public void Constructor_shall_accept_zero_settle_time()
    {
        var model = new RoicReadoutModel(new RoicReadoutConfig(SettleTimeUs: 0.0));
        model.Config.SettleTimeUs.Should().Be(0.0);
    }

    #endregion

    #region ReadFrame

    [Fact]
    public void ReadFrame_shall_return_correct_number_of_LineData()
    {
        var model = new RoicReadoutModel();
        var frame = CreateUniformFrame(8, 16, 1000);

        var result = model.ReadFrame(frame);

        result.Should().HaveCount(8);
    }

    [Fact]
    public void ReadFrame_shall_have_correct_RowIndex_per_entry()
    {
        var model = new RoicReadoutModel();
        var frame = CreateUniformFrame(4, 4, 500);

        var result = model.ReadFrame(frame);

        for (int r = 0; r < 4; r++)
        {
            result[r].RowIndex.Should().Be(r);
        }
    }

    [Fact]
    public void ReadFrame_shall_have_correct_pixel_count_matching_columns()
    {
        var model = new RoicReadoutModel();
        var frame = CreateUniformFrame(4, 32, 1000);

        var result = model.ReadFrame(frame);

        foreach (var line in result)
        {
            line.Pixels.Should().HaveCount(32);
        }
    }

    [Fact]
    public void ReadFrame_shall_preserve_pixel_values_exactly()
    {
        var model = new RoicReadoutModel();
        var frame = new ushort[2, 3];
        frame[0, 0] = 100; frame[0, 1] = 200; frame[0, 2] = 300;
        frame[1, 0] = 400; frame[1, 1] = 500; frame[1, 2] = 600;

        var result = model.ReadFrame(frame);

        result[0].Pixels.Should().Equal(100, 200, 300);
        result[1].Pixels.Should().Equal(400, 500, 600);
    }

    [Fact]
    public void ReadFrame_shall_populate_settle_time()
    {
        var config = new RoicReadoutConfig(SettleTimeUs: 7.5);
        var model = new RoicReadoutModel(config);
        var frame = CreateUniformFrame(2, 2, 100);

        var result = model.ReadFrame(frame);

        foreach (var line in result)
        {
            line.SettleTimeUs.Should().Be(7.5);
        }
    }

    [Fact]
    public void ReadFrame_shall_populate_readout_time()
    {
        var config = new RoicReadoutConfig(SettleTimeUs: 5.0, AdcConversionTimeUs: 2.0);
        var model = new RoicReadoutModel(config);
        var frame = CreateUniformFrame(2, 2, 100);

        var result = model.ReadFrame(frame);

        foreach (var line in result)
        {
            line.ReadoutTimeUs.Should().Be(7.0); // 5.0 + 2.0
        }
    }

    [Fact]
    public void ReadFrame_shall_throw_for_null_frame()
    {
        var model = new RoicReadoutModel();
        var act = () => model.ReadFrame(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Table-Driven Frame Size Tests

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(256, 256)]
    [InlineData(3, 7)]
    public void ReadFrame_shall_handle_various_frame_sizes(int rows, int cols)
    {
        var model = new RoicReadoutModel();
        var frame = CreateUniformFrame(rows, cols, 42);

        var result = model.ReadFrame(frame);

        result.Should().HaveCount(rows);
        foreach (var line in result)
        {
            line.Pixels.Should().HaveCount(cols);
        }
    }

    #endregion

    #region CalculateTotalReadoutTimeMs

    [Fact]
    public void CalculateTotalReadoutTimeMs_shall_return_correct_value()
    {
        // settle=5, adc=2 -> 7 us/row, 100 rows -> 700 us -> 0.7 ms
        var model = new RoicReadoutModel();
        var frame = CreateUniformFrame(100, 64, 0);
        model.ReadFrame(frame);

        double totalMs = model.CalculateTotalReadoutTimeMs();

        totalMs.Should().BeApproximately(0.7, 0.0001);
    }

    [Fact]
    public void CalculateTotalReadoutTimeMs_shall_accept_explicit_row_count()
    {
        var model = new RoicReadoutModel();

        double totalMs = model.CalculateTotalReadoutTimeMs(rows: 1000);

        // 1000 * (5+2) / 1000 = 7.0 ms
        totalMs.Should().BeApproximately(7.0, 0.0001);
    }

    [Fact]
    public void CalculateTotalReadoutTimeMs_shall_throw_without_frame_or_rows()
    {
        var model = new RoicReadoutModel();
        var act = () => model.CalculateTotalReadoutTimeMs();
        act.Should().Throw<ArgumentException>().WithMessage("*Row count*positive*");
    }

    [Fact]
    public void CalculateTotalReadoutTimeMs_shall_scale_linearly_with_rows()
    {
        var model = new RoicReadoutModel();

        double time100 = model.CalculateTotalReadoutTimeMs(rows: 100);
        double time200 = model.CalculateTotalReadoutTimeMs(rows: 200);

        time200.Should().BeApproximately(time100 * 2.0, 0.0001);
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
