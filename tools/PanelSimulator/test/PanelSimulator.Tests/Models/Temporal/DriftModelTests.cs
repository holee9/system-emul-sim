using System;
using FluentAssertions;
using Xunit;
using PanelSimulator.Models.Temporal;

namespace PanelSimulator.Tests.Models.Temporal;

/// <summary>
/// Tests for DriftModel.
/// Validates temperature-dependent offset drift simulation with
/// linear and sinusoidal patterns.
/// </summary>
public class DriftModelTests
{
    #region Construction and Config

    [Fact]
    public void DefaultConfig_shall_have_expected_values()
    {
        var model = new DriftModel();

        model.Config.DriftRateDNPerHour.Should().Be(2.0);
        model.Config.Pattern.Should().Be(DriftPattern.Linear);
        model.Config.SinusoidalPeriodHours.Should().Be(1.0);
        model.Config.FrameIntervalMs.Should().Be(100.0);
    }

    [Fact]
    public void Constructor_shall_accept_null_config()
    {
        var model = new DriftModel(null);
        model.Config.DriftRateDNPerHour.Should().Be(2.0);
    }

    [Fact]
    public void Constructor_shall_throw_for_negative_drift_rate()
    {
        var act = () => new DriftModel(new DriftConfig(DriftRateDNPerHour: -1));
        act.Should().Throw<ArgumentException>().WithMessage("*Drift rate*non-negative*");
    }

