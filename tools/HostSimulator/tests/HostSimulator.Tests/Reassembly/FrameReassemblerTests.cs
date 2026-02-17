using HostSimulator.Core.Reassembly;
using Xunit;
using FluentAssertions;
using Common.Dto.Dtos;

namespace HostSimulator.Tests.Reassembly;

/// <summary>
/// Tests for FrameReassembler class.
/// REQ-SIM-040: Receive UDP packets and reassemble complete frames.
/// REQ-SIM-041: Correctly reassemble frame using packet_index when packets arrive out of order.
/// REQ-SIM-042: Mark frame as incomplete and report missing packets after timeout.
/// AC-SIM-007: Frame reassembly from in-order packets.
/// AC-SIM-008: Frame reassembly from out-of-order packets.
/// </summary>
public class FrameReassemblerTests
{
    [Fact]
    public void ProcessPacket_ShouldCreateNewFrameBuffer_WhenNewFrameArrives()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromSeconds(1));
        var header = CreateFrameHeader(frameId: 1, packetSeq: 0, totalPackets: 5);
        var payload = new byte[100];

        // Act
        var result = reassembler.ProcessPacket(header, payload);

        // Assert
        result.Status.Should().Be(FrameReassemblyStatus.Pending);
        result.Frame.Should().BeNull();
    }

    [Fact]
    public void ProcessPacket_ShouldReturnCompleteFrame_WhenAllPacketsReceived()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromSeconds(1));
        var totalPackets = 3;

        // Act - Add all packets
        FrameReassemblyResult? result = null;
        for (ushort i = 0; i < totalPackets; i++)
        {
            var header = CreateFrameHeader(frameId: 1, packetSeq: i, totalPackets: (ushort)totalPackets);
            var payload = CreatePixelPayload(value: (ushort)(i * 100));
            result = reassembler.ProcessPacket(header, payload);
        }

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(FrameReassemblyStatus.Complete);
        result.Frame.Should().NotBeNull();
        result.Frame!.FrameNumber.Should().Be(1);
        result.Frame.Width.Should().Be(1024);
        result.Frame.Height.Should().Be(1024);
    }

    [Fact]
    public void ProcessPacket_ShouldHandleOutOfOrderPackets()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromSeconds(1));
        var totalPackets = 5;

        // Act - Add packets out of order
        FrameReassemblyResult? result = null;
        var order = new[] { 4, 0, 2, 1, 3 }; // Out of order
        foreach (var seq in order)
        {
            var header = CreateFrameHeader(frameId: 1, packetSeq: (ushort)seq, totalPackets: (ushort)totalPackets);
            var payload = CreatePixelPayload(value: (ushort)(seq * 100));
            result = reassembler.ProcessPacket(header, payload);
        }

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(FrameReassemblyStatus.Complete);
        result.Frame.Should().NotBeNull();
    }

    [Fact]
    public void ProcessPacket_ShouldReturnIncomplete_WhenTimeoutOccurs()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromMilliseconds(50));
        var header = CreateFrameHeader(frameId: 1, packetSeq: 0, totalPackets: 5);
        var payload = new byte[100];

        // Act
        reassembler.ProcessPacket(header, payload);
        Thread.Sleep(75); // Wait for timeout
        var result = reassembler.CheckTimeouts();

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(FrameReassemblyStatus.Incomplete);
        result.MissingPackets.Should().NotBeEmpty();
        result.MissingPackets.Should().ContainInOrder(new ushort[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void ProcessPacket_ShouldHandleMultipleFramesConcurrently()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromSeconds(1));

        // Act - Interleave packets from two frames
        var headers = new[]
        {
            CreateFrameHeader(frameId: 1, packetSeq: 0, totalPackets: 3),
            CreateFrameHeader(frameId: 2, packetSeq: 0, totalPackets: 3),
            CreateFrameHeader(frameId: 1, packetSeq: 1, totalPackets: 3),
            CreateFrameHeader(frameId: 2, packetSeq: 1, totalPackets: 3),
            CreateFrameHeader(frameId: 1, packetSeq: 2, totalPackets: 3),
            CreateFrameHeader(frameId: 2, packetSeq: 2, totalPackets: 3),
        };

        FrameReassemblyResult? frame1Result = null;
        FrameReassemblyResult? frame2Result = null;

        foreach (var header in headers)
        {
            var payload = CreatePixelPayload(value: (ushort)(header.FrameId * 1000 + header.PacketSeq * 100));
            var result = reassembler.ProcessPacket(header, payload);

            if (result != null && result.Status == FrameReassemblyStatus.Complete)
            {
                if (result.Frame!.FrameNumber == 1)
                    frame1Result = result;
                else if (result.Frame.FrameNumber == 2)
                    frame2Result = result;
            }
        }

        // Assert
        frame1Result.Should().NotBeNull();
        frame1Result!.Status.Should().Be(FrameReassemblyStatus.Complete);
        frame1Result.Frame!.FrameNumber.Should().Be(1);

        frame2Result.Should().NotBeNull();
        frame2Result!.Status.Should().Be(FrameReassemblyStatus.Complete);
        frame2Result.Frame!.FrameNumber.Should().Be(2);
    }

    [Fact]
    public void ProcessPacket_ShouldIgnoreDuplicatePackets()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromSeconds(1));
        var header = CreateFrameHeader(frameId: 1, packetSeq: 0, totalPackets: 3);
        var payload = CreatePixelPayload(value: 100);

        // Act - Send same packet twice
        var result1 = reassembler.ProcessPacket(header, payload);
        var result2 = reassembler.ProcessPacket(header, payload);

        // Assert - First call should return Pending, second should return null (duplicate ignored)
        result1!.Status.Should().Be(FrameReassemblyStatus.Pending);
        result2.Should().BeNull("Duplicate packets should be ignored and return null");
    }

    [Fact]
    public void CheckTimeouts_ShouldReturnNull_WhenNoFramesAreTimedOut()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromSeconds(10));
        var header = CreateFrameHeader(frameId: 1, packetSeq: 0, totalPackets: 3);
        var payload = new byte[100];

        reassembler.ProcessPacket(header, payload);

        // Act
        var result = reassembler.CheckTimeouts();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CheckTimeouts_ShouldRemoveTimedOutFrame_WhenTimeoutOccurs()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromMilliseconds(50));
        var header = CreateFrameHeader(frameId: 1, packetSeq: 0, totalPackets: 3);
        var payload = new byte[100];

        reassembler.ProcessPacket(header, payload);
        Thread.Sleep(75); // Wait for timeout

        // Act - First call returns incomplete frame
        var result1 = reassembler.CheckTimeouts();
        result1.Should().NotBeNull();
        result1!.Status.Should().Be(FrameReassemblyStatus.Incomplete);

        // Second call should return null (frame already removed)
        var result2 = reassembler.CheckTimeouts();
        result2.Should().BeNull();
    }

    [Fact]
    public void Reset_ShouldClearAllPendingFrames()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromSeconds(1));
        var header = CreateFrameHeader(frameId: 1, packetSeq: 0, totalPackets: 3);
        var payload = new byte[100];

        reassembler.ProcessPacket(header, payload);

        // Act
        reassembler.Reset();

        // Assert - Timeout check should return null (no frames pending)
        Thread.Sleep(10);
        var result = reassembler.CheckTimeouts();
        result.Should().BeNull("Reset should clear all pending frames");
    }

    [Fact]
    public void GetPendingFrameCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromSeconds(1));

        // Act - Start two frames
        reassembler.ProcessPacket(CreateFrameHeader(frameId: 1, packetSeq: 0, totalPackets: 3), new byte[100]);
        reassembler.ProcessPacket(CreateFrameHeader(frameId: 2, packetSeq: 0, totalPackets: 3), new byte[100]);

        // Assert
        reassembler.GetPendingFrameCount().Should().Be(2);
    }

    [Fact]
    public void AssembleFrame_ShouldConvertPixelsToUshortArray()
    {
        // Arrange
        var reassembler = new FrameReassembler(timeout: TimeSpan.FromSeconds(1));
        var totalPackets = 1;

        // Create a small 2x2 frame (4 pixels)
        var header = CreateFrameHeader(frameId: 1, packetSeq: 0, totalPackets: (ushort)totalPackets, rows: 2, cols: 2);
        var payload = new byte[] { 1, 0, 2, 0, 3, 0, 4, 0 }; // Pixels: 1, 2, 3, 4 (little-endian)

        // Act
        var result = reassembler.ProcessPacket(header, payload);

        // Assert
        result!.Status.Should().Be(FrameReassemblyStatus.Complete);
        result.Frame.Should().NotBeNull();
        result.Frame!.Pixels.Should().Equal(new ushort[] { 1, 2, 3, 4 });
    }

    /// <summary>
    /// Creates a test frame header.
    /// </summary>
    private static FrameHeader CreateFrameHeader(uint frameId, ushort packetSeq, ushort totalPackets, ushort rows = 1024, ushort cols = 1024)
    {
        return new FrameHeader
        {
            Magic = 0xD7E01234,
            Version = 1,
            FrameId = frameId,
            PacketSeq = packetSeq,
            TotalPackets = totalPackets,
            TimestampNs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000,
            Rows = rows,
            Cols = cols,
            Crc16 = 0x1234, // Dummy CRC for testing
            BitDepth = 14,
            Flags = 0
        };
    }

    /// <summary>
    /// Creates pixel payload with specific values.
    /// </summary>
    private static byte[] CreatePixelPayload(ushort value)
    {
        var payload = new byte[1024 * 1024 * 2]; // Full frame payload
        for (int i = 0; i < payload.Length; i += 2)
        {
            payload[i] = (byte)(value & 0xFF);
            payload[i + 1] = (byte)((value >> 8) & 0xFF);
        }
        return payload;
    }
}
