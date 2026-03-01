namespace FpgaSimulator.Tests.Timing;

using FluentAssertions;
using FpgaSimulator.Core.Timing;
using Xunit;

public class ClockDomainTests
{
    // ---------------------------------------------------------------
    // Predefined domain frequency and period tests
    // ---------------------------------------------------------------

    [Fact]
    public void SystemDomain_ShouldHave100MHzFrequency()
    {
        // Assert
        ClockDomain.System.Name.Should().Be("SYS");
        ClockDomain.System.FrequencyHz.Should().Be(100_000_000);
    }

    [Fact]
    public void RoicDomain_ShouldHave50MHzFrequency()
    {
        // Assert
        ClockDomain.Roic.Name.Should().Be("ROIC");
        ClockDomain.Roic.FrequencyHz.Should().Be(50_000_000);
    }

    [Fact]
    public void Csi2Domain_ShouldHave125MHzFrequency()
    {
        // Assert
        ClockDomain.Csi2.Name.Should().Be("CSI2");
        ClockDomain.Csi2.FrequencyHz.Should().Be(125_000_000);
    }

    [Fact]
    public void PeriodNs_System100MHz_ShouldBe10Ns()
    {
        // Assert - 100 MHz -> 10 ns period
        ClockDomain.System.PeriodNs.Should().BeApproximately(10.0, 0.001);
    }

    [Fact]
    public void PeriodNs_Roic50MHz_ShouldBe20Ns()
    {
        // Assert - 50 MHz -> 20 ns period
        ClockDomain.Roic.PeriodNs.Should().BeApproximately(20.0, 0.001);
    }

    [Fact]
    public void PeriodNs_Csi2125MHz_ShouldBe8Ns()
    {
        // Assert - 125 MHz -> 8 ns period
        ClockDomain.Csi2.PeriodNs.Should().BeApproximately(8.0, 0.001);
    }

    // ---------------------------------------------------------------
    // MicrosecondsToTicks conversion tests
    // ---------------------------------------------------------------

    [Fact]
    public void MicrosecondsToTicks_System1us_ShouldReturn100Ticks()
    {
        // 1 us at 100 MHz = 100 ticks
        var ticks = ClockDomain.System.MicrosecondsToTicks(1.0);
        ticks.Should().Be(100);
    }

    [Fact]
    public void MicrosecondsToTicks_Roic1us_ShouldReturn50Ticks()
    {
        // 1 us at 50 MHz = 50 ticks
        var ticks = ClockDomain.Roic.MicrosecondsToTicks(1.0);
        ticks.Should().Be(50);
    }

    [Fact]
    public void MicrosecondsToTicks_Csi2_1us_ShouldReturn125Ticks()
    {
        // 1 us at 125 MHz = 125 ticks
        var ticks = ClockDomain.Csi2.MicrosecondsToTicks(1.0);
        ticks.Should().Be(125);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(10.0, 1000)]
    [InlineData(100.0, 10000)]
    [InlineData(1000.0, 100000)]
    public void MicrosecondsToTicks_VariousDurations_ShouldConvertCorrectly(double us, long expectedTicks)
    {
        // Arrange - System clock 100 MHz
        var ticks = ClockDomain.System.MicrosecondsToTicks(us);

        // Assert
        ticks.Should().Be(expectedTicks);
    }

    // ---------------------------------------------------------------
    // TicksToMicroseconds conversion tests
    // ---------------------------------------------------------------

    [Fact]
    public void TicksToMicroseconds_100TicksAt100MHz_ShouldReturn1us()
    {
        // 100 ticks at 100 MHz = 1 us
        var us = ClockDomain.System.TicksToMicroseconds(100);
        us.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void TicksToMicroseconds_50TicksAt50MHz_ShouldReturn1us()
    {
        var us = ClockDomain.Roic.TicksToMicroseconds(50);
        us.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void TicksToMicroseconds_ZeroTicks_ShouldReturnZero()
    {
        var us = ClockDomain.System.TicksToMicroseconds(0);
        us.Should().Be(0.0);
    }

    // ---------------------------------------------------------------
    // MillisecondsToTicks conversion tests
    // ---------------------------------------------------------------

    [Fact]
    public void MillisecondsToTicks_System1ms_ShouldReturn100000Ticks()
    {
        // 1 ms at 100 MHz = 100,000 ticks
        var ticks = ClockDomain.System.MillisecondsToTicks(1.0);
        ticks.Should().Be(100_000);
    }

    [Fact]
    public void MillisecondsToTicks_Roic1ms_ShouldReturn50000Ticks()
    {
        var ticks = ClockDomain.Roic.MillisecondsToTicks(1.0);
        ticks.Should().Be(50_000);
    }

    [Fact]
    public void MillisecondsToTicks_Csi2_100ms_ShouldReturn12500000Ticks()
    {
        // 100 ms at 125 MHz = 12,500,000 ticks
        var ticks = ClockDomain.Csi2.MillisecondsToTicks(100.0);
        ticks.Should().Be(12_500_000);
    }

    // ---------------------------------------------------------------
    // Round-trip conversion tests
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(1.0)]
    [InlineData(50.0)]
    [InlineData(1000.0)]
    public void RoundTrip_MicrosecondsToTicksAndBack_ShouldPreserveValue(double originalUs)
    {
        // Act
        var ticks = ClockDomain.System.MicrosecondsToTicks(originalUs);
        var roundTripped = ClockDomain.System.TicksToMicroseconds(ticks);

        // Assert
        roundTripped.Should().BeApproximately(originalUs, 0.01);
    }

    // ---------------------------------------------------------------
    // Custom domain tests
    // ---------------------------------------------------------------

    [Fact]
    public void CustomDomain_ShouldCalculateCorrectPeriod()
    {
        // Arrange - 200 MHz custom domain
        var domain = new ClockDomain
        {
            Name = "CUSTOM",
            FrequencyHz = 200_000_000
        };

        // Assert
        domain.PeriodNs.Should().BeApproximately(5.0, 0.001);
        domain.MicrosecondsToTicks(1.0).Should().Be(200);
        domain.MillisecondsToTicks(1.0).Should().Be(200_000);
    }
}
