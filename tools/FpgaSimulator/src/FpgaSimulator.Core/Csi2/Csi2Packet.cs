namespace FpgaSimulator.Core.Csi2;

/// <summary>
/// Represents a CSI-2 packet with header, payload, and CRC.
/// </summary>
public record Csi2Packet
{
    /// <summary>Packet type (FS, FE, LS, LE, or Line Data)</summary>
    public required Csi2PacketType PacketType { get; init; }

    /// <summary>Virtual channel number (0-3)</summary>
    public required int VirtualChannel { get; init; }

    /// <summary>Line number (for Line Data packets)</summary>
    public int LineNumber { get; init; }

    /// <summary>Number of pixels in the packet (for Line Data packets)</summary>
    public int PixelCount { get; init; }

    /// <summary>CRC-16 checksum (for Line Data packets)</summary>
    public ushort Crc16 { get; init; }

    /// <summary>Complete packet data including header, payload, and CRC</summary>
    public required byte[] Data { get; init; }

    /// <summary>Word count (bytes/2 for CSI-2 long packets)</summary>
    public int WordCount => Data.Length > 4 ? (Data.Length - 8) / 2 : 0;
}
