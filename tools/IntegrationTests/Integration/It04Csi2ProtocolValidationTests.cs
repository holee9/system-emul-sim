using Common.Dto.Dtos;
using FluentAssertions;
using IntegrationTests.Helpers;
using Xunit;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-04: CSI-2 protocol validation tests.
/// Validates CSI-2 packet structure, magic numbers, CRC, and data types.
/// Uses PacketFactory for packet generation and validation.
/// </summary>
public class It04Csi2ProtocolValidationTests
{
    [Fact]
    public void Csi2Packet_MagicNumber_VerifiesCorrectValue()
    {
        // Arrange & Act
        uint magic = PacketFactory.Csi2Magic;

        // Assert - CSI-2 magic should be 0xD7E01234
        magic.Should().Be(0xD7E01234);
    }

    [Fact]
    public void Csi2Packet_Raw16DataType_DefaultCreation()
    {
        // Arrange & Act
        var packet = PacketFactory.CreateCsi2Packet();

        // Assert
        packet.DataType.Should().Be(Csi2DataType.Raw16);
        packet.VirtualChannel.Should().Be(0);
        packet.Payload.Length.Should().Be(256);
    }

    [Fact]
    public void Csi2Packet_AllDataTypes_Supported()
    {
        // Arrange & Act
        var raw8 = PacketFactory.CreateCsi2Packet(Csi2DataType.Raw8);
        var raw10 = PacketFactory.CreateCsi2Packet(Csi2DataType.Raw10);
        var raw12 = PacketFactory.CreateCsi2Packet(Csi2DataType.Raw12);
        var raw14 = PacketFactory.CreateCsi2Packet(Csi2DataType.Raw14);
        var raw16 = PacketFactory.CreateCsi2Packet(Csi2DataType.Raw16);

        // Assert - All data types should create valid packets
        raw8.DataType.Should().Be(Csi2DataType.Raw8);
        raw10.DataType.Should().Be(Csi2DataType.Raw10);
        raw12.DataType.Should().Be(Csi2DataType.Raw12);
        raw14.DataType.Should().Be(Csi2DataType.Raw14);
        raw16.DataType.Should().Be(Csi2DataType.Raw16);
    }

    [Fact]
    public void Csi2Packet_VirtualChannelRange_0to3_Valid()
    {
        // Arrange & Act
        var vc0 = PacketFactory.CreateCsi2Packet(virtualChannel: 0);
        var vc1 = PacketFactory.CreateCsi2Packet(virtualChannel: 1);
        var vc2 = PacketFactory.CreateCsi2Packet(virtualChannel: 2);
        var vc3 = PacketFactory.CreateCsi2Packet(virtualChannel: 3);

        // Assert
        vc0.VirtualChannel.Should().Be(0);
        vc1.VirtualChannel.Should().Be(1);
        vc2.VirtualChannel.Should().Be(2);
        vc3.VirtualChannel.Should().Be(3);
    }

