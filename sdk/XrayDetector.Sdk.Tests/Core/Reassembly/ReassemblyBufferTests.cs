using System.Buffers;
using XrayDetector.Core.Reassembly;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Reassembly;

/// <summary>
/// Specification tests for ReassemblyBuffer.
/// Circular buffer for sorting and storing out-of-order packets.
/// </summary>
public class ReassemblyBufferTests : IDisposable
{
    private readonly ArrayPool<ushort> _pool;

    public ReassemblyBufferTests()
    {
        _pool = ArrayPool<ushort>.Shared;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Create_WithValidParameters_CreatesBuffer()
    {
        // Arrange
        const uint expectedPackets = 10;
        const int pixelsPerPacket = 100;

        // Act
        var buffer = ReassemblyBuffer.Create(1u, expectedPackets, pixelsPerPacket, _pool);

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal<uint>(1u, buffer.FrameNumber);
        Assert.Equal(expectedPackets, buffer.ExpectedPackets);
        Assert.Equal(pixelsPerPacket, buffer.PixelsPerPacket);
    }

    [Fact]
    public void Create_WithZeroFrameNumber_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReassemblyBuffer.Create(0u, 10u, 100, _pool));
    }

    [Fact]
    public void Create_WithZeroExpectedPackets_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReassemblyBuffer.Create(1u, 0u, 100, _pool));
    }

    [Fact]
    public void Create_WithZeroPixelsPerPacket_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReassemblyBuffer.Create(1u, 10u, 0, _pool));
    }

    [Fact]
    public void AddPacket_WithFirstPacket_InsertsSuccessfully()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels = new ushort[10];

        // Act
        bool success = buffer.AddPacket(0u, pixels);

        // Assert
        Assert.True(success);
        Assert.Equal<uint>(1u, buffer.ReceivedPackets);
        Assert.True(buffer.HasPacket(0u));
    }

    [Fact]
    public void AddPacket_WithLastPacket_InsertsSuccessfully()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels = new ushort[10];

        // Act
        bool success = buffer.AddPacket(2u, pixels); // Last packet (0-indexed: 2 of 3)

        // Assert
        Assert.True(success);
        Assert.Equal<uint>(1u, buffer.ReceivedPackets);
        Assert.True(buffer.HasPacket(2u));
    }

    [Fact]
    public void AddPacket_WithOutOfRangePacketNumber_ReturnsFalse()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels = new ushort[10];

        // Act
        bool success = buffer.AddPacket(5u, pixels); // Out of range

        // Assert
        Assert.False(success);
        Assert.Equal<uint>(0u, buffer.ReceivedPackets);
    }

    [Fact]
    public void AddPacket_WithDuplicatePacketNumber_ReturnsFalse()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels1 = new ushort[10];
        ushort[] pixels2 = new ushort[10];
        buffer.AddPacket(0u, pixels1);

        // Act
        bool success = buffer.AddPacket(0u, pixels2); // Duplicate

        // Assert
        Assert.False(success);
        Assert.Equal<uint>(1u, buffer.ReceivedPackets);
    }

    [Fact]
    public void AddPacket_OutOfOrder_InsertsCorrectly()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] packet0 = new ushort[10];
        ushort[] packet2 = new ushort[10];
        ushort[] packet1 = new ushort[10];

        // Add out of order: 2, 0, 1
        buffer.AddPacket(2u, packet2);
        buffer.AddPacket(0u, packet0);
        buffer.AddPacket(1u, packet1);

        // Assert
        Assert.Equal<uint>(3u, buffer.ReceivedPackets);
        Assert.True(buffer.HasPacket(0u));
        Assert.True(buffer.HasPacket(1u));
        Assert.True(buffer.HasPacket(2u));
    }

    [Fact]
    public void HasPacket_WithMissingPacket_ReturnsFalse()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels = new ushort[10];
        buffer.AddPacket(0u, pixels);
        buffer.AddPacket(2u, pixels);

        // Act
        bool hasPacket1 = buffer.HasPacket(1u);

        // Assert
        Assert.False(hasPacket1);
    }

    [Fact]
    public void IsComplete_WithAllPackets_ReturnsTrue()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels = new ushort[10];
        buffer.AddPacket(0u, pixels);
        buffer.AddPacket(1u, pixels);
        buffer.AddPacket(2u, pixels);

        // Act
        bool isComplete = buffer.IsComplete;

        // Assert
        Assert.True(isComplete);
    }

    [Fact]
    public void IsComplete_WithMissingPacket_ReturnsFalse()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels = new ushort[10];
        buffer.AddPacket(0u, pixels);
        buffer.AddPacket(2u, pixels); // Missing packet 1

        // Act
        bool isComplete = buffer.IsComplete;

        // Assert
        Assert.False(isComplete);
    }

    [Fact]
    public void GetMissingPacketIndices_WithCompleteFrame_ReturnsEmpty()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels = new ushort[10];
        buffer.AddPacket(0u, pixels);
        buffer.AddPacket(1u, pixels);
        buffer.AddPacket(2u, pixels);

        // Act
        var missing = buffer.GetMissingPacketIndices();

        // Assert
        Assert.Empty(missing);
    }

    [Fact]
    public void GetMissingPacketIndices_WithMissingPackets_ReturnsIndices()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 5u, 10, _pool);
        ushort[] pixels = new ushort[10];
        buffer.AddPacket(0u, pixels);
        buffer.AddPacket(2u, pixels); // Missing 1, 3, 4

        // Act
        var missing = buffer.GetMissingPacketIndices();

        // Assert
        Assert.Equal(3, missing.Count);
        Assert.Contains(1u, missing);
        Assert.Contains(3u, missing);
        Assert.Contains(4u, missing);
    }

    [Fact]
    public void GetMissingPacketIndices_WithNoPackets_ReturnsAllIndices()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);

        // Act
        var missing = buffer.GetMissingPacketIndices();

        // Assert
        Assert.Equal(3, missing.Count);
        Assert.Contains(0u, missing);
        Assert.Contains(1u, missing);
        Assert.Contains(2u, missing);
    }

    [Fact]
    public void FillMissingPackets_WithMissingPackets_FillsWithZeros()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels = new ushort[10];
        Array.Fill(pixels, (ushort)0xFFFF);
        buffer.AddPacket(0u, pixels);
        buffer.AddPacket(2u, pixels); // Missing packet 1

        // Act
        buffer.FillMissingPackets();

        // Assert
        Assert.True(buffer.IsComplete);
        Assert.True(buffer.HasPacket(1u));
    }

    [Fact]
    public void AssembleFrame_WithCompleteFrame_ReturnsAllPixels()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] packet0 = CreateTestPacket(0, 10);
        ushort[] packet1 = CreateTestPacket(1, 10);
        ushort[] packet2 = CreateTestPacket(2, 10);
        buffer.AddPacket(0u, packet0);
        buffer.AddPacket(1u, packet1);
        buffer.AddPacket(2u, packet2);

        // Act
        ushort[] frame = buffer.AssembleFrame();

        // Assert
        Assert.Equal(30, frame.Length);
        Assert.Equal(0, frame[0]);   // Packet 0, pixel 0
        Assert.Equal(9, frame[9]);   // Packet 0, pixel 9
        Assert.Equal(10, frame[10]); // Packet 1, pixel 0
        Assert.Equal(19, frame[19]); // Packet 1, pixel 9
        Assert.Equal(20, frame[20]); // Packet 2, pixel 0
        Assert.Equal(29, frame[29]); // Packet 2, pixel 9
    }

    [Fact]
    public void AssembleFrame_WithIncompleteFrame_ReturnsPartialFrame()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] packet0 = CreateTestPacket(0, 10);
        ushort[] packet1 = CreateTestPacket(1, 10);
        buffer.AddPacket(0u, packet0);
        buffer.AddPacket(1u, packet1); // Missing packet 2

        // Act
        ushort[] frame = buffer.AssembleFrame();

        // Assert
        Assert.Equal(20, frame.Length); // Only 20 pixels from 2 packets
        Assert.Equal(0, frame[0]);
        Assert.Equal(19, frame[19]);
    }

    [Fact]
    public void AssembleFrame_WithFilledMissingPackets_ReturnsCompleteFrame()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] packet0 = CreateTestPacket(0, 10);
        ushort[] packet1 = CreateTestPacket(1, 10);
        buffer.AddPacket(0u, packet0);
        buffer.AddPacket(1u, packet1); // Missing packet 2
        buffer.FillMissingPackets();

        // Act
        ushort[] frame = buffer.AssembleFrame();

        // Assert
        Assert.Equal(30, frame.Length);
        Assert.Equal(0, frame[0]);
        Assert.Equal(0, frame[20]); // Filled with zeros
        Assert.Equal(0, frame[29]);
    }

    [Fact]
    public void Age_WhenCreated_IsZero()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);

        // Act
        TimeSpan age = buffer.Age;

        // Assert
        Assert.True(age < TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Dispose_ReturnsBuffersToPool()
    {
        // Arrange
        var buffer = ReassemblyBuffer.Create(1u, 3u, 10, _pool);
        ushort[] pixels = new ushort[10];
        buffer.AddPacket(0u, pixels);

        // Act - Should not throw
        buffer.Dispose();

        // Assert - No assertion, just verify no exception thrown
    }

    // Helper method to create test packet with predictable values
    private static ushort[] CreateTestPacket(int packetNumber, int size)
    {
        ushort[] packet = new ushort[size];
        for (int i = 0; i < size; i++)
        {
            packet[i] = (ushort)(packetNumber * size + i);
        }
        return packet;
    }
}
