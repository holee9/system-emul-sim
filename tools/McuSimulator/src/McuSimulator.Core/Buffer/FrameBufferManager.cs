// Copyright (c) 2026 ABYZ Lab. All rights reserved.

namespace McuSimulator.Core.Buffer;

/// <summary>
/// Core frame buffer manager implementing a 4-buffer ring with oldest-drop policy.
/// 1:1 C# port from fw/src/frame_manager.c.
///
/// REQ-FW-050~052: 4-buffer ring with oldest-drop policy.
/// REQ-FW-111: Runtime statistics.
///
/// Thread safety: All state-modifying operations are protected by lock.
/// Buffer index mapping: frameNumber % NumBuffers.
/// </summary>
public class FrameBufferManager
{
    private readonly FrameBufferDescriptor[] _buffers;
    private readonly int _numBuffers;
    private readonly int _frameSize;
    private readonly FrameManagerStatistics _stats;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes the Frame Buffer Manager.
    /// Allocates NumBuffers buffer descriptors, all starting in FREE state.
    /// </summary>
    /// <param name="config">Frame Manager configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
    public FrameBufferManager(FrameManagerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _numBuffers = config.NumBuffers;
        _frameSize = config.FrameSize;
        _stats = new FrameManagerStatistics();

        _buffers = new FrameBufferDescriptor[_numBuffers];
        for (int i = 0; i < _numBuffers; i++)
        {
            _buffers[i] = new FrameBufferDescriptor
            {
                Data = new byte[_frameSize],
                Size = _frameSize,
                State = BufferState.Free,
                FrameNumber = 0,
                TotalPackets = 0,
                SentPackets = 0
            };
        }
    }

    /// <summary>
    /// Acquire buffer for CSI-2 RX (Producer).
    /// Transitions buffer from FREE to FILLING state.
    /// Implements oldest-drop policy (REQ-FW-051):
    ///   If target slot is not FREE, find oldest non-FREE buffer
    ///   (prefer READY over SENDING), drop it, and reuse.
    /// </summary>
    /// <param name="frameNumber">Frame sequence number.</param>
    /// <param name="buffer">Output: buffer data array.</param>
    /// <param name="size">Output: buffer size in bytes.</param>
    /// <returns>0 on success, negative on error.</returns>
    public int GetBuffer(uint frameNumber, out byte[] buffer, out int size)
    {
        lock (_lock)
        {
            int index = (int)(frameNumber % (uint)_numBuffers);
            var desc = _buffers[index];

            if (desc.State != BufferState.Free)
            {
                // Oldest-drop policy: find oldest non-FREE buffer to drop.
                // Prefer READY over SENDING (READY is less costly to drop).
                int dropIndex = -1;

                // First pass: look for oldest READY buffer
                for (int i = 0; i < _numBuffers; i++)
                {
                    if (_buffers[i].State == BufferState.Ready)
                    {
                        if (dropIndex < 0 || _buffers[i].FrameNumber < _buffers[dropIndex].FrameNumber)
                        {
                            dropIndex = i;
                        }
                    }
                }

                // Second pass: if no READY, look for any non-FREE buffer
                if (dropIndex < 0)
                {
                    for (int i = 0; i < _numBuffers; i++)
                    {
                        if (_buffers[i].State != BufferState.Free)
                        {
                            if (dropIndex < 0 || _buffers[i].FrameNumber < _buffers[dropIndex].FrameNumber)
                            {
                                dropIndex = i;
                            }
                        }
                    }
                }

                if (dropIndex < 0)
                {
                    // Should not happen since target slot is not FREE
                    buffer = Array.Empty<byte>();
                    size = 0;
                    return -1;
                }

                // Drop the buffer
                _buffers[dropIndex].State = BufferState.Free;
                _stats.FramesDropped++;
                _stats.Overruns++;

                // Use the dropped slot
                index = dropIndex;
                desc = _buffers[index];
            }

            // Transition to FILLING
            desc.State = BufferState.Filling;
            desc.FrameNumber = frameNumber;
            desc.TotalPackets = 0;
            desc.SentPackets = 0;

            buffer = desc.Data!;
            size = desc.Size;
            return 0;
        }
    }

