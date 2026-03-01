using McuSimulator.Core.Buffer;

namespace McuSimulator.Tests.Buffer;

public class FrameBufferManagerTests
{
    private readonly FrameBufferManager _manager;
    private readonly FrameManagerConfig _config;

    public FrameBufferManagerTests()
    {
        _config = new FrameManagerConfig
        {
            Rows = 4,
            Cols = 4,
            BitDepth = 8,
            NumBuffers = 4
        };
        _manager = new FrameBufferManager(_config);
    }

    #region Normal Single Frame Cycle

    [Fact]
    public void Normal_SingleFrameCycle_GetBuffer_Commit_GetReady_Release()
    {
        // GetBuffer: FREE -> FILLING
        int result = _manager.GetBuffer(0, out byte[] buffer, out int size);
        Assert.Equal(0, result);
        Assert.NotNull(buffer);
        Assert.Equal(16, size); // 4*4*1 = 16 bytes
        Assert.Equal(BufferState.Filling, _manager.GetBufferState(0));

        // CommitBuffer: FILLING -> READY
        result = _manager.CommitBuffer(0);
        Assert.Equal(0, result);
        Assert.Equal(BufferState.Ready, _manager.GetBufferState(0));

        // GetReadyBuffer: READY -> SENDING
        result = _manager.GetReadyBuffer(out byte[]? readyBuf, out int readySize, out uint frameNum);
        Assert.Equal(0, result);
        Assert.NotNull(readyBuf);
        Assert.Equal(16, readySize);
        Assert.Equal(0u, frameNum);
        Assert.Equal(BufferState.Sending, _manager.GetBufferState(0));

        // ReleaseBuffer: SENDING -> FREE
        result = _manager.ReleaseBuffer(0);
        Assert.Equal(0, result);
        Assert.Equal(BufferState.Free, _manager.GetBufferState(0));

        // Verify statistics
        var stats = _manager.GetStatistics();
        Assert.Equal(1ul, stats.FramesReceived);
        Assert.Equal(1ul, stats.FramesSent);
        Assert.Equal(0ul, stats.FramesDropped);
        Assert.Equal(0ul, stats.Overruns);
    }

    #endregion

    #region Sequential Use

    [Fact]
    public void FourBuffer_SequentialUse_AllBuffersUsed()
    {
        // Fill all 4 buffers sequentially
        for (uint i = 0; i < 4; i++)
        {
            int result = _manager.GetBuffer(i, out _, out _);
            Assert.Equal(0, result);
            Assert.Equal(BufferState.Filling, _manager.GetBufferState(i));

            result = _manager.CommitBuffer(i);
            Assert.Equal(0, result);
            Assert.Equal(BufferState.Ready, _manager.GetBufferState(i));
        }

        // All 4 should be READY
        for (uint i = 0; i < 4; i++)
        {
            Assert.Equal(BufferState.Ready, _manager.GetBufferState(i));
        }

        // Consume all 4 in order (oldest first)
        for (uint i = 0; i < 4; i++)
        {
            int result = _manager.GetReadyBuffer(out _, out _, out uint frameNum);
            Assert.Equal(0, result);
            Assert.Equal(i, frameNum);

            result = _manager.ReleaseBuffer(frameNum);
            Assert.Equal(0, result);
        }

        var stats = _manager.GetStatistics();
        Assert.Equal(4ul, stats.FramesReceived);
        Assert.Equal(4ul, stats.FramesSent);
    }

    #endregion

    #region Oldest Drop Policy

    [Fact]
    public void FifthFrame_OldestDrop_OldestReadyDropped()
    {
        // Fill and commit all 4 buffers (frames 0-3)
        for (uint i = 0; i < 4; i++)
        {
            _manager.GetBuffer(i, out _, out _);
            _manager.CommitBuffer(i);
        }

        // 5th frame (frame 4) maps to index 0 (4 % 4 = 0), which is READY
        // Oldest READY should be dropped
        int result = _manager.GetBuffer(4, out _, out _);
        Assert.Equal(0, result);

        var stats = _manager.GetStatistics();
        Assert.Equal(1ul, stats.FramesDropped);
        Assert.Equal(1ul, stats.Overruns);
    }

