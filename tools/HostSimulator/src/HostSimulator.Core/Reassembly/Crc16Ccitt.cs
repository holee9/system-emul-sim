using System.Buffers.Binary;

namespace HostSimulator.Core.Reassembly;

/// <summary>
/// CRC-16/CCITT polynomial implementation.
/// Polynomial: x^16 + x^12 + x^5 + 1 (0x1021)
/// Initial value: 0xFFFF
/// Reflected: false
/// Final XOR: 0x0000
/// Reference: docs/api/ethernet-protocol.md Section 7
/// </summary>
public static class Crc16Ccitt
{
    private const ushort Polynomial = 0x1021;
    private const ushort InitialValue = 0xFFFF;

    /// <summary>
    /// Precomputed CRC table for fast calculation.
    /// </summary>
    private static readonly ushort[] CrcTable = new ushort[256];

    static Crc16Ccitt()
    {
        // Initialize CRC table
        for (uint i = 0; i < 256; i++)
        {
            ushort crc = (ushort)(i << 8);
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ Polynomial);
                }
                else
                {
                    crc <<= 1;
                }
            }
            CrcTable[i] = crc;
        }
    }

    /// <summary>
    /// Calculates CRC-16/CCITT over the specified byte range.
    /// </summary>
    /// <param name="data">Input data buffer.</param>
    /// <param name="offset">Starting offset in buffer.</param>
    /// <param name="length">Number of bytes to process.</param>
    /// <returns>CRC-16 checksum.</returns>
    public static ushort Calculate(byte[] data, int offset, int length)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (offset < 0 || offset >= data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0 || offset + length > data.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        ushort crc = InitialValue;

        for (int i = offset; i < offset + length; i++)
        {
            ushort index = (ushort)(((crc >> 8) ^ data[i]) & 0xFF);
            crc = (ushort)((crc << 8) ^ CrcTable[index]);
        }

        return crc;
    }

    /// <summary>
    /// Calculates CRC-16/CCITT over a span of bytes.
    /// </summary>
    /// <param name="data">Input data span.</param>
    /// <returns>CRC-16 checksum.</returns>
    public static ushort Calculate(ReadOnlySpan<byte> data)
    {
        ushort crc = InitialValue;

        for (int i = 0; i < data.Length; i++)
        {
            ushort index = (ushort)(((crc >> 8) ^ data[i]) & 0xFF);
            crc = (ushort)((crc << 8) ^ CrcTable[index]);
        }

        return crc;
    }
}
