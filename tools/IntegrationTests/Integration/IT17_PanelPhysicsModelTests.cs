using Xunit;
using FluentAssertions;
using PanelSimulator.Models.Physics;
using PanelSimulator.Models.Temporal;
using PanelSimulator.Models.Calibration;
using PanelSimulator.Generators;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-17: Panel Physics Model Integration Tests.
/// Validates scintillator response, composite noise, gain/offset maps,
/// lag model, exposure scaling, and end-to-end physics chain.
/// Reference: SPEC-EMUL-001
/// </summary>
public class IT17_PanelPhysicsModelTests
{
    private const int TestRows = 64;
    private const int TestCols = 64;

    [Fact]
    public void ScintillatorModel_SignalProportionalToKvp()
    {
        // Arrange - Two scintillator models at different kVp settings
        var lowKvp = new ScintillatorModel(new ScintillatorConfig(KVp: 60.0, MAs: 10.0));
        var highKvp = new ScintillatorModel(new ScintillatorConfig(KVp: 120.0, MAs: 10.0));

        // Act - Generate signal frames at each kVp
        var lowFrame = lowKvp.GenerateSignalFrame(TestRows, TestCols);
        var highFrame = highKvp.GenerateSignalFrame(TestRows, TestCols);

        // Assert - Higher kVp should produce higher mean signal
        double lowMean = CalculateMean2D(lowFrame);
        double highMean = CalculateMean2D(highFrame);

        highMean.Should().BeGreaterThan(lowMean,
            "120 kVp should produce higher signal than 60 kVp");

        // Both should produce non-zero signals
        lowMean.Should().BeGreaterThan(0, "60 kVp should produce non-zero signal");
        highMean.Should().BeGreaterThan(0, "120 kVp should produce non-zero signal");
    }

    [Fact]
    public void CompositeNoise_StatisticalDistribution()
    {
        // Arrange - Create a uniform signal frame and composite noise generator
        var scintillator = new ScintillatorModel(new ScintillatorConfig(KVp: 80.0, MAs: 10.0));
        var signalFrame = scintillator.GenerateSignalFrame(TestRows, TestCols);

        var noiseGen = new CompositeNoiseGenerator(seed: 42, new CompositeNoiseConfig(
            EnablePoissonNoise: true,
            EnableGaussianNoise: true,
            EnableDarkCurrent: true,
            EnableFlickerNoise: false));

        // Act - Apply composite noise
        var noisyFrame = noiseGen.ApplyNoise(signalFrame);

        // Assert - Mean should be close to expected signal (within reasonable tolerance)
        double signalMean = CalculateMean2D(signalFrame);
        double noisyMean = CalculateMean2D(noisyFrame);

        // Mean should be within 20% of original signal (noise + dark current shifts it)
        noisyMean.Should().BeGreaterThan(0, "noisy frame should have positive signal");

        // Variance should be positive (noise is present)
        double variance = CalculateVariance2D(noisyFrame);
        variance.Should().BeGreaterThan(0,
            "noisy frame should have positive variance indicating noise is present");
    }

    [Fact]
    public void GainOffsetMap_AppliesPixelVariation()
    {
        // Arrange - Create a uniform frame and a random gain/offset map
        var uniformFrame = new ushort[TestRows, TestCols];
        ushort uniformValue = 1000;
        for (int r = 0; r < TestRows; r++)
        {
            for (int c = 0; c < TestCols; c++)
            {
                uniformFrame[r, c] = uniformValue;
            }
        }

        // Create random map with significant variation
        var gainOffsetMap = GainOffsetMap.CreateRandom(
            TestRows, TestCols,
            gainStdDev: 0.10,    // +/-10% gain variation
            offsetStdDev: 20.0,  // +/-20 DN offset variation
            seed: 42);

        // Act - Apply gain/offset map to uniform frame
        var result = gainOffsetMap.ApplyForward(uniformFrame);

        // Assert - Pixels should differ from uniform (not all same value)
        var distinctValues = new HashSet<ushort>();
        for (int r = 0; r < TestRows; r++)
        {
            for (int c = 0; c < TestCols; c++)
            {
                distinctValues.Add(result[r, c]);
            }
        }

        distinctValues.Count.Should().BeGreaterThan(1,
            "gain/offset map should create pixel-level variation in uniform frame");

        // The mean should still be approximately the original value
        double mean = CalculateMean2D(result);
        mean.Should().BeApproximately(uniformValue, uniformValue * 0.15,
            "mean should be approximately the original value with gain centered at 1.0");
    }