    [Fact]
    public void Constructor_shall_throw_for_zero_sinusoidal_period()
    {
        var act = () => new DriftModel(new DriftConfig(SinusoidalPeriodHours: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Sinusoidal period*positive*");
    }

    [Fact]
    public void Constructor_shall_throw_for_zero_frame_interval()
    {
        var act = () => new DriftModel(new DriftConfig(FrameIntervalMs: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Frame interval*positive*");
    }

    #endregion

    #region Initial Frame - No Drift

    [Fact]
    public void InitialFrame_shall_have_no_drift()
    {
        var model = new DriftModel(new DriftConfig(DriftRateDNPerHour: 10));
        var frame = CreateUniformFrame(8, 8, 10000);

        // Frame count = 0, elapsed = 0 -> drift = 0
        var result = model.ApplyDrift(frame);

        // With zero elapsed time, drift should be negligible
        result[0, 0].Should().Be(10000);
    }

    [Fact]
    public void FrameCount_shall_start_at_zero()
    {
        var model = new DriftModel();
        model.FrameCount.Should().Be(0);
        model.ElapsedHours.Should().Be(0);
    }

    [Fact]
    public void FrameCount_shall_increment_after_ApplyDrift()
    {
        var model = new DriftModel();
        var frame = CreateUniformFrame(4, 4, 1000);

        model.ApplyDrift(frame);

        model.FrameCount.Should().Be(1);
    }

    [Fact]
    public void CalculateCurrentDriftDN_shall_be_zero_initially()
    {
        var model = new DriftModel();
        double drift = model.CalculateCurrentDriftDN();
        drift.Should().Be(0);
    }

    #endregion

    #region Linear Drift

    [Fact]
    public void LinearDrift_shall_increase_over_time()
    {
        // 10 DN/hour, 100ms frames -> after 36000 frames = 1 hour -> drift = 10 DN
        var config = new DriftConfig(
            DriftRateDNPerHour: 10,
            Pattern: DriftPattern.Linear,
            FrameIntervalMs: 100);
        var model = new DriftModel(config);
        var frame = CreateUniformFrame(4, 4, 10000);

        // Apply many frames to accumulate drift
        ushort[] valueAtFrame = new ushort[3];
        for (int i = 0; i < 36000; i++)
        {
            var result = model.ApplyDrift(frame);
            if (i == 0) valueAtFrame[0] = result[0, 0];
            if (i == 18000) valueAtFrame[1] = result[0, 0];
            if (i == 35999) valueAtFrame[2] = result[0, 0];
        }

        // Values should increase over time for linear drift
        valueAtFrame[2].Should().BeGreaterThan(valueAtFrame[0],
            "linear drift should increase pixel values over time");
    }

    [Fact]
    public void CalculateDriftAtTime_linear_shall_be_proportional_to_time()
    {
        var config = new DriftConfig(
            DriftRateDNPerHour: 5,
            Pattern: DriftPattern.Linear);
        var model = new DriftModel(config);

        double drift1h = model.CalculateDriftAtTime(1.0);
        double drift2h = model.CalculateDriftAtTime(2.0);

        drift1h.Should().BeApproximately(5.0, 0.001);
        drift2h.Should().BeApproximately(10.0, 0.001);
        drift2h.Should().BeApproximately(drift1h * 2, 0.001);
    }

    [Fact]
    public void CalculateDriftAtTime_shall_return_zero_for_zero_rate()
    {
        var config = new DriftConfig(DriftRateDNPerHour: 0, Pattern: DriftPattern.Linear);
        var model = new DriftModel(config);

        double drift = model.CalculateDriftAtTime(1.0);
        drift.Should().Be(0);
    }

    #endregion

    #region Sinusoidal Drift

    [Fact]
    public void SinusoidalDrift_shall_oscillate()
    {
        var config = new DriftConfig(
            DriftRateDNPerHour: 10,
            Pattern: DriftPattern.Sinusoidal,
            SinusoidalPeriodHours: 1.0);
        var model = new DriftModel(config);

        // At t=0: sin(0) = 0
        double driftAt0 = model.CalculateDriftAtTime(0);
        driftAt0.Should().BeApproximately(0, 0.01);

        // At t=0.25h: sin(pi/2) = 1 -> drift = 10
        double driftAtQuarter = model.CalculateDriftAtTime(0.25);
        driftAtQuarter.Should().BeApproximately(10.0, 0.01);

        // At t=0.5h: sin(pi) = 0
        double driftAtHalf = model.CalculateDriftAtTime(0.5);
        driftAtHalf.Should().BeApproximately(0, 0.01);

        // At t=0.75h: sin(3*pi/2) = -1 -> drift = -10
        double driftAtThreeQuarter = model.CalculateDriftAtTime(0.75);
        driftAtThreeQuarter.Should().BeApproximately(-10.0, 0.01);
    }

    [Fact]
    public void SinusoidalDrift_shall_be_bounded_by_drift_rate()
    {
        var config = new DriftConfig(
            DriftRateDNPerHour: 5,
            Pattern: DriftPattern.Sinusoidal,
            SinusoidalPeriodHours: 2.0);
        var model = new DriftModel(config);

        // Check multiple time points
        for (double t = 0; t <= 4.0; t += 0.1)
        {
            double drift = model.CalculateDriftAtTime(t);
            Math.Abs(drift).Should().BeLessOrEqualTo(5.01,
                $"sinusoidal drift at t={t}h should be bounded by drift rate");
        }
    }

    [Fact]
    public void SinusoidalDrift_shall_return_zero_for_zero_rate()
    {
        var config = new DriftConfig(
            DriftRateDNPerHour: 0,
            Pattern: DriftPattern.Sinusoidal);
        var model = new DriftModel(config);

        double drift = model.CalculateDriftAtTime(0.25);
        drift.Should().Be(0);
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_shall_clear_frame_count()
    {
        var model = new DriftModel();
        var frame = CreateUniformFrame(4, 4, 1000);

        model.ApplyDrift(frame);
        model.ApplyDrift(frame);
        model.FrameCount.Should().Be(2);

        model.Reset();

        model.FrameCount.Should().Be(0);
        model.ElapsedHours.Should().Be(0);
    }

    [Fact]
    public void Reset_shall_return_to_zero_drift()
    {
        var config = new DriftConfig(
            DriftRateDNPerHour: 100,
            Pattern: DriftPattern.Linear,
            FrameIntervalMs: 1000); // 1 second per frame
        var model = new DriftModel(config);
        var frame = CreateUniformFrame(4, 4, 10000);

        // Apply many frames to accumulate significant drift
        for (int i = 0; i < 3600; i++) // 1 hour
        {
            model.ApplyDrift(frame);
        }

        model.FrameCount.Should().BeGreaterThan(0);
        model.Reset();

        // After reset, drift should be zero again
        double drift = model.CalculateCurrentDriftDN();
        drift.Should().Be(0);

        // Next frame should have no drift
        var result = model.ApplyDrift(frame);
        result[0, 0].Should().Be(10000);
    }

    #endregion

    #region ApplyDrift Behavior

    [Fact]
    public void ApplyDrift_shall_throw_for_null_frame()
    {
        var model = new DriftModel();
        var act = () => model.ApplyDrift(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyDrift_shall_return_correct_dimensions()
    {
        var model = new DriftModel();
        var frame = CreateUniformFrame(32, 64, 5000);

        var result = model.ApplyDrift(frame);

        result.GetLength(0).Should().Be(32);
        result.GetLength(1).Should().Be(64);
    }

    [Fact]
    public void ApplyDrift_shall_not_modify_input_frame()
    {
        var model = new DriftModel(new DriftConfig(DriftRateDNPerHour: 100));
        var frame = CreateUniformFrame(4, 4, 10000);

        // Apply drift many times to build up offset
        for (int i = 0; i < 100; i++)
        {
            model.ApplyDrift(frame);
        }

        // Original frame should be unchanged
        frame[0, 0].Should().Be(10000);
    }

    [Fact]
    public void ApplyDrift_shall_clamp_to_ushort_range()
    {
        var config = new DriftConfig(
            DriftRateDNPerHour: 1000,
            Pattern: DriftPattern.Linear,
            FrameIntervalMs: 3600_000); // 1 hour per frame for fast drift
        var model = new DriftModel(config);

        // First frame at t=0 -> no drift
        var frame = CreateUniformFrame(4, 4, 60000);
        model.ApplyDrift(frame);

        // Second frame at t=1h -> drift = 1000 DN, 60000+1000 > clamped
        var result = model.ApplyDrift(frame);
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                result[r, c].Should().BeLessOrEqualTo((ushort)65535);
            }
        }
    }

    [Fact]
    public void ElapsedHours_shall_track_correctly()
    {
        var config = new DriftConfig(FrameIntervalMs: 1000); // 1 sec per frame
        var model = new DriftModel(config);
        var frame = CreateUniformFrame(4, 4, 1000);

        // 3600 frames * 1 sec = 1 hour
        for (int i = 0; i < 3600; i++)
        {
            model.ApplyDrift(frame);
        }

        model.ElapsedHours.Should().BeApproximately(1.0, 0.001);
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