    [Fact]
    public void OldestDrop_FramesDropped_Incremented()
    {
        // Fill all 4 buffers
        for (uint i = 0; i < 4; i++)
        {
            _manager.GetBuffer(i, out _, out _);
            _manager.CommitBuffer(i);
        }

        // Each new frame causes a drop
        _manager.GetBuffer(4, out _, out _);
        _manager.CommitBuffer(4);

        _manager.GetBuffer(5, out _, out _);
        _manager.CommitBuffer(5);

        var stats = _manager.GetStatistics();
        Assert.Equal(2ul, stats.FramesDropped);
        Assert.Equal(2ul, stats.Overruns);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void GetReadyBuffer_NoReady_ReturnsNegative()
    {
        // No buffers committed, nothing READY
        int result = _manager.GetReadyBuffer(out byte[]? buffer, out int size, out uint frameNum);

        Assert.Equal(-1, result);
        Assert.Null(buffer);
        Assert.Equal(0, size);
        Assert.Equal(0u, frameNum);
    }

    [Fact]
    public void CommitBuffer_NotFilling_ReturnsError()
    {
        // Buffer at index 0 is FREE, not FILLING
        int result = _manager.CommitBuffer(0);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void CommitBuffer_AlreadyReady_ReturnsError()
    {
        _manager.GetBuffer(0, out _, out _);
        _manager.CommitBuffer(0);

        // Try to commit again when already READY
        int result = _manager.CommitBuffer(0);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void ReleaseBuffer_NotSending_ReturnsError()
    {
        // Buffer at index 0 is FREE, not SENDING
        int result = _manager.ReleaseBuffer(0);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void ReleaseBuffer_Filling_ReturnsError()
    {
        _manager.GetBuffer(0, out _, out _);
        // Buffer is FILLING, not SENDING
        int result = _manager.ReleaseBuffer(0);
        Assert.Equal(-1, result);
    }

    #endregion

    #region Statistics Accuracy

    [Fact]
    public void Statistics_Accuracy_ProcessTenFrames()
    {
        // Process 10 frames through a 4-buffer ring
        for (uint i = 0; i < 10; i++)
        {
            _manager.GetBuffer(i, out _, out _);
            _manager.CommitBuffer(i);

            int readyResult = _manager.GetReadyBuffer(out _, out _, out uint frameNum);
            Assert.Equal(0, readyResult);
            _manager.ReleaseBuffer(frameNum);
        }

        var stats = _manager.GetStatistics();
        Assert.Equal(10ul, stats.FramesReceived);
        Assert.Equal(10ul, stats.FramesSent);
    }

    [Fact]
    public void Statistics_GetStatistics_ReturnsClone()
    {
        _manager.GetBuffer(0, out _, out _);
        _manager.CommitBuffer(0);

        var stats1 = _manager.GetStatistics();
        var stats2 = _manager.GetStatistics();

        // Should be different instances
        Assert.NotSame(stats1, stats2);
        Assert.Equal(stats1.FramesReceived, stats2.FramesReceived);
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ClearsAllState()
    {
        // Build up some state
        for (uint i = 0; i < 4; i++)
        {
            _manager.GetBuffer(i, out _, out _);
            _manager.CommitBuffer(i);
        }

        _manager.Reset();

        // All buffers should be FREE
        for (uint i = 0; i < 4; i++)
        {
            Assert.Equal(BufferState.Free, _manager.GetBufferState(i));
        }

        // Statistics should be zeroed
        var stats = _manager.GetStatistics();
        Assert.Equal(0ul, stats.FramesReceived);
        Assert.Equal(0ul, stats.FramesSent);
        Assert.Equal(0ul, stats.FramesDropped);
        Assert.Equal(0ul, stats.Overruns);
        Assert.Equal(0ul, stats.PacketsSent);
        Assert.Equal(0ul, stats.BytesSent);
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FrameBufferManager(null!));
    }

    #endregion
}
