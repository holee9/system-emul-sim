// Copyright (c) 2026 ABYZ Lab. All rights reserved.

using Xunit;
using FluentAssertions;
using McuSimulator.Core.Buffer;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-15: Race Condition Characterization Tests.
/// Documents the specific race condition that was fixed in commit eaf638d.
/// This test ensures the fix remains robust under high-contention scenarios.
///
/// Root Cause: GetBuffer() could drop a FILLING buffer, causing:
/// 1. Data corruption (two producers writing to same buffer)
/// 2. CommitBuffer() failure (state mismatch: FREE instead of FILLING)
/// 3. Statistics corruption (FramesReceived not incremented)
///
/// Fix: GetBuffer() now only drops READY or SENDING buffers, never FILLING.
/// </summary>
public class IT15_RaceConditionCharacterizationTests
{
    private readonly FrameManagerConfig _config;

    public IT15_RaceConditionCharacterizationTests()
    {
        _config = new FrameManagerConfig
        {
            Rows = 16,
            Cols = 16,
            BitDepth = 16,
            NumBuffers = 4
        };
    }

    /// <summary>
    /// Characterization test: Verify FILLING buffers are never dropped.
    /// This test creates extreme contention where multiple producers compete
    /// for the same buffer slots, ensuring the oldest-drop policy never
    /// drops a buffer that is actively being filled.
    /// </summary>
    [Fact]
    public void ConcurrentProducers_NeverDropFillingBuffers()
    {
        // Arrange
        var manager = new FrameBufferManager(_config);
        const int numProducers = 4;
        const int framesPerProducer = 50;
        var exceptions = new List<Exception>();
        var commitFailures = 0;

        // Act - Multiple producers running concurrently
        var producerTasks = new Task[numProducers];
        for (int p = 0; p < numProducers; p++)
        {
            int producerId = p;
            producerTasks[p] = Task.Run(() =>
            {
                try
                {
                    uint frameStart = (uint)producerId * 1000; // Non-overlapping frame numbers
                    for (uint f = 0; f < framesPerProducer; f++)
                    {
                        uint frame = frameStart + f;

                        // Get buffer for this frame
                        int getResult = manager.GetBuffer(frame, out _, out _);

                        // GetBuffer should always succeed
                        if (getResult != 0)
                        {
                            Interlocked.Increment(ref commitFailures);
                            continue;
                        }

                        // Simulate filling time (random delay to increase race probability)
                        Thread.Sleep(Random.Shared.Next(0, 2));

                        // Commit the buffer
                        int commitResult = manager.CommitBuffer(frame);

                        // CommitBuffer should never fail
                        // (failure would indicate buffer was dropped while FILLING)
                        if (commitResult != 0)
                        {
                            Interlocked.Increment(ref commitFailures);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            });
        }

        Task.WaitAll(producerTasks);

        // Assert - No exceptions should occur
        exceptions.Should().BeEmpty("concurrent producers should be thread-safe");

        // Assert - No commit failures (buffer never dropped while FILLING)
        commitFailures.Should().Be(0,
            "CommitBuffer should never fail - buffers should never be dropped while in FILLING state");

        // Assert - All frames should be received
        var stats = manager.GetStatistics();
        stats.FramesReceived.Should().Be((uint)(numProducers * framesPerProducer),
            $"all {numProducers * framesPerProducer} frames should be received and committed");
    }

    /// <summary>
    /// Characterization test: Verify oldest-drop policy only drops READY buffers first.
    /// When no READY buffers exist, it may drop SENDING buffers, but never FILLING.
    /// </summary>
    [Fact]
    public void OldestDrop_PreferReadyOverSending()
    {
        // Arrange
        var manager = new FrameBufferManager(_config);

        // Fill all 4 buffers and commit to READY
        for (uint frame = 0; frame < 4; frame++)
        {
            manager.GetBuffer(frame, out _, out _);
            manager.CommitBuffer(frame);
        }

        // Start sending buffer 0 (transition to SENDING)
        manager.GetReadyBuffer(out _, out _, out _);

        // Verify states: buffer 0 = SENDING, buffers 1-3 = READY
        manager.GetBufferState(0).Should().Be(BufferState.Sending);
        for (uint frame = 1; frame < 4; frame++)
        {
            manager.GetBufferState(frame).Should().Be(BufferState.Ready);
        }

        // Act - Request 5th buffer (should drop oldest READY, not SENDING)
        var result = manager.GetBuffer(4, out _, out _);

        // Assert - Should succeed
        result.Should().Be(0, "GetBuffer should succeed by dropping oldest READY buffer");

        // Verify buffer 0 is still SENDING (not dropped)
        manager.GetBufferState(0).Should().Be(BufferState.Sending,
            "SENDING buffer should not be dropped when READY buffers are available");

        // Verify one of the READY buffers (1, 2, or 3) was dropped
        var readyCount = 0;
        for (uint frame = 1; frame < 4; frame++)
        {
            if (manager.GetBufferState(frame) == BufferState.Ready)
                readyCount++;
        }
        readyCount.Should().Be(2, "one READY buffer should have been dropped (3 - 1 = 2)");
    }

    /// <summary>
    /// Characterization test: Verify GetBuffer/Commit consistency under stress.
    /// This test runs many iterations to catch any remaining race conditions.
    /// </summary>
    [Fact]
    public void StressTest_1000Frames_NoCommitFailures()
    {
        // Arrange
        var manager = new FrameBufferManager(_config);
        const int totalFrames = 1000;
        var exceptions = new List<Exception>();
        var commitFailures = 0;
        var successfulCommits = 0;

        // Act - Producer and consumer running concurrently
        var producerTask = Task.Run(() =>
        {
            try
            {
                for (uint frame = 0; frame < totalFrames; frame++)
                {
                    if (manager.GetBuffer(frame, out _, out _) == 0)
                    {
                        // Small random delay to increase interleaving
                        if (frame % 10 == 0)
                            Thread.Sleep(1);

                        if (manager.CommitBuffer(frame) == 0)
                        {
                            Interlocked.Increment(ref successfulCommits);
                        }
                        else
                        {
                            Interlocked.Increment(ref commitFailures);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        });

        // Consumer
        var consumerTask = Task.Run(() =>
        {
            try
            {
                int consecutiveEmptyChecks = 0;
                const int maxEmptyChecks = 50;

                while (consecutiveEmptyChecks < maxEmptyChecks)
                {
                    var result = manager.GetReadyBuffer(out _, out _, out var frameNum);
                    if (result == 0)
                    {
                        manager.ReleaseBuffer(frameNum);
                        consecutiveEmptyChecks = 0;
                    }
                    else
                    {
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

        // Assert - No exceptions
        exceptions.Should().BeEmpty();

        // Assert - No commit failures (this is the key invariant)
        commitFailures.Should().Be(0,
            "CommitBuffer should never fail - race condition should be fixed");

        // Assert - Most frames should be committed
        successfulCommits.Should().BeGreaterThan((int)(totalFrames * 0.9),
            "at least 90% of frames should be successfully committed");
    }
}
