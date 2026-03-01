using McuSimulator.Core.Network;

namespace IntegrationTests.Helpers;

/// <summary>
/// Simulates network impairments between MCU and Host layers.
/// Applies configurable packet loss, reordering, corruption, and delay.
/// Thread-safe via lock on mutable state.
/// </summary>
public sealed class NetworkChannel
{
    private readonly object _lock = new();
    private Random _random;
    private double _packetLossRate;
    private double _reorderRate;
    private double _corruptionRate;
    private int _minDelayMs;
    private int _maxDelayMs;

    /// <summary>Total packets submitted to the channel.</summary>
    public long PacketsSent { get; private set; }

    /// <summary>Total packets dropped due to simulated loss.</summary>
    public long PacketsLost { get; private set; }

    /// <summary>Total packets that were reordered.</summary>
    public long PacketsReordered { get; private set; }

    /// <summary>Total packets that were corrupted.</summary>
    public long PacketsCorrupted { get; private set; }

    /// <summary>
    /// Creates a new NetworkChannel with the specified configuration.
    /// </summary>
    /// <param name="config">Network impairment configuration.</param>
    public NetworkChannel(NetworkChannelConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _packetLossRate = config.PacketLossRate;
        _reorderRate = config.ReorderRate;
        _corruptionRate = config.CorruptionRate;
        _minDelayMs = config.MinDelayMs;
        _maxDelayMs = config.MaxDelayMs;
        _random = new Random(config.Seed);
    }

    /// <summary>
    /// Transmits packets through the simulated network channel,
    /// applying loss, reorder, and corruption effects.
    /// </summary>
    /// <param name="packets">Input packets from MCU layer.</param>
    /// <returns>Packets after network impairment simulation.</returns>
    public List<UdpFramePacket> TransmitPackets(List<UdpFramePacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);

        lock (_lock)
        {
            PacketsSent += packets.Count;

            // Step 1: Apply packet loss
            var surviving = new List<UdpFramePacket>(packets.Count);
            foreach (var packet in packets)
            {
                if (_random.NextDouble() < _packetLossRate)
                {
                    PacketsLost++;
                    continue;
                }
                surviving.Add(packet);
            }

            // Step 2: Apply corruption (flip random bytes in payload)
            var processed = new List<UdpFramePacket>(surviving.Count);
            foreach (var packet in surviving)
            {
                if (_random.NextDouble() < _corruptionRate)
                {
                    PacketsCorrupted++;
                    var corruptedData = (byte[])packet.Data.Clone();
                    if (corruptedData.Length > 32) // Only corrupt payload, not header
                    {
                        int corruptIndex = 32 + _random.Next(corruptedData.Length - 32);
                        corruptedData[corruptIndex] ^= (byte)(1 << _random.Next(8));
                    }
                    processed.Add(new UdpFramePacket
                    {
                        Data = corruptedData,
                        PacketIndex = packet.PacketIndex,
                        TotalPackets = packet.TotalPackets,
                        Flags = packet.Flags
                    });
                }
                else
                {
                    processed.Add(packet);
                }
            }

            // Step 3: Apply reordering (Fisher-Yates partial shuffle)
            if (_reorderRate > 0 && processed.Count > 1)
            {
                int reorderCount = 0;
                for (int i = processed.Count - 1; i > 0; i--)
                {
                    if (_random.NextDouble() < _reorderRate)
                    {
                        int j = _random.Next(i + 1);
                        if (i != j)
                        {
                            (processed[i], processed[j]) = (processed[j], processed[i]);
                            reorderCount++;
                        }
                    }
                }
                PacketsReordered += reorderCount;
            }

            return processed;
        }
    }

    /// <summary>
    /// Updates the packet loss rate at runtime.
    /// </summary>
    /// <param name="rate">New loss rate (0.0-1.0).</param>
    public void SetLossRate(double rate)
    {
        if (rate < 0.0 || rate > 1.0)
            throw new ArgumentOutOfRangeException(nameof(rate), "Loss rate must be between 0.0 and 1.0.");

        lock (_lock)
        {
            _packetLossRate = rate;
        }
    }

    /// <summary>
    /// Updates the reorder rate at runtime.
    /// </summary>
    /// <param name="rate">New reorder rate (0.0-1.0).</param>
    public void SetReorderRate(double rate)
    {
        if (rate < 0.0 || rate > 1.0)
            throw new ArgumentOutOfRangeException(nameof(rate), "Reorder rate must be between 0.0 and 1.0.");

        lock (_lock)
        {
            _reorderRate = rate;
        }
    }

    /// <summary>
    /// Updates the corruption rate at runtime.
    /// </summary>
    /// <param name="rate">New corruption rate (0.0-1.0).</param>
    public void SetCorruptionRate(double rate)
    {
        if (rate < 0.0 || rate > 1.0)
            throw new ArgumentOutOfRangeException(nameof(rate), "Corruption rate must be between 0.0 and 1.0.");

        lock (_lock)
        {
            _corruptionRate = rate;
        }
    }

    /// <summary>
    /// Resets all statistics counters to zero.
    /// </summary>
    public void ResetStatistics()
    {
        lock (_lock)
        {
            PacketsSent = 0;
            PacketsLost = 0;
            PacketsReordered = 0;
            PacketsCorrupted = 0;
        }
    }
}
