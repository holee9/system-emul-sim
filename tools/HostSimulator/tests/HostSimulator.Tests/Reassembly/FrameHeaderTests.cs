using HostSimulator.Core.Reassembly;
using Xunit;
using FluentAssertions;

namespace HostSimulator.Tests.Reassembly;

/// <summary>
/// Tests for FrameHeader parsing and validation.
/// REQ-SIM-032: Frame header format with magic (0xD7E01234), frame_seq, timestamp, width, height, bit_depth, packet_index, total_packets, crc16.
/// Reference: docs/api/ethernet-protocol.md Section 2.1
/// </summary>
public class FrameHeaderTests
{
    /// <summary>
    /// magic number for frame header synchronization.
    /// </summary>
    private const uint FRAME_MAGIC = 0xD7E01234u;

    [Fact]
    public void TryParse_ShouldReturnTrue_WhenHeaderIsValid()
    {
        // Arrange
        var headerBytes = CreateValidHeaderBytes();

        // Act
        var result = FrameHeader.TryParse(headerBytes, out var header);

        // Assert
        result.Should().BeTrue();
        header.Should().NotBeNull();
        header!.Magic.Should().Be(FRAME_MAGIC);
        header.Version.Should().Be(1);
        header.FrameId.Should().Be(0);
        header.PacketSeq.Should().Be(0);
        header.TotalPackets.Should().Be(256);
        header.Rows.Should().Be(1024);
        header.Cols.Should().Be(1024);
        header.BitDepth.Should().Be(14);
    }

    [Fact]
    public void TryParse_ShouldReturnFalse_WhenMagicNumberIsInvalid()
    {
        // Arrange
        var headerBytes = CreateValidHeaderBytes();
        // Corrupt magic number
        headerBytes[0] = 0x00;
        headerBytes[1] = 0x00;
        headerBytes[2] = 0x00;
        headerBytes[3] = 0x00;

        // Act
        var result = FrameHeader.TryParse(headerBytes, out var header);

        // Assert
        result.Should().BeFalse();
        header.Should().BeNull();
    }

    [Fact]
    public void TryParse_ShouldReturnFalse_WhenHeaderIsTooShort()
    {
        // Arrange - Header is only 31 bytes (needs 32)
        var shortHeader = new byte[31];

        // Act
        var result = FrameHeader.TryParse(shortHeader, out var header);

        // Assert
        result.Should().BeFalse();
        header.Should().BeNull();
    }

    [Fact]
    public void TryParse_ShouldReturnFalse_WhenCrcIsInvalid()
    {
        // Arrange
        var headerBytes = CreateValidHeaderBytes();
        // Corrupt CRC at offset 28
        headerBytes[28] = 0xFF;
        headerBytes[29] = 0xFF;

        // Act
        var result = FrameHeader.TryParse(headerBytes, out var header);

        // Assert
        result.Should().BeFalse();
        header.Should().BeNull();
    }

    [Fact]
    public void TryParse_ShouldReturnFalse_WhenVersionIsNotSupported()
    {
        // Arrange
        var headerBytes = CreateValidHeaderBytes();
        // Set invalid version at offset 4
        headerBytes[4] = 0xFF;

        // Act
        var result = FrameHeader.TryParse(headerBytes, out var header);

        // Assert
        result.Should().BeFalse();
        header.Should().BeNull();
    }

    [Fact]
    public void TryParse_ShouldCorrectlyParseAllFields()
    {
        // Arrange
        var headerBytes = new byte[32];
        var span = new Span<byte>(headerBytes);

        // Magic
        span[0] = 0x34;
        span[1] = 0x12;
        span[2] = 0xE0;
        span[3] = 0xD7;

        // Version
        span[4] = 0x01;
        span[5] = 0x00;
        span[6] = 0x00;
        span[7] = 0x00;

        // Frame ID = 42
        span[8] = 42;
        span[9] = 0;
        span[10] = 0;
        span[11] = 0;

        // Packet Seq = 5
        span[12] = 5;
        span[13] = 0;

        // Total Packets = 100
        span[14] = 100;
        span[15] = 0;

        // Timestamp = 1234567890
        var timestamp = 1234567890UL;
        var tsSpan = new Span<byte>(headerBytes, 16, 8);
        BitConverter.TryWriteBytes(tsSpan, timestamp);

        // Rows = 2048
        span[24] = 0;
        span[25] = 8;

        // Cols = 2048
        span[26] = 0;
        span[27] = 8;

        // Calculate CRC over bytes 0-27
        ushort crc = Crc16Ccitt.Calculate(headerBytes, 0, 28);
        span[28] = (byte)(crc & 0xFF);
        span[29] = (byte)((crc >> 8) & 0xFF);

        // Bit depth = 16
        span[30] = 16;

        // Flags = 0
        span[31] = 0;

        // Act
        var result = FrameHeader.TryParse(headerBytes, out var header);

        // Assert
        result.Should().BeTrue();
        header.Should().NotBeNull();
        header!.Magic.Should().Be(FRAME_MAGIC);
        header.Version.Should().Be(1);
        header.FrameId.Should().Be(42);
        header.PacketSeq.Should().Be(5);
        header.TotalPackets.Should().Be(100);
        header.TimestampNs.Should().Be(1234567890UL);
        header.Rows.Should().Be(2048);
        header.Cols.Should().Be(2048);
        header.BitDepth.Should().Be(16);
        header.Flags.Should().Be(0);
    }

