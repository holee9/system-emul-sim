using XrayDetector.Common.Dto;
using Xunit;

namespace XrayDetector.Sdk.Tests.Common.Dto;

/// <summary>
/// Specification tests for PacketHeader value object.
/// Tests packet header serialization, deserialization, and CRC validation.
/// </summary>
public class PacketHeaderTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesHeader()
    {
        // Arrange
        const ushort packetId = 1;
        const ushort totalPackets = 100;
        const ushort currentPacket = 0;
        const ushort crc16 = 0x1234;

        // Act
        var header = new PacketHeader(packetId, totalPackets, currentPacket, crc16);

        // Assert
        Assert.Equal(packetId, header.PacketId);
        Assert.Equal(totalPackets, header.TotalPackets);
        Assert.Equal(currentPacket, header.CurrentPacket);
        Assert.Equal(crc16, header.Crc16);
    }

    [Fact]
    public void Constructor_CurrentPacketExceedsTotal_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PacketHeader(1, 10, 15, 0)); // currentPacket >= totalPackets
    }

    [Theory]
    [InlineData(0, 100, 0)]    // First packet
    [InlineData(1, 100, 0)]    // First packet of frame 1
    [InlineData(1, 100, 99)]   // Last packet of frame 1
    [InlineData(42, 1000, 0)]  // First packet of frame 42
    [InlineData(42, 1000, 999)] // Last packet of frame 42
    public void Constructor_AcceptsValidRanges(ushort packetId, ushort totalPackets, ushort currentPacket)
    {
        // Act
        var header = new PacketHeader(packetId, totalPackets, currentPacket, 0x1234);

        // Assert
        Assert.Equal(packetId, header.PacketId);
        Assert.Equal(totalPackets, header.TotalPackets);
        Assert.Equal(currentPacket, header.CurrentPacket);
    }

    [Fact]
    public void IsFirstPacket_ReturnsTrueForZeroCurrentPacket()
    {
        // Arrange
        var header = new PacketHeader(1, 100, 0, 0x1234);

        // Act & Assert
        Assert.True(header.IsFirstPacket);
    }

    [Fact]
    public void IsFirstPacket_ReturnsFalseForNonZeroCurrentPacket()
    {
        // Arrange
        var header = new PacketHeader(1, 100, 5, 0x1234);

        // Act & Assert
        Assert.False(header.IsFirstPacket);
    }

    [Fact]
    public void IsLastPacket_ReturnsTrueForLastPacket()
    {
        // Arrange
        var header = new PacketHeader(1, 100, 99, 0x1234);

        // Act & Assert
        Assert.True(header.IsLastPacket);
    }

    [Fact]
    public void IsLastPacket_ReturnsFalseForNonLastPacket()
    {
        // Arrange
        var header = new PacketHeader(1, 100, 50, 0x1234);

        // Act & Assert
        Assert.False(header.IsLastPacket);
    }

    [Fact]
    public void IsLastPacket_ReturnsTrueForSinglePacketFrame()
    {
        // Arrange
        var header = new PacketHeader(1, 1, 0, 0x1234);

        // Act & Assert
        Assert.True(header.IsFirstPacket);
        Assert.True(header.IsLastPacket);
    }

    [Fact]
    public void Serialize_ReturnsCorrectByteArray()
    {
        // Arrange
        var header = new PacketHeader(0x1234, 100, 50, 0xABCD);
        var expected = new byte[8];
        // PacketId: 0x1234
        expected[0] = 0x12;
        expected[1] = 0x34;
        // TotalPackets: 100
        expected[2] = 0;
        expected[3] = 100;
        // CurrentPacket: 50
        expected[4] = 0;
        expected[5] = 50;
        // Crc16: 0xABCD
        expected[6] = 0xAB;
        expected[7] = 0xCD;

        // Act
        var serialized = header.Serialize();

        // Assert
        Assert.Equal(expected, serialized);
    }

    [Fact]
    public void Deserialize_WithValidBytes_ReturnsHeader()
    {
        // Arrange
        var data = new byte[8];
        // PacketId: 0x1234
        data[0] = 0x12;
        data[1] = 0x34;
        // TotalPackets: 100
        data[2] = 0;
        data[3] = 100;
        // CurrentPacket: 50
        data[4] = 0;
        data[5] = 50;
        // Crc16: 0xABCD
        data[6] = 0xAB;
        data[7] = 0xCD;

        // Act
        var header = PacketHeader.Deserialize(data);

        // Assert
        Assert.Equal(0x1234, header.PacketId);
        Assert.Equal(100, header.TotalPackets);
        Assert.Equal(50, header.CurrentPacket);
        Assert.Equal(0xABCD, header.Crc16);
    }

    [Fact]
    public void Deserialize_WithInvalidLength_ThrowsArgumentException()
    {
        // Arrange
        var invalidData = new byte[7]; // Too short

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PacketHeader.Deserialize(invalidData));
    }

    [Fact]
    public void Deserialize_WithNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            PacketHeader.Deserialize(null!));
    }

    [Fact]
    public void Serialize_Deserialize_RoundTripProducesEquivalentHeader()
    {
        // Arrange
        var original = new PacketHeader(0xABCD, 250, 125, 0x1234);

        // Act
        var serialized = original.Serialize();
        var deserialized = PacketHeader.Deserialize(serialized);

        // Assert
        Assert.Equal(original.PacketId, deserialized.PacketId);
        Assert.Equal(original.TotalPackets, deserialized.TotalPackets);
        Assert.Equal(original.CurrentPacket, deserialized.CurrentPacket);
        Assert.Equal(original.Crc16, deserialized.Crc16);
    }

    [Fact]
    public void ComputeCrc16_CCITT_ProducesKnownValues()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var crc = PacketHeader.ComputeCrc16Ccitt(data);

        // Assert - Known CRC-16/CCITT-FALSE value for this data
        // CRC-16/CCITT-FALSE: Poly=0x1021, Init=0xFFFF, RefIn=false, RefOut=false
        // For bytes 01 02 03 04 05, computed CRC is 0x9304
        Assert.Equal(0x9304, crc);
    }

    [Fact]
    public void ComputeCrc16_CCITT_EmptyData_ReturnsInitialValue()
    {
        // Arrange
        var data = Array.Empty<byte>();

        // Act
        var crc = PacketHeader.ComputeCrc16Ccitt(data);

        // Assert
        Assert.Equal(0xFFFF, crc); // Initial value
    }

    [Fact]
    public void WithCrc16_ReturnsNewInstanceWithUpdatedCrc()
    {
        // Arrange
        var original = new PacketHeader(1, 100, 50, 0x0000);

        // Act
        var updated = original.WithCrc16(0xABCD);

        // Assert - immutability verified
        Assert.NotSame(original, updated);
        Assert.Equal(0x0000, original.Crc16);
        Assert.Equal(0xABCD, updated.Crc16);

        // Other fields preserved
        Assert.Equal(original.PacketId, updated.PacketId);
        Assert.Equal(original.TotalPackets, updated.TotalPackets);
        Assert.Equal(original.CurrentPacket, updated.CurrentPacket);
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var header1 = new PacketHeader(1, 100, 50, 0x1234);
        var header2 = new PacketHeader(1, 100, 50, 0x1234);

        // Act & Assert
        Assert.Equal(header1, header2);
        Assert.Equal(header1.GetHashCode(), header2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentCrc_ReturnsFalse()
    {
        // Arrange
        var header1 = new PacketHeader(1, 100, 50, 0x1234);
        var header2 = new PacketHeader(1, 100, 50, 0x5678);

        // Act & Assert
        Assert.NotEqual(header1, header2);
    }

    [Fact]
    public void Size_ReturnsConstantHeaderSize()
    {
        // Act & Assert
        Assert.Equal(8, PacketHeader.Size);
    }
}
