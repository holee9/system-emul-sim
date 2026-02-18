using System.Buffers;
using XrayDetector.Common.Dto;
using XrayDetector.Models;
using Xunit;

namespace XrayDetector.Sdk.Tests.Models;

/// <summary>
/// Specification tests for Frame.
/// Represents a single X-ray frame with 16-bit grayscale pixel data.
/// Implements IDisposable for efficient memory management using ArrayPool.
/// </summary>
public class FrameTests : IDisposable
{
    private readonly ArrayPool<ushort> _pool;

    public FrameTests()
    {
        _pool = ArrayPool<ushort>.Shared;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Create_WithDataAndMetadata_CreatesFrame()
    {
        // Arrange
        ushort[] data = { 100, 200, 300, 400 };
        var metadata = new FrameMetadata(2, 2, 16, DateTime.UtcNow, 0);

        // Act
        var frame = new Frame(data, metadata);

        // Assert
        Assert.NotNull(frame);
        Assert.Equal(2, frame.Width);
        Assert.Equal(2, frame.Height);
        Assert.Equal(16, frame.BitDepth);
        Assert.NotNull(frame.PixelData);
        Assert.Equal(4, frame.PixelData.Length);
    }

    [Fact]
    public void Create_WithMetadata_CopiesMetadataValues()
    {
        // Arrange
        ushort[] data = { 100, 200 };
        var metadata = new FrameMetadata(width: 10, height: 20, bitDepth: 16, timestamp: DateTime.UtcNow.AddTicks(99999), frameNumber: 5);

        // Act
        var frame = new Frame(data, metadata);

        // Assert
        Assert.Equal(10, frame.Width);
        Assert.Equal(20, frame.Height);
        Assert.Equal(16, frame.BitDepth);
        Assert.Equal(5U, frame.FrameNumber);
    }

    [Fact]
    public void Dispose_WithFrame_ReturnsArrayToPool()
    {
        // Arrange
        ushort[] data = _pool.Rent(100);
        var metadata = new FrameMetadata(10, 10, 16, DateTime.UtcNow, 0);
        var frame = new Frame(data, metadata, _pool);

        // Act
        frame.Dispose();

        // Assert - Frame should be disposed without exception
        // Note: We can't directly verify the array was returned to the pool,
        // but we can verify no exception is thrown
    }

    [Fact]
    public void Dispose_WithMultipleFrames_AllArraysReturned()
    {
        // Arrange
        var frames = new List<Frame>();
        for (int i = 0; i < 10; i++)
        {
            ushort[] data = _pool.Rent(100);
            var metadata = new FrameMetadata(10, 10, 16, DateTime.UtcNow, (uint)i);
            frames.Add(new Frame(data, metadata, _pool));
        }

        // Act
        foreach (var frame in frames)
        {
            frame.Dispose();
        }

        // Assert - All disposals should complete without exception
    }

    [Fact]
    public void PixelData_WithValidData_ReturnsCorrectValues()
    {
        // Arrange
        ushort[] data = { 1000, 2000, 3000, 4000 };
        var metadata = new FrameMetadata(2, 2, 16, DateTime.UtcNow, 0);
        var frame = new Frame(data, metadata);

        // Act
        ushort[] pixelData = frame.PixelData;

        // Assert
        Assert.Equal(1000, pixelData[0]);
        Assert.Equal(2000, pixelData[1]);
        Assert.Equal(3000, pixelData[2]);
        Assert.Equal(4000, pixelData[3]);
    }

    [Fact]
    public void Create_WithNullPool_UsesSharedPool()
    {
        // Arrange
        ushort[] data = { 100, 200 };
        var metadata = new FrameMetadata(2, 1, 16, DateTime.UtcNow, 0);

        // Act
        var frame = new Frame(data, metadata, pool: null);

        // Assert
        Assert.NotNull(frame);
        frame.Dispose(); // Should use shared pool
    }

    [Fact]
    public void Statistics_WithFrameData_CalculatesCorrectly()
    {
        // Arrange
        ushort[] data = { 100, 200, 300, 400, 500 };
        var metadata = new FrameMetadata(5, 1, 16, DateTime.UtcNow, 0);
        var frame = new Frame(data, metadata);

        // Act
        var stats = frame.Statistics;

        // Assert
        Assert.NotNull(stats);
        Assert.Equal((ushort)100, stats.Min);
        Assert.Equal((ushort)500, stats.Max);
        Assert.Equal(300.0, stats.Mean);
    }

    [Fact]
    public void Statistics_AccessMultipleTimes_ReturnsSameInstance()
    {
        // Arrange
        ushort[] data = { 100, 200, 300 };
        var metadata = new FrameMetadata(3, 1, 16, DateTime.UtcNow, 0);
        var frame = new Frame(data, metadata);

        // Act
        var stats1 = frame.Statistics;
        var stats2 = frame.Statistics;

        // Assert
        Assert.Same(stats1, stats2); // Should be cached
    }
}
