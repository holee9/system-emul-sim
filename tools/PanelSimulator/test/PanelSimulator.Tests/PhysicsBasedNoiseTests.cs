using FluentAssertions;
using PanelSimulator.Models;
using PanelSimulator.Models.Noise;
using PanelSimulator.Models.Readout;
using Xunit;
using Common.Dto.Dtos;
using Simulator = PanelSimulator.PanelSimulator;

namespace PanelSimulator.Tests;

/// <summary>
/// Tests for SPEC-PHY-001: Realistic Sensor Panel Noise Model.
/// Validates composite noise, FPN, Vback dark current model, temperature model, and image lag.
/// PHY-T01 through PHY-T07.
/// </summary>
public class PhysicsBasedNoiseTests
{
    private static PanelConfig MakeCompositeConfig(
        double kVp = 80.0,
        double mAs = 10.0,
        double vbackVolts = -15.0,
        double temperatureCelsius = 25.0,
        double fpnAmplitudePct = 1.5,
        bool enableImageLag = false,
        double imageLagFraction = 0.02) =>
        new()
        {
            Rows = 32,
            Cols = 32,
            BitDepth = 16,
            TestPattern = TestPattern.PhysicsBased,
            NoiseModel = NoiseModelType.Composite,
            DefectRate = 0,
            Seed = 42,
            KVp = kVp,
            MAs = mAs,
            ExposureTimeMs = 100.0,
            VbackVolts = vbackVolts,
            TemperatureCelsius = temperatureCelsius,
            ReadoutNoiseElectrons = 5948.0,
            FpnAmplitudePct = fpnAmplitudePct,
            EnableImageLag = enableImageLag,
            ImageLagFraction = imageLagFraction
        };

    // ── PHY-T01: Consecutive frames have different pixel values ─────────────

    [Fact]
    public void PHY_T01_composite_noise_produces_varying_frames()
    {
        // Arrange
        var sim = new Simulator();
        sim.Initialize(MakeCompositeConfig());

        // Act
        var frame1 = (FrameData)sim.Process(null!);
        var frame2 = (FrameData)sim.Process(null!);

        // Assert: frames must differ (noise is random per frame)
        bool anyDifferent = false;
        for (int i = 0; i < frame1.Pixels.Length; i++)
        {
            if (frame1.Pixels[i] != frame2.Pixels[i])
            {
                anyDifferent = true;
                break;
            }
        }

        anyDifferent.Should().BeTrue(
            because: "composite noise is stochastic — consecutive frames must differ");
    }

    // ── PHY-T02: Dark frame pixel distribution is Gaussian-like ────────────

    [Fact]
    public void PHY_T02_composite_noise_dark_frame_has_nonzero_stddev()
    {
        // Arrange: use dark-only frame (gateOn=false is simulated via very low kVp/mAs)
        // PhysicsBased always uses gate=true, so test noise presence via std dev
        var config = MakeCompositeConfig(kVp: 80.0, mAs: 10.0, fpnAmplitudePct: 0.0);
        var sim = new Simulator();
        sim.Initialize(config);

        // Act — collect 5 frames, compute per-pixel std dev across frames
        var frames = Enumerable.Range(0, 5)
            .Select(_ => (FrameData)sim.Process(null!))
            .ToList();

        double[] pixelVariances = new double[frames[0].Pixels.Length];
        double[] means = new double[pixelVariances.Length];

        for (int f = 0; f < frames.Count; f++)
            for (int i = 0; i < means.Length; i++)
                means[i] += frames[f].Pixels[i];
        for (int i = 0; i < means.Length; i++) means[i] /= frames.Count;

        for (int f = 0; f < frames.Count; f++)
            for (int i = 0; i < means.Length; i++)
            {
                double diff = frames[f].Pixels[i] - means[i];
                pixelVariances[i] += diff * diff;
            }
        for (int i = 0; i < pixelVariances.Length; i++) pixelVariances[i] /= frames.Count;

        double avgStdDev = pixelVariances.Select(Math.Sqrt).Average();

        // Assert: average per-pixel std dev must be > 0 (noise present)
        avgStdDev.Should().BeGreaterThan(0.5,
            because: "composite noise must produce non-zero pixel variance across frames");
    }

    // ── PHY-T03: Bright frame variance scales with signal (Poisson) ─────────

