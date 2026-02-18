namespace XrayDetector.Common.Dto;

/// <summary>
/// Value object representing the packet header for frame data transmission.
/// Provides serialization, deserialization, and CRC-16/CCITT validation.
/// </summary>
public sealed class PacketHeader : IEquatable<PacketHeader>
{
    /// <summary>Unique identifier for the packet (typically frame number).</summary>
    public ushort PacketId { get; }

    /// <summary>Total number of packets in this frame.</summary>
    public ushort TotalPackets { get; }

    /// <summary>Current packet index (0-based).</summary>
    public ushort CurrentPacket { get; }

    /// <summary>CRC-16/CCITT checksum for payload validation.</summary>
    public ushort Crc16 { get; }

    /// <summary>Constant header size in bytes (4 fields × 2 bytes).</summary>
    public const int Size = 8;

    /// <summary>
    /// Creates a new PacketHeader instance.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when currentPacket >= totalPackets.
    /// </exception>
    public PacketHeader(ushort packetId, ushort totalPackets, ushort currentPacket, ushort crc16)
    {
        if (currentPacket >= totalPackets)
            throw new ArgumentOutOfRangeException(nameof(currentPacket),
                "Current packet must be less than total packets.");

        PacketId = packetId;
        TotalPackets = totalPackets;
        CurrentPacket = currentPacket;
        Crc16 = crc16;
    }

    /// <summary>Returns true if this is the first packet (CurrentPacket == 0).</summary>
    public bool IsFirstPacket => CurrentPacket == 0;

    /// <summary>Returns true if this is the last packet (CurrentPacket == TotalPackets - 1).</summary>
    public bool IsLastPacket => CurrentPacket == TotalPackets - 1;

    /// <summary>Creates new instance with updated CRC.</summary>
    public PacketHeader WithCrc16(ushort crc16) =>
        new(PacketId, TotalPackets, CurrentPacket, crc16);

    /// <summary>Serializes header to byte array (big-endian).</summary>
    public byte[] Serialize()
    {
        var result = new byte[Size];
        result[0] = (byte)(PacketId >> 8);
        result[1] = (byte)PacketId;
        result[2] = (byte)(TotalPackets >> 8);
        result[3] = (byte)TotalPackets;
        result[4] = (byte)(CurrentPacket >> 8);
        result[5] = (byte)CurrentPacket;
        result[6] = (byte)(Crc16 >> 8);
        result[7] = (byte)Crc16;
        return result;
    }

    /// <summary>Deserializes header from byte array.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when data length is less than Size (8 bytes).
    /// </exception>
    public static PacketHeader Deserialize(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length < Size)
            throw new ArgumentException($"Data must be at least {Size} bytes.", nameof(data));

        var packetId = (ushort)((data[0] << 8) | data[1]);
        var totalPackets = (ushort)((data[2] << 8) | data[3]);
        var currentPacket = (ushort)((data[4] << 8) | data[5]);
        var crc16 = (ushort)((data[6] << 8) | data[7]);

        return new PacketHeader(packetId, totalPackets, currentPacket, crc16);
    }

    /// <summary>
    /// Computes CRC-16/CCITT-FALSE checksum.
    /// Polynomial: 0x1021, Initial: 0xFFFF, ReflectIn: false, ReflectOut: false
    /// </summary>
    public static ushort ComputeCrc16Ccitt(byte[] data)
    {
        const ushort poly = 0x1021;
        ushort crc = 0xFFFF;

        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ poly);
                else
                    crc <<= 1;
            }
        }

        return crc;
    }

    /// <inheritdoc />
    public bool Equals(PacketHeader? other) =>
        other != null &&
        PacketId == other.PacketId &&
        TotalPackets == other.TotalPackets &&
        CurrentPacket == other.CurrentPacket &&
        Crc16 == other.Crc16;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PacketHeader);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(PacketId, TotalPackets, CurrentPacket, Crc16);
}
