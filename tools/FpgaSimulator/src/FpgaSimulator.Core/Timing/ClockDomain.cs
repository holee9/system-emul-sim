namespace FpgaSimulator.Core.Timing;

/// <summary>
/// Defines clock domain parameters for the FPGA design.
/// Models the three main clock domains: SYS (100 MHz), ROIC, and CSI-2 (125 MHz).
/// </summary>
public sealed class ClockDomain
{
    /// <summary>Clock domain name for identification</summary>
    public required string Name { get; init; }

    /// <summary>Clock frequency in Hz</summary>
    public required long FrequencyHz { get; init; }

    /// <summary>Clock period in nanoseconds</summary>
    public double PeriodNs => 1_000_000_000.0 / FrequencyHz;

    /// <summary>
    /// Converts a duration in microseconds to clock ticks in this domain.
    /// </summary>
    /// <param name="microseconds">Duration in microseconds</param>
    /// <returns>Number of clock ticks</returns>
    public long MicrosecondsToTicks(double microseconds)
    {
        return (long)(microseconds * FrequencyHz / 1_000_000.0);
    }

    /// <summary>
    /// Converts clock ticks to duration in microseconds.
    /// </summary>
    /// <param name="ticks">Number of clock ticks</param>
    /// <returns>Duration in microseconds</returns>
    public double TicksToMicroseconds(long ticks)
    {
        return ticks * 1_000_000.0 / FrequencyHz;
    }

    /// <summary>
    /// Converts a duration in milliseconds to clock ticks in this domain.
    /// </summary>
    /// <param name="milliseconds">Duration in milliseconds</param>
    /// <returns>Number of clock ticks</returns>
    public long MillisecondsToTicks(double milliseconds)
    {
        return (long)(milliseconds * FrequencyHz / 1_000.0);
    }

    /// <summary>System clock domain: 100 MHz main FPGA clock</summary>
    public static readonly ClockDomain System = new()
    {
        Name = "SYS",
        FrequencyHz = 100_000_000
    };

    /// <summary>ROIC clock domain: variable frequency for ROIC interface</summary>
    public static readonly ClockDomain Roic = new()
    {
        Name = "ROIC",
        FrequencyHz = 50_000_000
    };

    /// <summary>CSI-2 byte clock domain: 125 MHz for CSI-2 TX</summary>
    public static readonly ClockDomain Csi2 = new()
    {
        Name = "CSI2",
        FrequencyHz = 125_000_000
    };
}
