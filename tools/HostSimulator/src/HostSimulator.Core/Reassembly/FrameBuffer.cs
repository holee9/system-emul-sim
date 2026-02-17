using System.Collections.Concurrent;

namespace HostSimulator.Core.Reassembly;

/// <summary>
/// Tracks received packets for a single frame during reassembly.
/// REQ-SIM-041: Correctly reassemble frame using packet_index when packets arrive out of order.
/// REQ-SIM-042: Mark frame as incomplete and report missing packets after timeout.
/// </summary>
public sealed class FrameBuffer
{
    private readonly ConcurrentDictionary<ushort, byte[]> _packets;
    private readonly DateTime _createdAt;

    /// <summary>
    /// Gets the frame identifier.
    /// </summary>
    public uint FrameId { get; }

    /// <summary>
    /// Gets the total number of packets expected for this frame.
    /// </summary>
    public ushort TotalPackets { get; }

    /// <summary>
    /// Gets the frame height in pixels (rows).
    /// </summary>
    public ushort Rows { get; }

    /// <summary>
    /// Gets the frame width in pixels (cols).
    /// </summary>
    public ushort Cols { get; }

    /// <summary>
    /// Gets the number of packets received so far.
    /// </summary>
    public int ReceivedPacketCount => _packets.Count;

    /// <summary>
    /// Gets whether all packets for this frame have been received.
    /// </summary>
    public bool IsComplete => _packets.Count >= TotalPackets;

    /// <summary>
    /// Initializes a new instance of the FrameBuffer class.
    /// </summary>
    /// <param name="frameId">Frame identifier.</param>
    /// <param name="totalPackets">Total number of packets in the frame.</param>
    /// <param name="rows">Frame height in pixels.</param>
    /// <param name="cols">Frame width in pixels.</param>
    public FrameBuffer(uint frameId, ushort totalPackets, ushort rows, ushort cols)
    {
        if (totalPackets == 0)
            throw new ArgumentException("Total packets must be greater than zero.", nameof(totalPackets));
        if (rows == 0)
            throw new ArgumentException("Rows must be greater than zero.", nameof(rows));
        if (cols == 0)
            throw new ArgumentException("Cols must be greater than zero.", nameof(cols));

        FrameId = frameId;
        TotalPackets = totalPackets;
        Rows = rows;
        Cols = cols;
        _packets = new ConcurrentDictionary<ushort, byte[]>();
        _createdAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a packet to the buffer.
    /// </summary>
    /// <param name="packetSeq">Packet sequence number within the frame.</param>
    /// <param name="payload">Packet payload data.</param>
    /// <returns>True if the packet was added, false if it was a duplicate.</returns>
    public bool AddPacket(ushort packetSeq, byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        return _packets.TryAdd(packetSeq, payload);
    }

    /// <summary>
    /// Checks whether a specific packet has been received.
    /// </summary>
    /// <param name="packetSeq">Packet sequence number to check.</param>
    /// <returns>True if the packet has been received.</returns>
    public bool HasPacket(ushort packetSeq)
    {
        return _packets.ContainsKey(packetSeq);
    }

    /// <summary>
    /// Gets the indices of missing packets.
    /// </summary>
    /// <returns>Array of missing packet indices.</returns>
    public ushort[] GetMissingPackets()
    {
        var missing = new List<ushort>();

        for (ushort i = 0; i < TotalPackets; i++)
        {
            if (!_packets.ContainsKey(i))
            {
                missing.Add(i);
            }
        }

        return missing.ToArray();
    }

    /// <summary>
    /// Gets the total payload size of all received packets.
    /// </summary>
    /// <returns>Total payload size in bytes.</returns>
    public int GetTotalPayloadSize()
    {
        return _packets.Values.Sum(p => p.Length);
    }

    /// <summary>
    /// Assembles the complete frame from all received packets in order.
    /// </summary>
    /// <returns>Complete frame payload.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the frame is incomplete.</exception>
    public byte[] AssembleFrame()
    {
        if (!IsComplete)
            throw new InvalidOperationException($"Cannot assemble incomplete frame {FrameId}. Missing packets: {string.Join(", ", GetMissingPackets())}");

        int totalSize = GetTotalPayloadSize();
        var result = new byte[totalSize];
        int offset = 0;

        for (ushort i = 0; i < TotalPackets; i++)
        {
            var packet = _packets[i];
            Buffer.BlockCopy(packet, 0, result, offset, packet.Length);
            offset += packet.Length;
        }

        return result;
    }

    /// <summary>
    /// Gets the age of this buffer since creation.
    /// </summary>
    /// <returns>Time elapsed since creation.</returns>
    public TimeSpan GetAge()
    {
        return DateTime.UtcNow - _createdAt;
    }

    /// <summary>
    /// Checks whether this buffer has exceeded the specified timeout.
    /// </summary>
    /// <param name="timeout">Timeout duration.</param>
    /// <returns>True if timeout has been exceeded.</returns>
    public bool IsTimedOut(TimeSpan timeout)
    {
        return GetAge() > timeout;
    }

    /// <summary>
    /// Returns a string representation of the buffer state.
    /// </summary>
    public override string ToString()
    {
        return $"FrameBuffer {{ FrameId={FrameId}, Packets={ReceivedPacketCount}/{TotalPackets}, Size={Cols}x{Rows}, Age={GetAge().TotalMilliseconds:F0}ms }}";
    }
}
