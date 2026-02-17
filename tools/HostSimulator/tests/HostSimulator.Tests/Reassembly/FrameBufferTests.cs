using HostSimulator.Core.Reassembly;
using Xunit;
using FluentAssertions;

namespace HostSimulator.Tests.Reassembly;

/// <summary>
/// Tests for FrameBuffer class.
/// REQ-SIM-041: Correctly reassemble frame using packet_index when packets arrive out of order.
/// REQ-SIM-042: Mark frame as incomplete and report missing packets after timeout.
/// </summary>
public class FrameBufferTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithExpectedPacketCount()
    {
        // Arrange & Act
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 10, rows: 1024, cols: 1024);

        // Assert
        buffer.FrameId.Should().Be(1);
        buffer.TotalPackets.Should().Be(10);
        buffer.Rows.Should().Be(1024);
        buffer.Cols.Should().Be(1024);
        buffer.IsComplete.Should().BeFalse();
        buffer.ReceivedPacketCount.Should().Be(0);
    }

    [Fact]
    public void AddPacket_ShouldStorePacketData()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 5, rows: 1024, cols: 1024);
        var packetData = new byte[100];

        // Act
        buffer.AddPacket(packetSeq: 0, payload: packetData);

        // Assert
        buffer.ReceivedPacketCount.Should().Be(1);
        buffer.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void AddPacket_ShouldHandleOutOfOrderPackets()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 5, rows: 1024, cols: 1024);
        var packet0 = new byte[100]; Array.Fill(packet0, (byte)1);
        var packet4 = new byte[100]; Array.Fill(packet4, (byte)4);
        var packet2 = new byte[100]; Array.Fill(packet2, (byte)7);

        // Act - Add packets out of order
        buffer.AddPacket(4, packet4);
        buffer.AddPacket(0, packet0);
        buffer.AddPacket(2, packet2);

        // Assert
        buffer.ReceivedPacketCount.Should().Be(3);
        buffer.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void AddPacket_ShouldIgnoreDuplicatePackets()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 5, rows: 1024, cols: 1024);
        var packetData = new byte[100];

        // Act
        buffer.AddPacket(0, packetData);
        buffer.AddPacket(0, packetData); // Duplicate

        // Assert
        buffer.ReceivedPacketCount.Should().Be(1); // Should not increment
    }

    [Fact]
    public void IsComplete_ShouldReturnTrue_WhenAllPacketsReceived()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 3, rows: 1024, cols: 1024);

        // Act
        buffer.AddPacket(0, new byte[100]);
        buffer.AddPacket(1, new byte[100]);
        buffer.AddPacket(2, new byte[100]);

        // Assert
        buffer.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void IsComplete_ShouldReturnFalse_WhenSomePacketsMissing()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 5, rows: 1024, cols: 1024);

        // Act - Missing packet 3
        buffer.AddPacket(0, new byte[100]);
        buffer.AddPacket(1, new byte[100]);
        buffer.AddPacket(2, new byte[100]);
        buffer.AddPacket(4, new byte[100]);

        // Assert
        buffer.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void GetMissingPackets_ShouldReturnMissingIndices()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 5, rows: 1024, cols: 1024);
        buffer.AddPacket(0, new byte[100]);
        buffer.AddPacket(1, new byte[100]);
        buffer.AddPacket(4, new byte[100]);
        // Missing: 2, 3

        // Act
        var missing = buffer.GetMissingPackets();

        // Assert
        missing.Should().BeEquivalentTo(new[] { 2, 3 });
    }

    [Fact]
    public void GetMissingPackets_ShouldReturnEmpty_WhenFrameIsComplete()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 3, rows: 1024, cols: 1024);
        buffer.AddPacket(0, new byte[100]);
        buffer.AddPacket(1, new byte[100]);
        buffer.AddPacket(2, new byte[100]);

        // Act
        var missing = buffer.GetMissingPackets();

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public void GetMissingPackets_ShouldReturnAllPackets_WhenNoneReceived()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 3, rows: 1024, cols: 1024);

        // Act
        var missing = buffer.GetMissingPackets();

        // Assert
        missing.Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }

    [Fact]
    public void HasPacket_ShouldReturnTrue_WhenPacketReceived()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 5, rows: 1024, cols: 1024);
        buffer.AddPacket(2, new byte[100]);

        // Act
        var has0 = buffer.HasPacket(0);
        var has2 = buffer.HasPacket(2);
        var has4 = buffer.HasPacket(4);

        // Assert
        has0.Should().BeFalse();
        has2.Should().BeTrue();
        has4.Should().BeFalse();
    }

    [Fact]
    public void GetPayloadSize_ShouldReturnSumOfAllPayloads()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 3, rows: 1024, cols: 1024);
        buffer.AddPacket(0, new byte[100]);
        buffer.AddPacket(1, new byte[150]);
        buffer.AddPacket(2, new byte[200]);

        // Act
        var payloadSize = buffer.GetTotalPayloadSize();

        // Assert
        payloadSize.Should().Be(450);
    }

    [Fact]
    public void AssembleFrame_ShouldReturnAllPayloadsInOrder()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 3, rows: 2, cols: 2); // 4 pixels = 8 bytes
        var packet0 = new byte[] { 1, 0, 2, 0 }; // Pixels 0, 1
        var packet2 = new byte[] { 5, 0, 6, 0 }; // Pixels 4, 5 (out of order)
        var packet1 = new byte[] { 3, 0, 4, 0 }; // Pixels 2, 3

        // Add out of order
        buffer.AddPacket(0, packet0);
        buffer.AddPacket(2, packet2);
        buffer.AddPacket(1, packet1);

        // Act
        var assembled = buffer.AssembleFrame();

        // Assert - Should be in order: packet0, packet1, packet2
        assembled.Should().Equal(1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0);
    }

    [Fact]
    public void AssembleFrame_ShouldThrow_WhenFrameIsIncomplete()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 3, rows: 2, cols: 2);
        buffer.AddPacket(0, new byte[100]);
        buffer.AddPacket(1, new byte[100]);
        // Missing packet 2

        // Act
        var act = () => buffer.AssembleFrame();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*incomplete*");
    }

    [Fact]
    public void GetAge_ShouldReturnTimeSinceCreation()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 5, rows: 1024, cols: 1024);

        // Act - Wait a bit
        Thread.Sleep(50);
        var age = buffer.GetAge();

        // Assert
        age.TotalMilliseconds.Should().BeGreaterOrEqualTo(50);
        age.TotalMilliseconds.Should().BeLessThan(200); // Should not take more than 200ms
    }

    [Fact]
    public void IsTimedOut_ShouldReturnTrue_WhenTimeoutExceeded()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 5, rows: 1024, cols: 1024);
        var timeout = TimeSpan.FromMilliseconds(50);

        // Act - Wait longer than timeout
        Thread.Sleep(75);
        var isTimedOut = buffer.IsTimedOut(timeout);

        // Assert
        isTimedOut.Should().BeTrue();
    }

    [Fact]
    public void IsTimedOut_ShouldReturnFalse_WhenTimeoutNotExceeded()
    {
        // Arrange
        var buffer = new FrameBuffer(frameId: 1, totalPackets: 5, rows: 1024, cols: 1024);
        var timeout = TimeSpan.FromMilliseconds(200);

        // Act - Wait less than timeout
        Thread.Sleep(25);
        var isTimedOut = buffer.IsTimedOut(timeout);

        // Assert
        isTimedOut.Should().BeFalse();
    }
}
