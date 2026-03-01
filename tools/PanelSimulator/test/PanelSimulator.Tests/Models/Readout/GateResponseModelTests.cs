using System;
using FluentAssertions;
using Xunit;
using PanelSimulator.Models.Readout;

namespace PanelSimulator.Tests.Models.Readout;

/// <summary>
/// Tests for GateResponseModel.
/// Validates gate (TFT switch) response behavior including signal level calculation,
/// dark current, exposure scaling, and saturation clamping.
/// </summary>
public class GateResponseModelTests
{
    #region Config Defaults

    [Fact]
    public void DefaultConfig_shall_have_expected_values()
    {
        var model = new GateResponseModel();

        model.Config.MaxExposureTimeMs.Should().Be(200.0);
        model.Config.DarkCurrentPerMs.Should().Be(0.5);
        model.Config.SaturationLevel.Should().Be(65535);
    }

    [Fact]
    public void Constructor_shall_accept_null_config()
    {
        var model = new GateResponseModel(null);
        model.Config.MaxExposureTimeMs.Should().Be(200.0);
    }

    [Fact]
    public void Constructor_shall_accept_custom_config()
    {
        var config = new GateResponseConfig(
            MaxExposureTimeMs: 100.0, DarkCurrentPerMs: 1.0, SaturationLevel: 4095);
        var model = new GateResponseModel(config);

        model.Config.MaxExposureTimeMs.Should().Be(100.0);
        model.Config.DarkCurrentPerMs.Should().Be(1.0);
        model.Config.SaturationLevel.Should().Be(4095);
    }

    #endregion

    #region Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_shall_throw_for_invalid_max_exposure(double maxExp)
    {
        var act = () => new GateResponseModel(new GateResponseConfig(MaxExposureTimeMs: maxExp));
        act.Should().Throw<ArgumentException>().WithMessage("*Max exposure time*positive*");
    }

    [Fact]
    public void Constructor_shall_throw_for_negative_dark_current()
    {
        var act = () => new GateResponseModel(new GateResponseConfig(DarkCurrentPerMs: -0.1));
        act.Should().Throw<ArgumentException>().WithMessage("*Dark current*non-negative*");
    }

    [Fact]
    public void Constructor_shall_throw_for_zero_saturation_level()
    {
        var act = () => new GateResponseModel(new GateResponseConfig(SaturationLevel: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Saturation level*greater than zero*");
    }

    [Fact]
    public void Constructor_shall_accept_zero_dark_current()
    {
        var model = new GateResponseModel(new GateResponseConfig(DarkCurrentPerMs: 0.0));
        model.Config.DarkCurrentPerMs.Should().Be(0.0);
    }

    #endregion

    #region Gate Off - Dark Current Only

    [Fact]
    public void GateOff_shall_return_dark_current_only()
    {
        var config = new GateResponseConfig(DarkCurrentPerMs: 1.0);
        var model = new GateResponseModel(config);

        ushort signal = model.CalculateSignalLevel(
            gateOn: false, exposureTimeMs: 100.0, kvp: 80, mAs: 10);

        // Dark current only: 1.0 * 100 = 100 DN
        signal.Should().Be(100);
    }

    [Fact]
    public void GateOff_shall_ignore_kvp_and_mAs()
    {
        var config = new GateResponseConfig(DarkCurrentPerMs: 0.5);
        var model = new GateResponseModel(config);

        ushort signal1 = model.CalculateSignalLevel(false, 100.0, kvp: 80, mAs: 10);
        ushort signal2 = model.CalculateSignalLevel(false, 100.0, kvp: 150, mAs: 50);

        // Both should be the same: dark current only
        signal1.Should().Be(signal2);
    }

    [Fact]
    public void GateOff_with_zero_exposure_shall_return_zero()
    {
        var model = new GateResponseModel();

        ushort signal = model.CalculateSignalLevel(false, 0.0, kvp: 80, mAs: 10);

        signal.Should().Be(0);
    }

    #endregion

    #region Gate On - Signal Proportional to Parameters

    [Fact]
    public void GateOn_shall_return_signal_proportional_to_exposure_time()
    {
        var config = new GateResponseConfig(DarkCurrentPerMs: 0.0);
        var model = new GateResponseModel(config);

        ushort signalShort = model.CalculateSignalLevel(true, 50.0, kvp: 80, mAs: 10);
        ushort signalLong = model.CalculateSignalLevel(true, 100.0, kvp: 80, mAs: 10);

        signalLong.Should().BeGreaterThan(signalShort);
    }

    [Fact]
    public void GateOn_higher_kvp_shall_give_higher_signal()
    {
        var config = new GateResponseConfig(DarkCurrentPerMs: 0.0);
        var model = new GateResponseModel(config);

        ushort signalLow = model.CalculateSignalLevel(true, 100.0, kvp: 60, mAs: 10);
        ushort signalHigh = model.CalculateSignalLevel(true, 100.0, kvp: 120, mAs: 10);

        signalHigh.Should().BeGreaterThan(signalLow);
    }

    [Fact]
    public void GateOn_higher_mAs_shall_give_higher_signal()
    {
        var config = new GateResponseConfig(DarkCurrentPerMs: 0.0);
        var model = new GateResponseModel(config);

        ushort signalLow = model.CalculateSignalLevel(true, 100.0, kvp: 80, mAs: 5);
        ushort signalHigh = model.CalculateSignalLevel(true, 100.0, kvp: 80, mAs: 20);

        signalHigh.Should().BeGreaterThan(signalLow);
    }

    [Fact]
    public void GateOn_zero_exposure_shall_return_zero()
    {
        var config = new GateResponseConfig(DarkCurrentPerMs: 0.0);
        var model = new GateResponseModel(config);

        ushort signal = model.CalculateSignalLevel(true, 0.0, kvp: 80, mAs: 10);

        signal.Should().Be(0);
    }

    [Fact]
    public void GateOn_signal_shall_be_clamped_to_saturation()
    {
        // Use low saturation to trigger clamping
        var config = new GateResponseConfig(SaturationLevel: 1000, DarkCurrentPerMs: 0.0);
        var model = new GateResponseModel(config);

        ushort signal = model.CalculateSignalLevel(true, 200.0, kvp: 150, mAs: 100);

        signal.Should().Be(1000);
    }

    #endregion

    #region CalculateSignalLevel Validation

    [Fact]
    public void CalculateSignalLevel_shall_throw_for_negative_exposure()
    {
        var model = new GateResponseModel();
        var act = () => model.CalculateSignalLevel(true, -1.0, kvp: 80, mAs: 10);
        act.Should().Throw<ArgumentException>().WithMessage("*Exposure time*non-negative*");
    }

    #endregion

    #region ApplyGateResponse

    [Fact]
    public void ApplyGateResponse_gate_off_shall_fill_with_dark_current()
    {
        var config = new GateResponseConfig(DarkCurrentPerMs: 2.0);
        var model = new GateResponseModel(config);
        var frame = CreateUniformFrame(4, 4, 10000);

        var result = model.ApplyGateResponse(frame, gateOn: false, exposureTimeMs: 50.0);

        // Dark current: 2.0 * 50 = 100 DN
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                result[r, c].Should().Be(100);
            }
        }
    }

