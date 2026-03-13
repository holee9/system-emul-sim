using Xunit;
using FluentAssertions;
using McuSimulator.Core.Buffer;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-15: MCU FrameBufferManager Overflow Handling Tests.
/// Validates the 4-buffer ring with oldest-drop policy, concurrent access,
/// and statistics tracking.
/// Reference: SPEC-EMUL-001, REQ-FW-050~052
/// </summary>
public class IT15_FrameBufferOverflowTests
{
    private readonly FrameManagerConfig _config;

    public IT15_FrameBufferOverflowTests()
    {
        // Use small frame size for fast tests
        _config = new FrameManagerConfig
        {
            Rows = 16,
            Cols = 16,
            BitDepth = 16,
            NumBuffers = 4
        };
    }

    [Fact]
    public void NormalOperation_4Buffers_NoOverflow()
    {
        // Arrange
        var manager = new FrameBufferManager(_config);

        // Act - Get 4 buffers, commit, read, and release each one
        for (uint frame = 0; frame < 4; frame++)
        {
            // Producer: get and fill buffer
            var result = manager.GetBuffer(frame, out var buffer, out var size);
            result.Should().Be(0, $"GetBuffer for frame {frame} should succeed");
            buffer.Should().NotBeNull();
            size.Should().Be(_config.FrameSize);

            // Producer: commit buffer
            manager.CommitBuffer(frame).Should().Be(0,
                $"CommitBuffer for frame {frame} should succeed");
        }

        // Consumer: read and release all buffers
        for (uint frame = 0; frame < 4; frame++)
        {
            var readResult = manager.GetReadyBuffer(out var readBuffer, out var readSize, out var readFrame);
            readResult.Should().Be(0, $"GetReadyBuffer should find frame {frame}");
            readBuffer.Should().NotBeNull();

            manager.ReleaseBuffer(readFrame).Should().Be(0,
                $"ReleaseBuffer for frame {readFrame} should succeed");
        }

        // Assert - No overruns should have occurred
        var stats = manager.GetStatistics();
        stats.FramesReceived.Should().Be(4);
        stats.FramesSent.Should().Be(4);
        stats.FramesDropped.Should().Be(0, "no overflow should occur with 4 frames in 4 buffers");
        stats.Overruns.Should().Be(0);
    }

    [Fact]
    public void Overflow_AllBuffersFull_DropsOldest()
    {
        // Arrange - Fill all 4 buffers and commit them to READY state
        var manager = new FrameBufferManager(_config);

        for (uint frame = 0; frame < 4; frame++)
        {
            manager.GetBuffer(frame, out _, out _);
            manager.CommitBuffer(frame);
        }

        // Verify all 4 buffers are READY
        for (uint frame = 0; frame < 4; frame++)
        {
            manager.GetBufferState(frame).Should().Be(BufferState.Ready,
                $"buffer {frame} should be READY");
        }

        // Act - Request 5th buffer (should drop oldest READY)
        var result = manager.GetBuffer(4, out var buffer, out var size);

        // Assert - Should succeed by dropping oldest READY buffer
        result.Should().Be(0, "GetBuffer should succeed by dropping oldest READY buffer");
        buffer.Should().NotBeNull();
        size.Should().Be(_config.FrameSize);

        // Verify statistics reflect the drop
        var stats = manager.GetStatistics();
        stats.FramesDropped.Should().Be(1, "one frame should have been dropped");
        stats.Overruns.Should().Be(1, "one overrun should be recorded");
    }

    [Fact]
    public void ProducerConsumer_Concurrent_ThreadSafe()
    {
        // Arrange
        var manager = new FrameBufferManager(_config);
        const int totalFrames = 100;
        var exceptions = new List<Exception>();
        var framesProcessed = 0;

        // Act - Producer runs independently
        var producerTask = Task.Run(() =>
        {
            try
            {
                for (uint frame = 0; frame < totalFrames; frame++)
                {
                    manager.GetBuffer(frame, out _, out _);
                    manager.CommitBuffer(frame);
                    // Small delay to allow consumer to interleave
                    Thread.Yield();
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        });

        // Consumer runs concurrently and consumes all available buffers
        var consumerTask = Task.Run(() =>
        {
            try
            {
                int consecutiveEmptyChecks = 0;
                const int maxEmptyChecks = 10; // Prevent infinite loop

                while (consecutiveEmptyChecks < maxEmptyChecks)
                {
                    var result = manager.GetReadyBuffer(out var buffer, out var size, out var frameNum);
                    if (result == 0 && buffer != null)
                    {
                        manager.ReleaseBuffer(frameNum);
                        framesProcessed++;
                        consecutiveEmptyChecks = 0; // Reset - got a frame
                    }
                    else
                    {
                        // No ready buffers, wait a bit and check again
                        consecutiveEmptyChecks++;
                        Thread.Sleep(1);
                    }
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        });

        Task.WaitAll(producerTask, consumerTask);

        // Assert - No exceptions should occur (thread safety)
        exceptions.Should().BeEmpty("concurrent operations should be thread-safe");

        // Verify basic invariants
        var stats = manager.GetStatistics();
        stats.FramesReceived.Should().Be((uint)totalFrames,
            $"all {totalFrames} frames should be received");
        stats.FramesSent.Should().Be((uint)framesProcessed,
            $"{framesProcessed} frames should be sent (matching framesProcessed)");
    }

    [Fact]
    public void Statistics_TracksReceivedSentDropped()
    {
        // Arrange
        var manager = new FrameBufferManager(_config);

        // Act - Process frames with intentional overflows
        // Fill 4 buffers
        for (uint frame = 0; frame < 4; frame++)
        {
            manager.GetBuffer(frame, out _, out _);
            manager.CommitBuffer(frame);
        }

        // Read and release 2 buffers (consume frames 0 and 1)
        for (int i = 0; i < 2; i++)
        {
            manager.GetReadyBuffer(out _, out _, out var frameNum);
            manager.ReleaseBuffer(frameNum);
        }

        // Add 3 more frames (causing 1 overflow since 2 buffers still READY)
        for (uint frame = 4; frame < 7; frame++)
        {
            manager.GetBuffer(frame, out _, out _);
            manager.CommitBuffer(frame);
        }

        // Assert - Verify all statistics counters
        var stats = manager.GetStatistics();
        stats.FramesReceived.Should().Be(7,
            "7 frames were committed");
        stats.FramesSent.Should().Be(2,
            "2 frames were consumed (released)");
        stats.FramesDropped.Should().BeGreaterThan(0,
            "overflow should have caused drops");
        stats.Overruns.Should().Be(stats.FramesDropped,
            "overruns should equal dropped frames");
    }
}