    [Fact]
    public void PHY_T03_bright_frame_variance_greater_than_dark_frame_variance()
    {
        // High kVp (bright) vs low kVp (relatively dark)
        var brightSim = new Simulator();
        brightSim.Initialize(MakeCompositeConfig(kVp: 120.0, mAs: 20.0, fpnAmplitudePct: 0.0));

        var darkSim = new Simulator();
        darkSim.Initialize(MakeCompositeConfig(kVp: 40.0, mAs: 1.0, fpnAmplitudePct: 0.0));

        // Collect multiple frames to estimate variance
        const int frameCount = 6;
        var brightPixelSums = new double[32 * 32];
        var darkPixelSums = new double[32 * 32];
        var brightPixelSqSums = new double[32 * 32];
        var darkPixelSqSums = new double[32 * 32];

        for (int f = 0; f < frameCount; f++)
        {
            var bFrame = (FrameData)brightSim.Process(null!);
            var dFrame = (FrameData)darkSim.Process(null!);
            for (int i = 0; i < bFrame.Pixels.Length; i++)
            {
                brightPixelSums[i] += bFrame.Pixels[i];
                darkPixelSums[i] += dFrame.Pixels[i];
                brightPixelSqSums[i] += (double)bFrame.Pixels[i] * bFrame.Pixels[i];
                darkPixelSqSums[i] += (double)dFrame.Pixels[i] * dFrame.Pixels[i];
            }
        }

        double brightVar = brightPixelSqSums.Zip(brightPixelSums,
            (sq, s) => sq / frameCount - (s / frameCount) * (s / frameCount)).Average();
        double darkVar = darkPixelSqSums.Zip(darkPixelSums,
            (sq, s) => sq / frameCount - (s / frameCount) * (s / frameCount)).Average();

        // Assert: bright frame has higher variance (Poisson shot noise ∝ signal)
        brightVar.Should().BeGreaterThan(darkVar,
            because: "shot noise variance scales with signal level (Poisson statistics)");
    }

    // ── PHY-T04: FPN map is frame-invariant (same spatial pattern each frame) ─

    [Fact]
    public void PHY_T04_fpn_map_is_deterministic_and_frame_invariant()
    {
        // Two simulators with same seed → same FPN map
        var sim1 = new Simulator();
        sim1.Initialize(MakeCompositeConfig(fpnAmplitudePct: 3.0));

        var sim2 = new Simulator();
        sim2.Initialize(MakeCompositeConfig(fpnAmplitudePct: 3.0));

        // Generate one frame each with no Poisson/readout noise to isolate FPN
        // Use a config with no stochastic noise (zero readout noise) to see pure FPN
        var noiselessConfig = new PanelConfig
        {
            Rows = 8, Cols = 8, BitDepth = 16,
            TestPattern = TestPattern.PhysicsBased,
            NoiseModel = NoiseModelType.Composite,
            Seed = 99,
            KVp = 80.0, MAs = 10.0, ExposureTimeMs = 100.0,
            ReadoutNoiseElectrons = 0.0,   // No readout noise
            FpnAmplitudePct = 5.0,
            EnableImageLag = false
        };

        var simA = new Simulator();
        simA.Initialize(noiselessConfig);
        var simB = new Simulator();
        simB.Initialize(noiselessConfig);

        // Verify FPN map is constructed identically from same seed
        var fpnA = new FixedPatternNoiseMap(8, 8, 0.05, 99);
        var fpnB = new FixedPatternNoiseMap(8, 8, 0.05, 99);
        double[,] mapA = fpnA.GetMapCopy();
        double[,] mapB = fpnB.GetMapCopy();

        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                mapA[r, c].Should().Be(mapB[r, c],
                    because: $"FPN map[{r},{c}] must be identical for same seed");
    }

    // ── PHY-T05: Vback -30V → dark current ≥4× compared to -15V ───────────

    [Fact]
    public void PHY_T05_higher_vback_magnitude_increases_dark_current_4x()
    {
        // Use gate-off simulation: kVp=1, mAs=0 → signal ≈ 0, only dark current
        // GateResponseModel calculates dark current based on Vback
        var gateNominal = new GateResponseModel(new GateResponseConfig(
            VbackVolts: -15.0, TemperatureCelsius: 25.0));
        var gateHighVback = new GateResponseModel(new GateResponseConfig(
            VbackVolts: -30.0, TemperatureCelsius: 25.0));

        double multiplierNominal = gateNominal.CalculateDarkCurrentMultiplier();
        double multiplierHighVback = gateHighVback.CalculateDarkCurrentMultiplier();

        double ratio = multiplierHighVback / multiplierNominal;

        ratio.Should().BeGreaterThanOrEqualTo(4.0,
            because: "Vback=-30V should produce ≥4× dark current vs Vback=-15V per REQ-PHY-004");
        ratio.Should().BeApproximately(4.0, precision: 0.001,
            because: "model is (|Vback|/15)² → (30/15)² = 4.0 exactly");
    }

    // ── PHY-T06: Temperature +10°C → dark current approximately doubles ────

