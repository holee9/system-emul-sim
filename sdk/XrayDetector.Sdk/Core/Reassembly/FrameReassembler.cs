using System.Buffers;
using System.Collections.Concurrent;

namespace XrayDetector.Core.Reassembly;

/// <summary>
/// Frame reassembler with out-of-order packet handling, CRC validation, and timeout management.
/// Supports up to 8 concurrent reassembly slots by default.
/// </summary>
public sealed class FrameReassembler
{
    private readonly ConcurrentDictionary<uint, ReassemblyBuffer> _frames;
    private readonly ArrayPool<ushort> _pool;
    private readonly TimeSpan _timeout;
    private readonly uint _maxConcurrentFrames;

    /// <summary>Maximum number of concurrent frames to reassemble.</summary>
    public uint MaxConcurrentFrames => _maxConcurrentFrames;

    /// <summary>Timeout for incomplete frames.</summary>
    public TimeSpan Timeout => _timeout;

    /// <summary>Current number of active frame slots.</summary>
    public int ActiveFrameCount => _frames.Count;

    /// <summary>
    /// Creates a new frame reassembler with default settings.
    /// </summary>
    public FrameReassembler(ArrayPool<ushort>? pool = null)
        : this(pool ?? ArrayPool<ushort>.Shared, maxConcurrentFrames: 8, timeout: TimeSpan.FromMilliseconds(500))
    {
    }

    /// <summary>
    /// Creates a new frame reassembler with custom settings.
    /// </summary>
    public FrameReassembler(ArrayPool<ushort>? pool, uint maxConcurrentFrames, TimeSpan timeout)
    {
        _pool = pool ?? ArrayPool<ushort>.Shared;
        _maxConcurrentFrames = maxConcurrentFrames;
        _timeout = timeout;
        _frames = new ConcurrentDictionary<uint, ReassemblyBuffer>();
    }

    /// <summary>
    /// Processes an incoming packet.
    /// </summary>
    /// <param name="packet">Raw packet data including header.</param>
    /// <returns>Reassembly result indicating status and frame data if complete.</returns>
    public FrameReassemblyResult ProcessPacket(byte[] packet)
    {
        try
        {
            // Validate packet length
            if (packet == null || packet.Length < 34)
                return new FrameReassemblyResult(ReassemblyStatus.Error, 0, null, "Invalid packet length");

            // Validate CRC
            if (!Crc16CcittValidator.ValidateHeader(packet))
                return new FrameReassemblyResult(ReassemblyStatus.CrcError, 0, null, "CRC validation failed");

            // Parse header
            uint frameNumber = ParseFrameNumber(packet);
            uint packetSeq = ParsePacketSeq(packet);
            uint totalPackets = ParseTotalPackets(packet);
            uint cols = ParseCols(packet); // This represents pixels per packet

            // Get or create reassembly buffer
            var buffer = GetOrCreateBuffer(frameNumber, totalPackets, (int)cols);

            // Extract pixel data (after 34-byte header)
            int pixelDataSize = packet.Length - 34;
            int pixelCount = pixelDataSize / 2; // 2 bytes per pixel
            ushort[] pixels = new ushort[pixelCount];

            for (int i = 0; i < pixelCount; i++)
            {
                int offset = 34 + (i * 2);
                pixels[i] = (ushort)((packet[offset] << 8) | packet[offset + 1]);
            }

            // Add packet to buffer
            if (!buffer.AddPacket(packetSeq, pixels))
            {
                // Duplicate or out-of-range packet - ignore
                return new FrameReassemblyResult(ReassemblyStatus.Processing, frameNumber, null, null);
            }

            // Check if frame is complete
            if (buffer.IsComplete)
            {
                // Remove buffer from slots and return complete frame
                _frames.TryRemove(frameNumber, out _);
                ushort[] frameData = buffer.AssembleFrame();
                buffer.Dispose();

                return new FrameReassemblyResult(ReassemblyStatus.Complete, frameNumber, frameData, null);
            }

            // Check for timeout
            if (buffer.Age > _timeout)
            {
                // Timeout - return partial frame with zero-fill
                _frames.TryRemove(frameNumber, out _);
                buffer.FillMissingPackets();
                ushort[] frameData = buffer.AssembleFrame();
                buffer.Dispose();

                return new FrameReassemblyResult(ReassemblyStatus.Partial, frameNumber, frameData, "Frame timeout");
            }

            return new FrameReassemblyResult(ReassemblyStatus.Processing, frameNumber, null, null);
        }
        catch (Exception ex)
        {
            return new FrameReassemblyResult(ReassemblyStatus.Error, 0, null, ex.Message);
        }
    }

    /// <summary>
    /// Gets status information for a specific frame.
    /// </summary>
    public FrameStatus? GetFrameStatus(uint frameNumber)
    {
        if (_frames.TryGetValue(frameNumber, out var buffer))
        {
            return new FrameStatus(
                buffer.FrameNumber,
                buffer.ExpectedPackets,
                buffer.ReceivedPackets,
                buffer.IsComplete,
                buffer.Age
            );
        }

        return null;
    }

    /// <summary>
    /// Cleans up expired frames and returns count of removed frames.
    /// </summary>
    public int CleanupExpiredFrames()
    {
        var expiredKeys = _frames
            .Where(kvp => kvp.Value.Age > _timeout)
            .Select(kvp => kvp.Key)
            .ToList();

        int removed = 0;
        foreach (var key in expiredKeys)
        {
            if (_frames.TryRemove(key, out var buffer))
            {
                buffer.Dispose();
                removed++;
            }
        }

        return removed;
    }

    private ReassemblyBuffer GetOrCreateBuffer(uint frameNumber, uint totalPackets, int pixelsPerPacket)
    {
        // Try to get existing buffer
        if (_frames.TryGetValue(frameNumber, out var existingBuffer))
        {
            return existingBuffer;
        }

        // Check if we need to evict old slots
        if (_frames.Count >= _maxConcurrentFrames)
        {
            // Evict oldest frame
            var oldest = _frames.OrderBy(kvp => kvp.Value.Age).FirstOrDefault();
            if (oldest.Key != 0)
            {
                _frames.TryRemove(oldest.Key, out var evictedBuffer);
                evictedBuffer?.Dispose();
            }
        }

        // Create new buffer
        var newBuffer = ReassemblyBuffer.Create(frameNumber, totalPackets, pixelsPerPacket, _pool);
        _frames.TryAdd(frameNumber, newBuffer);

        return newBuffer;
    }

    private static uint ParseFrameNumber(byte[] packet)
    {
        return (uint)((packet[8] << 24) | (packet[9] << 16) | (packet[10] << 8) | packet[11]);
    }

    private static uint ParsePacketSeq(byte[] packet)
    {
        return (uint)((packet[12] << 24) | (packet[13] << 16) | (packet[14] << 8) | packet[15]);
    }

    private static uint ParseTotalPackets(byte[] packet)
    {
        return (uint)((packet[16] << 24) | (packet[17] << 16) | (packet[18] << 8) | packet[19]);
    }

    private static uint ParseCols(byte[] packet)
    {
        return (uint)((packet[32] << 8) | packet[33]);
    }
}
