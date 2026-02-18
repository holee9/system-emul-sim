using XrayDetector.Common.Dto;
using Xunit;

namespace XrayDetector.Sdk.Tests.Common.Dto;

/// <summary>
/// Specification tests for UdpCommand enumeration.
/// Tests command type definitions, serialization, and validation.
/// </summary>
public class UdpCommandTests
{
    [Fact]
    public void CommandType_Values_AreWellDefined()
    {
        // Assert all command types exist and are distinct
        Assert.Equal(0x01, (byte)UdpCommandType.Ping);
        Assert.Equal(0x02, (byte)UdpCommandType.Discover);
        Assert.Equal(0x10, (byte)UdpCommandType.Config);
        Assert.Equal(0x11, (byte)UdpCommandType.StartAcquisition);
        Assert.Equal(0x12, (byte)UdpCommandType.StopAcquisition);
        Assert.Equal(0xFF, (byte)UdpCommandType.Error);
    }

    [Fact]
    public void Ping_Command_HasCorrectCode()
    {
        // Act & Assert
        Assert.Equal(0x01, (byte)UdpCommandType.Ping);
        Assert.Equal("PING", UdpCommandType.Ping.ToString().ToUpper());
    }

    [Fact]
    public void Discover_Command_HasCorrectCode()
    {
        // Act & Assert
        Assert.Equal(0x02, (byte)UdpCommandType.Discover);
        Assert.Equal("DISCOVER", UdpCommandType.Discover.ToString().ToUpper());
    }

    [Fact]
    public void Config_Command_HasCorrectCode()
    {
        // Act & Assert
        Assert.Equal(0x10, (byte)UdpCommandType.Config);
        Assert.Equal("CONFIG", UdpCommandType.Config.ToString().ToUpper());
    }

    [Fact]
    public void StartAcquisition_Command_HasCorrectCode()
    {
        // Act & Assert
        Assert.Equal(0x11, (byte)UdpCommandType.StartAcquisition);
        Assert.Equal("STARTACQUISITION", UdpCommandType.StartAcquisition.ToString().ToUpper());
    }

    [Fact]
    public void StopAcquisition_Command_HasCorrectCode()
    {
        // Act & Assert
        Assert.Equal(0x12, (byte)UdpCommandType.StopAcquisition);
        Assert.Equal("STOPACQUISITION", UdpCommandType.StopAcquisition.ToString().ToUpper());
    }

    [Fact]
    public void Error_Command_HasCorrectCode()
    {
        // Act & Assert
        Assert.Equal(0xFF, (byte)UdpCommandType.Error);
        Assert.Equal("ERROR", UdpCommandType.Error.ToString().ToUpper());
    }

    [Fact]
    public void Constructor_WithPingCommand_CreatesCommand()
    {
        // Arrange
        var commandType = UdpCommandType.Ping;
        var payload = Array.Empty<byte>();
        const byte sequenceNumber = 1;

        // Act
        var command = new UdpCommand(commandType, payload, sequenceNumber);

        // Assert
        Assert.Equal(commandType, command.CommandType);
        Assert.Equal(payload, command.Payload);
        Assert.Equal(sequenceNumber, command.SequenceNumber);
        Assert.Equal(0, command.PayloadLength);
    }

    [Fact]
    public void Constructor_WithConfigCommand_CreatesCommand()
    {
        // Arrange
        var commandType = UdpCommandType.Config;
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        const byte sequenceNumber = 42;

        // Act
        var command = new UdpCommand(commandType, payload, sequenceNumber);

        // Assert
        Assert.Equal(commandType, command.CommandType);
        Assert.Equal(payload, command.Payload);
        Assert.Equal(sequenceNumber, command.SequenceNumber);
        Assert.Equal(4, command.PayloadLength);
    }

    [Fact]
    public void Constructor_WithNullPayload_UsesEmptyPayload()
    {
        // Arrange
        var commandType = UdpCommandType.StartAcquisition;
        byte[]? payload = null;
        const byte sequenceNumber = 100;

        // Act
        var command = new UdpCommand(commandType, payload!, sequenceNumber);

        // Assert
        Assert.NotNull(command.Payload);
        Assert.Empty(command.Payload);
        Assert.Equal(0, command.PayloadLength);
    }

    [Fact]
    public void Serialize_PingCommand_ReturnsCorrectBytes()
    {
        // Arrange
        var command = new UdpCommand(UdpCommandType.Ping, Array.Empty<byte>(), 1);
        var expected = new byte[4];
        expected[0] = 0x01; // Command type
        expected[1] = 0;     // Payload length high
        expected[2] = 0;     // Payload length low
        expected[3] = 1;     // Sequence number

        // Act
        var serialized = command.Serialize();

        // Assert
        Assert.Equal(expected, serialized);
    }

    [Fact]
    public void Serialize_ConfigCommandWithPayload_ReturnsCorrectBytes()
    {
        // Arrange
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var command = new UdpCommand(UdpCommandType.Config, payload, 42);
        var expected = new byte[8];
        expected[0] = 0x10;      // Command type
        expected[1] = 0;         // Payload length high
        expected[2] = 4;         // Payload length low
        expected[3] = 42;        // Sequence number
        expected[4] = 0x01;      // Payload[0]
        expected[5] = 0x02;      // Payload[1]
        expected[6] = 0x03;      // Payload[2]
        expected[7] = 0x04;      // Payload[3]

        // Act
        var serialized = command.Serialize();

        // Assert
        Assert.Equal(expected, serialized);
    }

