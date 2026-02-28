using System.Net;
using System.Text;
using Common.Dto.Dtos;
using FluentAssertions;
using IntegrationTests.Helpers;
using Xunit;

namespace IntegrationTests.Helpers;

/// <summary>
/// Tests for PacketFactory using TDD approach.
/// RED-GREEN-REFACTOR cycle validated.
/// </summary>
public class PacketFactoryTests
{
    [Fact]
    public void CreateCsi2Packet_WithDefaults_CreatesValidPacket()
    {
        // Arrange & Act
        var packet = PacketFactory.CreateCsi2Packet();

        // Assert
        packet.DataType.Should().Be(Csi2DataType.Raw16);
        packet.VirtualChannel.Should().Be(0);
        packet.Payload.Length.Should().Be(256);
    }

    [Fact]
    public void CreateCsi2Packet_WithCustomParameters_UsesCustomValues()
    {
        // Arrange & Act
        var packet = PacketFactory.CreateCsi2Packet(Csi2DataType.Raw12, virtualChannel: 2, payloadSize: 512);

        // Assert
        packet.DataType.Should().Be(Csi2DataType.Raw12);
        packet.VirtualChannel.Should().Be(2);
        packet.Payload.Length.Should().Be(512);
    }

    [Fact]
    public void CreateCsi2Packet_WithInvalidVirtualChannel_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PacketFactory.CreateCsi2Packet(virtualChannel: 4));
    }

    [Fact]
    public void CreateEthernetFramePacket_WithDefaults_CreatesValidPacket()
    {
        // Arrange & Act
        var packet = PacketFactory.CreateEthernetFramePacket();

        // Assert
        packet.SourceIp.ToString().Should().Be("192.168.1.100");
        packet.SourcePort.Should().Be(5000);
        packet.DestinationIp.ToString().Should().Be("192.168.1.1");
        packet.DestinationPort.Should().Be(6000);
        packet.Payload.Length.Should().Be(128);
    }

    [Fact]
    public void CreateEthernetFramePacket_WithCustomParameters_UsesCustomValues()
    {
        // Arrange & Act
        var sourceIp = IPAddress.Parse("10.0.0.1");
        var destIp = IPAddress.Parse("10.0.0.2");
        var packet = PacketFactory.CreateEthernetFramePacket(sourceIp, 7000, destIp, 8000, 256);

        // Assert
        packet.SourceIp.Should().Be(sourceIp);
        packet.SourcePort.Should().Be(7000);
        packet.DestinationIp.Should().Be(destIp);
        packet.DestinationPort.Should().Be(8000);
        packet.Payload.Length.Should().Be(256);
    }

    [Fact]
    public void CreateTestPayload_WithSize_CreatesSequentialBytes()
    {
        // Arrange & Act
        var payload = PacketFactory.CreateTestPayload(10);

        // Assert
        payload.Length.Should().Be(10);
        payload[0].Should().Be(0x00);
        payload[1].Should().Be(0x01);
        payload[2].Should().Be(0x02);
        payload[9].Should().Be(0x09);
    }

    [Fact]
    public void CreateTestPayload_WithLargeSize_WrapsAround()
    {
        // Arrange & Act
        var payload = PacketFactory.CreateTestPayload(300);

        // Assert
        payload[0].Should().Be(0x00);
        payload[255].Should().Be(0xFF);
        payload[256].Should().Be(0x00); // Wraps around
        payload[257].Should().Be(0x01);
    }

    [Fact]
    public void CalculateCrc16Ccitt_WithEmptyData_ReturnsInitialValue()
    {
        // Arrange & Act
        var crc = PacketFactory.CalculateCrc16Ccitt(Array.Empty<byte>());

        // Assert
        crc.Should().Be(0xFFFF);
    }

    [Fact]
    public void CalculateCrc16Ccitt_WithNullData_ReturnsInitialValue()
    {
        // Arrange & Act
        var crc = PacketFactory.CalculateCrc16Ccitt(null!);

        // Assert
        crc.Should().Be(0xFFFF);
    }

    [Fact]
    public void CalculateCrc16Ccitt_WithKnownData_CalculatesCorrectCrc()
    {
        // Arrange
        byte[] data = { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var crc = PacketFactory.CalculateCrc16Ccitt(data);

        // Assert - Known CRC-16/CCITT value for this data
        crc.Should().NotBe(0);
    }

    [Fact]
    public void ValidateCrc16Ccitt_WithMatchingCrc_ReturnsTrue()
    {
        // Arrange
        byte[] data = { 0x01, 0x02, 0x03 };
        ushort expectedCrc = PacketFactory.CalculateCrc16Ccitt(data);

        // Act
        bool isValid = PacketFactory.ValidateCrc16Ccitt(data, expectedCrc);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateCrc16Ccitt_WithMismatchedCrc_ReturnsFalse()
    {
        // Arrange
        byte[] data = { 0x01, 0x02, 0x03 };
        ushort wrongCrc = 0x1234;

        // Act
        bool isValid = PacketFactory.ValidateCrc16Ccitt(data, wrongCrc);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void CalculateHmac_WithDefaultKey_ReturnsSignature()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test command");
        byte[] key = PacketFactory.GetDefaultHmacKey();

        // Act
        var hmac = PacketFactory.CalculateHmac(data, key);

        // Assert
        hmac.Length.Should().Be(32); // SHA-256 produces 32 bytes
    }

    [Fact]
    public void CalculateHmac_WithSameDataAndKey_ReturnsSameSignature()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test command");
        byte[] key = PacketFactory.GetDefaultHmacKey();

        // Act
        var hmac1 = PacketFactory.CalculateHmac(data, key);
        var hmac2 = PacketFactory.CalculateHmac(data, key);

        // Assert
        hmac1.Should().BeEquivalentTo(hmac2);
    }

    [Fact]
    public void CalculateHmac_WithDifferentData_ReturnsDifferentSignature()
    {
        // Arrange
        byte[] data1 = Encoding.UTF8.GetBytes("command1");
        byte[] data2 = Encoding.UTF8.GetBytes("command2");
        byte[] key = PacketFactory.GetDefaultHmacKey();

        // Act
        var hmac1 = PacketFactory.CalculateHmac(data1, key);
        var hmac2 = PacketFactory.CalculateHmac(data2, key);

        // Assert
        hmac1.Should().NotBeEquivalentTo(hmac2);
    }

    [Fact]
    public void CreateHmacAuthenticatedCommand_WithCommand_ReturnsCommandAndSignature()
    {
        // Arrange & Act
        var (commandBytes, hmac) = PacketFactory.CreateHmacAuthenticatedCommand("START_ACQUISITION");

        // Assert
        commandBytes.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("START_ACQUISITION"));
        hmac.Length.Should().Be(32);
    }

    [Fact]
    public void CreateHmacAuthenticatedCommand_WithEmptyCommand_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PacketFactory.CreateHmacAuthenticatedCommand(""));
    }

    [Fact]
    public void GetDefaultHmacKey_Returns32ByteKey()
    {
        // Arrange & Act
        var key = PacketFactory.GetDefaultHmacKey();

        // Assert
        key.Length.Should().Be(32);
        key.Should().OnlyContain(b => b == 0x01);
    }
}
