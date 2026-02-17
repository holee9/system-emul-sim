using System.Buffers.Binary;

namespace HostSimulator.Core.Reassembly;

/// <summary>
/// Represents a frame header from UDP packets.
/// REQ-SIM-032: Frame header format with magic, frame_seq, timestamp, width, height, bit_depth, packet_index, total_packets, crc16.
/// Reference: docs/api/ethernet-protocol.md Section 2.1
/// </summary>
public sealed record FrameHeader
{
    /// <summary>
    /// Frame magic number for synchronization (0xD7E01234).
    /// </summary>
    public const uint FRAME_MAGIC = 0xD7E01234u;

    /// <summary>
    /// Supported protocol version.
    /// </summary>
    public const byte SUPPORTED_VERSION = 0x01;

    /// <summary>
    /// Frame header size in bytes.
    /// </summary>
    public const int HEADER_SIZE = 32;

    /// <summary>
    /// Gets the magic number (0xD7E01234).
    /// </summary>
    public required uint Magic { get; init; }

    /// <summary>
    /// Gets the protocol version.
    /// </summary>
    public required byte Version { get; init; }

    /// <summary>
    /// Gets the frame identifier (monotonically increasing).
    /// </summary>
    public required uint FrameId { get; init; }

    /// <summary>
    /// Gets the packet sequence number within the frame (0-based).
    /// </summary>
    public required ushort PacketSeq { get; init; }

    /// <summary>
    /// Gets the total number of packets in the frame.
    /// </summary>
    public required ushort TotalPackets { get; init; }

    /// <summary>
    /// Gets the timestamp in nanoseconds since SoC boot.
    /// </summary>
    public required ulong TimestampNs { get; init; }

    /// <summary>
    /// Gets the frame height in pixels (rows).
    /// </summary>
    public required ushort Rows { get; init; }

    /// <summary>
    /// Gets the frame width in pixels (cols).
    /// </summary>
    public required ushort Cols { get; init; }

    /// <summary>
    /// Gets the CRC-16 checksum over bytes 0-27.
    /// </summary>
    public required ushort Crc16 { get; init; }

    /// <summary>
    /// Gets the pixel bit depth (14 or 16).
    /// </summary>
    public required byte BitDepth { get; init; }

    /// <summary>
    /// Gets the status flags.
    /// </summary>
    public required byte Flags { get; init; }

    /// <summary>
    /// Gets whether this is the last packet of the frame.
    /// </summary>
    public bool IsLastPacket => (Flags & 0x01) != 0;

    /// <summary>
    /// Gets whether this frame may contain errors.
    /// </summary>
    public bool IsErrorFrame => (Flags & 0x02) != 0;

    /// <summary>
    /// Gets whether this is a calibration frame.
    /// </summary>
    public bool IsCalibrationFrame => (Flags & 0x04) != 0;

    /// <summary>
    /// Attempts to parse a frame header from a byte array.
    /// </summary>
    /// <param name="data">Input byte array containing the header.</param>
    /// <param name="header">Parsed frame header, or null if parsing failed.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(byte[] data, out FrameHeader? header)
    {
        return TryParse(new ReadOnlySpan<byte>(data), out header);
    }

    /// <summary>
    /// Attempts to parse a frame header from a read-only byte span.
    /// </summary>
    /// <param name="data">Input span containing the header.</param>
    /// <param name="header">Parsed frame header, or null if parsing failed.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(ReadOnlySpan<byte> data, out FrameHeader? header)
    {
        header = null;

        // Check minimum size
        if (data.Length < HEADER_SIZE)
            return false;

        // Read magic number (little-endian)
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (magic != FRAME_MAGIC)
            return false;

        // Check version
        byte version = data[4];
        if (version != SUPPORTED_VERSION)
            return false;

        // Read frame ID (little-endian)
        uint frameId = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8));

        // Read packet sequence (little-endian)
        ushort packetSeq = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(12));

        // Read total packets (little-endian)
        ushort totalPackets = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(14));

        // Read timestamp (little-endian)
        ulong timestampNs = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(16));

        // Read rows (little-endian)
        ushort rows = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(24));

        // Read cols (little-endian)
        ushort cols = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(26));

        // Read and verify CRC
        ushort crc16 = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(28));
        ushort computedCrc = Crc16Ccitt.Calculate(data.Slice(0, 28));
        if (crc16 != computedCrc)
            return false;

        // Read bit depth
        byte bitDepth = data[30];

        // Read flags
        byte flags = data[31];

        header = new FrameHeader
        {
            Magic = magic,
            Version = version,
            FrameId = frameId,
            PacketSeq = packetSeq,
            TotalPackets = totalPackets,
            TimestampNs = timestampNs,
            Rows = rows,
            Cols = cols,
            Crc16 = crc16,
            BitDepth = bitDepth,
            Flags = flags
        };

        return true;
    }

    /// <summary>
    /// Returns a string representation of the frame header.
    /// </summary>
    public override string ToString()
    {
        return $"FrameHeader {{ FrameId={FrameId}, PacketSeq={PacketSeq}/{TotalPackets}, Size={Cols}x{Rows}, BitDepth={BitDepth}, Flags=0x{Flags:X2} }}";
    }
}
