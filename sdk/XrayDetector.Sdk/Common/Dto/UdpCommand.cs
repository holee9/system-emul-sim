namespace XrayDetector.Common.Dto;

/// <summary>
/// UDP command types for detector communication.
/// </summary>
public enum UdpCommandType : byte
{
    /// <summary>Ping request to check device availability.</summary>
    Ping = 0x01,

    /// <summary>Discover devices on the network.</summary>
    Discover = 0x02,

    /// <summary>Configure detector parameters.</summary>
    Config = 0x10,

    /// <summary>Start frame acquisition.</summary>
    StartAcquisition = 0x11,

    /// <summary>Stop frame acquisition.</summary>
    StopAcquisition = 0x12,

    /// <summary>Error response.</summary>
    Error = 0xFF
}

/// <summary>
/// Value object representing a UDP command message.
/// Provides serialization and deserialization for command protocol.
/// </summary>
public sealed class UdpCommand : IEquatable<UdpCommand>
{
    /// <summary>Command type.</summary>
    public UdpCommandType CommandType { get; }

    /// <summary>Command payload data.</summary>
    public byte[] Payload { get; }

    /// <summary>Sequence number for request/response correlation.</summary>
    public byte SequenceNumber { get; }

    /// <summary>Payload length in bytes.</summary>
    public int PayloadLength => Payload.Length;

    /// <summary>Constant header size in bytes.</summary>
    public const int HeaderSize = 4;

    /// <summary>Maximum payload size (UInt16.MaxValue).</summary>
    public const int MaxPayloadSize = UInt16.MaxValue;

    /// <summary>
    /// Creates a new UdpCommand instance.
    /// </summary>
    public UdpCommand(UdpCommandType commandType, byte[] payload, byte sequenceNumber)
    {
        CommandType = commandType;
        Payload = payload ?? Array.Empty<byte>();
        SequenceNumber = sequenceNumber;
    }

    /// <summary>Serializes command to byte array (big-endian).</summary>
    public byte[] Serialize()
    {
        var result = new byte[HeaderSize + Payload.Length];
        result[0] = (byte)CommandType;
        result[1] = (byte)(Payload.Length >> 8);
        result[2] = (byte)Payload.Length;
        result[3] = SequenceNumber;
        Buffer.BlockCopy(Payload, 0, result, HeaderSize, Payload.Length);
        return result;
    }

    /// <summary>Deserializes command from byte array.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when data length is less than HeaderSize (4 bytes) or payload length mismatch.
    /// </exception>
    public static UdpCommand Deserialize(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length < HeaderSize)
            throw new ArgumentException($"Data must be at least {HeaderSize} bytes.", nameof(data));

        var commandType = (UdpCommandType)data[0];
        var payloadLength = (ushort)((data[1] << 8) | data[2]);
        var sequenceNumber = data[3];

        if (data.Length < HeaderSize + payloadLength)
            throw new ArgumentException(
                $"Data length {data.Length} is less than expected {HeaderSize + payloadLength} bytes.",
                nameof(data));

        var payload = new byte[payloadLength];
        Buffer.BlockCopy(data, HeaderSize, payload, 0, payloadLength);

        return new UdpCommand(commandType, payload, sequenceNumber);
    }

    /// <summary>Creates a PING command.</summary>
    public static UdpCommand CreatePing(byte sequenceNumber = 0) =>
        new(UdpCommandType.Ping, Array.Empty<byte>(), sequenceNumber);

    /// <summary>Creates a DISCOVER command.</summary>
    public static UdpCommand CreateDiscover(byte sequenceNumber = 0) =>
        new(UdpCommandType.Discover, Array.Empty<byte>(), sequenceNumber);

    /// <summary>Creates a START_ACQUISITION command with optional payload.</summary>
    public static UdpCommand CreateStartAcquisition(byte[] payload, byte sequenceNumber = 0) =>
        new(UdpCommandType.StartAcquisition, payload, sequenceNumber);

    /// <summary>Creates a STOP_ACQUISITION command.</summary>
    public static UdpCommand CreateStopAcquisition(byte sequenceNumber = 0) =>
        new(UdpCommandType.StopAcquisition, Array.Empty<byte>(), sequenceNumber);

    /// <inheritdoc />
    public bool Equals(UdpCommand? other) =>
        other != null &&
        CommandType == other.CommandType &&
        SequenceNumber == other.SequenceNumber &&
        PayloadLength == other.PayloadLength &&
        Payload.SequenceEqual(other.Payload);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as UdpCommand);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(CommandType, SequenceNumber, PayloadLength);
}
