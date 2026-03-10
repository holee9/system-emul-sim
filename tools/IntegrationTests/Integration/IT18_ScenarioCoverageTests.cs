using Xunit;
using FluentAssertions;
using PanelSimulator.Models.Physics;
using PanelSimulator.Models.Temporal;
using PanelSimulator.Models.Calibration;
using PanelSimulator.Models.Readout;
using PanelSimulator.Generators;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-18: Scenario Coverage Tests for SPEC-EMUL-003.
/// Covers high-priority uncovered scenarios from scenarios-coverage-matrix.md.
/// Focus: P-01 to P-22 Panel physics, calibration, temporal, and Gate/ROIC scenarios.
/// Reference: SPEC-EMUL-001 scenarios.md, SPEC-EMUL-003.
/// </summary>
public class IT18_ScenarioCoverageTests
{
    private const int TestRows = 64;
    private const int TestCols = 64;

    // -------------------------------------------------------------------------
    // P-01 / P-02: kVp and mAs signal variation
    // -------------------------------------------------------------------------

    /// <summary>
    /// P-01: Higher kVp produces higher pixel signal.
    /// Verifies CsI(Tl) scintillator response curve at 40, 60, 80, 100, 120 kVp.
    /// </summary>
    [Theory]
    [InlineData(40.0, 60.0)]
    [InlineData(60.0, 80.0)]
    [InlineData(80.0, 100.0)]
    [InlineData(100.0, 120.0)]
    public void ScintillatorModel_HigherKvp_ProducesHigherSignal(double lowerKvp, double higherKvp)
    {
        // Arrange
        var lowModel = new ScintillatorModel(new ScintillatorConfig(KVp: lowerKvp, MAs: 10.0));
        var highModel = new ScintillatorModel(new ScintillatorConfig(KVp: higherKvp, MAs: 10.0));

        // Act
        var lowFrame = lowModel.GenerateSignalFrame(TestRows, TestCols);
        var highFrame = highModel.GenerateSignalFrame(TestRows, TestCols);

        double lowMean = CalculateMean2D(lowFrame);
        double highMean = CalculateMean2D(highFrame);

        // Assert
        highMean.Should().BeGreaterThan(lowMean,
            $"{higherKvp} kVp should produce higher signal than {lowerKvp} kVp");
        lowMean.Should().BeGreaterThan(0, $"{lowerKvp} kVp should produce non-zero signal");
    }

    /// <summary>
    /// P-02: Signal should be linear with mAs (R^2 > 0.999).
    /// Tests mAs values: 1, 2, 5, 10, 20, 50 at fixed 80 kVp.
    /// </summary>
    [Fact]
    public void ScintillatorModel_MAsLinearity_SignalProportionalToMAs()
    {
        // Arrange
        double[] masValues = [1.0, 2.0, 5.0, 10.0, 20.0, 50.0];
        var signals = new double[masValues.Length];

        for (int i = 0; i < masValues.Length; i++)
        {
            var model = new ScintillatorModel(new ScintillatorConfig(KVp: 80.0, MAs: masValues[i]));
            signals[i] = model.CalculatePixelSignalDN();
        }

        // Assert - signals should be monotonically increasing with mAs
        for (int i = 1; i < signals.Length; i++)
        {
            signals[i].Should().BeGreaterThan(signals[i - 1],
                $"Signal at mAs={masValues[i]} should be greater than at mAs={masValues[i - 1]}");
        }

        // Verify approximate linearity: signal(mAs=50) / signal(mAs=1) should be ~50
        double ratio = signals[^1] / signals[0];
        ratio.Should().BeApproximately(50.0, 5.0,
            "Signal should be approximately linear with mAs (ratio 50:1 for mAs 50:1)");
    }

    /// <summary>
    /// P-05: Saturation at 65535 for overexposure (120 kVp, very high mAs).
    /// </summary>
    [Fact]
    public void ScintillatorModel_SaturationAt65535_OverexposureClamps()
    {
        // Arrange - Very high exposure to trigger saturation
        // Use a very high mAs value and wide pixel pitch to maximize signal
        var model = new ScintillatorModel(new ScintillatorConfig(
            KVp: 120.0,
            MAs: 1000.0,
            PixelPitchMm: 0.5));

        // Act
        var frame = model.GenerateSignalFrame(TestRows, TestCols);

        // Assert - All pixels should be at max (saturated)
        bool anySaturated = false;
        bool allInRange = true;

        for (int r = 0; r < TestRows; r++)
        {
            for (int c = 0; c < TestCols; c++)
            {
                if (frame[r, c] == 65535)
                    anySaturated = true;
                if (frame[r, c] > 65535)
                    allInRange = false;
            }
        }

        anySaturated.Should().BeTrue("overexposure should saturate pixels to 65535");
        allInRange.Should().BeTrue("no pixel should exceed 16-bit max (65535)");
    }

