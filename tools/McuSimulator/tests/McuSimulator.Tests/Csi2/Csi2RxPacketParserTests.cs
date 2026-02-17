using FpgaSimulator.Core.Csi2;
using McuSimulator.Core.Csi2;
using Xunit;

namespace McuSimulator.Tests.Csi2;

/// <summary>
/// Tests for Csi2RxPacketParser.
/// Follows TDD: RED-GREEN-REFACTOR cycle.
/// REQ-SIM-004: McuSimulator shall implement CSI-2 RX packet parser.
/// </summary>
public sealed class Csi2RxPacketParserTests
{
    [Fact]
    public void ParseFrameStart_ShouldReturnValidFrameStartPacket()
    {
        // Arrange
        var parser = new Csi2RxPacketParser();
        var fsPacket = new Csi2Packet
        {
            PacketType = Csi2PacketType.FrameStart,
            VirtualChannel = 0,
            Data = new byte[] { 0x00, 0x00, 0x00, 0x0B } // VC=0, DT=0x00, Data=0x0000, ECC=0x0B
        };

        // Act
        var result = parser.ParseFrameStart(fsPacket);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0, result.VirtualChannel);
        Assert.Equal(0, result.FrameNumber);
    }

    [Fact]
    public void ParseFrameEnd_ShouldReturnValidFrameEndPacket()
    {
        // Arrange
        var parser = new Csi2RxPacketParser();
        var fePacket = new Csi2Packet
        {
            PacketType = Csi2PacketType.FrameEnd,
            VirtualChannel = 0,
            Data = new byte[] { 0x01, 0x00, 0x00, 0x0A } // VC=0, DT=0x01, Data=0x0000, ECC=0x0A
        };

        // Act
        var result = parser.ParseFrameEnd(fePacket);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0, result.VirtualChannel);
        Assert.Equal(0, result.FrameNumber);
    }

    [Fact]
    public void ParseLineData_ShouldExtractPixelPayload()
    {
        // Arrange
        var parser = new Csi2RxPacketParser();
        var pixels = new ushort[] { 0x0100, 0x0200, 0x0300 };
        var generator = new Csi2TxPacketGenerator();
        var linePacket = generator.GenerateLineData(pixels, lineNumber: 0);

        // Act
        var result = parser.ParseLineData(linePacket);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0, result.LineNumber);
        Assert.Equal(3, result.PixelCount);
        Assert.Equal(pixels, result.Pixels);
    }

    [Fact]
    public void VerifyEcc_ShouldValidateShortPacketEcc()
    {
        // Arrange
        var parser = new Csi2RxPacketParser();
        // Generate actual packet to get correct ECC
        var generator = new Csi2TxPacketGenerator();
        var fsPacket = generator.GenerateFrameStart(); // Frame 0, VC=0

        byte[] header = new byte[] { fsPacket.Data[0], fsPacket.Data[1], fsPacket.Data[2] };
        byte validEcc = fsPacket.Data[3];

        // Act
        bool isValid = parser.VerifyEcc(header, validEcc);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyCrc16_ShouldValidateLongPacketCrc()
    {
        // Arrange
        var parser = new Csi2RxPacketParser();
        var pixels = new ushort[] { 0x0100, 0x0200, 0x0300 };
        var generator = new Csi2TxPacketGenerator();
        var linePacket = generator.GenerateLineData(pixels, lineNumber: 0);

        // Act
        bool isValid = parser.VerifyCrc16(linePacket);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ParseFullFrame_ShouldReassembleCompleteFrame()
    {
        // Arrange
        var parser = new Csi2RxPacketParser();
        var generator = new Csi2TxPacketGenerator();
        var frame = new ushort[,] { { 0x0100, 0x0200 }, { 0x0300, 0x0400 } };
        var packets = generator.GenerateFullFrame(frame);

        // Act
        var result = parser.ParseFullFrame(packets);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(2, result.Rows);
        Assert.Equal(2, result.Cols);
        Assert.Equal(4, result.TotalPixels);
        Assert.Equal(frame, result.Pixels);
    }
}
