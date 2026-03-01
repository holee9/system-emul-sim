namespace IntegrationTests.Helpers;

/// <summary>
/// Configuration for network channel impairment simulation.
/// Controls packet loss, reordering, corruption, and delay parameters.
/// </summary>
public sealed record NetworkChannelConfig
{
    /// <summary>
    /// Probability of dropping a packet (0.0 = no loss, 1.0 = all dropped).
    /// </summary>
    public double PacketLossRate { get; init; } = 0.0;

    /// <summary>
    /// Probability of reordering packets (0.0 = no reorder, 1.0 = always reorder).
    /// </summary>
    public double ReorderRate { get; init; } = 0.0;

    /// <summary>
    /// Minimum simulated delay in milliseconds.
    /// </summary>
    public int MinDelayMs { get; init; } = 0;

    /// <summary>
    /// Maximum simulated delay in milliseconds.
    /// </summary>
    public int MaxDelayMs { get; init; } = 0;

    /// <summary>
    /// Probability of corrupting a packet (0.0 = no corruption, 1.0 = always corrupt).
    /// </summary>
    public double CorruptionRate { get; init; } = 0.0;

    /// <summary>
    /// Random seed for deterministic simulation.
    /// </summary>
    public int Seed { get; init; } = 42;
}
