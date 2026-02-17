using System.Collections.Concurrent;
using Common.Dto.Dtos;

namespace HostSimulator.Core.Reassembly;

/// <summary>
/// Reassembles complete frames from UDP packets.
/// REQ-SIM-040: Receive UDP packets and reassemble complete frames.
/// REQ-SIM-041: Correctly reassemble frame using packet_index when packets arrive out of order.
/// REQ-SIM-042: Mark frame as incomplete and report missing packets after timeout.
/// </summary>
public sealed class FrameReassembler
{
    private readonly ConcurrentDictionary<uint, FrameBuffer> _pendingFrames;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the FrameReassembler class.
    /// </summary>
    /// <param name="timeout">Timeout for incomplete frames (default: 5 seconds).</param>
    public FrameReassembler(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _pendingFrames = new ConcurrentDictionary<uint, FrameBuffer>();
    }

    /// <summary>
    /// Processes a packet and returns the reassembly result.
    /// </summary>
    /// <param name="header">Parsed frame header.</param>
    /// <param name="payload">Packet payload data (after the 32-byte header).</param>
    /// <returns>Reassembly result (null if packet was ignored as duplicate).</returns>
    public FrameReassemblyResult? ProcessPacket(FrameHeader header, byte[] payload)
    {
        if (header == null)
            throw new ArgumentNullException(nameof(header));
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        // Get or create frame buffer
        var buffer = _pendingFrames.GetOrAdd(header.FrameId, id =>
            new FrameBuffer(id, header.TotalPackets, header.Rows, header.Cols));

        // Add packet to buffer
        bool added = buffer.AddPacket(header.PacketSeq, payload);

        // If duplicate packet, ignore
        if (!added)
            return null;

        // Check if frame is complete
        if (buffer.IsComplete)
        {
            // Remove from pending and return complete frame
            _pendingFrames.TryRemove(header.FrameId, out _);
            var frameData = AssembleFrameData(buffer, header);
            return FrameReassemblyResult.Complete(header.FrameId, frameData);
        }

        return FrameReassemblyResult.Pending(header.FrameId);
    }

    /// <summary>
    /// Checks for timed-out frames and returns the first incomplete frame.
    /// </summary>
    /// <returns>First incomplete frame result, or null if no frames are timed out.</returns>
    public FrameReassemblyResult? CheckTimeouts()
    {
        foreach (var kvp in _pendingFrames)
        {
            if (kvp.Value.IsTimedOut(_timeout))
            {
                // Remove timed out frame
                _pendingFrames.TryRemove(kvp.Key, out _);
                var missing = kvp.Value.GetMissingPackets();
                return FrameReassemblyResult.Incomplete(kvp.Key, missing);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the number of pending frames.
    /// </summary>
    public int GetPendingFrameCount()
    {
        return _pendingFrames.Count;
    }

    /// <summary>
    /// Resets the reassembler, clearing all pending frames.
    /// </summary>
    public void Reset()
    {
        _pendingFrames.Clear();
    }

    /// <summary>
    /// Assembles frame data from a complete buffer.
    /// </summary>
    private static FrameData AssembleFrameData(FrameBuffer buffer, FrameHeader header)
    {
        var assembledBytes = buffer.AssembleFrame();

        // Convert byte array to ushort array (little-endian)
        int pixelCount = buffer.Rows * buffer.Cols;
        var pixels = new ushort[pixelCount];

        for (int i = 0; i < pixelCount; i++)
        {
            int byteIndex = i * 2;
            pixels[i] = (ushort)(assembledBytes[byteIndex] | (assembledBytes[byteIndex + 1] << 8));
        }

        return new FrameData(
            frameNumber: (int)header.FrameId,
            width: buffer.Cols,
            height: buffer.Rows,
            pixels: pixels
        );
    }
}
