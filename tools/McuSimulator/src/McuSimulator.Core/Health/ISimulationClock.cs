namespace McuSimulator.Core.Health;

/// <summary>
/// Abstraction for time source, enabling deterministic testing.
/// </summary>
public interface ISimulationClock
{
    /// <summary>Gets the current time in milliseconds.</summary>
    long GetCurrentTimeMs();
}

/// <summary>
/// Default clock using system time.
/// </summary>
public sealed class SystemClock : ISimulationClock
{
    public long GetCurrentTimeMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
