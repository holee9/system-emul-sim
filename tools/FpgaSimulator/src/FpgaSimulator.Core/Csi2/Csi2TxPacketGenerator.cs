namespace FpgaSimulator.Core.Csi2;

/// <summary>
/// Generates CSI-2 packets following MIPI CSI-2 v1.3 specification.
/// Implements packet generation for FPGA CSI-2 TX subsystem.
/// See fpga-design.md Section 5.4 for packet format.
/// </summary>
public sealed class Csi2TxPacketGenerator
{
    private readonly int _virtualChannel;
    private readonly Csi2DataType _dataType;

    /// <summary>
    /// Initializes a new instance with VC0 and RAW16 data type.
    /// </summary>
    public Csi2TxPacketGenerator() : this(virtualChannel: 0, Csi2DataType.Raw16)
    {
    }

    /// <summary>
    /// Initializes a new instance with specified virtual channel and data type.
    /// </summary>
    /// <param name="virtualChannel">Virtual channel number (0-3)</param>
    /// <param name="dataType">CSI-2 data type</param>
    public Csi2TxPacketGenerator(int virtualChannel, Csi2DataType dataType)
    {
        _virtualChannel = Math.Clamp(virtualChannel, 0, 3);
        _dataType = dataType;
    }

    /// <summary>Current virtual channel setting</summary>
    public int VirtualChannel => _virtualChannel;

    /// <summary>Current data type setting</summary>
    public Csi2DataType DataType => _dataType;

    /// <summary>
    /// Generates a Frame Start (FS) short packet.
    /// Format: [PH] DataID=0x00, WC=0x0000, ECC
    /// </summary>
    public Csi2Packet GenerateFrameStart()
    {
        var data = new byte[4];
        data[0] = (byte)((_virtualChannel << 6) | (byte)Csi2PacketType.FrameStart);
        data[1] = 0x00; // WC low byte
        data[2] = 0x00; // WC high byte
        data[3] = CalculateEcc(data[0], data[1], data[2]);

        return new Csi2Packet
        {
            PacketType = Csi2PacketType.FrameStart,
            VirtualChannel = _virtualChannel,
            Data = data,
            Crc16 = 0
        };
    }

    /// <summary>
    /// Generates a Frame End (FE) short packet.
    /// Format: [PH] DataID=0x01, WC=0x0000, ECC
    /// </summary>
    public Csi2Packet GenerateFrameEnd()
    {
        var data = new byte[4];
        data[0] = (byte)((_virtualChannel << 6) | (byte)Csi2PacketType.FrameEnd);
        data[1] = 0x00; // WC low byte
        data[2] = 0x00; // WC high byte
        data[3] = CalculateEcc(data[0], data[1], data[2]);

        return new Csi2Packet
        {
            PacketType = Csi2PacketType.FrameEnd,
            VirtualChannel = _virtualChannel,
            Data = data,
            Crc16 = 0
        };
    }

    /// <summary>
    /// Generates a Line Start (LS) short packet.
    /// Format: [PH] DataID=0x02, WC=lineNumber, ECC
    /// </summary>
    /// <param name="lineNumber">Line number encoded in word count field</param>
    public Csi2Packet GenerateLineStart(int lineNumber)
    {
        var data = new byte[4];
        data[0] = (byte)((_virtualChannel << 6) | (byte)Csi2PacketType.LineStart);
        data[1] = (byte)(lineNumber & 0xFF);        // WC low byte (line number)
        data[2] = (byte)((lineNumber >> 8) & 0xFF);  // WC high byte
        data[3] = CalculateEcc(data[0], data[1], data[2]);

        return new Csi2Packet
        {
            PacketType = Csi2PacketType.LineStart,
            VirtualChannel = _virtualChannel,
            LineNumber = lineNumber,
            Data = data,
            Crc16 = 0
        };
    }

    /// <summary>
    /// Generates a Line End (LE) short packet.
    /// Format: [PH] DataID=0x03, WC=lineNumber, ECC
    /// </summary>
    /// <param name="lineNumber">Line number encoded in word count field</param>
    public Csi2Packet GenerateLineEnd(int lineNumber)
    {
        var data = new byte[4];
        data[0] = (byte)((_virtualChannel << 6) | (byte)Csi2PacketType.LineEnd);
        data[1] = (byte)(lineNumber & 0xFF);        // WC low byte (line number)
        data[2] = (byte)((lineNumber >> 8) & 0xFF);  // WC high byte
        data[3] = CalculateEcc(data[0], data[1], data[2]);

        return new Csi2Packet
        {
            PacketType = Csi2PacketType.LineEnd,
            VirtualChannel = _virtualChannel,
            LineNumber = lineNumber,
            Data = data,
            Crc16 = 0
        };
    }