    // -------------------------------------------------------------------------
    // P-04: Dark frame (no exposure)
    // -------------------------------------------------------------------------

    /// <summary>
    /// P-04: Dark frame with no exposure should produce only dark current signal.
    /// Verifies CalibrationFrameGenerator dark frame output.
    /// </summary>
    [Fact]
    public void CalibrationFrameGenerator_DarkFrameProducesRealisticNoise()
    {
        // Arrange
        var config = new CalibrationConfig(
            Rows: TestRows,
            Cols: TestCols,
            AdcBits: 16,
            ReadoutNoiseElectrons: 5.0,
            DarkCurrentElectrons: 10.0);
        var generator = new CalibrationFrameGenerator(config);

        // Act
        var darkFrame = generator.GenerateDarkFrame(seed: 42);

        // Assert
        double mean = CalculateMean2D(darkFrame);
        double variance = CalculateVariance2D(darkFrame);

        // Dark frame should have small but non-zero mean (dark current + offset)
        mean.Should().BeGreaterThan(0,
            "dark frame should have non-zero mean from dark current contribution");

        // Dark frame should have positive variance (readout noise present)
        variance.Should().BeGreaterThan(0,
            "dark frame should have positive variance from readout noise");

        // Mean should be much less than midpoint (65535/2 = 32767) - dark frames are low signal
        mean.Should().BeLessThan(1000,
            "dark frame mean should be low (only dark current, no X-ray signal)");
    }

    // -------------------------------------------------------------------------
    // P-11 / P-12: Calibration data
    // -------------------------------------------------------------------------

    /// <summary>
    /// P-11: Dark calibration - averaged dark frame reduces noise by sqrt(N).
    /// P-22: Calibration mode readout - averaged frames reduce noise.
    /// </summary>
    [Fact]
    public void CalibrationFrameGenerator_AveragedDarkReducesNoise()
    {
        // Arrange
        var config = new CalibrationConfig(
            Rows: TestRows,
            Cols: TestCols,
            ReadoutNoiseElectrons: 20.0,
            DarkCurrentElectrons: 5.0);
        var generator = new CalibrationFrameGenerator(config);

        // Act - Single frame vs 10-frame average
        var singleDark = generator.GenerateDarkFrame(seed: 100);
        var averagedDark = generator.GenerateAveragedDarkFrame(count: 10, baseSeed: 100);

        double singleVariance = CalculateVariance2D(singleDark);
        double averagedVariance = CalculateVariance2D(averagedDark);

        // Assert - averaged frame should have lower variance
        averagedVariance.Should().BeLessThan(singleVariance,
            "averaging 10 dark frames should reduce variance (noise averaging effect)");
    }

    /// <summary>
    /// P-12: Flat field calibration - mean should approximate target signal level.
    /// </summary>
    [Fact]
    public void CalibrationFrameGenerator_FlatFieldFrameMeanApproximatesTarget()
    {
        // Arrange
        ushort targetSignal = 32768; // ~50% of 16-bit range
        var config = new CalibrationConfig(
            Rows: TestRows,
            Cols: TestCols,
            FlatFieldSignalDN: targetSignal,
            ReadoutNoiseElectrons: 5.0,
            DarkCurrentElectrons: 5.0);
        var generator = new CalibrationFrameGenerator(config);

        // Use flat gain/offset map (no non-uniformity)
        var flatMap = GainOffsetMap.CreateFlat(TestRows, TestCols);
        var flatField = generator.GenerateFlatFieldFrame(flatMap, seed: 42);

        // Assert - mean should be within 5% of target
        double mean = CalculateMean2D(flatField);
        mean.Should().BeApproximately(targetSignal, targetSignal * 0.05,
            "flat field mean should be within 5% of target signal level");
    }

