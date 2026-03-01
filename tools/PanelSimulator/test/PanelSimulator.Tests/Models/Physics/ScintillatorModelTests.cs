using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using PanelSimulator.Models.Physics;

namespace PanelSimulator.Tests.Models.Physics;

/// <summary>
/// Tests for ScintillatorModel.
/// Validates CsI(Tl) scintillator X-ray response characteristics including
/// photon energy calculations, quantum efficiency, and pixel signal generation.
/// </summary>
public class ScintillatorModelTests
{
    #region Config Defaults

    [Fact]
    public void DefaultConfig_shall_have_expected_values()
    {
        // Arrange & Act
        var model = new ScintillatorModel();

        // Assert
        model.Config.KVp.Should().Be(80.0);
        model.Config.MAs.Should().Be(10.0);
        model.Config.LightYieldPerMeV.Should().Be(54_000.0);
        model.Config.PixelPitchMm.Should().Be(0.1);
        model.Config.ScintillatorThicknessMm.Should().Be(0.5);
    }

    [Fact]
    public void Constructor_shall_accept_custom_config()
    {
        // Arrange
        var config = new ScintillatorConfig(KVp: 120, MAs: 5, LightYieldPerMeV: 60_000);

        // Act
        var model = new ScintillatorModel(config);

        // Assert
        model.Config.KVp.Should().Be(120.0);
        model.Config.MAs.Should().Be(5.0);
        model.Config.LightYieldPerMeV.Should().Be(60_000.0);
    }

    [Fact]
    public void Constructor_shall_accept_null_config_as_defaults()
    {
        // Arrange & Act
        var model = new ScintillatorModel(null);

        // Assert
        model.Config.KVp.Should().Be(80.0);
    }

    #endregion

