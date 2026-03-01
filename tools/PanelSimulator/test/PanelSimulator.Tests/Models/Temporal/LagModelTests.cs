using System;
using FluentAssertions;
using Xunit;
using PanelSimulator.Models.Temporal;

namespace PanelSimulator.Tests.Models.Temporal;

/// <summary>
/// Tests for LagModel.
/// Validates image lag (ghosting) simulation where residual signal from
/// previous frames carries over into the current frame.
/// </summary>
public class LagModelTests
{
    #region Construction and Config

    [Fact]
    public void DefaultConfig_shall_have_expected_values()
    {
        var model = new LagModel();

        model.Config.LagCoefficient.Should().Be(0.02);
        model.Config.DecayOrder.Should().Be(1);
    }

    [Fact]
    public void Constructor_shall_accept_null_config()
    {
        var model = new LagModel(null);
        model.Config.LagCoefficient.Should().Be(0.02);
    }

    [Fact]
    public void Constructor_shall_throw_for_negative_lag_coefficient()
    {
        var act = () => new LagModel(new LagConfig(LagCoefficient: -0.1));
        act.Should().Throw<ArgumentException>().WithMessage("*Lag coefficient*");
    }

    [Fact]
    public void Constructor_shall_throw_for_lag_coefficient_ge_one()
    {
        var act = () => new LagModel(new LagConfig(LagCoefficient: 1.0));
        act.Should().Throw<ArgumentException>().WithMessage("*Lag coefficient*");
    }