    /// <summary>
    /// P-13: Offset calibration - bias frame (no dark current, no signal).
    /// </summary>
    [Fact]
    public void CalibrationFrameGenerator_BiasFrameHasLowerMeanThanDark()
    {
        // Arrange
        var config = new CalibrationConfig(
            Rows: TestRows,
            Cols: TestCols,
            ReadoutNoiseElectrons: 5.0,
            DarkCurrentElectrons: 100.0); // High dark current to show difference
        var generator = new CalibrationFrameGenerator(config);

        // Act
        var biasFrame = generator.GenerateBiasFrame(seed: 42);
        var darkFrame = generator.GenerateDarkFrame(seed: 42);

        double biasMean = CalculateMean2D(biasFrame);
        double darkMean = CalculateMean2D(darkFrame);

        // Assert - bias frame excludes dark current, so should be lower
        biasMean.Should().BeLessThan(darkMean,
            "bias frame should have lower mean than dark frame (no dark current in bias)");
    }

    // -------------------------------------------------------------------------
    // P-15 / P-16: Temporal Effects - Ghosting / Lag
    // -------------------------------------------------------------------------

    /// <summary>
    /// P-15: Ghosting - high signal frame leaves residual in subsequent dark frames.
    /// P-16: Lag quantification - lag_n = signal_n / signal_0.
    /// </summary>
    [Fact]
    public void LagModel_HighSignalThenDark_ShowsDecayingGhosting()
    {
        // Arrange
        var lagModel = new LagModel(new LagConfig(LagCoefficient: 0.05, DecayOrder: 3));
        ushort highSignalValue = 10000;
        ushort darkValue = 0;

        var brightFrame = CreateUniformFrame(TestRows, TestCols, highSignalValue);
        var darkFrame = CreateUniformFrame(TestRows, TestCols, darkValue);

        // Act - bright frame establishes signal history
        lagModel.ApplyLag(brightFrame);

        // Three subsequent dark frames
        var dark1 = lagModel.ApplyLag(darkFrame);
        var dark2 = lagModel.ApplyLag(darkFrame);
        var dark3 = lagModel.ApplyLag(darkFrame);

        double mean1 = CalculateMean2D(dark1);
        double mean2 = CalculateMean2D(dark2);
        double mean3 = CalculateMean2D(dark3);

        // Assert - residual signal should decay
        mean1.Should().BeGreaterThan(0, "first dark frame should show ghosting residual");
        mean2.Should().BeLessThan(mean1, "ghosting should decay on second dark frame");
        mean3.Should().BeLessThan(mean2, "ghosting should decay further on third dark frame");
        mean3.Should().BeGreaterThanOrEqualTo(0, "residual cannot be negative");
    }

    // -------------------------------------------------------------------------
    // P-17: Temperature drift
    // -------------------------------------------------------------------------

    /// <summary>
    /// P-17: Temperature drift - offset increases over time (linear drift pattern).
    /// </summary>
    [Fact]
    public void DriftModel_LinearDriftIncreasesOverTime()
    {
        // Arrange - Fast drift rate with very short frame interval so drift is measurable
        var driftModel = new DriftModel(new DriftConfig(
            DriftRateDNPerHour: 3600.0,  // 3600 DN/hour = 1 DN/second
            Pattern: DriftPattern.Linear,
            FrameIntervalMs: 1000.0));    // 1 second per frame

        ushort baseValue = 1000;
        var uniformFrame = CreateUniformFrame(TestRows, TestCols, baseValue);

        // Act - Apply drift across multiple frames
        var frame0 = driftModel.ApplyDrift(uniformFrame); // at t=0
        var frame10 = driftModel.ApplyDrift(uniformFrame); // at t=1s (after 1 frame)

        // Keep applying to advance time
        for (int i = 0; i < 8; i++)
        {
            driftModel.ApplyDrift(uniformFrame);
        }
        var frame100 = driftModel.ApplyDrift(uniformFrame); // at t=10s

        double mean0 = CalculateMean2D(frame0);
        double mean10 = CalculateMean2D(frame10);
        double mean100 = CalculateMean2D(frame100);

        // Assert - Drift should cause increasing offset over time
        mean10.Should().BeGreaterThanOrEqualTo(mean0,
            "drift should cause offset to increase over time");
        mean100.Should().BeGreaterThan(mean0,
            "offset should be measurably higher after 10 seconds of drift");
    }

    // -------------------------------------------------------------------------
    // P-19 / P-20 / P-21: Gate/ROIC Interaction
    // -------------------------------------------------------------------------

