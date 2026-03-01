using System.Buffers.Binary;
using FluentAssertions;
using HostSimulator.Core.Reassembly;
using Xunit;

namespace HostSimulator.Tests.Reassembly;

/// <summary>
/// Tests verifying that HostSimulator correctly detects incomplete frame timeouts.
/// REQ-SIM-042: Mark frame as incomplete and report missing packets after timeout.
/// </summary>
public class HostTimeoutDetectionTests
{
    /// <summary>
    /// Creates a valid 32-byte UDP frame header with correct CRC.
    /// </summary>
    private static byte[] CreateValidPacketHeader(
        uint frameId, ushort packetSeq, ushort totalPackets,
        ushort rows, ushort cols, byte flags = 0x00)
    {
        var header = new byte[32];

        // Magic (0xD7E01234, little-endian)
        header[0] = 0x34; header[1] = 0x12; header[2] = 0xE0; header[3] = 0xD7;

        // Version
        header[4] = 0x01;

        // Frame ID
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), frameId);

        // Packet sequence
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12, 2), packetSeq);

        // Total packets
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(14, 2), totalPackets);

        // Timestamp
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16, 8), DateTime.UtcNow.Ticks * 100);

        // Rows / Cols
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(24, 2), rows);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(26, 2), cols);

        // CRC-16 over bytes 0-27
        ushort crc = Crc16Ccitt.Calculate(header.AsSpan(0, 28));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(28, 2), crc);

        // Bit depth
        header[30] = 16;

        // Flags
        header[31] = flags;

        return header;
    }

    /// <summary>
    /// Creates a complete UDP packet (header + payload).
    /// </summary>
    private static byte[] CreatePacket(
        uint frameId, ushort packetSeq, ushort totalPackets,
        ushort rows, ushort cols, int payloadSize, byte flags = 0x00)
    {
        var header = CreateValidPacketHeader(frameId, packetSeq, totalPackets, rows, cols, flags);
        var packet = new byte[32 + payloadSize];
        Array.Copy(header, 0, packet, 0, 32);
        return packet;
    }

    [Fact]
    public void CheckTimeouts_NoPendingFrames_ReturnsNull()
    {
        var reassembler = new FrameReassembler(TimeSpan.FromMilliseconds(100));

        var result = reassembler.CheckTimeouts();

        result.Should().BeNull("no frames are pending");
    }

    [Fact]
    public void CheckTimeouts_CompleteFrame_DoesNotTimeout()
    {
        // Arrange - single-packet frame (complete)
        var reassembler = new FrameReassembler(TimeSpan.FromMilliseconds(100));
        ushort rows = 16, cols = 16;
        int payloadSize = rows * cols * 2;

        var packet = CreatePacket(1, 0, 1, rows, cols, payloadSize, flags: 0x01);
        FrameHeader.TryParse(packet, out var header);
        var payload = new byte[payloadSize];
        Array.Copy(packet, 32, payload, 0, payloadSize);
        reassembler.ProcessPacket(header!, payload);

        // Act
        var result = reassembler.CheckTimeouts();

        // Assert
        result.Should().BeNull("complete frame should have been removed from pending");
        reassembler.GetPendingFrameCount().Should().Be(0);
    }

    [Fact]
    public async Task IncompleteFrame_AfterTimeout_DetectedAsIncomplete()
    {
        // Arrange - 3-packet frame, send only first packet
        var reassembler = new FrameReassembler(TimeSpan.FromMilliseconds(50));
        ushort rows = 32, cols = 32;
        ushort totalPackets = 3;
        int payloadSize = 512;

        var packet = CreatePacket(1, 0, totalPackets, rows, cols, payloadSize);
        FrameHeader.TryParse(packet, out var header);
        var payload = new byte[payloadSize];
        Array.Copy(packet, 32, payload, 0, payloadSize);
        reassembler.ProcessPacket(header!, payload);

        reassembler.GetPendingFrameCount().Should().Be(1);

        // Act - Wait for timeout
        await Task.Delay(100);
        var result = reassembler.CheckTimeouts();

        // Assert
        result.Should().NotBeNull("timed-out incomplete frame should be detected");
        result!.Status.Should().Be(FrameReassemblyStatus.Incomplete);
        result.FrameId.Should().Be(1);
        result.MissingPackets.Should().NotBeEmpty();
        result.MissingPackets!.Length.Should().Be(2, "packets 1 and 2 are missing");
        result.MissingPackets.Should().Contain((ushort)1);
        result.MissingPackets.Should().Contain((ushort)2);

        reassembler.GetPendingFrameCount().Should().Be(0,
            "timed-out frame should be removed from pending");
    }

    [Fact]
    public async Task SinglePacketSent_RemainingMissing_TimeoutReportsAll()
    {
        // Arrange - 5-packet frame, send only packet index 0
        var reassembler = new FrameReassembler(TimeSpan.FromMilliseconds(50));
        ushort rows = 64, cols = 64;
        ushort totalPackets = 5;
        int payloadSize = 256;

        var packet = CreatePacket(42, 0, totalPackets, rows, cols, payloadSize);
        FrameHeader.TryParse(packet, out var header);
        var payload = new byte[payloadSize];
        Array.Copy(packet, 32, payload, 0, payloadSize);
        reassembler.ProcessPacket(header!, payload);

        // Act
        await Task.Delay(100);
        var result = reassembler.CheckTimeouts();

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(FrameReassemblyStatus.Incomplete);
        result.FrameId.Should().Be(42);
        result.MissingPackets!.Length.Should().Be(4, "packets 1-4 are missing");

        for (int i = 0; i < result.MissingPackets.Length; i++)
        {
            result.MissingPackets[i].Should().Be((ushort)(i + 1));
        }
    }
}
