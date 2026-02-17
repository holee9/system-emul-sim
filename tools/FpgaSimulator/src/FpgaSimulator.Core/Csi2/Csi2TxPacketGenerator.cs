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
    /// Generates all packets for a complete frame.
    /// </summary>
    /// <param name="frame">2D pixel array [rows, cols]</param>
    /// <returns>Array of CSI-2 packets (FS + Lines + FE)</returns>
    public Csi2Packet[] GenerateFullFrame(ushort[,] frame)
    {
        var rows = frame.GetLength(0);
        var cols = frame.GetLength(1);
        var packets = new List<Csi2Packet>(rows + 2);

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
            packets.Add(GenerateLineData(linePixels, row));
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
    /// Calculates ECC for short packet header.
    /// MIPI CSI-2 uses 6-bit ECC for short packet integrity.
    /// </summary>
    private static byte CalculateEcc(byte dataId, byte wcLow, byte wcHigh)
    {
        // Simplified ECC calculation for CSI-2 short packets
        // In a real implementation, this would use the full 6-bit ECC algorithm
        var ecc = 0;
        ecc ^= dataId & 0x3F;
        ecc ^= wcLow & 0x3F;
        ecc ^= wcHigh & 0x3F;
        return (byte)(ecc & 0x3F);
    }
}
