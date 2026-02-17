namespace FpgaSimulator.Tests.Csi2;

using FluentAssertions;
using FpgaSimulator.Core.Csi2;
using Xunit;

public class Csi2TxPacketGeneratorTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var generator = new Csi2TxPacketGenerator();

        // Assert
        generator.VirtualChannel.Should().Be(0);
        generator.DataType.Should().Be(Csi2DataType.Raw16);
    }

    [Fact]
    public void Constructor_WithParameters_ShouldSetProperties()
    {
        // Arrange & Act
        var generator = new Csi2TxPacketGenerator(virtualChannel: 1, dataType: Csi2DataType.Raw16);

        // Assert
        generator.VirtualChannel.Should().Be(1);
        generator.DataType.Should().Be(Csi2DataType.Raw16);
    }

    [Fact]
    public void GenerateFrameStart_ShouldCreateValidPacket()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();

        // Act
        var packet = generator.GenerateFrameStart();

        // Assert
        packet.PacketType.Should().Be(Csi2PacketType.FrameStart);
        packet.VirtualChannel.Should().Be(0);
        packet.Data.Length.Should().Be(4); // FS is 4 bytes
    }

    [Fact]
    public void GenerateFrameEnd_ShouldCreateValidPacket()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();

        // Act
        var packet = generator.GenerateFrameEnd();

        // Assert
        packet.PacketType.Should().Be(Csi2PacketType.FrameEnd);
        packet.VirtualChannel.Should().Be(0);
        packet.Data.Length.Should().Be(4); // FE is 4 bytes
    }

    [Fact]
    public void GenerateLineData_ShouldCreateValidPacket()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();
        var pixels = new ushort[] { 0x1234, 0x5678, 0x9ABC, 0xDEF0 };

        // Act
        var packet = generator.GenerateLineData(pixels, lineNumber: 5);

        // Assert
        packet.PacketType.Should().Be(Csi2PacketType.LineData);
        packet.VirtualChannel.Should().Be(0);
        packet.LineNumber.Should().Be(5);
        packet.Data.Length.Should().BeGreaterThan(pixels.Length * 2); // Header + pixels + CRC
    }

    [Fact]
    public void GenerateLineData_ShouldIncludeCorrectCrc()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();
        var pixels = new ushort[] { 0x1234, 0x5678 };

        // Act
        var packet = generator.GenerateLineData(pixels, lineNumber: 0);

        // Assert - CRC should be non-zero for non-zero data
        packet.Crc16.Should().NotBe(0);
    }

    [Fact]
    public void GenerateLineData_EmptyPixelArray_ShouldCreatePacketWithOnlyHeaderAndCrc()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();
        var pixels = Array.Empty<ushort>();

        // Act
        var packet = generator.GenerateLineData(pixels, lineNumber: 0);

        // Assert
        packet.PacketType.Should().Be(Csi2PacketType.LineData);
        packet.PixelCount.Should().Be(0);
    }

    [Fact]
    public void GenerateFullFrame_ShouldCreateAllPackets()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();
        var frame = new ushort[4, 4]
        {
            { 1, 2, 3, 4 },
            { 5, 6, 7, 8 },
            { 9, 10, 11, 12 },
            { 13, 14, 15, 16 }
        };

        // Act
        var packets = generator.GenerateFullFrame(frame);

        // Assert
        packets.Should().HaveCount(6); // 1 FS + 4 Lines + 1 FE
        packets[0].PacketType.Should().Be(Csi2PacketType.FrameStart);
        packets[^1].PacketType.Should().Be(Csi2PacketType.FrameEnd);
    }

    [Fact]
    public void GenerateFullFrame_FrameWithSingleRow_ShouldCreateThreePackets()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();
        var frame = new ushort[1, 2];
        frame[0, 0] = 100;
        frame[0, 1] = 200;

        // Act
        var packets = generator.GenerateFullFrame(frame);

        // Assert
        packets.Should().HaveCount(3); // 1 FS + 1 Line + 1 FE
    }

    [Fact]
    public void CalculateCrc16_ShouldBeDeterministic()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var crc1 = generator.CalculateCrc16(data);
        var crc2 = generator.CalculateCrc16(data);

        // Assert
        crc1.Should().Be(crc2);
    }

    [Fact]
    public void CalculateCrc16_DifferentData_ShouldProduceDifferentCrc()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();

        // Act
        var crc1 = generator.CalculateCrc16(new byte[] { 0x01, 0x02 });
        var crc2 = generator.CalculateCrc16(new byte[] { 0x02, 0x01 });

        // Assert
        crc1.Should().NotBe(crc2);
    }

    [Fact]
    public void VirtualChannel_ShouldBeIncludedInPackets()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator(virtualChannel: 2, Csi2DataType.Raw16);

        // Act
        var packet = generator.GenerateFrameStart();

        // Assert
        packet.VirtualChannel.Should().Be(2);
    }

    [Fact]
    public void Raw16DataType_ShouldMatchSpecification()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();

        // Act & Assert
        generator.DataType.Should().Be(Csi2DataType.Raw16);
        ((byte)generator.DataType).Should().Be(0x2E); // Per MIPI CSI-2 v1.3 spec
    }

    [Fact]
    public void GenerateLineData_MaximumLineSize_ShouldSucceed()
    {
        // Arrange
        var generator = new Csi2TxPacketGenerator();
        var pixels = new ushort[3072]; // Maximum line width

        // Act
        var packet = generator.GenerateLineData(pixels, lineNumber: 0);

        // Assert
        packet.PacketType.Should().Be(Csi2PacketType.LineData);
        packet.PixelCount.Should().Be(3072);
    }
}
