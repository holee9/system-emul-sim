using FluentAssertions;
using PanelSimulator.Models;
using Xunit;
using Common.Dto.Dtos;
using Simulator = PanelSimulator.PanelSimulator;

namespace PanelSimulator.Tests;

/// <summary>
/// Tests for PhysicsBased test pattern in PanelSimulator.
/// Verifies kVp/mAs drive pixel signal and that other patterns are invariant.
/// </summary>
public class PhysicsBasedPatternTests
{
    private static PanelConfig MakePhysicsConfig(double kVp = 80.0, double mAs = 10.0) =>
        new()
        {
            Rows = 64,
            Cols = 64,
            BitDepth = 16,
            TestPattern = TestPattern.PhysicsBased,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42,
            KVp = kVp,
            MAs = mAs,
            ExposureTimeMs = 100.0
        };

    [Fact]
    public void PhysicsBased_higher_kVp_produces_higher_pixel_values()
    {
        // Arrange
        var sim80 = new Simulator();
        sim80.Initialize(MakePhysicsConfig(kVp: 80.0, mAs: 10.0));

        var sim120 = new Simulator();
        sim120.Initialize(MakePhysicsConfig(kVp: 120.0, mAs: 10.0));

        // Act
        var frame80 = (FrameData)sim80.Process(null!);
        var frame120 = (FrameData)sim120.Process(null!);

        // Assert: higher kVp → more photons → higher pixel signal
        double mean80 = frame80.Pixels.Select(p => (double)p).Average();
        double mean120 = frame120.Pixels.Select(p => (double)p).Average();
        mean120.Should().BeGreaterThan(mean80,
            because: "kVp=120 generates more X-ray photons than kVp=80");
    }

    [Fact]
    public void PhysicsBased_higher_mAs_produces_higher_pixel_values()
    {
        // Arrange
        var sim10 = new Simulator();
        sim10.Initialize(MakePhysicsConfig(kVp: 80.0, mAs: 10.0));

        var sim20 = new Simulator();
        sim20.Initialize(MakePhysicsConfig(kVp: 80.0, mAs: 20.0));

        // Act
        var frame10 = (FrameData)sim10.Process(null!);
        var frame20 = (FrameData)sim20.Process(null!);

        // Assert: higher mAs → more charge accumulated → higher pixel signal
        double mean10 = frame10.Pixels.Select(p => (double)p).Average();
        double mean20 = frame20.Pixels.Select(p => (double)p).Average();
        mean20.Should().BeGreaterThan(mean10,
            because: "mAs=20 accumulates twice the charge as mAs=10");
    }

    [Fact]
    public void PhysicsBased_signal_is_proportional_to_mAs()
    {
        // Arrange — mAs doubles → signal should approximately double
        var sim10 = new Simulator();
        sim10.Initialize(MakePhysicsConfig(kVp: 80.0, mAs: 10.0));

        var sim20 = new Simulator();
        sim20.Initialize(MakePhysicsConfig(kVp: 80.0, mAs: 20.0));

        // Act
        var frame10 = (FrameData)sim10.Process(null!);
        var frame20 = (FrameData)sim20.Process(null!);

        double mean10 = frame10.Pixels.Select(p => (double)p).Average();
        double mean20 = frame20.Pixels.Select(p => (double)p).Average();

        // Assert: ratio should be close to 2.0 (within 10%)
        double ratio = mean20 / mean10;
        ratio.Should().BeApproximately(2.0, precision: 0.2,
            because: "signal should scale linearly with mAs");
    }

    [Fact]
    public void PhysicsBased_zero_kVp_throws_on_process()
    {
        // Arrange — ScintillatorModel validates kVp > 0 at construction time (inside Process)
        var simulator = new Simulator();
        simulator.Initialize(MakePhysicsConfig(kVp: 0.0, mAs: 10.0));

        // Act
        Action act = () => simulator.Process(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*KVp*");
    }

    [Fact]
    public void Counter_pattern_is_invariant_to_kVp_mAs_in_config()
    {
        // IT-11 regression: Counter pattern bypasses physics model
        // Arrange — counter pattern with different kVp/mAs
        var sim1 = new Simulator();
        sim1.Initialize(new PanelConfig
        {
            Rows = 64, Cols = 64, BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0, Seed = 42,
            KVp = 80.0, MAs = 10.0
        });

        var sim2 = new Simulator();
        sim2.Initialize(new PanelConfig
        {
            Rows = 64, Cols = 64, BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0, Seed = 42,
            KVp = 120.0, MAs = 20.0
        });

        // Act
        var frame1 = (FrameData)sim1.Process(null!);
        var frame2 = (FrameData)sim2.Process(null!);

        // Assert: counter pattern pixel values are identical regardless of kVp/mAs
        frame1.Pixels.Should().Equal(frame2.Pixels,
            because: "Counter pattern bypasses physics model — kVp/mAs have no effect");
    }

    [Fact]
    public void Checkerboard_pattern_is_invariant_to_kVp_mAs_in_config()
    {
        // IT-11 regression: Checkerboard pattern must not be affected by kVp/mAs
        // Arrange
        var sim1 = new Simulator();
        sim1.Initialize(new PanelConfig
        {
            Rows = 64, Cols = 64, BitDepth = 16,
            TestPattern = TestPattern.Checkerboard,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0, Seed = 42,
            KVp = 80.0, MAs = 10.0
        });

        var sim2 = new Simulator();
        sim2.Initialize(new PanelConfig
        {
            Rows = 64, Cols = 64, BitDepth = 16,
            TestPattern = TestPattern.Checkerboard,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0, Seed = 42,
            KVp = 120.0, MAs = 20.0
        });

        // Act
        var frame1 = (FrameData)sim1.Process(null!);
        var frame2 = (FrameData)sim2.Process(null!);

        // Assert: checkerboard pixel values are identical regardless of kVp/mAs
        frame1.Pixels.Should().Equal(frame2.Pixels,
            because: "Checkerboard pattern bypasses physics model — kVp/mAs have no effect");
    }

    [Fact]
    public void PhysicsBased_generates_nonzero_pixels()
    {
        // Arrange
        var simulator = new Simulator();
        simulator.Initialize(MakePhysicsConfig(kVp: 80.0, mAs: 10.0));

        // Act
        var frame = (FrameData)simulator.Process(null!);

        // Assert: physics model should produce non-zero signal for valid kVp/mAs
        frame.Pixels.Should().Contain(p => p > 0,
            because: "X-ray exposure must produce a measurable pixel signal");
    }
}
