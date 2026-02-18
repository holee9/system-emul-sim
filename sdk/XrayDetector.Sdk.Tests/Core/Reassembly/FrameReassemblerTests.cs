using System.Buffers;
using XrayDetector.Core.Reassembly;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Reassembly;

/// <summary>
/// Specification tests for FrameReassembler.
/// Handles out-of-order packet delivery, CRC validation, and timeout handling.
/// </summary>
public class FrameReassemblerTests : IDisposable
{
    private readonly ArrayPool<ushort> _pool;

    public FrameReassemblerTests()
    {
        _pool = ArrayPool<ushort>.Shared;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Create_WithDefaultParameters_CreatesReassembler()
    {
        // Act
        var reassembler = new FrameReassembler(_pool);

        // Assert
        Assert.NotNull(reassembler);
        Assert.Equal<uint>(8u, reassembler.MaxConcurrentFrames); // Default
        Assert.Equal(TimeSpan.FromMilliseconds(500), reassembler.Timeout); // Default
    }

    [Fact]
    public void Create_WithCustomParameters_CreatesReassembler()
    {
        // Act
        var reassembler = new FrameReassembler(_pool, maxConcurrentFrames: 4, timeout: TimeSpan.FromSeconds(2));

        // Assert
        Assert.NotNull(reassembler);
        Assert.Equal<uint>(4u, reassembler.MaxConcurrentFrames);
        Assert.Equal(TimeSpan.FromSeconds(2), reassembler.Timeout);
    }

    [Fact]
    public void ProcessPacket_WithFirstFrame_CreatesNewSlot()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool);
        byte[] packet = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 2, pixelData: [1, 2, 3, 4]);

        // Act
        FrameReassemblyResult result = reassembler.ProcessPacket(packet);

        // Assert
        Assert.Equal(ReassemblyStatus.Processing, result.Status);
        Assert.Equal<uint>(1u, result.FrameNumber);
        Assert.Equal(1, reassembler.ActiveFrameCount);
    }

    [Fact]
    public void ProcessPacket_WithCompleteFrame_ReturnsCompleteFrame()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool);
        byte[] packet1 = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 2, pixelData: [1, 2, 3, 4]);
        byte[] packet2 = CreateTestPacket(frameNumber: 1, packetSeq: 1, totalPackets: 2, pixelData: [5, 6, 7, 8]);

        // Act
        var result1 = reassembler.ProcessPacket(packet1);
        var result2 = reassembler.ProcessPacket(packet2);

        // Assert
        Assert.Equal(ReassemblyStatus.Processing, result1.Status);
        Assert.Equal(ReassemblyStatus.Complete, result2.Status);
        Assert.NotNull(result2.FrameData);
        Assert.Equal(8, result2.FrameData.Length); // 4 + 4 pixels
        Assert.Equal(1, result2.FrameData[0]);
        Assert.Equal(8, result2.FrameData[7]);
        Assert.Equal(0, reassembler.ActiveFrameCount); // Slot released
    }

    [Fact]
    public void ProcessPacket_WithOutOfOrderPackets_ReturnsCompleteFrame()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool);
        byte[] packet2 = CreateTestPacket(frameNumber: 1, packetSeq: 1, totalPackets: 3, pixelData: [5, 6]);
        byte[] packet3 = CreateTestPacket(frameNumber: 1, packetSeq: 2, totalPackets: 3, pixelData: [7, 8]);
        byte[] packet1 = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 3, pixelData: [1, 2]);

        // Act - Receive out of order: 2, 3, 1
        var result2 = reassembler.ProcessPacket(packet2);
        var result3 = reassembler.ProcessPacket(packet3);
        var result1 = reassembler.ProcessPacket(packet1);

        // Assert
        Assert.Equal(ReassemblyStatus.Processing, result2.Status);
        Assert.Equal(ReassemblyStatus.Processing, result3.Status);
        Assert.Equal(ReassemblyStatus.Complete, result1.Status);
        Assert.NotNull(result1.FrameData);
        Assert.Equal(6, result1.FrameData.Length);
        Assert.Equal(1, result1.FrameData[0]); // Packet 1, pixel 0
        Assert.Equal(8, result1.FrameData[5]); // Packet 3, pixel 1
    }

    [Fact]
    public void ProcessPacket_WithInvalidCrc_ReturnsCrcError()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool);
        byte[] packet = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 1, pixelData: [1, 2, 3, 4]);
        packet[28] = 0xFF; // Corrupt CRC
        packet[29] = 0xFF;

        // Act
        FrameReassemblyResult result = reassembler.ProcessPacket(packet);

        // Assert
        Assert.Equal(ReassemblyStatus.CrcError, result.Status);
        Assert.Equal(0, reassembler.ActiveFrameCount);
    }

    [Fact]
    public void ProcessPacket_WithMissingPackets_AfterTimeout_CleansUpFrame()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool, maxConcurrentFrames: 8, timeout: TimeSpan.FromMilliseconds(100));
        byte[] packet1 = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 3, pixelData: [1, 2]);
        byte[] packet2 = CreateTestPacket(frameNumber: 1, packetSeq: 1, totalPackets: 3, pixelData: [3, 4]);

        // Act
        reassembler.ProcessPacket(packet1);
        reassembler.ProcessPacket(packet2);
        Thread.Sleep(150); // Wait for timeout
        int removed = reassembler.CleanupExpiredFrames();

        // Assert
        Assert.Equal(1, removed); // One frame cleaned up
        Assert.Equal(0, reassembler.ActiveFrameCount);
    }

    [Fact]
    public void ProcessPacket_WithConcurrentFrames_HandlesMultipleSlots()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool, maxConcurrentFrames: 3, timeout: TimeSpan.FromMilliseconds(500));
        byte[] frame1Packet = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 1, pixelData: [1]);
        byte[] frame2Packet = CreateTestPacket(frameNumber: 2, packetSeq: 0, totalPackets: 1, pixelData: [2]);
        byte[] frame3Packet = CreateTestPacket(frameNumber: 3, packetSeq: 0, totalPackets: 1, pixelData: [3]);

        // Act
        var result1 = reassembler.ProcessPacket(frame1Packet);
        var result2 = reassembler.ProcessPacket(frame2Packet);
        var result3 = reassembler.ProcessPacket(frame3Packet);

        // Assert
        Assert.Equal(ReassemblyStatus.Complete, result1.Status);
        Assert.Equal(ReassemblyStatus.Complete, result2.Status);
        Assert.Equal(ReassemblyStatus.Complete, result3.Status);
    }

    [Fact]
    public void ProcessPacket_WhenSlotsFull_ReplacesOldestSlot()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool, maxConcurrentFrames: 2, timeout: TimeSpan.FromMilliseconds(500));
        byte[] frame1Packet = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 2, pixelData: [1]);
        byte[] frame2Packet = CreateTestPacket(frameNumber: 2, packetSeq: 0, totalPackets: 2, pixelData: [2]);
        byte[] frame3Packet = CreateTestPacket(frameNumber: 3, packetSeq: 0, totalPackets: 2, pixelData: [3]);

        // Act - Fill slots, then add one more
        reassembler.ProcessPacket(frame1Packet);
        reassembler.ProcessPacket(frame2Packet);
        var result = reassembler.ProcessPacket(frame3Packet); // Should replace frame 1

        // Assert
        Assert.Equal(2, reassembler.ActiveFrameCount);
        Assert.Equal(ReassemblyStatus.Processing, result.Status);
    }

    [Fact]
    public void ProcessPacket_WithDuplicateFrameNumber_UpdatesExistingSlot()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool);
        byte[] packet1 = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 2, pixelData: [1, 2]);
        byte[] packet2 = CreateTestPacket(frameNumber: 1, packetSeq: 1, totalPackets: 2, pixelData: [3, 4]);

        // Act
        var result1 = reassembler.ProcessPacket(packet1);
        var result2 = reassembler.ProcessPacket(packet2);

        // Assert
        Assert.Equal(0, reassembler.ActiveFrameCount); // Slot released after completion
        Assert.Equal(ReassemblyStatus.Processing, result1.Status);
        Assert.Equal(ReassemblyStatus.Complete, result2.Status);
    }

    [Fact]
    public void CleanupExpiredFrames_WithExpiredFrames_RemovesThem()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool, maxConcurrentFrames: 8, timeout: TimeSpan.FromMilliseconds(100));
        byte[] packet1 = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 2, pixelData: [1]);
        byte[] packet2 = CreateTestPacket(frameNumber: 2, packetSeq: 0, totalPackets: 2, pixelData: [2]);

        reassembler.ProcessPacket(packet1);
        reassembler.ProcessPacket(packet2);
        Thread.Sleep(150); // Wait for timeout

        // Act
        int removed = reassembler.CleanupExpiredFrames();

        // Assert
        Assert.Equal(2, removed);
        Assert.Equal(0, reassembler.ActiveFrameCount);
    }

    [Fact]
    public void CleanupExpiredFrames_WithActiveFrames_KeepsThem()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool, maxConcurrentFrames: 8, timeout: TimeSpan.FromSeconds(10));
        byte[] packet = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 2, pixelData: [1]);
        reassembler.ProcessPacket(packet);

        // Act
        int removed = reassembler.CleanupExpiredFrames();

        // Assert
        Assert.Equal(0, removed);
        Assert.Equal(1, reassembler.ActiveFrameCount);
    }

    [Fact]
    public void GetFrameStatus_WithActiveFrame_ReturnsStatus()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool);
        byte[] packet = CreateTestPacket(frameNumber: 1, packetSeq: 0, totalPackets: 3, pixelData: [1, 2]);
        reassembler.ProcessPacket(packet);

        // Act
        FrameStatus? status = reassembler.GetFrameStatus(1);

        // Assert
        Assert.NotNull(status);
        Assert.Equal<uint>(1u, status.FrameNumber);
        Assert.Equal<uint>(3u, status.TotalPackets);
        Assert.Equal<uint>(1u, status.ReceivedPackets);
    }

    [Fact]
    public void GetFrameStatus_WithNonExistentFrame_ReturnsNull()
    {
        // Arrange
        var reassembler = new FrameReassembler(_pool);

        // Act
        FrameStatus? status = reassembler.GetFrameStatus(999);

        // Assert
        Assert.Null(status);
    }

    // Helper method to create test packet with header
    private static byte[] CreateTestPacket(uint frameNumber, uint packetSeq, uint totalPackets, ushort[] pixelData)
    {
        // Header: Magic (4) + Version (2) + Reserved (2) + FrameID (4) +
        //         PacketSeq (4) + TotalPackets (4) + Timestamp (8) + CRC (2) + Rows (2) + Cols (2) = 34 bytes
        // Payload: pixelData.Length * 2 bytes
        int headerSize = 34;
        byte[] packet = new byte[headerSize + pixelData.Length * 2];

        int offset = 0;

        // Magic: "XRAY"
        packet[offset++] = 0x58; // X
        packet[offset++] = 0x52; // R
        packet[offset++] = 0x41; // A
        packet[offset++] = 0x59; // Y

        // Version: 1
        packet[offset++] = 0x00;
        packet[offset++] = 0x01;

        // Reserved0: 0
        packet[offset++] = 0x00;
        packet[offset++] = 0x00;

        // Frame ID (big-endian)
        packet[offset++] = (byte)(frameNumber >> 24);
        packet[offset++] = (byte)(frameNumber >> 16);
        packet[offset++] = (byte)(frameNumber >> 8);
        packet[offset++] = (byte)frameNumber;

        // Packet Sequence (big-endian)
        packet[offset++] = (byte)(packetSeq >> 24);
        packet[offset++] = (byte)(packetSeq >> 16);
        packet[offset++] = (byte)(packetSeq >> 8);
        packet[offset++] = (byte)packetSeq;

        // Total Packets (big-endian)
        packet[offset++] = (byte)(totalPackets >> 24);
        packet[offset++] = (byte)(totalPackets >> 16);
        packet[offset++] = (byte)(totalPackets >> 8);
        packet[offset++] = (byte)totalPackets;

        // Timestamp NS (8 bytes, zero for test) - offset 20-27
        offset += 8;

        // CRC (2 bytes) - offset 28-29
        int crcOffset = offset;
        offset += 2;

        // Rows (2 bytes, big-endian) - offset 30-31
        packet[offset++] = 0x00;
        packet[offset++] = 0x01;

        // Cols (2 bytes, big-endian) - offset 32-33
        packet[offset++] = (byte)(pixelData.Length >> 8);
        packet[offset++] = (byte)pixelData.Length;

        // Pixel data (big-endian) - offset 34+
        for (int i = 0; i < pixelData.Length; i++)
        {
            packet[offset++] = (byte)(pixelData[i] >> 8);
            packet[offset++] = (byte)pixelData[i];
        }

        // Compute CRC over bytes 0-27 (Magic through Timestamp)
        // According to SPEC-SDK-001 AC-008: bytes 0-27
        ushort crc = Crc16CcittValidator.ComputeCrc16(packet[..28]);
        packet[crcOffset] = (byte)(crc >> 8);
        packet[crcOffset + 1] = (byte)crc;

        return packet;
    }
}