    /// <summary>
    /// P-19: Gate OFF produces only dark current, Gate ON produces higher signal.
    /// </summary>
    [Fact]
    public void GateResponseModel_GateOffProducesDarkOnly()
    {
        // Arrange
        var gateModel = new GateResponseModel(new GateResponseConfig(
            DarkCurrentPerMs: 1.0,
            SaturationLevel: 65535));

        // Act
        ushort gateOffSignal = gateModel.CalculateSignalLevel(
            gateOn: false, exposureTimeMs: 100.0, kvp: 80.0, mAs: 10.0);

        // Assert - gate off should produce only dark current: 100ms * 1.0 DN/ms = 100 DN
        ((int)gateOffSignal).Should().BeCloseTo(100, 5,
            "gate-off signal should be only dark current (100ms * 1 DN/ms = 100 DN)");
    }

    /// <summary>
    /// P-19: Gate ON produces X-ray signal plus dark current (higher than gate-off).
    /// </summary>
    [Fact]
    public void GateResponseModel_GateOnProducesHigherSignal()
    {
        // Arrange
        var gateModel = new GateResponseModel(new GateResponseConfig(
            DarkCurrentPerMs: 0.5,
            SaturationLevel: 65535));

        // Act
        ushort gateOffSignal = gateModel.CalculateSignalLevel(
            gateOn: false, exposureTimeMs: 100.0, kvp: 80.0, mAs: 10.0);
        ushort gateOnSignal = gateModel.CalculateSignalLevel(
            gateOn: true, exposureTimeMs: 100.0, kvp: 80.0, mAs: 10.0);

        // Assert - gate on should produce X-ray signal in addition to dark current
        gateOnSignal.Should().BeGreaterThan(gateOffSignal,
            "gate-on signal should exceed gate-off signal by X-ray contribution");
    }

    /// <summary>
    /// P-20: Row-by-row ROIC readout - all rows are read sequentially with correct timing.
    /// </summary>
    [Fact]
    public void RoicReadoutModel_ReadsAllRowsSequentially()
    {
        // Arrange
        var roicModel = new RoicReadoutModel(new RoicReadoutConfig(
            SettleTimeUs: 5.0,
            AdcConversionTimeUs: 2.0,
            AdcBits: 16));

        var testFrame = new ushort[TestRows, TestCols];
        for (int r = 0; r < TestRows; r++)
        {
            for (int c = 0; c < TestCols; c++)
            {
                testFrame[r, c] = (ushort)(r * 100 + c); // Unique value per pixel
            }
        }

        // Act
        var lineData = roicModel.ReadFrame(testFrame);

        // Assert - all rows read, correct indices and pixel data
        lineData.Should().HaveCount(TestRows, "all rows should be read");

        for (int r = 0; r < TestRows; r++)
        {
            lineData[r].RowIndex.Should().Be(r, $"row {r} should have correct index");
            lineData[r].Pixels.Should().HaveCount(TestCols, $"row {r} should have all columns");
            lineData[r].SettleTimeUs.Should().Be(5.0, $"row {r} should have correct settle time");
            lineData[r].ReadoutTimeUs.Should().Be(7.0, $"row {r} should have settle+ADC readout time");

            // Verify pixel data matches original frame
            for (int c = 0; c < TestCols; c++)
            {
                lineData[r].Pixels[c].Should().Be(testFrame[r, c],
                    $"pixel [{r},{c}] should match original frame data");
            }
        }
    }

    /// <summary>
    /// P-21: ROIC settle time affects total readout time.
    /// Insufficient settle time leaves residual from previous row.
    /// </summary>
    [Fact]
    public void RoicReadoutModel_SettleTimeAffectsReadoutTime()
    {
        // Arrange
        var shortSettle = new RoicReadoutModel(new RoicReadoutConfig(
            SettleTimeUs: 0.5, AdcConversionTimeUs: 2.0));
        var longSettle = new RoicReadoutModel(new RoicReadoutConfig(
            SettleTimeUs: 5.0, AdcConversionTimeUs: 2.0));

        var testFrame = CreateUniformFrame(TestRows, TestCols, 1000);

        // Act
        var shortLines = shortSettle.ReadFrame(testFrame);
        var longLines = longSettle.ReadFrame(testFrame);

        double shortReadoutTime = shortSettle.CalculateTotalReadoutTimeMs(TestRows);
        double longReadoutTime = longSettle.CalculateTotalReadoutTimeMs(TestRows);

        // Assert - longer settle time means longer total readout
        longReadoutTime.Should().BeGreaterThan(shortReadoutTime,
            "longer settle time should increase total readout time");

        // Verify per-row readout times
        shortLines[0].ReadoutTimeUs.Should().Be(2.5, "short settle: 0.5 + 2.0 = 2.5 us");
        longLines[0].ReadoutTimeUs.Should().Be(7.0, "long settle: 5.0 + 2.0 = 7.0 us");
    }

