using System.Buffers.Binary;
using Common.Dto.Dtos;

namespace HostSimulator.Core.Storage;

/// <summary>
/// Writes frames in TIFF format (16-bit grayscale).
/// REQ-SIM-043: Save frames in TIFF format (16-bit grayscale).
/// Reference: TIFF 6.0 Specification
/// </summary>
public sealed class TiffWriter : IFrameStorage
{
    // TIFF tag constants
    private const ushort TIFFTag_ImageWidth = 256;
    private const ushort TIFFTag_ImageLength = 257;
    private const ushort TIFFTag_BitsPerSample = 258;
    private const ushort TIFFTag_Compression = 259;
    private const ushort TIFFTag_PhotometricInterpretation = 262;
    private const ushort TIFFTag_StripOffsets = 273;
    private const ushort TIFFTag_SamplesPerPixel = 277;
    private const ushort TIFFTag_RowsPerStrip = 278;
    private const ushort TIFFTag_StripByteCounts = 279;
    private const ushort TIFFTag_XResolution = 282;
    private const ushort TIFFTag_YResolution = 283;
    private const ushort TIFFTag_ResolutionUnit = 296;

    // TIFF data type constants
    private const ushort TIFFType_SHORT = 3;
    private const ushort TIFFType_LONG = 4;
    private const ushort TIFFType_RATIONAL = 5;

    // TIFF constants
    private const ushort TIFF_Compression_None = 1;
    private const ushort TIFF_Photometric_BlackIsZero = 1;
    private const ushort TIFF_ResolutionUnit_None = 1;

    /// <summary>
    /// Writes a frame to a TIFF file.
    /// </summary>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="frame">Frame data to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveAsync(string filePath, FrameData frame, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));

        // Calculate TIFF structure
        int pixelDataSize = frame.Width * frame.Height * 2;
        int headerSize = 8;
        int ifdEntryCount = 12;
        int ifdSize = 2 + (ifdEntryCount * 12) + 4;
        int xResolutionOffset = headerSize + ifdSize;
        int yResolutionOffset = xResolutionOffset + 8;
        int pixelDataOffset = yResolutionOffset + 8;

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);

        // Write TIFF header (8 bytes)
        writer.Write((byte)0x49); // 'I' = little-endian
        writer.Write((byte)0x49); // 'I'
        writer.Write((ushort)42); // TIFF magic number
        writer.Write((uint)headerSize); // Offset to first IFD

        // Write IFD
        writer.Write((ushort)ifdEntryCount);

        // ImageWidth (256) - LONG, 1 value
        WriteIfdEntry(writer, TIFFTag_ImageWidth, TIFFType_LONG, 1, (uint)frame.Width);

        // ImageLength (257) - LONG, 1 value
        WriteIfdEntry(writer, TIFFTag_ImageLength, TIFFType_LONG, 1, (uint)frame.Height);

        // BitsPerSample (258) - SHORT, 1 value, value=16
        // For count=1 SHORT type, value is stored directly in value/offset field
        WriteIfdEntryShortValue(writer, TIFFTag_BitsPerSample, 1, 16);

        // Compression (259) - SHORT, 1 value, value=1 (none)
        WriteIfdEntryShortValue(writer, TIFFTag_Compression, 1, TIFF_Compression_None);

        // PhotometricInterpretation (262) - SHORT, 1 value, value=1 (black is zero)
        WriteIfdEntryShortValue(writer, TIFFTag_PhotometricInterpretation, 1, TIFF_Photometric_BlackIsZero);

        // StripOffsets (273) - LONG, 1 value
        WriteIfdEntry(writer, TIFFTag_StripOffsets, TIFFType_LONG, 1, (uint)pixelDataOffset);

        // SamplesPerPixel (277) - SHORT, 1 value, value=1 (grayscale)
        WriteIfdEntryShortValue(writer, TIFFTag_SamplesPerPixel, 1, 1);

        // RowsPerStrip (278) - LONG, 1 value
        WriteIfdEntry(writer, TIFFTag_RowsPerStrip, TIFFType_LONG, 1, (uint)frame.Height);

        // StripByteCounts (279) - LONG, 1 value
        WriteIfdEntry(writer, TIFFTag_StripByteCounts, TIFFType_LONG, 1, (uint)pixelDataSize);

        // XResolution (282) - RATIONAL, 1 value, offset to value
        WriteIfdEntry(writer, TIFFTag_XResolution, TIFFType_RATIONAL, 1, (uint)xResolutionOffset);

        // YResolution (283) - RATIONAL, 1 value, offset to value
        WriteIfdEntry(writer, TIFFTag_YResolution, TIFFType_RATIONAL, 1, (uint)yResolutionOffset);

        // ResolutionUnit (296) - SHORT, 1 value, value=1 (none)
        WriteIfdEntryShortValue(writer, TIFFTag_ResolutionUnit, 1, TIFF_ResolutionUnit_None);

        writer.Write((uint)0); // Next IFD offset (0 = none)

        // Write extra values
        // XResolution rational (1/1)
        writer.Write((uint)1); // Numerator
        writer.Write((uint)1); // Denominator

        // YResolution rational (1/1)
        writer.Write((uint)1); // Numerator
        writer.Write((uint)1); // Denominator

        // Write pixel data
        foreach (ushort pixel in frame.Pixels)
        {
            writer.Write(pixel);
        }

        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Writes an IFD entry (12 bytes).
    /// </summary>
    private static void WriteIfdEntry(BinaryWriter writer, ushort tag, ushort type, uint count, uint value)
    {
        writer.Write(tag);        // 2 bytes
        writer.Write(type);       // 2 bytes
        writer.Write(count);      // 4 bytes
        writer.Write(value);      // 4 bytes
    }

    /// <summary>
    /// Writes an IFD entry for SHORT type with count=1 (value stored in value field).
    /// </summary>
    private static void WriteIfdEntryShortValue(BinaryWriter writer, ushort tag, uint count, ushort value)
    {
        writer.Write(tag);             // 2 bytes
        writer.Write(TIFFType_SHORT);  // 2 bytes
        writer.Write(count);           // 4 bytes
        writer.Write((uint)value);     // 4 bytes (value, not offset)
    }

    /// <summary>
    /// Writes a frame to a TIFF file.
    /// </summary>
    public async Task WriteAsync(string filePath, FrameData frame, CancellationToken cancellationToken = default)
    {
        await SaveAsync(filePath, frame, cancellationToken);
    }
}