    [Fact]
    public void TryParse_ShouldHandleLastPacketFlag()
    {
        // Arrange
        var headerBytes = CreateValidHeaderBytes();
        // Set last_packet flag (bit 0) in flags field at offset 31
        headerBytes[31] = 0x01;

        // Recalculate CRC
        ushort crc = Crc16Ccitt.Calculate(headerBytes, 0, 28);
        headerBytes[28] = (byte)(crc & 0xFF);
        headerBytes[29] = (byte)((crc >> 8) & 0xFF);

        // Act
        var result = FrameHeader.TryParse(headerBytes, out var header);

        // Assert
        result.Should().BeTrue();
        header.Should().NotBeNull();
        header!.IsLastPacket.Should().BeTrue();
    }

    [Fact]
    public void TryParse_ShouldHandleErrorFrameFlag()
    {
        // Arrange
        var headerBytes = CreateValidHeaderBytes();
        // Set error_frame flag (bit 1) in flags field at offset 31
        headerBytes[31] = 0x02;

        // Recalculate CRC
        ushort crc = Crc16Ccitt.Calculate(headerBytes, 0, 28);
        headerBytes[28] = (byte)(crc & 0xFF);
        headerBytes[29] = (byte)((crc >> 8) & 0xFF);

        // Act
        var result = FrameHeader.TryParse(headerBytes, out var header);

        // Assert
        result.Should().BeTrue();
        header.Should().NotBeNull();
        header!.IsErrorFrame.Should().BeTrue();
    }

    [Fact]
    public void TryParse_ShouldHandleCalibrationFrameFlag()
    {
        // Arrange
        var headerBytes = CreateValidHeaderBytes();
        // Set calibration flag (bit 2) in flags field at offset 31
        headerBytes[31] = 0x04;

        // Recalculate CRC
        ushort crc = Crc16Ccitt.Calculate(headerBytes, 0, 28);
        headerBytes[28] = (byte)(crc & 0xFF);
        headerBytes[29] = (byte)((crc >> 8) & 0xFF);

        // Act
        var result = FrameHeader.TryParse(headerBytes, out var header);

        // Assert
        result.Should().BeTrue();
        header.Should().NotBeNull();
        header!.IsCalibrationFrame.Should().BeTrue();
    }

    /// <summary>
    /// Creates a valid frame header byte array with default values.
    /// 1024x1024, 14-bit, frame 0, packet 0 of 256.
    /// </summary>
    private static byte[] CreateValidHeaderBytes()
    {
        var headerBytes = new byte[32];
        var span = new Span<byte>(headerBytes);

        // Magic: 0xD7E01234 (little-endian)
        span[0] = 0x34;
        span[1] = 0x12;
        span[2] = 0xE0;
        span[3] = 0xD7;

        // Version: 1
        span[4] = 0x01;
        span[5] = 0x00;
        span[6] = 0x00;
        span[7] = 0x00;

        // Frame ID: 0
        span[8] = 0;
        span[9] = 0;
        span[10] = 0;
        span[11] = 0;

        // Packet Seq: 0
        span[12] = 0;
        span[13] = 0;

        // Total Packets: 256
        span[14] = 0;
        span[15] = 1;

        // Timestamp: 0
        span[16] = 0;
        span[17] = 0;
        span[18] = 0;
        span[19] = 0;
        span[20] = 0;
        span[21] = 0;
        span[22] = 0;
        span[23] = 0;

        // Rows: 1024
        span[24] = 0;
        span[25] = 4;

        // Cols: 1024
        span[26] = 0;
        span[27] = 4;

        // CRC will be calculated
        ushort crc = Crc16Ccitt.Calculate(headerBytes, 0, 28);
        span[28] = (byte)(crc & 0xFF);
        span[29] = (byte)((crc >> 8) & 0xFF);

        // Bit Depth: 14
        span[30] = 14;

        // Flags: 0
        span[31] = 0;

        return headerBytes;
    }
}
