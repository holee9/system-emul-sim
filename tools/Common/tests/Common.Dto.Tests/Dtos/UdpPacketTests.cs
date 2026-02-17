using System.Text.Json;
using System.Net;
using Common.Dto.Dtos;
using FluentAssertions;
using Xunit;

namespace Common.Dto.Tests.Dtos;

/// <summary>
/// Tests for UdpPacket DTO specification.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including UdpPacket.
/// </summary>
public class UdpPacketTests
{
    [Fact]
    public void UdpPacket_shall_be_immutable_record()
    {
        // Arrange & Act
        var packet = new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8080,
            IPAddress.Parse("192.168.1.1"),
            5000,
            new byte[100]);

        // Assert
        packet.Should().NotBeNull();
        packet.GetType().IsClass.Should().BeTrue();
        packet.GetType().IsValueType.Should().BeFalse();
        packet.GetType().Name.Should().Be("UdpPacket");
    }

    [Fact]
    public void UdpPacket_should_have_required_properties()
    {
        // Arrange
        var expectedSourceIp = IPAddress.Parse("192.168.1.100");
        var expectedSourcePort = 8080;
        var expectedDestIp = IPAddress.Parse("192.168.1.1");
        var expectedDestPort = 5000;
        var expectedPayload = new byte[] { 1, 2, 3, 4 };

        // Act
        var packet = new UdpPacket(
            expectedSourceIp,
            expectedSourcePort,
            expectedDestIp,
            expectedDestPort,
            expectedPayload);

        // Assert
        packet.SourceIp.Should().Be(expectedSourceIp);
        packet.SourcePort.Should().Be(expectedSourcePort);
        packet.DestinationIp.Should().Be(expectedDestIp);
        packet.DestinationPort.Should().Be(expectedDestPort);
        packet.Payload.Should().BeSameAs(expectedPayload);
    }

    [Fact]
    public void UdpPacket_should_validate_source_port_range()
    {
        // Arrange
        var payload = new byte[100];

        // Act
        var act = () => new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            0,
            IPAddress.Parse("192.168.1.1"),
            5000,
            payload);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sourcePort");
    }

    [Fact]
    public void UdpPacket_should_validate_destination_port_range()
    {
        // Arrange
        var payload = new byte[100];

        // Act
        var act = () => new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8080,
            IPAddress.Parse("192.168.1.1"),
            65536,
            payload);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("destinationPort");
    }

    [Fact]
    public void UdpPacket_should_validate_source_ip_is_not_null()
    {
        // Arrange
        var payload = new byte[100];

        // Act
        var act = () => new UdpPacket(
            null!,
            8080,
            IPAddress.Parse("192.168.1.1"),
            5000,
            payload);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sourceIp");
    }

    [Fact]
    public void UdpPacket_should_validate_destination_ip_is_not_null()
    {
        // Arrange
        var payload = new byte[100];

        // Act
        var act = () => new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8080,
            null!,
            5000,
            payload);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("destinationIp");
    }

    [Fact]
    public void UdpPacket_should_validate_payload_is_not_null()
    {
        // Act
        var act = () => new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8080,
            IPAddress.Parse("192.168.1.1"),
            5000,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("payload");
    }

    [Fact]
    public void UdpPacket_should_be_serializable_to_json()
    {
        // Arrange
        var packet = new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8080,
            IPAddress.Parse("192.168.1.1"),
            5000,
            new byte[] { 1, 2, 3, 4 });

        // Act
        var json = JsonSerializer.Serialize(packet);
        var deserialized = JsonSerializer.Deserialize<UdpPacket>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.SourceIp.ToString().Should().Be(packet.SourceIp.ToString());
        deserialized.SourcePort.Should().Be(packet.SourcePort);
        deserialized.DestinationIp.ToString().Should().Be(packet.DestinationIp.ToString());
        deserialized.DestinationPort.Should().Be(packet.DestinationPort);
        deserialized.Payload.Should().BeEquivalentTo(packet.Payload);
    }

    [Fact]
    public void UdpPacket_should_override_ToString()
    {
        // Arrange
        var packet = new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8080,
            IPAddress.Parse("192.168.1.1"),
            5000,
            new byte[] { 1, 2, 3, 4 });

        // Act
        var result = packet.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("192.168.1.100");
        result.Should().Contain("8080");
        result.Should().Contain("192.168.1.1");
        result.Should().Contain("5000");
    }

    [Fact]
    public void UdpPacket_should_implement_value_equality()
    {
        // Arrange
        var payload = new byte[] { 1, 2, 3, 4 };
        var packet1 = new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8080,
            IPAddress.Parse("192.168.1.1"),
            5000,
            payload);
        var packet2 = new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8080,
            IPAddress.Parse("192.168.1.1"),
            5000,
            payload);
        var packet3 = new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8081,
            IPAddress.Parse("192.168.1.1"),
            5000,
            payload);

        // Act & Assert
        packet1.Should().Be(packet2);
        packet1.Should().NotBe(packet3);
        (packet1 == packet2).Should().BeTrue();
        (packet1 == packet3).Should().BeFalse();
    }

    [Fact]
    public void UdpPacket_should_support_with_expression()
    {
        // Arrange
        var original = new UdpPacket(
            IPAddress.Parse("192.168.1.100"),
            8080,
            IPAddress.Parse("192.168.1.1"),
            5000,
            new byte[] { 1, 2, 3, 4 });

        // Act
        var modified = original with { SourcePort = 8081 };

        // Assert
        modified.SourcePort.Should().Be(8081);
        modified.SourceIp.Should().Be(original.SourceIp);
        modified.Payload.Should().BeSameAs(original.Payload);
    }
}