    [Fact]
    public void ApplyGateResponse_gate_on_shall_scale_frame()
    {
        var config = new GateResponseConfig(MaxExposureTimeMs: 200.0, DarkCurrentPerMs: 0.0);
        var model = new GateResponseModel(config);
        var frame = CreateUniformFrame(4, 4, 10000);

        // 100ms / 200ms = 0.5 ratio -> 10000 * 0.5 = 5000
        var result = model.ApplyGateResponse(frame, gateOn: true, exposureTimeMs: 100.0);

        result[0, 0].Should().Be(5000);
    }

    [Fact]
    public void ApplyGateResponse_shall_add_dark_current_when_gate_on()
    {
        var config = new GateResponseConfig(MaxExposureTimeMs: 100.0, DarkCurrentPerMs: 1.0);
        var model = new GateResponseModel(config);
        var frame = CreateUniformFrame(2, 2, 10000);

        // ratio = 100/100 = 1.0 -> scaled = 10000, dark = 1.0 * 100 = 100
        // total = 10100
        var result = model.ApplyGateResponse(frame, gateOn: true, exposureTimeMs: 100.0);

        result[0, 0].Should().Be(10100);
    }

    [Fact]
    public void ApplyGateResponse_shall_clamp_to_saturation()
    {
        var config = new GateResponseConfig(
            MaxExposureTimeMs: 100.0, DarkCurrentPerMs: 0.0, SaturationLevel: 5000);
        var model = new GateResponseModel(config);
        var frame = CreateUniformFrame(2, 2, 10000);

        var result = model.ApplyGateResponse(frame, gateOn: true, exposureTimeMs: 100.0);

        result[0, 0].Should().Be(5000);
    }

    [Fact]
    public void ApplyGateResponse_shall_throw_for_null_frame()
    {
        var model = new GateResponseModel();
        var act = () => model.ApplyGateResponse(null!, gateOn: true, exposureTimeMs: 100.0);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyGateResponse_shall_throw_for_negative_exposure()
    {
        var model = new GateResponseModel();
        var frame = CreateUniformFrame(2, 2, 100);
        var act = () => model.ApplyGateResponse(frame, gateOn: true, exposureTimeMs: -1.0);
        act.Should().Throw<ArgumentException>().WithMessage("*Exposure time*non-negative*");
    }

    [Fact]
    public void ApplyGateResponse_shall_preserve_frame_dimensions()
    {
        var model = new GateResponseModel();
        var frame = CreateUniformFrame(8, 16, 500);

        var result = model.ApplyGateResponse(frame, gateOn: true, exposureTimeMs: 100.0);

        result.GetLength(0).Should().Be(8);
        result.GetLength(1).Should().Be(16);
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