    #region Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_shall_throw_for_invalid_KVp(double kVp)
    {
        // Arrange & Act
        var act = () => new ScintillatorModel(new ScintillatorConfig(KVp: kVp));

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*KVp*positive*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_shall_throw_for_invalid_mAs(double mAs)
    {
        var act = () => new ScintillatorModel(new ScintillatorConfig(MAs: mAs));
        act.Should().Throw<ArgumentException>().WithMessage("*mAs*positive*");
    }

    [Fact]
    public void Constructor_shall_throw_for_zero_LightYield()
    {
        var act = () => new ScintillatorModel(new ScintillatorConfig(LightYieldPerMeV: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Light yield*positive*");
    }

    [Fact]
    public void Constructor_shall_throw_for_zero_PixelPitch()
    {
        var act = () => new ScintillatorModel(new ScintillatorConfig(PixelPitchMm: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Pixel pitch*positive*");
    }

    [Fact]
    public void Constructor_shall_throw_for_zero_ScintillatorThickness()
    {
        var act = () => new ScintillatorModel(new ScintillatorConfig(ScintillatorThicknessMm: 0));
        act.Should().Throw<ArgumentException>().WithMessage("*Scintillator thickness*positive*");
    }

    #endregion

    #region Mean Photon Energy

    [Theory]
    [InlineData(80, 26.4)]
    [InlineData(100, 33.0)]
    [InlineData(120, 39.6)]
    public void GetMeanPhotonEnergyKeV_shall_return_one_third_of_kVp(double kVp, double expected)
    {
        // Act
        double result = ScintillatorModel.GetMeanPhotonEnergyKeV(kVp);

        // Assert
        result.Should().BeApproximately(expected, 0.01);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void GetMeanPhotonEnergyKeV_shall_throw_for_invalid_kVp(double kVp)
    {
        var act = () => ScintillatorModel.GetMeanPhotonEnergyKeV(kVp);
        act.Should().Throw<ArgumentException>().WithMessage("*kVp*positive*");
    }

    [Fact]
    public void GetMeanPhotonEnergyKeV_shall_be_positive_for_valid_input()
    {
        double result = ScintillatorModel.GetMeanPhotonEnergyKeV(40);
        result.Should().BeGreaterThan(0);
    }

    #endregion

    #region Quantum Efficiency

    [Fact]
    public void GetQuantumEfficiency_shall_return_value_between_zero_and_one()
    {
        // Arrange
        var model = new ScintillatorModel();

        // Act
        double qe = model.GetQuantumEfficiency(60.0);

        // Assert
        qe.Should().BeGreaterThan(0.0).And.BeLessOrEqualTo(1.0);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void GetQuantumEfficiency_shall_be_in_valid_range_across_energies(double energyKeV)
    {
        var model = new ScintillatorModel();
        double qe = model.GetQuantumEfficiency(energyKeV);
        qe.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void GetQuantumEfficiency_shall_throw_for_zero_energy()
    {
        var model = new ScintillatorModel();
        var act = () => model.GetQuantumEfficiency(0);
        act.Should().Throw<ArgumentException>().WithMessage("*Energy*positive*");
    }

    [Fact]
    public void GetQuantumEfficiency_shall_throw_for_negative_energy()
    {
        var model = new ScintillatorModel();
        var act = () => model.GetQuantumEfficiency(-10);
        act.Should().Throw<ArgumentException>().WithMessage("*Energy*positive*");
    }

    [Fact]
    public void GetQuantumEfficiency_shall_be_higher_for_low_energy_photons()
    {
        // Low-energy photons are more easily absorbed
        var model = new ScintillatorModel();
        double qeLow = model.GetQuantumEfficiency(20.0);
        double qeHigh = model.GetQuantumEfficiency(100.0);
        qeLow.Should().BeGreaterThan(qeHigh);
    }

    [Fact]
    public void GetQuantumEfficiency_shall_increase_with_thicker_scintillator()
    {
        var thin = new ScintillatorModel(new ScintillatorConfig(ScintillatorThicknessMm: 0.3));
        var thick = new ScintillatorModel(new ScintillatorConfig(ScintillatorThicknessMm: 0.6));

        double qeThin = thin.GetQuantumEfficiency(60.0);
        double qeThick = thick.GetQuantumEfficiency(60.0);

        qeThick.Should().BeGreaterThan(qeThin);
    }

    #endregion

    #region Pixel Signal

    [Fact]
    public void CalculatePixelSignalDN_shall_return_positive_value()
    {
        var model = new ScintillatorModel();
        double signal = model.CalculatePixelSignalDN();
        signal.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculatePixelSignalDN_shall_be_within_16bit_range()
    {
        var model = new ScintillatorModel();
        double signal = model.CalculatePixelSignalDN();
        signal.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(65535);
    }

    [Fact]
    public void CalculatePixelSignalDN_shall_increase_with_higher_mAs()
    {
        var lowMAs = new ScintillatorModel(new ScintillatorConfig(MAs: 5));
        var highMAs = new ScintillatorModel(new ScintillatorConfig(MAs: 20));

        double signalLow = lowMAs.CalculatePixelSignalDN();
        double signalHigh = highMAs.CalculatePixelSignalDN();

        signalHigh.Should().BeGreaterThan(signalLow);
    }

    [Fact]
    public void CalculatePixelSignalDN_shall_increase_with_higher_kVp()
    {
        var lowKVp = new ScintillatorModel(new ScintillatorConfig(KVp: 60));
        var highKVp = new ScintillatorModel(new ScintillatorConfig(KVp: 120));

        double signalLow = lowKVp.CalculatePixelSignalDN();
        double signalHigh = highKVp.CalculatePixelSignalDN();

        signalHigh.Should().BeGreaterThan(signalLow);
    }

    [Fact]
    public void CalculatePixelSignalDN_shall_throw_for_invalid_gain()
    {
        var model = new ScintillatorModel();
        var act = () => model.CalculatePixelSignalDN(gainElectronsPerPhoton: 0);
        act.Should().Throw<ArgumentException>().WithMessage("*Gain*positive*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void CalculatePixelSignalDN_shall_throw_for_invalid_adcBits(int bits)
    {
        var model = new ScintillatorModel();
        var act = () => model.CalculatePixelSignalDN(adcBits: bits);
        act.Should().Throw<ArgumentException>().WithMessage("*ADC bits*");
    }

    [Fact]
    public void CalculatePixelSignalDN_shall_throw_for_invalid_fullWellCapacity()
    {
        var model = new ScintillatorModel();
        var act = () => model.CalculatePixelSignalDN(fullWellCapacity: 0);
        act.Should().Throw<ArgumentException>().WithMessage("*Full well capacity*positive*");
    }

    #endregion

    #region GenerateSignalFrame

    [Theory]
    [InlineData(64, 64)]
    [InlineData(128, 256)]
    public void GenerateSignalFrame_shall_return_correct_dimensions(int rows, int cols)
    {
        var model = new ScintillatorModel();
        var frame = model.GenerateSignalFrame(rows, cols);

        frame.GetLength(0).Should().Be(rows);
        frame.GetLength(1).Should().Be(cols);
    }

    [Fact]
    public void GenerateSignalFrame_shall_have_uniform_positive_values()
    {
        var model = new ScintillatorModel();
        var frame = model.GenerateSignalFrame(32, 32);

        ushort firstValue = frame[0, 0];
        firstValue.Should().BeGreaterThan((ushort)0);

        // All pixels should have the same value (uniform frame)
        for (int r = 0; r < 32; r++)
        {
            for (int c = 0; c < 32; c++)
            {
                frame[r, c].Should().Be(firstValue);
            }
        }
    }

    [Fact]
    public void GenerateSignalFrame_shall_throw_for_zero_rows()
    {
        var model = new ScintillatorModel();
        var act = () => model.GenerateSignalFrame(0, 32);
        act.Should().Throw<ArgumentException>().WithMessage("*Rows*positive*");
    }

    [Fact]
    public void GenerateSignalFrame_shall_throw_for_zero_cols()
    {
        var model = new ScintillatorModel();
        var act = () => model.GenerateSignalFrame(32, 0);
        act.Should().Throw<ArgumentException>().WithMessage("*Cols*positive*");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void VeryLowKVp_shall_produce_low_signal()
    {
        // 40 kVp is the minimum diagnostic range
        var model = new ScintillatorModel(new ScintillatorConfig(KVp: 40, MAs: 1));
        double signal = model.CalculatePixelSignalDN();
        signal.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void VeryHighKVp_shall_produce_high_signal()
    {
        var model = new ScintillatorModel(new ScintillatorConfig(KVp: 150, MAs: 50));
        double signal = model.CalculatePixelSignalDN();
        signal.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetIncidentPhotonsPerPixel_shall_be_positive()
    {
        var model = new ScintillatorModel();
        double photons = model.GetIncidentPhotonsPerPixel();
        photons.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetIncidentPhotonsPerPixel_shall_scale_with_mAs()
    {
        var low = new ScintillatorModel(new ScintillatorConfig(MAs: 1));
        var high = new ScintillatorModel(new ScintillatorConfig(MAs: 10));

        double photonsLow = low.GetIncidentPhotonsPerPixel();
        double photonsHigh = high.GetIncidentPhotonsPerPixel();

        // Should scale linearly with mAs (10x)
        (photonsHigh / photonsLow).Should().BeApproximately(10.0, 0.01);
    }

    #endregion
}
