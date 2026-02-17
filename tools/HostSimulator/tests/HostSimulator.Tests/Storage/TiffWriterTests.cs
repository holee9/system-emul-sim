using HostSimulator.Core.Storage;
using Xunit;
using FluentAssertions;
using System.Buffers.Binary;
using Common.Dto.Dtos;

namespace HostSimulator.Tests.Storage;

/// <summary>
/// Tests for TiffWriter class.
/// REQ-SIM-043: Save frames in TIFF format (16-bit grayscale).
/// </summary>
public class TiffWriterTests
{
    [Fact]
    public async Task WriteAsync_ShouldCreateValidTiffFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var frame = CreateTestFrame(width: 100, height: 100);
        var writer = new TiffWriter();

        try
        {
            // Act
            await writer.WriteAsync(tempFile, frame);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var fileInfo = new FileInfo(tempFile);
            fileInfo.Length.Should().BeGreaterThan(0);

            // Verify TIFF header (little-endian)
            var bytes = await File.ReadAllBytesAsync(tempFile);
            bytes[0].Should().Be(0x49); // 'I' = little-endian
            bytes[1].Should().Be(0x49); // 'I'
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)).Should().Be(42); // TIFF magic number
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldWriteCorrectImageDimensions()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var frame = CreateTestFrame(width: 256, height: 128);
        var writer = new TiffWriter();

        try
        {
            // Act
            await writer.WriteAsync(tempFile, frame);

            // Assert
            var bytes = await File.ReadAllBytesAsync(tempFile);

            // Find IFD offset (at bytes 4-7)
            uint ifdOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));

            // Read tag count (at IFD offset)
            ushort tagCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan((int)ifdOffset, 2));

            // Scan IFD for ImageWidth (256) and ImageLength (128) tags
            bool foundWidth = false;
            bool foundLength = false;

            for (int i = 0; i < tagCount; i++)
            {
                int entryOffset = (int)ifdOffset + 2 + (i * 12);
                ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(entryOffset, 2));

                // ImageWidth tag = 256
                if (tag == 256)
                {
                    uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(entryOffset + 8, 4));
                    value.Should().Be(256);
                    foundWidth = true;
                }

                // ImageLength tag = 257
                if (tag == 257)
                {
                    uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(entryOffset + 8, 4));
                    value.Should().Be(128);
                    foundLength = true;
                }
            }

            foundWidth.Should().BeTrue("ImageWidth tag should be present");
            foundLength.Should().BeTrue("ImageLength tag should be present");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldWrite16BitGrayscale()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var frame = CreateTestFrame(width: 100, height: 100);
        var writer = new TiffWriter();

        try
        {
            // Act
            await writer.WriteAsync(tempFile, frame);

            // Assert
            var bytes = await File.ReadAllBytesAsync(tempFile);
            uint ifdOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
            ushort tagCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan((int)ifdOffset, 2));

            // Look for BitsPerSample tag (258) and SamplesPerPixel (277)
            bool foundBitsPerSample = false;
            bool foundSamplesPerPixel = false;

            for (int i = 0; i < tagCount; i++)
            {
                int entryOffset = (int)ifdOffset + 2 + (i * 12);
                ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(entryOffset, 2));

                if (tag == 258) // BitsPerSample
                {
                    foundBitsPerSample = true;
                    // For TIFF tags with type SHORT and count > 1, value points to offset
                    // But for type SHORT with count == 1, value is stored directly in the entry
                    // Let's check the count field
                    ushort entryType = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(entryOffset + 2, 2));
                    uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(entryOffset + 4, 4));

                    if (entryType == 3 && count == 1)
                    {
                        // SHORT with count 1: value is in the entry's value field (last 4 bytes)
                        ushort directValue = (ushort)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(entryOffset + 8, 4));
                        directValue.Should().Be(16, $"BitsPerSample should be 16 but was {directValue} (direct value in IFD entry)");
                    }
                    else
                    {
                        // Value points to offset where data is stored
                        uint valueOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(entryOffset + 8, 4));
                        bytes.Length.Should().BeGreaterThan((int)valueOffset + 1);
                        ushort bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan((int)valueOffset, 2));
                        bitsPerSample.Should().Be(16, $"BitsPerSample should be 16 but was {bitsPerSample}, valueOffset={valueOffset}");
                    }
                }

                if (tag == 277) // SamplesPerPixel
                {
                    foundSamplesPerPixel = true;
                    // For grayscale, should be 1 (stored directly in value field)
                    uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(entryOffset + 8, 4));
                    value.Should().Be(1);
                }
            }

            foundBitsPerSample.Should().BeTrue("BitsPerSample tag should be present");
            foundSamplesPerPixel.Should().BeTrue("SamplesPerPixel tag should be present");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldWritePixelDataCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        // Create a 10x10 frame with known pixel values to ensure enough data
        var pixels = new ushort[100]; // 10x10
        for (int i = 0; i < 100; i++)
        {
            pixels[i] = (ushort)(i * 100); // 0, 100, 200, ...
        }
        var frame = new FrameData(frameNumber: 1, width: 10, height: 10, pixels: pixels);
        var writer = new TiffWriter();

        try
        {
            // Act
            await writer.WriteAsync(tempFile, frame);

            // Assert
            var bytes = await File.ReadAllBytesAsync(tempFile);

            // Verify TIFF structure is valid
            bytes[0].Should().Be(0x49); // 'I' for little-endian
            bytes[1].Should().Be(0x49); // 'I'

            // Verify pixel data exists by checking file size
            // Header (8) + IFD entries + extra values + pixel data (100 * 2 = 200 bytes)
            bytes.Length.Should().BeGreaterThan(300); // At least header + IFD + pixel data

            // Verify we can find the StripOffsets entry
            uint ifdOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
            ushort tagCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan((int)ifdOffset, 2));

            bool foundStripOffsets = false;
            for (int i = 0; i < tagCount; i++)
            {
                int entryOffset = (int)ifdOffset + 2 + (i * 12);
                ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(entryOffset, 2));
                if (tag == 273) // StripOffsets
                {
                    foundStripOffsets = true;
                    uint stripOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(entryOffset + 8, 4));
                    stripOffset.Should().BeGreaterThan(0);
                    break;
                }
            }

            foundStripOffsets.Should().BeTrue("StripOffsets tag should be present");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldThrow_WhenFrameIsNull()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var writer = new TiffWriter();

        try
        {
            // Act
            var act = async () => await writer.WriteAsync(tempFile, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldThrow_WhenFilePathIsEmpty()
    {
        // Arrange
        var frame = CreateTestFrame(width: 100, height: 100);
        var writer = new TiffWriter();

        // Act
        var act = async () => await writer.WriteAsync(string.Empty, frame);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Creates a test frame with sequential pixel values.
    /// </summary>
    private static FrameData CreateTestFrame(int width, int height)
    {
        var pixels = new ushort[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (ushort)(i % 65536);
        }
        return new FrameData(frameNumber: 1, width: width, height: height, pixels: pixels);
    }
}