    [Fact]
    public void Csi2Packet_VirtualChannel_Invalid_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PacketFactory.CreateCsi2Packet(virtualChannel: 4));

        Assert.Throws<ArgumentException>(() =>
            PacketFactory.CreateCsi2Packet(virtualChannel: -1));
    }

    [Fact]
    public void Csi2Packet_PayloadSize_Variable_Supported()
    {
        // Arrange & Act
        var small = PacketFactory.CreateCsi2Packet(payloadSize: 64);
        var medium = PacketFactory.CreateCsi2Packet(payloadSize: 512);
        var large = PacketFactory.CreateCsi2Packet(payloadSize: 4096);

        // Assert
        small.Payload.Length.Should().Be(64);
        medium.Payload.Length.Should().Be(512);
        large.Payload.Length.Should().Be(4096);
    }

    [Fact]
    public void Csi2Packet_TestPayload_SequentialBytes()
    {
        // Arrange & Act
        var payload = PacketFactory.CreateTestPayload(16);

        // Assert - Payload should contain sequential bytes 0x00-0x0F
        payload[0].Should().Be(0x00);
        payload[1].Should().Be(0x01);
        payload[15].Should().Be(0x0F);
    }

    [Fact]
    public void Crc16Ccitt_KnownVectors_VerifiesCorrectness()
    {
        // Arrange - Known test vectors for CRC-16/CCITT
        byte[] empty = Array.Empty<byte>();
        byte[] singleByte = { 0x00 };
        byte[] testBytes = { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var crcEmpty = PacketFactory.CalculateCrc16Ccitt(empty);
        var crcSingle = PacketFactory.CalculateCrc16Ccitt(singleByte);
        var crcTest = PacketFactory.CalculateCrc16Ccitt(testBytes);

        // Assert
        crcEmpty.Should().Be(0xFFFF); // Initial value
        crcTest.Should().NotBe(0); // Should have non-zero CRC
    }

    [Fact]
    public void Crc16Ccitt_DataChange_DetectsCorruption()
    {
        // Arrange
        byte[] original = { 0x01, 0x02, 0x03, 0x04 };
        byte[] corrupted = { 0x01, 0x02, 0xFF, 0x04 }; // One byte changed

        // Act
        var crcOriginal = PacketFactory.CalculateCrc16Ccitt(original);
        var crcCorrupted = PacketFactory.CalculateCrc16Ccitt(corrupted);

        // Assert - CRC should detect single-bit change
        crcCorrupted.Should().NotBe(crcOriginal);
    }

    [Fact]
    public void Crc16Ccitt_Validation_WorksCorrectly()
    {
        // Arrange
        byte[] data = { 0xAA, 0xBB, 0xCC, 0xDD };
        ushort correctCrc = PacketFactory.CalculateCrc16Ccitt(data);
        ushort wrongCrc = 0x1234;

        // Act & Assert
        PacketFactory.ValidateCrc16Ccitt(data, correctCrc).Should().BeTrue();
        PacketFactory.ValidateCrc16Ccitt(data, wrongCrc).Should().BeFalse();
    }

    [Fact]
    public void EthernetFrame_DefaultConfiguration_Valid()
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
    public void EthernetFrame_CustomConfiguration_UsesCustomValues()
    {
        // Arrange
        var sourceIp = System.Net.IPAddress.Parse("10.0.0.50");
        var destIp = System.Net.IPAddress.Parse("10.0.0.1");

        // Act
        var packet = PacketFactory.CreateEthernetFramePacket(
            sourceIp, 7000, destIp, 8000, 512);

        // Assert
        packet.SourceIp.Should().Be(sourceIp);
        packet.SourcePort.Should().Be(7000);
        packet.DestinationIp.Should().Be(destIp);
        packet.DestinationPort.Should().Be(8000);
        packet.Payload.Length.Should().Be(512);
    }

    [Fact]
    public void HmacAuthenticatedCommand_ValidSignature_Verifies()
    {
        // Arrange & Act
        var (commandBytes, hmac) = PacketFactory.CreateHmacAuthenticatedCommand("START_ACQUISITION");

        // Assert
        commandBytes.Should().NotBeEmpty();
        hmac.Length.Should().Be(32); // SHA-256 produces 32 bytes
    }

    [Fact]
    public void HmacAuthenticatedCommand_SameCommand_SameSignature()
    {
        // Arrange
        const string command = "TEST_COMMAND";

        // Act
        var (cmd1, sig1) = PacketFactory.CreateHmacAuthenticatedCommand(command);
        var (cmd2, sig2) = PacketFactory.CreateHmacAuthenticatedCommand(command);

        // Assert
        cmd1.Should().BeEquivalentTo(cmd2);
        sig1.Should().BeEquivalentTo(sig2);
    }

    [Fact]
    public void HmacAuthenticatedCommand_DifferentCommands_DifferentSignatures()
    {
        // Arrange & Act
        var (cmd1, sig1) = PacketFactory.CreateHmacAuthenticatedCommand("COMMAND_1");
        var (cmd2, sig2) = PacketFactory.CreateHmacAuthenticatedCommand("COMMAND_2");

        // Assert - Commands should be different
        cmd1.Should().NotBeEquivalentTo(cmd2);
        // Signatures should also be different
        sig1.Should().NotBeEquivalentTo(sig2);
    }

    [Fact]
    public void HmacTestHelper_ValidTestVector_ValidatesCorrectly()
    {
        // Arrange & Act
        var vector = HMACTestHelper.GetValidTestVector();

        // Assert
        vector.IsValid.Should().BeTrue();
        vector.Validate().Should().BeTrue();
    }

    [Fact]
    public void HmacTestHelper_InvalidTestVector_FailsValidation()
    {
        // Arrange & Act
        var vector = HMACTestHelper.GetInvalidTestVector();

        // Assert
        vector.IsValid.Should().BeFalse();
        vector.Validate().Should().BeFalse();
    }

    [Fact]
    public void HmacTestHelper_CustomCommand_CreatesValidVector()
    {
        // Arrange & Act
        var vector = HMACTestHelper.CreateTestVector("CUSTOM_TEST_CMD");

        // Assert
        vector.IsValid.Should().BeTrue();
        vector.Validate().Should().BeTrue();
        vector.Data.Should().BeEquivalentTo(System.Text.Encoding.UTF8.GetBytes("CUSTOM_TEST_CMD"));
    }

    [Fact]
    public void Csi2Packet_LargePayload_HandlesCorrectly()
    {
        // Arrange & Act - Create packet with maximum realistic payload
        var packet = PacketFactory.CreateCsi2Packet(payloadSize: 8192);

        // Assert
        packet.Payload.Length.Should().Be(8192);
        packet.Payload[0].Should().Be(0x00);
        packet.Payload[8191].Should().Be((byte)(8191 & 0xFF)); // Wraps around
    }
}