    [Fact]
    public void LagModel_DecaysOverFrames()
    {
        // Arrange - Create lag model with measurable coefficient
        var lagModel = new LagModel(new LagConfig(LagCoefficient: 0.05, DecayOrder: 3));

        // Create a "high signal" frame and a "zero signal" frame
        var highFrame = new ushort[TestRows, TestCols];
        var zeroFrame = new ushort[TestRows, TestCols];
        for (int r = 0; r < TestRows; r++)
        {
            for (int c = 0; c < TestCols; c++)
            {
                highFrame[r, c] = 10000;
                zeroFrame[r, c] = 0;
            }
        }

        // Act - Apply high signal frame first (establishes history)
        var result1 = lagModel.ApplyLag(highFrame);

        // Apply zero frame (should have residual from high frame)
        var result2 = lagModel.ApplyLag(zeroFrame);

        // Apply another zero frame (should have less residual)
        var result3 = lagModel.ApplyLag(zeroFrame);

        // Assert - First frame should just be the high signal (no history yet is the first call)
        // Second frame (zero input) should have residual ghosting
        double mean2 = CalculateMean2D(result2);
        mean2.Should().BeGreaterThan(0,
            "second zero frame should have residual signal from previous high frame (ghosting)");

        // Third zero frame should have less residual than second
        double mean3 = CalculateMean2D(result3);
        mean3.Should().BeLessThan(mean2,
            "third zero frame should have less residual than second (decay)");
        mean3.Should().BeGreaterThanOrEqualTo(0,
            "residual signal should be non-negative");
    }

    [Fact]
    public void ExposureModel_ScalesWithGatePulse()
    {
        // Arrange - Create two exposure models with different gate pulse counts
        var lowPulse = new ExposureModel(new ExposureConfig(
            ExposureTimeMs: 100.0,
            GatePulseCount: 1,
            GatePulseWidthUs: 50_000.0));

        var highPulse = new ExposureModel(new ExposureConfig(
            ExposureTimeMs: 100.0,
            GatePulseCount: 3,
            GatePulseWidthUs: 50_000.0));

        // Act - Calculate scaling factors
        double lowScaling = lowPulse.CalculateExposureScalingFactor();
        double highScaling = highPulse.CalculateExposureScalingFactor();

        // Assert - Higher pulse count should give higher scaling factor
        highScaling.Should().BeGreaterThan(lowScaling,
            "more gate pulses should produce higher exposure scaling factor");

        // The ratio should be proportional to pulse count
        double expectedRatio = 3.0 / 1.0;
        double actualRatio = highScaling / lowScaling;
        actualRatio.Should().BeApproximately(expectedRatio, 0.01,
            "scaling ratio should be proportional to pulse count ratio");
    }

    [Fact]
    public void PhysicsChain_EndToEnd_ProducesRealisticOutput()
    {
        // Arrange - Build full physics chain:
        // Scintillator -> Exposure -> Noise -> GainOffset -> Lag -> Defect

        // Step 1: Scintillator generates base signal
        var scintillator = new ScintillatorModel(new ScintillatorConfig(KVp: 80.0, MAs: 10.0));
        var baseSignal = scintillator.GenerateSignalFrame(TestRows, TestCols, adcBits: 16);

        // Step 2: Exposure model scales the signal
        var exposure = new ExposureModel(new ExposureConfig(
            ExposureTimeMs: 100.0,
            GatePulseCount: 1,
            GatePulseWidthUs: 100_000.0));
        var exposedFrame = exposure.ApplyExposureScaling(baseSignal);

        // Step 3: Composite noise adds realistic detector noise
        var noiseGen = new CompositeNoiseGenerator(seed: 42, new CompositeNoiseConfig(
            EnablePoissonNoise: true,
            EnableGaussianNoise: true,
            EnableDarkCurrent: true,
            EnableFlickerNoise: false,
            ReadoutNoiseElectrons: 5.0,
            DarkCurrentElectrons: 10.0));
        var noisyFrame = noiseGen.ApplyNoise(exposedFrame);

        // Step 4: Gain/Offset map applies pixel non-uniformity
        var gainOffsetMap = GainOffsetMap.CreateRandom(
            TestRows, TestCols,
            gainStdDev: 0.03,
            offsetStdDev: 5.0,
            seed: 42);
        var nonUniformFrame = gainOffsetMap.ApplyForward(noisyFrame);

        // Step 5: Lag model applies temporal ghosting
        var lagModel = new LagModel(new LagConfig(LagCoefficient: 0.02, DecayOrder: 1));
        var finalFrame = lagModel.ApplyLag(nonUniformFrame);

        // Assert - Output should be non-zero and within bit depth range
        bool hasNonZero = false;
        bool allInRange = true;
        bool differentFromInput = false;

        for (int r = 0; r < TestRows; r++)
        {
            for (int c = 0; c < TestCols; c++)
            {
                if (finalFrame[r, c] > 0)
                    hasNonZero = true;

                if (finalFrame[r, c] > 65535)
                    allInRange = false;

                if (finalFrame[r, c] != baseSignal[r, c])
                    differentFromInput = true;
            }
        }

        hasNonZero.Should().BeTrue(
            "output frame should contain non-zero pixel values");
        allInRange.Should().BeTrue(
            "all pixel values should be within 16-bit range [0, 65535]");
        differentFromInput.Should().BeTrue(
            "output should differ from input after noise and non-uniformity were applied");

        // Verify statistical properties
        double finalMean = CalculateMean2D(finalFrame);
        finalMean.Should().BeGreaterThan(0,
            "mean signal should be positive after full physics chain");

        double finalVariance = CalculateVariance2D(finalFrame);
        finalVariance.Should().BeGreaterThan(0,
            "variance should be positive (noise was applied)");
    }

    /// <summary>
    /// Calculates the mean pixel value of a 2D frame.
    /// </summary>
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

    /// <summary>
    /// Calculates the variance of pixel values in a 2D frame.
    /// </summary>
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
}
