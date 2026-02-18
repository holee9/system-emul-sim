using System.Buffers;
using System.Text.Json;
using XrayDetector.Common.Dto;

namespace XrayDetector.Core.Processing;

/// <summary>
/// Image encoder for TIFF and RAW formats.
/// Supports 16-bit grayscale X-ray image encoding.
/// </summary>
public sealed class ImageEncoder
{
    /// <summary>
    /// Encodes 16-bit grayscale pixel data as TIFF file.
    /// Uses LibTiff.Net format with proper tags for medical imaging.
    /// </summary>
    /// <param name="pixelData">16-bit grayscale pixel data.</param>
    /// <param name="metadata">Frame metadata.</param>
    /// <param name="outputPath">Output TIFF file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EncodeTiffAsync(
        ushort[] pixelData,
        FrameMetadata metadata,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (pixelData == null)
            throw new ArgumentNullException(nameof(pixelData));
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

        await Task.Run(() =>
        {
            using var fs = File.Create(outputPath);
            using var writer = new BinaryWriter(fs);

            // TIFF Header (Little-endian)
            writer.Write((ushort)0x4949); // "II" - Little-endian
            writer.Write((ushort)42);      // TIFF magic number
            writer.Write((uint)8);         // Offset to first IFD

            // IFD (Image File Directory) with 10 entries
            const int entryCount = 10;
            long ifdStart = 8;
            long dataStart = ifdStart + 2 + (entryCount * 12) + 4;

            // Write placeholder IFD
            long ifdPosition = fs.Position;
            writer.Write((ushort)entryCount); // Entry count

            // IFD Entries (12 bytes each)
            int currentEntry = 0;

            // ImageWidth (TAG 256, SHORT, 1 value)
            WriteIfdEntry(writer, 256, 3, 1, (uint)metadata.Width, ref currentEntry);

            // ImageLength (TAG 257, SHORT, 1 value)
            WriteIfdEntry(writer, 257, 3, 1, (uint)metadata.Height, ref currentEntry);

            // BitsPerSample (TAG 258, SHORT, 1 value = 16)
            WriteIfdEntry(writer, 258, 3, 1, 16, ref currentEntry);

            // Compression (TAG 259, SHORT, 1 value = 1 = none)
            WriteIfdEntry(writer, 259, 3, 1, 1, ref currentEntry);

            // PhotometricInterpretation (TAG 262, SHORT, 1 value = 1 = MINISBLACK)
            WriteIfdEntry(writer, 262, 3, 1, 1, ref currentEntry);

            // StripOffsets (TAG 273, LONG, 1 value)
            WriteIfdEntry(writer, 273, 4, 1, (uint)dataStart, ref currentEntry);

            // SamplesPerPixel (TAG 277, SHORT, 1 value = 1)
            WriteIfdEntry(writer, 277, 3, 1, 1, ref currentEntry);

            // RowsPerStrip (TAG 278, LONG/SHORT, 1 value = full height)
            WriteIfdEntry(writer, 278, 3, 1, (uint)metadata.Height, ref currentEntry);

            // StripByteCounts (TAG 279, LONG, 1 value)
            uint byteCount = (uint)((uint)metadata.Width * (uint)metadata.Height * 2);
            WriteIfdEntry(writer, 279, 4, 1, byteCount, ref currentEntry);

            // XResolution (TAG 282, RATIONAL, 1 value = pointer to data)
            WriteIfdEntry(writer, 282, 5, 1, (uint)(dataStart + byteCount), ref currentEntry);

            // Next IFD offset (0 = none)
            writer.Write((uint)0);

            // Seek to data position
            fs.Seek(dataStart, SeekOrigin.Begin);

            // Write pixel data (big-endian 16-bit)
            foreach (ushort pixel in pixelData)
            {
                byte high = (byte)((pixel >> 8) & 0xFF);
                byte low = (byte)(pixel & 0xFF);
                writer.Write(high);
                writer.Write(low);
            }

            // Write XResolution (72/1 = 72 DPI)
            writer.Write((uint)72);
            writer.Write((uint)1);
        }, cancellationToken);
    }

    /// <summary>
    /// Encodes 16-bit grayscale pixel data as RAW binary with JSON sidecar.
    /// </summary>
    /// <param name="pixelData">16-bit grayscale pixel data.</param>
    /// <param name="metadata">Frame metadata.</param>
    /// <param name="outputPath">Output RAW file path (JSON sidecar auto-generated).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EncodeRawAsync(
        ushort[] pixelData,
        FrameMetadata metadata,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (pixelData == null)
            throw new ArgumentNullException(nameof(pixelData));
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

        await Task.Run(() =>
        {
            // Write binary RAW file (little-endian)
            using (var fs = File.Create(outputPath))
            using (var writer = new BinaryWriter(fs))
            {
                foreach (ushort pixel in pixelData)
                {
                    writer.Write((ushort)pixel); // Little-endian
                }
            }

            // Write JSON sidecar
            string jsonPath = Path.ChangeExtension(outputPath, ".json");
            var sidecar = new
            {
                width = metadata.Width,
                height = metadata.Height,
                bitDepth = metadata.BitDepth,
                timestamp = metadata.Timestamp.Ticks,
                frameNumber = metadata.FrameNumber,
                format = "RAW16",
                endianness = "little"
            };

            string json = JsonSerializer.Serialize(sidecar, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(jsonPath, json);
        }, cancellationToken);
    }

    private static void WriteIfdEntry(BinaryWriter writer, ushort tag, ushort type, uint count, uint value, ref int entryIndex)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(value);

        // Padding for 4-byte alignment (12 bytes total)
        if (type == 3) // SHORT
        {
            writer.Write((ushort)0);
        }

        entryIndex++;
    }
}