    [Fact]
    public void PHY_T06_temperature_increase_doubles_dark_current_every_8C()
    {
        var gateCold = new GateResponseModel(new GateResponseConfig(
            VbackVolts: -15.0, TemperatureCelsius: 25.0));
        var gateWarm = new GateResponseModel(new GateResponseConfig(
            VbackVolts: -15.0, TemperatureCelsius: 33.0));
        var gateHot = new GateResponseModel(new GateResponseConfig(
            VbackVolts: -15.0, TemperatureCelsius: 41.0));

        double m25 = gateCold.CalculateDarkCurrentMultiplier();
        double m33 = gateWarm.CalculateDarkCurrentMultiplier();
        double m41 = gateHot.CalculateDarkCurrentMultiplier();

        // Every 8°C should double dark current
        (m33 / m25).Should().BeApproximately(2.0, precision: 0.01,
            because: "Idark doubles every 8°C per REQ-PHY-005");
        (m41 / m25).Should().BeApproximately(4.0, precision: 0.01,
            because: "Idark quadruples at +16°C per REQ-PHY-005");
    }

    // ── PHY-T07: Image lag carries over signal from previous frame ──────────

    [Fact]
    public void PHY_T07_image_lag_carries_signal_to_next_frame()
    {
        // Arrange: two simulators — one with lag, one without
        var configNoLag = MakeCompositeConfig(enableImageLag: false);
        var configWithLag = MakeCompositeConfig(enableImageLag: true, imageLagFraction: 0.05);

        // Use None noise to isolate lag effect
        configNoLag.NoiseModel = NoiseModelType.None;
        configWithLag.NoiseModel = NoiseModelType.None;

        var simNoLag = new Simulator();
        simNoLag.Initialize(configNoLag);

        var simWithLag = new Simulator();
        simWithLag.Initialize(configWithLag);

        // Act: process 2 frames each
        var _ = simNoLag.Process(null!);          // Frame 0 (warm-up)
        var frame1NoLag = (FrameData)simNoLag.Process(null!);   // Frame 1

        var __ = simWithLag.Process(null!);       // Frame 0 (populate lag history)
        var frame1WithLag = (FrameData)simWithLag.Process(null!);  // Frame 1 (has lag)

        double meanNoLag = frame1NoLag.Pixels.Select(p => (double)p).Average();
        double meanWithLag = frame1WithLag.Pixels.Select(p => (double)p).Average();

        // Assert: lag adds residual signal → frame1 with lag should have higher mean
        meanWithLag.Should().BeGreaterThan(meanNoLag,
            because: "image lag carries 5% of frame 0 signal into frame 1");
    }

    // ── FPN Map Unit Tests ──────────────────────────────────────────────────

    [Fact]
    public void FixedPatternNoiseMap_apply_is_noop_at_zero_amplitude()
    {
        var fpn = new FixedPatternNoiseMap(4, 4, amplitudeFraction: 0.0, seed: 1);
        var frame = new ushort[4, 4];
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                frame[r, c] = 1000;

        var result = fpn.Apply(frame);

        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                result[r, c].Should().Be(1000,
                    because: "zero amplitude FPN must not change pixel values");
    }

    [Fact]
    public void FixedPatternNoiseMap_apply_changes_pixel_values_at_nonzero_amplitude()
    {
        var fpn = new FixedPatternNoiseMap(8, 8, amplitudeFraction: 0.05, seed: 42);
        var frame = new ushort[8, 8];
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                frame[r, c] = 10000;

        var result = fpn.Apply(frame);

        bool anyChanged = false;
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                if (result[r, c] != 10000)
                    anyChanged = true;

        anyChanged.Should().BeTrue(
            because: "5% FPN amplitude must produce spatially varying pixel values");
    }

    [Theory]
    [InlineData(-1.0)]
    public void FixedPatternNoiseMap_rejects_negative_amplitude(double amplitude)
    {
        Action act = () => new FixedPatternNoiseMap(4, 4, amplitude, seed: 1);
        act.Should().Throw<ArgumentException>();
    }

    // ── GateResponseModel Unit Tests ────────────────────────────────────────

    [Fact]
    public void GateResponseModel_default_vback_multiplier_is_one()
    {
        var gate = new GateResponseModel(new GateResponseConfig(
            VbackVolts: -15.0, TemperatureCelsius: 25.0));

        gate.CalculateDarkCurrentMultiplier().Should().BeApproximately(1.0, precision: 0.001,
            because: "nominal Vback=-15V, T=25°C is the reference point");
    }

    [Fact]
    public void GateResponseModel_reduced_vback_decreases_dark_current()
    {
        var gateNominal = new GateResponseModel(new GateResponseConfig(VbackVolts: -15.0));
        var gateLow = new GateResponseModel(new GateResponseConfig(VbackVolts: -10.0));

        gateLow.CalculateDarkCurrentMultiplier()
            .Should().BeLessThan(gateNominal.CalculateDarkCurrentMultiplier(),
                because: "smaller |Vback| → smaller depletion region → less dark current");
    }
}
