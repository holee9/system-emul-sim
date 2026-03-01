using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using PanelSimulator.Models.Physics;

namespace PanelSimulator.Tests.Models.Physics;

/// <summary>
/// Tests for ExposureModel.
/// Validates gate_on pulse-based exposure timing, signal integration,
/// and dark current calculation.
/// </summary>
public class ExposureModelTests
{
    #region Config and Construction

    [Fact]
    public void DefaultConfig_shall_have_expected_values()
    {
        var model = new ExposureModel();

        model.Config.ExposureTimeMs.Should().Be(100.0);
        model.Config.GatePulseCount.Should().Be(1);
        model.Config.GatePulseWidthUs.Should().Be(100_000.0);
        model.Config.DarkCurrentRatePerSec.Should().Be(50.0);
    }

    [Fact]
    public void Constructor_shall_accept_null_config()
    {
        var model = new ExposureModel(null);
        model.Config.ExposureTimeMs.Should().Be(100.0);
    }

    [Fact]
    public void Constructor_shall_throw_for_zero_exposure_time()
    {
        var act = () => new ExposureModel(new ExposureConfig(ExposureTimeMs: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Exposure time*positive*");
    }

    [Fact]
    public void Constructor_shall_throw_for_zero_gate_pulse_count()
    {
        var act = () => new ExposureModel(new ExposureConfig(GatePulseCount: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Gate pulse count*at least 1*");
    }

    [Fact]
    public void Constructor_shall_throw_for_zero_gate_pulse_width()
    {
        var act = () => new ExposureModel(new ExposureConfig(GatePulseWidthUs: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Gate pulse width*positive*");
    }

    [Fact]
    public void Constructor_shall_throw_for_negative_dark_current()
    {
        var act = () => new ExposureModel(new ExposureConfig(DarkCurrentRatePerSec: -1));
        act.Should().Throw<ArgumentException>().WithMessage("*Dark current*non-negative*");
    }

    #endregion

    #region Effective Integration Time

    [Fact]
    public void CalculateEffectiveIntegrationTimeMs_shall_return_correct_value()
    {
        // Default: 1 pulse * 100,000 us / 1000 = 100 ms
        var model = new ExposureModel();
        double result = model.CalculateEffectiveIntegrationTimeMs();
        result.Should().BeApproximately(100.0, 0.001);
    }

    [Fact]
    public void CalculateEffectiveIntegrationTimeMs_shall_scale_with_pulse_count()
    {
        var config = new ExposureConfig(GatePulseCount: 4, GatePulseWidthUs: 50_000);
        var model = new ExposureModel(config);

        // 4 * 50,000 / 1000 = 200 ms
        double result = model.CalculateEffectiveIntegrationTimeMs();
        result.Should().BeApproximately(200.0, 0.001);
    }

    [Fact]
    public void CalculateEffectiveIntegrationTimeMs_shall_scale_with_pulse_width()
    {
        var config = new ExposureConfig(GatePulseCount: 1, GatePulseWidthUs: 200_000);
        var model = new ExposureModel(config);

        double result = model.CalculateEffectiveIntegrationTimeMs();
        result.Should().BeApproximately(200.0, 0.001);
    }

    #endregion

    #region Exposure Scaling Factor

    [Fact]
    public void CalculateExposureScalingFactor_shall_be_one_for_default()
    {
        // Effective = 100 ms, Nominal = 100 ms -> factor = 1.0
        var model = new ExposureModel();
        double factor = model.CalculateExposureScalingFactor();
        factor.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CalculateExposureScalingFactor_shall_scale_proportionally()
    {
        // Effective = 200 ms, Nominal = 100 ms -> factor = 2.0
        var config = new ExposureConfig(
            ExposureTimeMs: 100,
            GatePulseCount: 2,
            GatePulseWidthUs: 100_000);
        var model = new ExposureModel(config);

        double factor = model.CalculateExposureScalingFactor();
        factor.Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public void CalculateExposureScalingFactor_shall_be_less_than_one_for_short_exposure()
    {
        // Effective = 50 ms, Nominal = 100 ms -> factor = 0.5
        var config = new ExposureConfig(
            ExposureTimeMs: 100,
            GatePulseCount: 1,
            GatePulseWidthUs: 50_000);
        var model = new ExposureModel(config);

        double factor = model.CalculateExposureScalingFactor();
        factor.Should().BeApproximately(0.5, 0.001);
    }

    #endregion

    #region ApplyExposureScaling

    [Fact]
    public void ApplyExposureScaling_shall_return_correct_dimensions()
    {
        var model = new ExposureModel();
        var frame = CreateUniformFrame(64, 128, 10000);

        var result = model.ApplyExposureScaling(frame);

        result.GetLength(0).Should().Be(64);
        result.GetLength(1).Should().Be(128);
    }

    [Fact]
    public void ApplyExposureScaling_shall_preserve_values_with_factor_one()
    {
        var model = new ExposureModel(); // factor = 1.0
        var frame = CreateUniformFrame(16, 16, 10000);

        var result = model.ApplyExposureScaling(frame);

        result[0, 0].Should().Be(10000);
    }

    [Fact]
    public void ApplyExposureScaling_shall_double_values_with_factor_two()
    {
        var config = new ExposureConfig(
            ExposureTimeMs: 100,
            GatePulseCount: 2,
            GatePulseWidthUs: 100_000);
        var model = new ExposureModel(config);
        var frame = CreateUniformFrame(4, 4, 10000);

        var result = model.ApplyExposureScaling(frame);

        result[0, 0].Should().Be(20000);
    }

    [Fact]
    public void ApplyExposureScaling_shall_clamp_to_ushort_max()
    {
        var config = new ExposureConfig(
            ExposureTimeMs: 100,
            GatePulseCount: 10,
            GatePulseWidthUs: 100_000);
        var model = new ExposureModel(config);
        var frame = CreateUniformFrame(4, 4, 50000);

        var result = model.ApplyExposureScaling(frame);

        result[0, 0].Should().Be(65535);
    }

    [Fact]
    public void ApplyExposureScaling_shall_throw_for_null_frame()
    {
        var model = new ExposureModel();
        var act = () => model.ApplyExposureScaling(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Dark Current

    [Fact]
    public void CalculateDarkCurrentElectrons_shall_return_positive_value()
    {
        var model = new ExposureModel();
        double darkCurrent = model.CalculateDarkCurrentElectrons();
        darkCurrent.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateDarkCurrentElectrons_shall_scale_with_exposure_time()
    {
        // Default: 100 ms effective, 50 e-/s -> 5 electrons
        var model = new ExposureModel();
        double darkCurrent = model.CalculateDarkCurrentElectrons();
        darkCurrent.Should().BeApproximately(5.0, 0.01);
    }

    [Fact]
    public void CalculateDarkCurrentElectrons_shall_be_zero_with_zero_rate()
    {
        var config = new ExposureConfig(DarkCurrentRatePerSec: 0);
        var model = new ExposureModel(config);

        double darkCurrent = model.CalculateDarkCurrentElectrons();
        darkCurrent.Should().Be(0);
    }

    [Fact]
    public void GenerateDarkCurrentFrame_shall_return_correct_dimensions()
    {
        var model = new ExposureModel();
        var frame = model.GenerateDarkCurrentFrame(32, 64);

        frame.GetLength(0).Should().Be(32);
        frame.GetLength(1).Should().Be(64);
    }

    [Fact]
    public void GenerateDarkCurrentFrame_shall_have_uniform_values()
    {
        var model = new ExposureModel();
        var frame = model.GenerateDarkCurrentFrame(16, 16);

        ushort firstValue = frame[0, 0];
        for (int r = 0; r < 16; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                frame[r, c].Should().Be(firstValue);
            }
        }
    }

    [Fact]
    public void GenerateDarkCurrentFrame_shall_throw_for_invalid_dimensions()
    {
        var model = new ExposureModel();
        var act = () => model.GenerateDarkCurrentFrame(0, 16);
        act.Should().Throw<ArgumentException>().WithMessage("*Rows*positive*");
    }

    #endregion

    #region Accumulated Exposure (Thread Safety)

    [Fact]
    public void AccumulatedExposureMs_shall_start_at_zero()
    {
        var model = new ExposureModel();
        model.AccumulatedExposureMs.Should().Be(0);
        model.AccumulatedPulses.Should().Be(0);
    }

    [Fact]
    public void AccumulatedExposureMs_shall_increase_after_apply()
    {
        var model = new ExposureModel();
        var frame = CreateUniformFrame(4, 4, 1000);

        model.ApplyExposureScaling(frame);

        model.AccumulatedExposureMs.Should().BeApproximately(100.0, 0.001);
        model.AccumulatedPulses.Should().Be(1);
    }

    [Fact]
    public void AccumulatedExposureMs_shall_accumulate_multiple_frames()
    {
        var model = new ExposureModel();
        var frame = CreateUniformFrame(4, 4, 1000);

        model.ApplyExposureScaling(frame);
        model.ApplyExposureScaling(frame);
        model.ApplyExposureScaling(frame);

        model.AccumulatedExposureMs.Should().BeApproximately(300.0, 0.001);
        model.AccumulatedPulses.Should().Be(3);
    }

    [Fact]
    public void Reset_shall_clear_accumulated_exposure()
    {
        var model = new ExposureModel();
        var frame = CreateUniformFrame(4, 4, 1000);

        model.ApplyExposureScaling(frame);
        model.Reset();

        model.AccumulatedExposureMs.Should().Be(0);
        model.AccumulatedPulses.Should().Be(0);
    }

    [Fact]
    public void AccumulatedExposure_shall_be_thread_safe()
    {
        var config = new ExposureConfig(GatePulseCount: 1, GatePulseWidthUs: 1000);
        var model = new ExposureModel(config);
        var frame = CreateUniformFrame(4, 4, 1000);
        int iterations = 100;

        // Run concurrent exposure scaling
        Parallel.For(0, iterations, _ =>
        {
            model.ApplyExposureScaling(frame);
        });

        // Each iteration adds 1 ms of exposure
        model.AccumulatedExposureMs.Should().BeApproximately(iterations * 1.0, 0.01);
        model.AccumulatedPulses.Should().Be(iterations);
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