    [Fact]
    public void Constructor_shall_throw_for_invalid_decay_order()
    {
        var act = () => new LagModel(new LagConfig(DecayOrder: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Decay order*at least 1*");
    }

    [Fact]
    public void Constructor_shall_accept_zero_lag_coefficient()
    {
        // Zero lag means no ghosting - should be valid
        var model = new LagModel(new LagConfig(LagCoefficient: 0.0));
        model.Config.LagCoefficient.Should().Be(0.0);
    }

    #endregion

    #region First Frame - No Lag

    [Fact]
    public void FirstFrame_shall_have_no_lag_effect()
    {
        var model = new LagModel(new LagConfig(LagCoefficient: 0.1));
        var frame = CreateUniformFrame(16, 16, 10000);

        var result = model.ApplyLag(frame);

        // First frame: no history, so output = input
        for (int r = 0; r < 16; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                result[r, c].Should().Be(10000);
            }
        }
    }

    [Fact]
    public void HistoryCount_shall_start_at_zero()
    {
        var model = new LagModel();
        model.HistoryCount.Should().Be(0);
    }

    [Fact]
    public void HistoryCount_shall_increase_after_frame()
    {
        var model = new LagModel();
        var frame = CreateUniformFrame(4, 4, 1000);

        model.ApplyLag(frame);

        model.HistoryCount.Should().Be(1);
    }

    #endregion

    #region Second Frame - Lag Visible

    [Fact]
    public void SecondFrame_shall_show_lag_from_first_frame()
    {
        double lagCoeff = 0.1; // 10% lag for easy verification
        var model = new LagModel(new LagConfig(LagCoefficient: lagCoeff));

        // First frame: bright (10000)
        var brightFrame = CreateUniformFrame(8, 8, 10000);
        model.ApplyLag(brightFrame);

        // Second frame: dark (0)
        var darkFrame = CreateUniformFrame(8, 8, 0);
        var result = model.ApplyLag(darkFrame);

        // Expected: 0 + 0.1 * 10000 = 1000
        result[0, 0].Should().Be(1000,
            "second dark frame should show ghosting from bright first frame");
    }

    [Fact]
    public void SecondFrame_shall_add_lag_to_current_signal()
    {
        double lagCoeff = 0.05;
        var model = new LagModel(new LagConfig(LagCoefficient: lagCoeff));

        var frame1 = CreateUniformFrame(4, 4, 20000);
        model.ApplyLag(frame1);

        var frame2 = CreateUniformFrame(4, 4, 10000);
        var result = model.ApplyLag(frame2);

        // Expected: 10000 + 0.05 * 20000 = 11000
        result[0, 0].Should().Be(11000);
    }

    #endregion

    #region Higher Lag Coefficient = More Ghosting

    [Fact]
    public void HigherLagCoefficient_shall_produce_more_ghosting()
    {
        var lowLag = new LagModel(new LagConfig(LagCoefficient: 0.01));
        var highLag = new LagModel(new LagConfig(LagCoefficient: 0.1));

        var brightFrame = CreateUniformFrame(8, 8, 10000);
        var darkFrame = CreateUniformFrame(8, 8, 0);

        // Process first frame (bright)
        lowLag.ApplyLag(brightFrame);
        highLag.ApplyLag(brightFrame);

        // Process second frame (dark) - measure ghosting
        var resultLow = lowLag.ApplyLag(darkFrame);
        var resultHigh = highLag.ApplyLag(darkFrame);

        resultHigh[0, 0].Should().BeGreaterThan(resultLow[0, 0],
            "higher lag coefficient should produce more ghosting");
    }

    [Fact]
    public void ZeroLagCoefficient_shall_produce_no_ghosting()
    {
        var model = new LagModel(new LagConfig(LagCoefficient: 0.0));

        var brightFrame = CreateUniformFrame(8, 8, 10000);
        model.ApplyLag(brightFrame);

        var darkFrame = CreateUniformFrame(8, 8, 5000);
        var result = model.ApplyLag(darkFrame);

        // No lag -> output = input
        result[0, 0].Should().Be(5000);
    }

    #endregion

    #region Multi-Frame Decay

    [Fact]
    public void Lag_shall_decay_over_multiple_frames()
    {
        double lagCoeff = 0.1;
        var model = new LagModel(new LagConfig(LagCoefficient: lagCoeff, DecayOrder: 3));

        // First frame: bright
        var brightFrame = CreateUniformFrame(4, 4, 10000);
        model.ApplyLag(brightFrame);

        // Subsequent dark frames should show decreasing ghosting
        ushort[] ghostValues = new ushort[3];
        for (int i = 0; i < 3; i++)
        {
            var darkFrame = CreateUniformFrame(4, 4, 0);
            var result = model.ApplyLag(darkFrame);
            ghostValues[i] = result[0, 0];
        }

        // Each subsequent frame should have less ghosting
        ghostValues[0].Should().BeGreaterThan(ghostValues[1]);
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_shall_clear_history()
    {
        var model = new LagModel(new LagConfig(LagCoefficient: 0.1));

        var brightFrame = CreateUniformFrame(4, 4, 10000);
        model.ApplyLag(brightFrame);
        model.HistoryCount.Should().Be(1);

        model.Reset();

        model.HistoryCount.Should().Be(0);
    }

    [Fact]
    public void Reset_shall_eliminate_ghosting()
    {
        var model = new LagModel(new LagConfig(LagCoefficient: 0.1));

        // Build up history
        var brightFrame = CreateUniformFrame(4, 4, 10000);
        model.ApplyLag(brightFrame);

        // Reset clears all history
        model.Reset();

        // Next frame should have no ghosting
        var darkFrame = CreateUniformFrame(4, 4, 0);
        var result = model.ApplyLag(darkFrame);

        result[0, 0].Should().Be(0, "after reset, no ghosting should be present");
    }

    #endregion

    #region Null and Edge Cases

    [Fact]
    public void ApplyLag_shall_throw_for_null_frame()
    {
        var model = new LagModel();
        var act = () => model.ApplyLag(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyLag_shall_clamp_output_to_ushort_range()
    {
        var model = new LagModel(new LagConfig(LagCoefficient: 0.5));

        // First frame: near max
        var frame1 = CreateUniformFrame(4, 4, 60000);
        model.ApplyLag(frame1);

        // Second frame: also near max -> lag pushes beyond 65535
        var frame2 = CreateUniformFrame(4, 4, 60000);
        var result = model.ApplyLag(frame2);

        result[0, 0].Should().BeLessOrEqualTo((ushort)65535);
    }

    [Fact]
    public void ApplyLag_shall_handle_different_sized_frames_in_history()
    {
        // If frame dimensions change, lag from mismatched history should be ignored
        var model = new LagModel(new LagConfig(LagCoefficient: 0.1));

        var frame1 = CreateUniformFrame(8, 8, 10000);
        model.ApplyLag(frame1);

        // Different dimensions
        var frame2 = CreateUniformFrame(4, 4, 5000);
        var result = model.ApplyLag(frame2);

        // Should not crash, history dimension mismatch is handled
        result.GetLength(0).Should().Be(4);
        result.GetLength(1).Should().Be(4);
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