    [Fact]
    public void Deserialize_PingCommand_ReturnsCommand()
    {
        // Arrange
        var data = new byte[4];
        data[0] = 0x01; // PING
        data[1] = 0;    // Payload length high
        data[2] = 0;    // Payload length low
        data[3] = 1;    // Sequence number

        // Act
        var command = UdpCommand.Deserialize(data);

        // Assert
        Assert.Equal(UdpCommandType.Ping, command.CommandType);
        Assert.Empty(command.Payload);
        Assert.Equal(1, command.SequenceNumber);
    }

    [Fact]
    public void Deserialize_ConfigCommandWithPayload_ReturnsCommand()
    {
        // Arrange
        var data = new byte[8];
        data[0] = 0x10;      // CONFIG
        data[1] = 0;         // Payload length high
        data[2] = 4;         // Payload length low
        data[3] = 42;        // Sequence number
        data[4] = 0x01;      // Payload[0]
        data[5] = 0x02;      // Payload[1]
        data[6] = 0x03;      // Payload[2]
        data[7] = 0x04;      // Payload[3]

        // Act
        var command = UdpCommand.Deserialize(data);

        // Assert
        Assert.Equal(UdpCommandType.Config, command.CommandType);
        Assert.Equal(4, command.PayloadLength);
        Assert.Equal(42, command.SequenceNumber);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, command.Payload);
    }

    [Fact]
    public void Deserialize_WithInvalidLength_ThrowsArgumentException()
    {
        // Arrange
        var invalidData = new byte[3]; // Minimum is 4 bytes (header)

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            UdpCommand.Deserialize(invalidData));
    }

    [Fact]
    public void Deserialize_WithNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            UdpCommand.Deserialize(null!));
    }

    [Fact]
    public void Deserialize_WithPayloadLengthMismatch_ThrowsArgumentException()
    {
        // Arrange
        var data = new byte[10];
        data[0] = 0x10;  // CONFIG
        data[1] = 0;     // Payload length high
        data[2] = 10;    // Payload length low (claims 10 bytes)
        data[3] = 42;    // Sequence number
        // Only 6 bytes remaining, but header claims 10

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            UdpCommand.Deserialize(data));
    }

    [Fact]
    public void Serialize_Deserialize_RoundTripProducesEquivalentCommand()
    {
        // Arrange
        var payload = new byte[] { 0xAA, 0xBB, 0xCC };
        var original = new UdpCommand(UdpCommandType.StartAcquisition, payload, 123);

        // Act
        var serialized = original.Serialize();
        var deserialized = UdpCommand.Deserialize(serialized);

        // Assert
        Assert.Equal(original.CommandType, deserialized.CommandType);
        Assert.Equal(original.SequenceNumber, deserialized.SequenceNumber);
        Assert.Equal(original.PayloadLength, deserialized.PayloadLength);
        Assert.Equal(original.Payload, deserialized.Payload);
    }

    [Fact]
    public void CreatePing_ReturnsPingCommand()
    {
        // Act
        var command = UdpCommand.CreatePing(sequenceNumber: 1);

        // Assert
        Assert.Equal(UdpCommandType.Ping, command.CommandType);
        Assert.Empty(command.Payload);
        Assert.Equal(1, command.SequenceNumber);
    }

    [Fact]
    public void CreateDiscover_ReturnsDiscoverCommand()
    {
        // Act
        var command = UdpCommand.CreateDiscover(sequenceNumber: 2);

        // Assert
        Assert.Equal(UdpCommandType.Discover, command.CommandType);
        Assert.Empty(command.Payload);
        Assert.Equal(2, command.SequenceNumber);
    }

    [Fact]
    public void CreateStartAcquisition_ReturnsStartCommand()
    {
        // Arrange
        var payload = new byte[] { 0x01, 0x02 };

        // Act
        var command = UdpCommand.CreateStartAcquisition(payload, sequenceNumber: 3);

        // Assert
        Assert.Equal(UdpCommandType.StartAcquisition, command.CommandType);
        Assert.Equal(payload, command.Payload);
        Assert.Equal(3, command.SequenceNumber);
    }

    [Fact]
    public void CreateStopAcquisition_ReturnsStopCommand()
    {
        // Act
        var command = UdpCommand.CreateStopAcquisition(sequenceNumber: 4);

        // Assert
        Assert.Equal(UdpCommandType.StopAcquisition, command.CommandType);
        Assert.Empty(command.Payload);
        Assert.Equal(4, command.SequenceNumber);
    }

    [Fact]
    public void HeaderSize_ReturnsConstantSize()
    {
        // Act & Assert
        Assert.Equal(4, UdpCommand.HeaderSize);
    }

    [Fact]
    public void MaxPayloadSize_ReturnsMaximumAllowed()
    {
        // Act & Assert
        Assert.Equal(65535, UdpCommand.MaxPayloadSize); // UInt16.MaxValue
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var payload = new byte[] { 0x01, 0x02 };
        var command1 = new UdpCommand(UdpCommandType.Config, payload, 42);
        var command2 = new UdpCommand(UdpCommandType.Config, payload, 42);

        // Act & Assert
        Assert.Equal(command1, command2);
        Assert.Equal(command1.GetHashCode(), command2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentSequenceNumber_ReturnsFalse()
    {
        // Arrange
        var command1 = new UdpCommand(UdpCommandType.Ping, Array.Empty<byte>(), 1);
        var command2 = new UdpCommand(UdpCommandType.Ping, Array.Empty<byte>(), 2);

        // Act & Assert
        Assert.NotEqual(command1, command2);
    }
}