    /// <summary>
    /// Generates a Line Data long packet with pixel payload.
    /// Format: [PH] DataID, WC, [ECC] [Payload] [CRC16]
    /// </summary>
    /// <param name="pixels">Pixel data array (16-bit values)</param>
    /// <param name="lineNumber">Line number for metadata</param>
    /// <returns>Complete CSI-2 packet with header, payload, and CRC</returns>
    public Csi2Packet GenerateLineData(ushort[] pixels, int lineNumber)
    {
        // Convert pixel array to byte array (little-endian)
        var payload = new byte[pixels.Length * 2];
        for (int i = 0; i < pixels.Length; i++)
        {
            payload[i * 2] = (byte)(pixels[i] & 0xFF);         // LSB
            payload[i * 2 + 1] = (byte)((pixels[i] >> 8) & 0xFF); // MSB
        }

        // Calculate word count (payload size in bytes)
        var wordCount = (ushort)payload.Length;

        // Build packet header
        var header = new byte[4];
        header[0] = (byte)((_virtualChannel << 6) | (byte)_dataType);
        header[1] = (byte)(wordCount & 0xFF);        // WC low byte
        header[2] = (byte)((wordCount >> 8) & 0xFF); // WC high byte
        header[3] = CalculateEcc(header[0], header[1], header[2]);

        // Calculate CRC-16 over payload
        var crc = CalculateCrc16(payload);

        // Assemble complete packet
        var packetData = new byte[4 + payload.Length + 2];
        Array.Copy(header, 0, packetData, 0, 4);
        Array.Copy(payload, 0, packetData, 4, payload.Length);
        packetData[^2] = (byte)(crc & 0xFF);        // CRC low byte
        packetData[^1] = (byte)((crc >> 8) & 0xFF); // CRC high byte

        return new Csi2Packet
        {
            PacketType = Csi2PacketType.LineData,
            VirtualChannel = _virtualChannel,
            LineNumber = lineNumber,
            PixelCount = pixels.Length,
            Crc16 = crc,
            Data = packetData
        };
    }

    /// <summary>
    /// Generates all packets for a complete frame including LS/LE packets.
    /// </summary>
    /// <param name="frame">2D pixel array [rows, cols]</param>
    /// <param name="includeLineSync">Whether to include LS/LE packets (default: true)</param>
    /// <returns>Array of CSI-2 packets (FS + [LS + Lines + LE]* + FE)</returns>
    public Csi2Packet[] GenerateFullFrame(ushort[,] frame, bool includeLineSync = false)
    {
        var rows = frame.GetLength(0);
        var cols = frame.GetLength(1);

        // Estimate capacity: FS + (LS + LineData + LE) * rows + FE
        var estimatedCapacity = includeLineSync ? (rows * 3 + 2) : (rows + 2);
        var packets = new List<Csi2Packet>(estimatedCapacity);

        // Add Frame Start
        packets.Add(GenerateFrameStart());

        // Add line data packets
        for (int row = 0; row < rows; row++)
        {
            var linePixels = new ushort[cols];
            for (int col = 0; col < cols; col++)
            {
                linePixels[col] = frame[row, col];
            }

            if (includeLineSync)
                packets.Add(GenerateLineStart(row));

            packets.Add(GenerateLineData(linePixels, row));

            if (includeLineSync)
                packets.Add(GenerateLineEnd(row));
        }

        // Add Frame End
        packets.Add(GenerateFrameEnd());

        return packets.ToArray();
    }

    /// <summary>
    /// Calculates CRC-16 checksum over data bytes.
    /// Uses polynomial 0x1021 (XMODEM/CRC-16-CCITT).
    /// </summary>
    /// <param name="data">Input data</param>
    /// <returns>CRC-16 checksum</returns>
    public ushort CalculateCrc16(byte[] data)
    {
        const ushort polynomial = 0x1021;
        ushort crc = 0xFFFF;

        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ polynomial);
                }
                else
                {
                    crc <<= 1;
                }
            }
        }

        return crc;
    }

    /// <summary>
    /// Calculates 6-bit ECC for CSI-2 packet header per MIPI CSI-2 v1.3 spec.
    /// ECC protects the 24-bit header (DataID + WC[15:0]).
    /// Uses Hamming(6,24) code: 6 parity bits cover 24 data bits.
    /// </summary>
    /// <param name="dataId">Data identifier byte (VC[1:0] + DataType[5:0])</param>
    /// <param name="wcLow">Word count low byte</param>
    /// <param name="wcHigh">Word count high byte</param>
    /// <returns>6-bit ECC value</returns>
    internal static byte CalculateEcc(byte dataId, byte wcLow, byte wcHigh)
    {
        // Combine 24-bit header into a single value for bit-level access
        // D[23:16] = wcHigh, D[15:8] = wcLow, D[7:0] = dataId
        uint d = ((uint)wcHigh << 16) | ((uint)wcLow << 8) | dataId;

        // MIPI CSI-2 ECC parity bit generators (Hamming code)
        // P0 covers D[0,1,2,4,5,7,10,11,13,16,20,21,22,23]
        byte p0 = (byte)(
            Parity(d & 0x00F4_A937));

        // P1 covers D[0,1,3,4,6,8,10,12,14,17,20,21,22,23]
        byte p1 = (byte)(
            Parity(d & 0x00F5_541B));

        // P2 covers D[0,2,3,5,6,9,11,12,15,18,20,21,22,23]
        byte p2 = (byte)(
            Parity(d & 0x00F7_A86D));

        // P3 covers D[1,2,3,7,8,9,13,14,15,19,20,21,22,23]
        byte p3 = (byte)(
            Parity(d & 0x00FF_E38E));

        // P4 covers D[4,5,6,7,10,11,12,13,16,17,18,19]
        byte p4 = (byte)(
            Parity(d & 0x000F_3CF0));

        // P5 covers D[14,15,16,17,18,19,20,21,22,23]
        byte p5 = (byte)(
            Parity(d & 0x00FF_C000));

        return (byte)((p5 << 5) | (p4 << 4) | (p3 << 3) | (p2 << 2) | (p1 << 1) | p0);
    }

    /// <summary>
    /// Computes even parity of the set bits in the given value.
    /// </summary>
    private static byte Parity(uint value)
    {
        // Count set bits; parity is 1 if odd number of bits set
        value ^= value >> 16;
        value ^= value >> 8;
        value ^= value >> 4;
        value ^= value >> 2;
        value ^= value >> 1;
        return (byte)(value & 1);
    }
}
