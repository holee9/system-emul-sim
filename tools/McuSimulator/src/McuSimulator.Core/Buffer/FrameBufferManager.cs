// Copyright (c) 2026 ABYZ Lab. All rights reserved.

// @MX:NOTE: 4버퍼 링 버퍼 관리자 - fw/src/frame_manager.c의 1:1 C# 포트
// REQ-FW-050~052: oldest-drop 정책을 사용하는 4버퍼 링
// REQ-FW-111: 런타임 통계 제공
// @MX:ANCHOR: 프레임 버퍼 할당/해제 핵심 API - McuTopSimulator, 테스트에서 호출
// @MX:WARN: 모든 상태 변경 작업은 lock으로 보호됩니다 (스레드 안전성)

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
    ///   If all buffers are busy, find oldest non-FREE buffer
    ///   (prefer READY over SENDING), drop it, and reuse.
    ///   NEVER drops FILLING buffers to avoid race with pending CommitBuffer calls.
    /// </summary>
    /// <param name="frameNumber">Frame sequence number.</param>
    /// <param name="buffer">Output: buffer data array.</param>
    /// <param name="size">Output: buffer size in bytes.</param>
    /// <returns>0 on success, -1 on error (all buffers FILLING).</returns>
    // @MX:ANCHOR: CSI-2 RX용 버퍼 획득 (Producer) - McuTopSimulator에서 호출
    public int GetBuffer(uint frameNumber, out byte[] buffer, out int size)
    {
        lock (_lock)
        {
            // First, try to find a FREE buffer
            int freeIndex = -1;
            for (int i = 0; i < _numBuffers; i++)
            {
                if (_buffers[i].State == BufferState.Free)
                {
                    freeIndex = i;
                    break;
                }
            }

            // If no FREE buffer, use oldest-drop policy
            if (freeIndex < 0)
            {
                // Prefer READY over SENDING (READY is less costly to drop)
                // NEVER drop FILLING to avoid race with pending CommitBuffer calls
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

                // Second pass: if no READY, look for oldest SENDING buffer
                if (dropIndex < 0)
                {
                    for (int i = 0; i < _numBuffers; i++)
                    {
                        if (_buffers[i].State == BufferState.Sending)
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
                    // All buffers are FILLING - cannot proceed
                    buffer = Array.Empty<byte>();
                    size = 0;
                    return -1;
                }

                // Drop the buffer
                _buffers[dropIndex].State = BufferState.Free;
                _stats.FramesDropped++;
                _stats.Overruns++;

                freeIndex = dropIndex;
            }

            // Use the FREE buffer
            var desc = _buffers[freeIndex];
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
    // @MX:ANCHOR: 버퍼 커밋 (Producer) - McuTopSimulator에서 호출
    public int CommitBuffer(uint frameNumber)
    {
        lock (_lock)
        {
            // Search for the buffer with matching frameNumber in FILLING state
            // This is necessary because GetBuffer may have dropped the target slot
            // and used a different slot (oldest-drop policy)
            int targetIndex = -1;
            for (int i = 0; i < _numBuffers; i++)
            {
                if (_buffers[i].State == BufferState.Filling && _buffers[i].FrameNumber == frameNumber)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                // Buffer not found in FILLING state with matching frameNumber
                return -1;
            }

            _buffers[targetIndex].State = BufferState.Ready;
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
    // @MX:ANCHOR: TX용 READY 버퍼 획득 (Consumer) - McuTopSimulator에서 호출
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
    // @MX:ANCHOR: 버퍼 해제 (Consumer) - McuTopSimulator에서 호출
    public int ReleaseBuffer(uint frameNumber)
    {
        lock (_lock)
        {
            // Search for the buffer with matching frameNumber in SENDING state
            // This is necessary because GetBuffer may have dropped the target slot
            // and used a different slot (oldest-drop policy)
            int targetIndex = -1;
            for (int i = 0; i < _numBuffers; i++)
            {
                if (_buffers[i].State == BufferState.Sending && _buffers[i].FrameNumber == frameNumber)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                // Buffer not found in SENDING state with matching frameNumber
                return -1;
            }

            _buffers[targetIndex].State = BufferState.Free;
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
    /// Get the current state of the buffer with the given frame number.
    /// Searches all buffers to find the one matching the frame number.
    /// Useful for debugging and testing.
    /// </summary>
    /// <param name="frameNumber">Frame sequence number.</param>
    /// <returns>Current buffer state, or Free if not found.</returns>
    public BufferState GetBufferState(uint frameNumber)
    {
        lock (_lock)
        {
            // Search for the buffer with matching frameNumber
            // This is necessary because GetBuffer may have dropped the target slot
            // and used a different slot (oldest-drop policy)
            for (int i = 0; i < _numBuffers; i++)
            {
                if (_buffers[i].FrameNumber == frameNumber)
                {
                    return _buffers[i].State;
                }
            }

            // Frame not found, return Free state
            return BufferState.Free;
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
