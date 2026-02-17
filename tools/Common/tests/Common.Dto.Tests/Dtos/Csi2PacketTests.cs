using System.Text.Json;
using Common.Dto.Dtos;
using FluentAssertions;
using Xunit;

namespace Common.Dto.Tests.Dtos;

/// <summary>
/// Tests for Csi2Packet DTO specification.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including Csi2Packet.
/// </summary>
public class Csi2PacketTests
{
    [Fact]
    public void Csi2Packet_shall_be_immutable_record()
    {
        // Arrange & Act
        var packet = new Csi2Packet(Csi2DataType.Raw16, 0, new byte[100]);

        // Assert
        packet.Should().NotBeNull();
        packet.GetType().IsClass.Should().BeTrue();
        packet.GetType().IsValueType.Should().BeFalse();
        packet.GetType().Name.Should().Be("Csi2Packet");
    }

    [Fact]
    public void Csi2Packet_should_have_required_properties()
    {
        // Arrange
        var expectedDataType = Csi2DataType.Raw16;
        var expectedVirtualChannel = 0;
        var expectedPayload = new byte[] { 1, 2, 3, 4 };

        // Act
        var packet = new Csi2Packet(expectedDataType, expectedVirtualChannel, expectedPayload);

        // Assert
        packet.DataType.Should().Be(expectedDataType);
        packet.VirtualChannel.Should().Be(expectedVirtualChannel);
        packet.Payload.Should().BeSameAs(expectedPayload);
    }

    [Fact]
    public void Csi2Packet_should_validate_virtual_channel_range()
    {
        // Arrange
        var payload = new byte[100];

        // Act
        var act = () => new Csi2Packet(Csi2DataType.Raw16, 4, payload);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("virtualChannel");
    }

    [Fact]
    public void Csi2Packet_should_validate_payload_is_not_null()
    {
        // Act
        var act = () => new Csi2Packet(Csi2DataType.Raw16, 0, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("payload");
    }

    [Fact]
    public void Csi2Packet_should_validate_payload_is_not_empty()
    {
        // Arrange
        var payload = Array.Empty<byte>();

        // Act
        var act = () => new Csi2Packet(Csi2DataType.Raw16, 0, payload);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("payload");
    }

    [Fact]
    public void Csi2Packet_should_be_serializable_to_json()
    {
        // Arrange
        var packet = new Csi2Packet(Csi2DataType.Raw16, 0, new byte[] { 1, 2, 3, 4 });

        // Act
        var json = JsonSerializer.Serialize(packet);
        var deserialized = JsonSerializer.Deserialize<Csi2Packet>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.DataType.Should().Be(packet.DataType);
        deserialized.VirtualChannel.Should().Be(packet.VirtualChannel);
        deserialized.Payload.Should().BeEquivalentTo(packet.Payload);
    }

    [Fact]
    public void Csi2Packet_should_override_ToString()
    {
        // Arrange
        var packet = new Csi2Packet(Csi2DataType.Raw16, 1, new byte[] { 1, 2, 3, 4 });

        // Act
        var result = packet.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Raw16");
        result.Should().Contain("VirtualChannel");
        result.Should().Contain("1");
        result.Should().Contain("PayloadLength");
    }

    [Fact]
    public void Csi2Packet_should_implement_value_equality()
    {
        // Arrange
        var payload = new byte[] { 1, 2, 3, 4 };
        var packet1 = new Csi2Packet(Csi2DataType.Raw16, 0, payload);
        var packet2 = new Csi2Packet(Csi2DataType.Raw16, 0, payload);
        var packet3 = new Csi2Packet(Csi2DataType.Raw8, 0, payload);

        // Act & Assert
        packet1.Should().Be(packet2);
        packet1.Should().NotBe(packet3);
        (packet1 == packet2).Should().BeTrue();
        (packet1 == packet3).Should().BeFalse();
    }

    [Fact]
    public void Csi2Packet_should_support_with_expression()
    {
        // Arrange
        var original = new Csi2Packet(Csi2DataType.Raw16, 0, new byte[] { 1, 2, 3, 4 });

        // Act
        var modified = original with { VirtualChannel = 1 };

        // Assert
        modified.VirtualChannel.Should().Be(1);
        modified.DataType.Should().Be(original.DataType);
        modified.Payload.Should().BeSameAs(original.Payload);
    }
}
