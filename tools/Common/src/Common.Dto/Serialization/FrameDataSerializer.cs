using System.Buffers.Binary;

namespace Common.Dto.Serialization;

/// <summary>
/// Serializes and deserializes ushort[,] frame data to/from binary .raw format.
/// Binary format: [4B magic "XFRA"][4B version][2B rows][2B cols][N * 2B pixels LE]
/// </summary>
public static class FrameDataSerializer
{
    /// <summary>File magic bytes: "XFRA" (X-ray FRAme)</summary>
    private static readonly byte[] Magic = "XFRA"u8.ToArray();

    /// <summary>Current binary format version.</summary>
    private const uint FormatVersion = 1;

    /// <summary>Header size in bytes: 4 (magic) + 4 (version) + 2 (rows) + 2 (cols) = 12</summary>
    private const int HeaderSize = 12;

    /// <summary>
    /// Serializes a 2D frame to binary .raw format.
    /// </summary>
    /// <param name="frame">2D pixel array [rows, cols].</param>
    /// <returns>Binary data in .raw format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when frame is null.</exception>
    public static byte[] Serialize(ushort[,] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        int pixelBytes = rows * cols * 2;
        var buffer = new byte[HeaderSize + pixelBytes];

        // Write header
        Magic.CopyTo(buffer.AsSpan(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(8, 2), (ushort)rows);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(10, 2), (ushort)cols);

        // Write pixel data (row-major, little-endian)
        int offset = HeaderSize;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset, 2), frame[r, c]);
                offset += 2;
            }
        }

        return buffer;
    }

    /// <summary>
    /// Deserializes binary .raw data to a 2D frame.
    /// </summary>
    /// <param name="data">Binary data in .raw format.</param>
    /// <returns>2D pixel array [rows, cols].</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when data format is invalid.</exception>
    public static ushort[,] Deserialize(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < HeaderSize)
            throw new InvalidDataException($"Data too short: expected at least {HeaderSize} bytes, got {data.Length}.");

        // Validate magic
        if (!data.AsSpan(0, 4).SequenceEqual(Magic))
            throw new InvalidDataException("Invalid magic bytes. Expected 'XFRA'.");

        // Read header
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
        if (version != FormatVersion)
            throw new InvalidDataException($"Unsupported format version: {version}. Expected {FormatVersion}.");

        int rows = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8, 2));
        int cols = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(10, 2));

        int expectedSize = HeaderSize + rows * cols * 2;
        if (data.Length < expectedSize)
            throw new InvalidDataException($"Data too short: expected {expectedSize} bytes for {rows}x{cols} frame, got {data.Length}.");

        // Read pixel data
        var frame = new ushort[rows, cols];
        int offset = HeaderSize;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                frame[r, c] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
                offset += 2;
            }
        }

        return frame;
    }

    /// <summary>
    /// Writes a frame to a .raw file.
    /// </summary>
    /// <param name="frame">2D pixel array.</param>
    /// <param name="path">Output file path.</param>
    public static void WriteToFile(ushort[,] frame, string path)
    {
        byte[] data = Serialize(frame);
        File.WriteAllBytes(path, data);
    }

    /// <summary>
    /// Reads a frame from a .raw file.
    /// </summary>
    /// <param name="path">Input file path.</param>
    /// <returns>2D pixel array.</returns>
    public static ushort[,] ReadFromFile(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        return Deserialize(data);
    }
}
