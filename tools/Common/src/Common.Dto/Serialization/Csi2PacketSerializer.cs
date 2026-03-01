using System.Buffers.Binary;
using Common.Dto.Dtos;

namespace Common.Dto.Serialization;

/// <summary>
/// Serializes and deserializes Csi2Packet arrays to/from binary .csi2 format.
/// Binary format: [4B magic "XCS2"][4B version][4B count][N * (4B length + packet_bytes)]
/// Each packet entry: [4B dataType][4B virtualChannel][4B payloadLength][payload_bytes]
/// </summary>
public static class Csi2PacketSerializer
{
    /// <summary>File magic bytes: "XCS2" (X-ray CSI-2)</summary>
    private static readonly byte[] Magic = "XCS2"u8.ToArray();

    /// <summary>Current binary format version.</summary>
    private const uint FormatVersion = 1;

    /// <summary>File header size: 4 (magic) + 4 (version) + 4 (count) = 12</summary>
    private const int FileHeaderSize = 12;

    /// <summary>Per-packet header size: 4 (dataType) + 4 (virtualChannel) + 4 (payloadLength) = 12</summary>
    private const int PacketHeaderSize = 12;

    /// <summary>
    /// Serializes an array of Csi2Packet to binary format.
    /// </summary>
    /// <param name="packets">Array of CSI-2 packets.</param>
    /// <returns>Binary data in .csi2 format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when packets is null.</exception>
    public static byte[] Serialize(Csi2Packet[] packets)
    {
        ArgumentNullException.ThrowIfNull(packets);

        // Calculate total size
        int totalSize = FileHeaderSize;
        foreach (var pkt in packets)
        {
            totalSize += PacketHeaderSize + pkt.Payload.Length;
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
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), (int)pkt.DataType);
            offset += 4;

            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), pkt.VirtualChannel);
            offset += 4;

            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), pkt.Payload.Length);
            offset += 4;

            pkt.Payload.CopyTo(buffer.AsSpan(offset, pkt.Payload.Length));
            offset += pkt.Payload.Length;
        }

        return buffer;
    }

    /// <summary>
    /// Deserializes binary .csi2 data to an array of Csi2Packet.
    /// </summary>
    /// <param name="data">Binary data in .csi2 format.</param>
    /// <returns>Array of CSI-2 packets.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when data format is invalid.</exception>
    public static Csi2Packet[] Deserialize(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < FileHeaderSize)
            throw new InvalidDataException($"Data too short: expected at least {FileHeaderSize} bytes, got {data.Length}.");

        // Validate magic
        if (!data.AsSpan(0, 4).SequenceEqual(Magic))
            throw new InvalidDataException("Invalid magic bytes. Expected 'XCS2'.");

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
        if (version != FormatVersion)
            throw new InvalidDataException($"Unsupported format version: {version}. Expected {FormatVersion}.");

        int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8, 4));
        var packets = new Csi2Packet[count];

        int offset = FileHeaderSize;
        for (int i = 0; i < count; i++)
        {
            if (offset + PacketHeaderSize > data.Length)
                throw new InvalidDataException($"Unexpected end of data at packet {i}.");

            var dataType = (Csi2DataType)BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            offset += 4;

            int virtualChannel = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            offset += 4;

            int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            offset += 4;

            if (offset + payloadLength > data.Length)
                throw new InvalidDataException($"Unexpected end of data at packet {i} payload.");

            var payload = new byte[payloadLength];
            data.AsSpan(offset, payloadLength).CopyTo(payload);
            offset += payloadLength;

            packets[i] = new Csi2Packet(dataType, virtualChannel, payload);
        }

        return packets;
    }

    /// <summary>
    /// Writes packets to a .csi2 file.
    /// </summary>
    /// <param name="packets">Array of CSI-2 packets.</param>
    /// <param name="path">Output file path.</param>
    public static void WriteToFile(Csi2Packet[] packets, string path)
    {
        byte[] data = Serialize(packets);
        File.WriteAllBytes(path, data);
    }

    /// <summary>
    /// Reads packets from a .csi2 file.
    /// </summary>
    /// <param name="path">Input file path.</param>
    /// <returns>Array of CSI-2 packets.</returns>
    public static Csi2Packet[] ReadFromFile(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        return Deserialize(data);
    }
}
