using XrayDetector.Core.Reassembly;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Reassembly;

/// <summary>
/// Specification tests for CRC-16/CCITT validator.
/// Validates frame header CRC-16/CCITT checksums per SPEC-SDK-001 AC-008.
/// Algorithm: Polynomial 0x8408 (reflected), Initial 0xFFFF
/// Covers header bytes 0-27 (magic, version, reserved0, frame_id, packet_seq, total_packets, timestamp_ns, rows, cols)
/// </summary>
public class Crc16CcittValidatorTests
{
    [Fact]
    public void ComputeCrc16_WithEmptyBuffer_ReturnsInitialValue()
    {
        // Arrange
        var emptyBuffer = Array.Empty<byte>();

        // Act
        ushort crc = Crc16CcittValidator.ComputeCrc16(emptyBuffer);

        // Assert - Initial value 0xFFFF should be inverted
        Assert.Equal(0xFFFF, crc);
    }

    [Fact]
    public void ComputeCrc16_WithSingleByte_ReturnsCorrectCrc()
    {
        // Arrange
        byte[] data = [0x00];

        // Act
        ushort crc = Crc16CcittValidator.ComputeCrc16(data);

        // Assert - Known CRC-16/CCITT (0x8408 reflected, init 0xFFFF) for 0x00
        Assert.Equal(0x0F87, crc);
    }

    [Fact]
    public void ComputeCrc16_WithKnownData_ReturnsCorrectCrc()
    {
        // Arrange - "123456789" standard test vector
        byte[] data = [0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39];

        // Act
        ushort crc = Crc16CcittValidator.ComputeCrc16(data);

        // Assert - Known CRC-16/CCITT (0x8408 reflected, init 0xFFFF) for "123456789"
        Assert.Equal(0x6F91, crc);
    }

    [Fact]
    public void ComputeCrc16_WithAllZeros_ReturnsCorrectCrc()
    {
        // Arrange
        byte[] data = new byte[30]; // 30 bytes of zeros (header size)

        // Act
        ushort crc = Crc16CcittValidator.ComputeCrc16(data);

        // Assert - Known CRC for 30 zero bytes
        Assert.Equal(0xA254, crc);
    }

    [Fact]
    public void ComputeCrc16_WithFrameHeader_ProducesConsistentResult()
    {
        // Arrange - Simulated frame header (30 bytes)
        byte[] header =
        [
            // Magic (4 bytes): "XRAY"
            0x58, 0x52, 0x41, 0x59,
            // Version (2 bytes)
            0x00, 0x01,
            // Reserved0 (2 bytes)
            0x00, 0x00,
            // Frame ID (4 bytes)
            0x00, 0x00, 0x00, 0x01,
            // Packet Sequence (4 bytes)
            0x00, 0x00, 0x00, 0x00,
            // Total Packets (4 bytes)
            0x00, 0x00, 0x00, 0x02,
            // Timestamp NS (8 bytes)
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        ];

        // Act
        ushort crc = Crc16CcittValidator.ComputeCrc16(header);

        // Assert - Should produce consistent CRC
        Assert.Equal(0xC9B0, crc);
    }

    [Fact]
    public void ValidateHeader_WithValidCrc_ReturnsTrue()
    {
        // Arrange - Frame header with correct CRC
        byte[] header = CreateFrameHeader(frameNumber: 1, packetSeq: 0, totalPackets: 2, rows: 1024, cols: 1024);
        ushort computedCrc = Crc16CcittValidator.ComputeCrc16(header[..28]); // Bytes 0-27
        header[28] = (byte)(computedCrc >> 8);  // CRC high byte at offset 28
        header[29] = (byte)computedCrc;         // CRC low byte at offset 29

        // Act
        bool isValid = Crc16CcittValidator.ValidateHeader(header);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateHeader_WithInvalidCrc_ReturnsFalse()
    {
        // Arrange - Frame header with incorrect CRC
        byte[] header = CreateFrameHeader(frameNumber: 1, packetSeq: 0, totalPackets: 2, rows: 1024, cols: 1024);
        header[28] = 0xFF;  // Wrong CRC high byte
        header[29] = 0xFF;  // Wrong CRC low byte

        // Act
        bool isValid = Crc16CcittValidator.ValidateHeader(header);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateHeader_WithShortBuffer_ReturnsFalse()
    {
        // Arrange - Buffer too short
        byte[] shortHeader = new byte[20]; // Less than 30 bytes

        // Act
        bool isValid = Crc16CcittValidator.ValidateHeader(shortHeader);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateHeader_WithCorruptedMagic_ReturnsFalse()
    {
        // Arrange - Valid CRC but corrupted magic
        byte[] header = CreateFrameHeader(frameNumber: 1, packetSeq: 0, totalPackets: 2, rows: 1024, cols: 1024);
        ushort computedCrc = Crc16CcittValidator.ComputeCrc16(header[..28]);
        header[28] = (byte)(computedCrc >> 8);
        header[29] = (byte)computedCrc;

        // Corrupt magic
        header[0] = 0xFF;
        header[1] = 0xFF;

        // Act - CRC should still validate even with bad magic
        bool isValid = Crc16CcittValidator.ValidateHeader(header);

        // Assert - CRC is valid (magic not part of CRC check, or is it?)
        // According to SPEC-SDK-001 AC-008: CRC covers bytes 0-27 (includes magic)
        // So corrupting magic should invalidate CRC
        Assert.False(isValid);
    }

    // Helper method to create test frame header (30 bytes minimum)
    private static byte[] CreateFrameHeader(uint frameNumber, uint packetSeq, uint totalPackets, uint rows, uint cols)
    {
        var header = new byte[30];

        // Magic: "XRAY"
        header[0] = 0x58; // X
        header[1] = 0x52; // R
        header[2] = 0x41; // A
        header[3] = 0x59; // Y

        // Version: 1
        header[4] = 0x00;
        header[5] = 0x01;

        // Reserved0: 0
        header[6] = 0x00;
        header[7] = 0x00;

        // Frame ID (big-endian)
        header[8] = (byte)(frameNumber >> 24);
        header[9] = (byte)(frameNumber >> 16);
        header[10] = (byte)(frameNumber >> 8);
        header[11] = (byte)frameNumber;

        // Packet Sequence (big-endian)
        header[12] = (byte)(packetSeq >> 24);
        header[13] = (byte)(packetSeq >> 16);
        header[14] = (byte)(packetSeq >> 8);
        header[15] = (byte)packetSeq;

        // Total Packets (big-endian)
        header[16] = (byte)(totalPackets >> 24);
        header[17] = (byte)(totalPackets >> 16);
        header[18] = (byte)(totalPackets >> 8);
        header[19] = (byte)totalPackets;

        // Timestamp NS (8 bytes, zero for test)
        // header[20..27] = 0

        // Rows (big-endian) - offset 28
        header[28] = (byte)(rows >> 8);
        header[29] = (byte)rows;

        // Cols would be at offset 30, but we only return 30 bytes
        // For full header, we'd need 32 bytes

        return header;
    }
}
