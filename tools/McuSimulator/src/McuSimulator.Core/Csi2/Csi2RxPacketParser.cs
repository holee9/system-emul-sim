using FpgaSimulator.Core.Csi2;

namespace McuSimulator.Core.Csi2;

/// <summary>
/// Result of parsing a CSI-2 Frame Start short packet.
/// </summary>
public readonly record struct FrameStartResult
{
    /// <summary>True if packet is valid</summary>
    public required bool IsValid { get; init; }

    /// <summary>Virtual channel (0-3)</summary>
    public required int VirtualChannel { get; init; }

    /// <summary>Frame number from packet data</summary>
    public required ushort FrameNumber { get; init; }
}

/// <summary>
/// Result of parsing a CSI-2 Frame End short packet.
/// </summary>
public readonly record struct FrameEndResult
{
    /// <summary>True if packet is valid</summary>
    public required bool IsValid { get; init; }

    /// <summary>Virtual channel (0-3)</summary>
    public required int VirtualChannel { get; init; }

    /// <summary>Frame number from packet data</summary>
    public required ushort FrameNumber { get; init; }
}

/// <summary>
/// Result of parsing a CSI-2 Line Data long packet.
/// </summary>
public readonly record struct LineDataResult
{
    /// <summary>True if packet is valid</summary>
    public required bool IsValid { get; init; }

    /// <summary>Line number</summary>
    public required int LineNumber { get; init; }

    /// <summary>Number of pixels in line</summary>
    public required int PixelCount { get; init; }

    /// <summary>Pixel data array</summary>
    public required ushort[] Pixels { get; init; }
}

/// <summary>
/// Result of parsing a complete CSI-2 frame.
/// </summary>
public readonly record struct FrameDataResult
{
    /// <summary>True if frame is valid and complete</summary>
    public required bool IsValid { get; init; }

    /// <summary>Frame height in rows</summary>
    public required int Rows { get; init; }

    /// <summary>Frame width in columns</summary>
    public required int Cols { get; init; }

    /// <summary>Total pixel count</summary>
    public required int TotalPixels { get; init; }

    /// <summary>2D pixel array [rows, cols]</summary>
    public required ushort[,] Pixels { get; init; }
}

/// <summary>
/// Parses CSI-2 packets received from FPGA CSI-2 TX.
/// Implements csi2-packet-format.md Sections 3-4.
/// </summary>
public sealed class Csi2RxPacketParser
{
    /// <summary>
    /// Parses a Frame Start short packet.
    /// </summary>
    /// <param name="packet">CSI-2 packet to parse</param>
    /// <returns>FrameStart result</returns>
    public FrameStartResult ParseFrameStart(Csi2Packet packet)
    {
        if (packet.PacketType != Csi2PacketType.FrameStart)
            return new FrameStartResult { IsValid = false, VirtualChannel = -1, FrameNumber = 0 };

        if (packet.Data == null || packet.Data.Length < 4)
            return new FrameStartResult { IsValid = false, VirtualChannel = -1, FrameNumber = 0 };

        // Extract frame number from bytes 1-2 (little-endian)
        ushort frameNumber = (ushort)((packet.Data[2] << 8) | packet.Data[1]);

        return new FrameStartResult
        {
            IsValid = true,
            VirtualChannel = packet.VirtualChannel,
            FrameNumber = frameNumber
        };
    }

    /// <summary>
    /// Parses a Frame End short packet.
    /// </summary>
    /// <param name="packet">CSI-2 packet to parse</param>
    /// <returns>FrameEnd result</returns>
    public FrameEndResult ParseFrameEnd(Csi2Packet packet)
    {
        if (packet.PacketType != Csi2PacketType.FrameEnd)
            return new FrameEndResult { IsValid = false, VirtualChannel = -1, FrameNumber = 0 };

        if (packet.Data == null || packet.Data.Length < 4)
            return new FrameEndResult { IsValid = false, VirtualChannel = -1, FrameNumber = 0 };

        // Extract frame number from bytes 1-2 (little-endian)
        ushort frameNumber = (ushort)((packet.Data[2] << 8) | packet.Data[1]);

        return new FrameEndResult
        {
            IsValid = true,
            VirtualChannel = packet.VirtualChannel,
            FrameNumber = frameNumber
        };
    }

    /// <summary>
    /// Parses a Line Data long packet and extracts pixel payload.
    /// </summary>
    /// <param name="packet">CSI-2 packet to parse</param>
    /// <returns>LineData result with pixel array</returns>
    public LineDataResult ParseLineData(Csi2Packet packet)
    {
        if (packet.PacketType != Csi2PacketType.LineData)
            return new LineDataResult { IsValid = false, LineNumber = -1, PixelCount = 0, Pixels = Array.Empty<ushort>() };

        if (packet.Data == null || packet.Data.Length < 6) // 4-byte header + 2-byte CRC minimum
            return new LineDataResult { IsValid = false, LineNumber = -1, PixelCount = 0, Pixels = Array.Empty<ushort>() };

        // Extract payload (skip 4-byte header, exclude 2-byte CRC)
        int payloadSize = packet.Data.Length - 4 - 2;
        if (payloadSize <= 0 || payloadSize % 2 != 0)
            return new LineDataResult { IsValid = false, LineNumber = -1, PixelCount = 0, Pixels = Array.Empty<ushort>() };

        int pixelCount = payloadSize / 2;
        var pixels = new ushort[pixelCount];

        for (int i = 0; i < pixelCount; i++)
        {
            // Little-endian: LSB first
            int offset = 4 + i * 2;
            pixels[i] = (ushort)((packet.Data[offset + 1] << 8) | packet.Data[offset]);
        }

        return new LineDataResult
        {
            IsValid = true,
            LineNumber = packet.LineNumber,
            PixelCount = pixelCount,
            Pixels = pixels
        };
    }