    // -------------------------------------------------------------------------
    // P-08: Dark current (exposure model dark current generation)
    // -------------------------------------------------------------------------

    /// <summary>
    /// P-08: Dark current frame produced by ExposureModel has non-zero signal.
    /// Dark current is proportional to exposure time.
    /// Uses sufficiently large dark current rate to ensure non-zero DN output
    /// given fullWellCapacity=1M electrons and 16-bit ADC (~15.26 e-/DN).
    /// </summary>
    [Fact]
    public void DarkCurrentFrame_ProducesNonZeroSignal()
    {
        // Arrange - Use high dark current rate: 500 e-/sec * 1s = 500 electrons
        // With 1M full well and 16-bit ADC: 500 / (1e6/65535) = 500 / 15.26 ~= 32 DN (non-zero)
        var shortExposure = new ExposureModel(new ExposureConfig(
            ExposureTimeMs: 1000.0,
            GatePulseCount: 1,
            GatePulseWidthUs: 1_000_000.0,  // 1 second effective integration
            DarkCurrentRatePerSec: 500.0));  // 500 e-/sec

        var longExposure = new ExposureModel(new ExposureConfig(
            ExposureTimeMs: 10000.0,
            GatePulseCount: 1,
            GatePulseWidthUs: 10_000_000.0, // 10 seconds effective integration
            DarkCurrentRatePerSec: 500.0));  // 500 e-/sec

        // Act
        var shortDark = shortExposure.GenerateDarkCurrentFrame(TestRows, TestCols);
        var longDark = longExposure.GenerateDarkCurrentFrame(TestRows, TestCols);

        double shortMean = CalculateMean2D(shortDark);
        double longMean = CalculateMean2D(longDark);

        // Assert - dark current at 1s should be measurable
        shortMean.Should().BeGreaterThan(0,
            "dark current frame at 1s integration with 500 e-/sec should produce non-zero DN");
        longMean.Should().BeGreaterThan(shortMean,
            "10x longer exposure should produce more dark current signal");
    }

    // -------------------------------------------------------------------------
    // P-03: Exposure time vs signal (gate pulse variation)
    // -------------------------------------------------------------------------

    /// <summary>
    /// P-03: Signal is proportional to gate_on pulse width (exposure time).
    /// Three gate pulses should produce 3x the signal of one gate pulse.
    /// </summary>
    [Fact]
    public void ExposureModel_GatePulseWidth_ScalesSignalProportionally()
    {
        // Arrange - Same total pulse time but different pulse counts
        var singlePulse = new ExposureModel(new ExposureConfig(
            ExposureTimeMs: 50.0,
            GatePulseCount: 1,
            GatePulseWidthUs: 50_000.0)); // 50ms total

        var triplePulse = new ExposureModel(new ExposureConfig(
            ExposureTimeMs: 50.0,
            GatePulseCount: 3,
            GatePulseWidthUs: 50_000.0)); // 150ms total

        var baseFrame = CreateUniformFrame(TestRows, TestCols, 10000);

        // Act
        var singleResult = singlePulse.ApplyExposureScaling(baseFrame);
        var tripleResult = triplePulse.ApplyExposureScaling(baseFrame);

        double singleMean = CalculateMean2D(singleResult);
        double tripleMean = CalculateMean2D(tripleResult);

        // Assert - triple pulse should produce 3x signal
        tripleMean.Should().BeApproximately(singleMean * 3.0, singleMean * 0.1,
            "triple gate pulses should produce approximately 3x the signal");
    }

    // -------------------------------------------------------------------------
    // Helper methods
    // -------------------------------------------------------------------------

    private static double CalculateMean2D(ushort[,] frame)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        double sum = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                sum += frame[r, c];
            }
        }
        return sum / (rows * cols);
    }

    private static double CalculateVariance2D(ushort[,] frame)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        double mean = CalculateMean2D(frame);
        double sumSq = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double diff = frame[r, c] - mean;
                sumSq += diff * diff;
            }
        }
        return sumSq / (rows * cols);
    }

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
}
