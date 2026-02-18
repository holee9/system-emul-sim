namespace XrayDetector.Core.Reassembly;

/// <summary>
/// CRC-16/CCITT validator for frame header validation.
/// Polynomial: 0x8408 (reflected), Initial value: 0xFFFF
/// Covers frame header bytes 0-27 (magic, version, reserved0, frame_id, packet_seq, total_packets, timestamp_ns, rows, cols)
/// CRC field at offset 28 (uint16).
/// </summary>
public static class Crc16CcittValidator
{
    /// <summary>
    /// Computes CRC-16/CCITT checksum.
    /// Polynomial: 0x8408 (reflected), Initial: 0xFFFF, XorOut: 0x0000
    /// </summary>
    /// <param name="data">Input data buffer.</param>
    /// <returns>CRC-16/CCITT checksum.</returns>
    public static ushort ComputeCrc16(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        const ushort poly = 0x8408; // Reflected polynomial
        ushort crc = 0xFFFF; // Initial value

        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (ushort)((crc >> 1) ^ poly);
                else
                    crc >>= 1;
            }
        }

        return crc;
    }

    /// <summary>
    /// Validates frame header CRC.
    /// </summary>
    /// <param name="header">Frame header buffer (must be at least 30 bytes).</param>
    /// <returns>True if CRC is valid, false otherwise.</returns>
    public static bool ValidateHeader(byte[] header)
    {
        if (header == null)
            return false;

        if (header.Length < 30)
            return false;

        // Compute CRC over bytes 0-27
        ushort computedCrc = ComputeCrc16(header[..28]);

        // Extract stored CRC from bytes 28-29
        ushort storedCrc = (ushort)((header[28] << 8) | header[29]);

        return computedCrc == storedCrc;
    }
}
