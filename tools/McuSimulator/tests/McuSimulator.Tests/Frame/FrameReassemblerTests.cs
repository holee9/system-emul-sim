using FpgaSimulator.Core.Csi2;
using McuSimulator.Core.Frame;
using Xunit;

namespace McuSimulator.Tests.Frame;

/// <summary>
/// Tests for FrameReassembler.
/// Follows TDD: RED-GREEN-REFACTOR cycle.
/// REQ-SIM-005: McuSimulator shall implement frame reassembly from CSI-2 packets.
/// </summary>
public sealed class FrameReassemblerTests
{
    [Fact]
    public void AddPacket_ShouldStoreFrameStartPacket()
    {
        // Arrange
        var reassembler = new FrameReassembler();
        var fsPacket = new Csi2Packet
        {
            PacketType = Csi2PacketType.FrameStart,
            VirtualChannel = 0,
            Data = new byte[] { 0x00, 0x00, 0x00, 0x00 }
        };

        // Act
        reassembler.AddPacket(fsPacket);

        // Assert
        Assert.True(reassembler.HasFrameStart);
    }

    [Fact]
    public void AddPacket_ShouldStoreLineDataPackets()
    {
        // Arrange
        var reassembler = new FrameReassembler();
        var generator = new Csi2TxPacketGenerator();
        var pixels = new ushort[] { 0x0100, 0x0200 };
        var linePacket = generator.GenerateLineData(pixels, lineNumber: 0);

        // Act
        reassembler.AddPacket(linePacket);

        // Assert
        Assert.Equal(1, reassembler.ReceivedLineCount);
    }

    [Fact]
    public void AddPacket_ShouldDetectFrameComplete_WhenFrameEndReceived()
    {
        // Arrange
        var reassembler = new FrameReassembler();
        var generator = new Csi2TxPacketGenerator();
        var frame = new ushort[,] { { 0x0100, 0x0200 }, { 0x0300, 0x0400 } };
        var packets = generator.GenerateFullFrame(frame);

        // Act
        foreach (var packet in packets)
        {
            reassembler.AddPacket(packet);
        }

        // Assert
        Assert.True(reassembler.IsFrameComplete);
    }

    [Fact]
    public void GetFrame_ShouldReturnCompleteFrame_WhenAllPacketsReceived()
    {
        // Arrange
        var reassembler = new FrameReassembler();
        var generator = new Csi2TxPacketGenerator();
        var frame = new ushort[,] { { 0x0100, 0x0200 }, { 0x0300, 0x0400 } };
        var packets = generator.GenerateFullFrame(frame);

        foreach (var packet in packets)
        {
            reassembler.AddPacket(packet);
        }

        // Act
        var result = reassembler.GetFrame();

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(2, result.Rows);
        Assert.Equal(2, result.Cols);
        Assert.Equal(frame, result.Pixels);
    }

    [Fact]
    public void GetFrame_ShouldReturnInvalid_WhenFrameIncomplete()
    {
        // Arrange
        var reassembler = new FrameReassembler();
        var generator = new Csi2TxPacketGenerator();
        var frame = new ushort[,] { { 0x0100, 0x0200 }, { 0x0300, 0x0400 } };
        var packets = generator.GenerateFullFrame(frame);

        // Only add FS and one line (missing FE)
        reassembler.AddPacket(packets[0]); // FS
        reassembler.AddPacket(packets[1]); // Line 0

        // Act
        var result = reassembler.GetFrame();

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Reset_ShouldClearAllState()
    {
        // Arrange
        var reassembler = new FrameReassembler();
        var generator = new Csi2TxPacketGenerator();
        var frame = new ushort[,] { { 0x0100 } };
        var packets = generator.GenerateFullFrame(frame);

        foreach (var packet in packets)
        {
            reassembler.AddPacket(packet);
        }

        // Act
        reassembler.Reset();

        // Assert
        Assert.False(reassembler.HasFrameStart);
        Assert.Equal(0, reassembler.ReceivedLineCount);
        Assert.False(reassembler.IsFrameComplete);
    }

    [Fact]
    public void AddPacket_ShouldHandleMissingPackets_Gracefully()
    {
        // Arrange
        var reassembler = new FrameReassembler();
        var generator = new Csi2TxPacketGenerator();

        // Create frame with 3 lines
        var frame = new ushort[3, 2] { { 0x0100, 0x0200 }, { 0x0300, 0x0400 }, { 0x0500, 0x0600 } };
        var packets = generator.GenerateFullFrame(frame);

        // Simulate missing middle packet
        reassembler.AddPacket(packets[0]); // FS
        reassembler.AddPacket(packets[2]); // Skip Line 1
        reassembler.AddPacket(packets[3]); // Line 2
        reassembler.AddPacket(packets[4]); // FE

        // Act
        var result = reassembler.GetFrame();

        // Assert - Frame should be complete but with a gap
        Assert.True(reassembler.IsFrameComplete);
        Assert.True(result.IsValid);
        Assert.Equal(2, result.ReceivedLineCount); // Only 2 lines received
    }

    [Fact]
    public void IsFrameComplete_ShouldReturnFalse_WhenNoPackets()
    {
        // Arrange
        var reassembler = new FrameReassembler();

        // Act & Assert
        Assert.False(reassembler.IsFrameComplete);
    }
}