    /// <summary>
    /// Commit filled buffer (Producer).
    /// Transitions buffer from FILLING to READY state.
    /// Increments frames_received counter.
    /// </summary>
    /// <param name="frameNumber">Frame sequence number.</param>
    /// <returns>0 on success, -1 if buffer is not in FILLING state.</returns>
    public int CommitBuffer(uint frameNumber)
    {
        lock (_lock)
        {
            int index = (int)(frameNumber % (uint)_numBuffers);
            var desc = _buffers[index];

            if (desc.State != BufferState.Filling)
            {
                return -1;
            }

            desc.State = BufferState.Ready;
            _stats.FramesReceived++;
            return 0;
        }
    }

    /// <summary>
    /// Acquire ready buffer for TX (Consumer).
    /// Finds oldest READY buffer and transitions it to SENDING state.
    /// </summary>
    /// <param name="buffer">Output: buffer data array, or null if no READY buffers.</param>
    /// <param name="size">Output: buffer size in bytes.</param>
    /// <param name="frameNumber">Output: frame sequence number.</param>
    /// <returns>0 on success, -1 if no READY buffers.</returns>
    public int GetReadyBuffer(out byte[]? buffer, out int size, out uint frameNumber)
    {
        lock (_lock)
        {
            // Find oldest READY buffer (lowest frame number)
            int readyIndex = -1;
            uint oldestFrameNumber = uint.MaxValue;

            for (int i = 0; i < _numBuffers; i++)
            {
                if (_buffers[i].State == BufferState.Ready &&
                    _buffers[i].FrameNumber < oldestFrameNumber)
                {
                    oldestFrameNumber = _buffers[i].FrameNumber;
                    readyIndex = i;
                }
            }

            if (readyIndex < 0)
            {
                buffer = null;
                size = 0;
                frameNumber = 0;
                return -1;
            }

            var desc = _buffers[readyIndex];
            desc.State = BufferState.Sending;

            buffer = desc.Data;
            size = desc.Size;
            frameNumber = desc.FrameNumber;
            return 0;
        }
    }

    /// <summary>
    /// Release transmitted buffer (Consumer).
    /// Transitions buffer from SENDING to FREE state.
    /// Increments frames_sent counter.
    /// </summary>
    /// <param name="frameNumber">Frame sequence number.</param>
    /// <returns>0 on success, -1 if buffer is not in SENDING state.</returns>
    public int ReleaseBuffer(uint frameNumber)
    {
        lock (_lock)
        {
            int index = (int)(frameNumber % (uint)_numBuffers);
            var desc = _buffers[index];

            if (desc.State != BufferState.Sending)
            {
                return -1;
            }

            desc.State = BufferState.Free;
            _stats.FramesSent++;
            return 0;
        }
    }

    /// <summary>
    /// Get a copy of the current statistics.
    /// REQ-FW-111: Runtime statistics.
    /// </summary>
    /// <returns>A copy of the current statistics.</returns>
    public FrameManagerStatistics GetStatistics()
    {
        lock (_lock)
        {
            return _stats.Clone();
        }
    }

    /// <summary>
    /// Get the current state of the buffer mapped to the given frame number.
    /// Useful for debugging and testing.
    /// </summary>
    /// <param name="frameNumber">Frame sequence number.</param>
    /// <returns>Current buffer state.</returns>
    public BufferState GetBufferState(uint frameNumber)
    {
        lock (_lock)
        {
            int index = (int)(frameNumber % (uint)_numBuffers);
            return _buffers[index].State;
        }
    }

    /// <summary>
    /// Reset all buffers to FREE state and clear statistics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            for (int i = 0; i < _numBuffers; i++)
            {
                _buffers[i].State = BufferState.Free;
                _buffers[i].FrameNumber = 0;
                _buffers[i].TotalPackets = 0;
                _buffers[i].SentPackets = 0;
            }

            _stats.FramesReceived = 0;
            _stats.FramesSent = 0;
            _stats.FramesDropped = 0;
            _stats.PacketsSent = 0;
            _stats.BytesSent = 0;
            _stats.Overruns = 0;
        }
    }
}
