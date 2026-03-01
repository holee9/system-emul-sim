using System.Buffers.Binary;

namespace Common.Dto.Serialization;

/// <summary>
/// Represents a single UDP frame packet entry for serialization.
/// Mirrors McuSimulator.Core.Network.UdpFramePacket to avoid cross-project dependency.
/// </summary>
public readonly record struct UdpPacketEntry
{
    /// <summary>Complete packet data including header and payload.</summary>
    public required byte[] Data { get; init; }

    /// <summary>Packet index within frame.</summary>
    public required int PacketIndex { get; init; }

    /// <summary>Total packets in frame.</summary>
    public required int TotalPackets { get; init; }

    /// <summary>Flags byte (bit 0 = last_packet).</summary>
    public required byte Flags { get; init; }
}

/// <summary>
/// Serializes and deserializes UDP frame packet arrays to/from binary .udp format.
/// Binary format: [4B magic "XUDP"][4B version][4B count][N * (4B dataLength + data_bytes + 4B packetIndex + 4B totalPackets + 1B flags)]
/// </summary>
public static class UdpPacketSerializer
{
    /// <summary>File magic bytes: "XUDP" (X-ray UDP)</summary>
    private static readonly byte[] Magic = "XUDP"u8.ToArray();

    /// <summary>Current binary format version.</summary>
    private const uint FormatVersion = 1;

    /// <summary>File header size: 4 (magic) + 4 (version) + 4 (count) = 12</summary>
    private const int FileHeaderSize = 12;

    /// <summary>Per-packet metadata: 4 (dataLength) + 4 (packetIndex) + 4 (totalPackets) + 1 (flags) = 13</summary>
    private const int PacketMetadataSize = 13;

    /// <summary>
    /// Serializes an array of UdpPacketEntry to binary format.
    /// </summary>
    /// <param name="packets">Array of UDP packet entries.</param>
    /// <returns>Binary data in .udp format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when packets is null.</exception>
    public static byte[] Serialize(UdpPacketEntry[] packets)
    {
        ArgumentNullException.ThrowIfNull(packets);

        int totalSize = FileHeaderSize;
        foreach (var pkt in packets)
        {
            totalSize += PacketMetadataSize + pkt.Data.Length;
        }

        var buffer = new byte[totalSize];

        // Write file header
        Magic.CopyTo(buffer.AsSpan(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), FormatVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(8, 4), (uint)packets.Length);

        // Write packets
        int offset = FileHeaderSize;
        foreach (var pkt in packets)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), pkt.Data.Length);
            offset += 4;

            pkt.Data.CopyTo(buffer.AsSpan(offset, pkt.Data.Length));
            offset += pkt.Data.Length;

            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), pkt.PacketIndex);
            offset += 4;

            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), pkt.TotalPackets);
            offset += 4;

            buffer[offset] = pkt.Flags;
            offset += 1;
        }

        return buffer;
    }

    /// <summary>
    /// Deserializes binary .udp data to an array of UdpPacketEntry.
    /// </summary>
    /// <param name="data">Binary data in .udp format.</param>
    /// <returns>Array of UDP packet entries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when data format is invalid.</exception>
    public static UdpPacketEntry[] Deserialize(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < FileHeaderSize)
            throw new InvalidDataException($"Data too short: expected at least {FileHeaderSize} bytes, got {data.Length}.");

        if (!data.AsSpan(0, 4).SequenceEqual(Magic))
            throw new InvalidDataException("Invalid magic bytes. Expected 'XUDP'.");

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
        if (version != FormatVersion)
            throw new InvalidDataException($"Unsupported format version: {version}. Expected {FormatVersion}.");

        int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8, 4));
        var packets = new UdpPacketEntry[count];

        int offset = FileHeaderSize;
        for (int i = 0; i < count; i++)
        {
            if (offset + 4 > data.Length)
                throw new InvalidDataException($"Unexpected end of data at packet {i}.");

            int dataLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            offset += 4;

            if (offset + dataLength + 9 > data.Length)
                throw new InvalidDataException($"Unexpected end of data at packet {i} payload.");

            var packetData = new byte[dataLength];
            data.AsSpan(offset, dataLength).CopyTo(packetData);
            offset += dataLength;

            int packetIndex = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            offset += 4;

            int totalPackets = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            offset += 4;

            byte flags = data[offset];
            offset += 1;

            packets[i] = new UdpPacketEntry
            {
                Data = packetData,
                PacketIndex = packetIndex,
                TotalPackets = totalPackets,
                Flags = flags
            };
        }

        return packets;
    }

    /// <summary>
    /// Writes packets to a .udp file.
    /// </summary>
    public static void WriteToFile(UdpPacketEntry[] packets, string path)
    {
        byte[] data = Serialize(packets);
        File.WriteAllBytes(path, data);
    }

    /// <summary>
    /// Reads packets from a .udp file.
    /// </summary>
    public static UdpPacketEntry[] ReadFromFile(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        return Deserialize(data);
    }
}
