using System.Buffers;

namespace XrayDetector.Core.Reassembly;

/// <summary>
/// Reassembly buffer for sorting and storing out-of-order packets.
/// Uses circular buffer pattern with zero-fill for missing packets.
/// </summary>
public sealed class ReassemblyBuffer : IDisposable
{
    private readonly ushort[][] _packets; // Array of rented buffers
    private readonly DateTime _createdAt;
    private readonly ArrayPool<ushort> _pool;
    private readonly bool[] _receivedFlags; // Track which packets have been received
    private int _receivedCount;

    /// <summary>Frame number for this reassembly buffer.</summary>
    public uint FrameNumber { get; }

    /// <summary>Expected total number of packets.</summary>
    public uint ExpectedPackets { get; }

    /// <summary>Number of pixels per packet.</summary>
    public int PixelsPerPacket { get; }

    /// <summary>Number of packets received so far.</summary>
    public uint ReceivedPackets => (uint)_receivedCount;

    /// <summary>Age of this buffer since creation.</summary>
    public TimeSpan Age => DateTime.UtcNow - _createdAt;

    private ReassemblyBuffer(uint frameNumber, uint expectedPackets, int pixelsPerPacket, ArrayPool<ushort> pool)
    {
        FrameNumber = frameNumber;
        ExpectedPackets = expectedPackets;
        PixelsPerPacket = pixelsPerPacket;
        _pool = pool;
        _createdAt = DateTime.UtcNow;
        _packets = new ushort[expectedPackets][];
        _receivedFlags = new bool[expectedPackets];
        _receivedCount = 0;
    }

    /// <summary>
    /// Creates a new reassembly buffer.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when frameNumber is 0, expectedPackets is 0, or pixelsPerPacket is 0.
    /// </exception>
    public static ReassemblyBuffer Create(uint frameNumber, uint expectedPackets, int pixelsPerPacket, ArrayPool<ushort> pool)
    {
        if (frameNumber == 0)
            throw new ArgumentOutOfRangeException(nameof(frameNumber), "Frame number must be greater than 0.");
        if (expectedPackets == 0)
            throw new ArgumentOutOfRangeException(nameof(expectedPackets), "Expected packets must be greater than 0.");
        if (pixelsPerPacket == 0)
            throw new ArgumentOutOfRangeException(nameof(pixelsPerPacket), "Pixels per packet must be greater than 0.");

        return new ReassemblyBuffer(frameNumber, expectedPackets, pixelsPerPacket, pool);
    }

    /// <summary>
    /// Adds a packet to the buffer.
    /// </summary>
    /// <param name="packetNumber">Zero-based packet number.</param>
    /// <param name="pixels">Pixel data array.</param>
    /// <returns>True if packet was added, false if packet number is out of range or duplicate.</returns>
    public bool AddPacket(uint packetNumber, ushort[] pixels)
    {
        if (packetNumber >= ExpectedPackets)
            return false;

        int index = (int)packetNumber;

        if (_receivedFlags[index])
            return false; // Duplicate packet

        // Copy pixel data
        _packets[index] = _pool.Rent(pixels.Length);
        Array.Copy(pixels, _packets[index], pixels.Length);
        _receivedFlags[index] = true;
        _receivedCount++;

        return true;
    }

    /// <summary>
    /// Checks if a specific packet has been received.
    /// </summary>
    /// <param name="packetNumber">Zero-based packet number.</param>
    /// <returns>True if packet was received, false otherwise.</returns>
    public bool HasPacket(uint packetNumber)
    {
        if (packetNumber >= ExpectedPackets)
            return false;

        return _receivedFlags[packetNumber];
    }

    /// <summary>
    /// Gets a value indicating whether all packets have been received.
    /// </summary>
    public bool IsComplete => _receivedCount == ExpectedPackets;

    /// <summary>
    /// Gets indices of missing packets.
    /// </summary>
    /// <returns>List of missing packet indices.</returns>
    public List<uint> GetMissingPacketIndices()
    {
        var missing = new List<uint>();

        for (uint i = 0; i < ExpectedPackets; i++)
        {
            if (!_receivedFlags[i])
                missing.Add(i);
        }

        return missing;
    }

    /// <summary>
    /// Fills missing packets with zero values (0x0000).
    /// </summary>
    public void FillMissingPackets()
    {
        for (uint i = 0; i < ExpectedPackets; i++)
        {
            if (!_receivedFlags[i])
            {
                _packets[i] = _pool.Rent(PixelsPerPacket);
                Array.Clear(_packets[i], 0, PixelsPerPacket);
                _receivedFlags[i] = true;
                _receivedCount++;
            }
        }
    }

    /// <summary>
    /// Assembles the frame from received packets.
    /// </summary>
    /// <returns>Assembled frame data. Returns partial frame if not complete.</returns>
    public ushort[] AssembleFrame()
    {
        // Calculate total pixels from received packets
        int totalPixels = _receivedCount * PixelsPerPacket;
        var frame = new ushort[totalPixels];
        int offset = 0;

        for (int i = 0; i < ExpectedPackets; i++)
        {
            if (_receivedFlags[i])
            {
                Array.Copy(_packets[i]!, 0, frame, offset, PixelsPerPacket);
                offset += PixelsPerPacket;
            }
        }

        return frame;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Return all rented arrays to pool
        for (int i = 0; i < ExpectedPackets; i++)
        {
            if (_packets[i] != null)
            {
                _pool.Return(_packets[i]!, clearArray: true);
            }
        }
    }
}