    /// <summary>
    /// Verifies ECC for a CSI-2 short packet header.
    /// Implements csi2-packet-format.md Section 5.
    /// </summary>
    /// <param name="header">3-byte header (VC+DT, Data LSB, Data MSB)</param>
    /// <param name="ecc">ECC byte to verify</param>
    /// <returns>True if ECC is valid</returns>
    public bool VerifyEcc(byte[] header, byte ecc)
    {
        if (header == null || header.Length != 3)
            return false;

        // Simplified ECC check (matching FpgaSimulator implementation)
        // In production, this would use the full SEC-DED Hamming code
        byte calculated = CalculateEcc(header[0], header[1], header[2]);
        return calculated == ecc;
    }

    /// <summary>
    /// Verifies CRC-16 for a CSI-2 long packet payload.
    /// Implements csi2-packet-format.md Section 6.
    /// </summary>
    /// <param name="packet">CSI-2 packet with CRC</param>
    /// <returns>True if CRC is valid</returns>
    public bool VerifyCrc16(Csi2Packet packet)
    {
        if (packet.Data == null || packet.Data.Length < 6)
            return false;

        // Extract payload (skip 4-byte header, exclude 2-byte CRC)
        int payloadSize = packet.Data.Length - 4 - 2;
        if (payloadSize <= 0)
            return false;

        var payload = new byte[payloadSize];
        Array.Copy(packet.Data, 4, payload, 0, payloadSize);

        // Calculate CRC over payload
        ushort calculated = CalculateCrc16(payload);

        // Extract CRC from packet (last 2 bytes, little-endian)
        ushort packetCrc = (ushort)((packet.Data[^1] << 8) | packet.Data[^2]);

        return calculated == packetCrc;
    }

    /// <summary>
    /// Parses a complete frame from a sequence of CSI-2 packets.
    /// Expected: FS + LineData[] + FE
    /// </summary>
    /// <param name="packets">Array of CSI-2 packets</param>
    /// <returns>FrameData result with 2D pixel array</returns>
    public FrameDataResult ParseFullFrame(Csi2Packet[] packets)
    {
        if (packets == null || packets.Length < 3) // Minimum: FS + 1 line + FE
            return new FrameDataResult { IsValid = false, Rows = 0, Cols = 0, TotalPixels = 0, Pixels = new ushort[0, 0] };

        // Verify first packet is Frame Start
        if (packets[0].PacketType != Csi2PacketType.FrameStart)
            return new FrameDataResult { IsValid = false, Rows = 0, Cols = 0, TotalPixels = 0, Pixels = new ushort[0, 0] };

        // Verify last packet is Frame End
        if (packets[^1].PacketType != Csi2PacketType.FrameEnd)
            return new FrameDataResult { IsValid = false, Rows = 0, Cols = 0, TotalPixels = 0, Pixels = new ushort[0, 0] };

        // Count line data packets
        int lineCount = packets.Length - 2; // Exclude FS and FE
        var lines = new List<LineDataResult>(lineCount);

        for (int i = 1; i < packets.Length - 1; i++)
        {
            var lineResult = ParseLineData(packets[i]);
            if (!lineResult.IsValid)
                return new FrameDataResult { IsValid = false, Rows = 0, Cols = 0, TotalPixels = 0, Pixels = new ushort[0, 0] };
            lines.Add(lineResult);
        }

        if (lines.Count == 0)
            return new FrameDataResult { IsValid = false, Rows = 0, Cols = 0, TotalPixels = 0, Pixels = new ushort[0, 0] };

        // Determine dimensions
        int rows = lines.Count;
        int cols = lines[0].PixelCount;

        // Verify all lines have same width
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].PixelCount != cols)
                return new FrameDataResult { IsValid = false, Rows = 0, Cols = 0, TotalPixels = 0, Pixels = new ushort[0, 0] };
        }

        // Assemble 2D array
        var pixels = new ushort[rows, cols];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                pixels[row, col] = lines[row].Pixels[col];
            }
        }

        return new FrameDataResult
        {
            IsValid = true,
            Rows = rows,
            Cols = cols,
            TotalPixels = rows * cols,
            Pixels = pixels
        };
    }

    /// <summary>
    /// Calculates CRC-16/CCITT over data bytes.
    /// Polynomial: 0x1021, Initial: 0xFFFF
    /// </summary>
    private static ushort CalculateCrc16(byte[] data)
    {
        const ushort polynomial = 0x1021;
        ushort crc = 0xFFFF;

        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ polynomial);
                else
                    crc <<= 1;
            }
        }

        return crc;
    }

    /// <summary>
    /// Calculates simplified ECC for CSI-2 short packet header.
    /// Matches FpgaSimulator implementation.
    /// </summary>
    private static byte CalculateEcc(byte dataId, byte wcLow, byte wcHigh)
    {
        var ecc = 0;
        ecc ^= dataId & 0x3F;
        ecc ^= wcLow & 0x3F;
        ecc ^= wcHigh & 0x3F;
        return (byte)(ecc & 0x3F);
    }
}
