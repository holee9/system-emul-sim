using McuSimulator.Core.Network;
using Xunit;

namespace McuSimulator.Tests.Network;

/// <summary>
/// Tests for UdpFrameTransmitter.
/// Follows TDD: RED-GREEN-REFACTOR cycle.
/// REQ-SIM-006: McuSimulator shall implement UDP frame transmitter.
/// </summary>
public sealed class UdpFrameTransmitterTests
{
    [Fact]
    public void CreateFrameHeader_ShouldProduceValid32ByteHeader()
    {
        // Arrange
        var transmitter = new UdpFrameTransmitter();
        var frameData = new ushort[,] { { 0x0100, 0x0200 } };

        // Act
        var header = transmitter.CreateFrameHeader(frameData, frameId: 0);

        // Assert
        Assert.Equal(32, header.Length);
        Assert.Equal(0xD7E01234, BitConverter.ToUInt32(header.AsSpan(0, 4))); // magic
    }

    [Fact]
    public void CreateFrameHeader_ShouldIncludeCorrectFrameId()
    {
        // Arrange
        var transmitter = new UdpFrameTransmitter();
        var frameData = new ushort[,] { { 0x0100 } };
        uint frameId = 12345;

        // Act
        var header = transmitter.CreateFrameHeader(frameData, frameId);

        // Assert
        uint readFrameId = BitConverter.ToUInt32(header.AsSpan(8, 4));
        Assert.Equal(frameId, readFrameId);
    }

    [Fact]
    public void CreateFrameHeader_ShouldIncludeDimensions()
    {
        // Arrange
        var transmitter = new UdpFrameTransmitter();
        var frameData = new ushort[3, 4]; // 3 rows, 4 cols

        // Act
        var header = transmitter.CreateFrameHeader(frameData, frameId: 0);

        // Assert
        ushort rows = BitConverter.ToUInt16(header.AsSpan(24, 2));
        ushort cols = BitConverter.ToUInt16(header.AsSpan(26, 2));
        Assert.Equal(3, rows);
        Assert.Equal(4, cols);
    }

    [Fact]
    public void FragmentFrame_ShouldProduceCorrectPackets()
    {
        // Arrange
        var transmitter = new UdpFrameTransmitter(maxPayload: 50);
        var frameData = new ushort[10, 10]; // 200 bytes

        // Act
        var packets = transmitter.FragmentFrame(frameData, frameId: 0);

        // Assert
        Assert.Equal(4, packets.Count); // 200 bytes / 50 max = 4 packets
    }

    [Fact]
    public void FragmentFrame_ShouldSetCorrectPacketIndices()
    {
        // Arrange
        var transmitter = new UdpFrameTransmitter(maxPayload: 50);
        var frameData = new ushort[10, 10]; // 200 bytes

        // Act
        var packets = transmitter.FragmentFrame(frameData, frameId: 0);

        // Assert
        Assert.Equal(0, packets[0].PacketIndex);
        Assert.Equal(1, packets[1].PacketIndex);
        Assert.Equal(2, packets[2].PacketIndex);
        Assert.Equal(3, packets[3].PacketIndex);
    }

    [Fact]
    public void FragmentFrame_ShouldSetLastPacketFlag()
    {
        // Arrange
        var transmitter = new UdpFrameTransmitter(maxPayload: 50);
        var frameData = new ushort[10, 10]; // 200 bytes

        // Act
        var packets = transmitter.FragmentFrame(frameData, frameId: 0);

        // Assert
        Assert.Equal(0, packets[0].Flags & 0x01); // Not last
        Assert.Equal(0, packets[1].Flags & 0x01); // Not last
        Assert.Equal(0, packets[2].Flags & 0x01); // Not last
        Assert.Equal(1, packets[3].Flags & 0x01); // Last packet
    }

    [Fact]
    public void CalculateCrc16_ShouldMatchReference()
    {
        // Arrange
        var transmitter = new UdpFrameTransmitter();
        byte[] data = new byte[] { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39 }; // "123456789"

        // Act
        ushort crc = transmitter.CalculateCrc16(data);

        // Assert - Reference value from ethernet-protocol.md
        Assert.Equal(0x6F91, crc);
    }

    [Fact]
    public void TransmitFrame_ShouldReturnListOfUdpPackets()
    {
        // Arrange
        var transmitter = new UdpFrameTransmitter();
        var frameData = new ushort[,] { { 0x0100, 0x0200 }, { 0x0300, 0x0400 } };

        // Act
        var packets = transmitter.TransmitFrame(frameData, frameId: 0);

        // Assert
        Assert.NotEmpty(packets);
        Assert.All(packets, p => Assert.True(p.Data.Length >= 32)); // At least header
    }
}
