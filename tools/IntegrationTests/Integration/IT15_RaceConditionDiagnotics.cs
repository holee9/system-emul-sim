// Copyright (c) 2026 ABYZ Lab. All rights reserved.

using Xunit;
using FluentAssertions;
using McuSimulator.Core.Buffer;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-15: Diagnostic test to understand the race condition.
/// </summary>
public class IT15_RaceConditionDiagnostics
{
    private readonly FrameManagerConfig _config;

    public IT15_RaceConditionDiagnostics()
    {
        _config = new FrameManagerConfig
        {
            Rows = 16,
            Cols = 16,
            BitDepth = 16,
            NumBuffers = 4
        };
    }

    [Fact]
    public void SimpleTest_TwoProducers_SameFrameMod()
    {
        // Arrange
        var manager = new FrameBufferManager(_config);

        // Act - Frame 100 and 104 both map to index 0
        var result1 = manager.GetBuffer(100, out _, out _);
        var result2 = manager.GetBuffer(104, out _, out _);

        // Assert
        result1.Should().Be(0);
        result2.Should().Be(0);

        // Commit both
        var commit1 = manager.CommitBuffer(100);
        var commit2 = manager.CommitBuffer(104);

        commit1.Should().Be(0, "frame 100 should commit successfully");
        commit2.Should().Be(0, "frame 104 should commit successfully");

        var stats = manager.GetStatistics();
        stats.FramesReceived.Should().Be(2, "both frames should be received");
    }

    [Fact]
    public void Test_GetBuffer_DropsCorrectBuffer()
    {
        // Arrange
        var manager = new FrameBufferManager(_config);

        // Fill all buffers with frames 0-3
        for (uint i = 0; i < 4; i++)
        {
            manager.GetBuffer(i, out _, out _);
            manager.CommitBuffer(i);
        }

        // All buffers should be READY
        manager.GetBufferState(0).Should().Be(BufferState.Ready);
        manager.GetBufferState(1).Should().Be(BufferState.Ready);
        manager.GetBufferState(2).Should().Be(BufferState.Ready);
        manager.GetBufferState(3).Should().Be(BufferState.Ready);

        // Act - Get frame 4 (will drop oldest READY buffer and use it)
        var result = manager.GetBuffer(4, out _, out _);

        // Assert - Should succeed and drop one frame
        result.Should().Be(0);

        // Frame 0 should have been dropped (state is Free for non-existent frames)
        manager.GetBufferState(0).Should().Be(BufferState.Free,
            "frame 0 should be dropped and no longer exists");

        // Frame 4 should be in FILLING state
        manager.GetBufferState(4).Should().Be(BufferState.Filling,
            "frame 4 should be in FILLING state");

        var stats = manager.GetStatistics();
        stats.FramesDropped.Should().Be(1, "one frame should be dropped");
    }

    [Fact]
    public void Test_CommitBuffer_FindsCorrectBuffer()
    {
        // Arrange
        var manager = new FrameBufferManager(_config);

        // Fill all buffers and commit to READY
        for (uint i = 0; i < 4; i++)
        {
            manager.GetBuffer(i, out _, out _);
            manager.CommitBuffer(i);
        }

        // Get frame 4 (will drop frame 0 and use slot 0)
        manager.GetBuffer(4, out _, out _);

        // Verify frame 4 is in FILLING state
        manager.GetBufferState(4).Should().Be(BufferState.Filling);

        // Act - Commit frame 4
        var result = manager.CommitBuffer(4);

        // Assert - Should succeed
        result.Should().Be(0, "CommitBuffer should find frame 4 even though it's at index 0");

        // Frame 4 should be READY
        manager.GetBufferState(4).Should().Be(BufferState.Ready);
    }
}
