using Common.Dto.Dtos;
using FluentAssertions;
using HostSimulator.Core.Configuration;
using HostSimulator.Core.Storage;
using System.Buffers.Binary;
using Xunit;

using CoreHostSimulator = HostSimulator.Core.HostSimulator;

namespace HostSimulator.Tests.Storage;

/// <summary>
/// Round-trip tests verifying that RawWriter and TiffWriter persist pixel data correctly,
/// and that HostSimulator.Process returns an identical FrameData when given one directly.
/// REQ-SIM-043: Save frames in TIFF format (16-bit grayscale) and RAW format.
/// </summary>
public class HostStorageRoundTripTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a frame with a counter pattern:
    ///   pixels[r * cols + c] = (ushort)((r * cols + c) &amp; 0xFFFF)
    /// </summary>
    private static FrameData CreateCounterFrame(int rows, int cols)
    {
        var pixels = new ushort[rows * cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                pixels[r * cols + c] = (ushort)((r * cols + c) & 0xFFFF);
            }
        }
        return new FrameData(frameNumber: 1, width: cols, height: rows, pixels: pixels);
    }

    // ------------------------------------------------------------------
    // Test 1: RawWriter file size
    // ------------------------------------------------------------------

    /// <summary>
    /// Verifies that a RAW file written for a 64x64 frame occupies exactly
    /// 64 * 64 * 2 bytes (one little-endian ushort per pixel).
    /// </summary>
    [Fact]
    public async Task RawWriter_FileSize_EqualsRowsTimesColsTimesTwo()
    {
        // Arrange
        const int rows = 64;
        const int cols = 64;
        var frame = CreateCounterFrame(rows, cols);
        var writer = new RawWriter();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await writer.SaveAsync(tempFile, frame);

            // Assert
            var fileInfo = new FileInfo(tempFile);
            fileInfo.Length.Should().Be(rows * cols * 2,
                "each ushort pixel is stored as 2 bytes in little-endian format");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    // ------------------------------------------------------------------
    // Test 2: RawWriter round-trip pixel correctness
    // ------------------------------------------------------------------

    /// <summary>
    /// Saves a 32x32 counter-pattern frame as RAW, reads the raw bytes back,
    /// converts each little-endian pair to ushort, and verifies pixel-for-pixel equality.
    /// </summary>
    [Fact]
    public async Task RawWriter_RoundTrip_PixelsMatch()
    {
        // Arrange
        const int rows = 32;
        const int cols = 32;
        var frame = CreateCounterFrame(rows, cols);
        var writer = new RawWriter();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await writer.SaveAsync(tempFile, frame);

            // Assert
            var bytes = await File.ReadAllBytesAsync(tempFile);
            bytes.Length.Should().Be(rows * cols * 2);

            var readPixels = new ushort[rows * cols];
            for (int i = 0; i < readPixels.Length; i++)
            {
                readPixels[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2, 2));
            }

            readPixels.Should().Equal(frame.Pixels,
                "all pixel values must survive the RAW write/read round-trip unchanged");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    // ------------------------------------------------------------------
    // Test 3: TiffWriter round-trip pixel correctness
    // ------------------------------------------------------------------

    /// <summary>
    /// Saves a 32x32 counter-pattern frame as TIFF, then locates the pixel data
    /// at the known fixed offset within the file and verifies pixel-for-pixel equality.
    ///
    /// TIFF layout produced by TiffWriter:
    ///   Offset   0 –   7 : 8-byte header (II, 42, IFD offset = 8)
    ///   Offset   8 –   9 : IFD entry count (11, but 12 entries written)
    ///   Offset  10 – 153 : 12 IFD entries × 12 bytes = 144 bytes
    ///   Offset 154 – 157 : next IFD offset (0)
    ///   Offset 158 – 165 : XResolution rational (8 bytes)
    ///   Offset 166 – 173 : YResolution rational (8 bytes)
    ///   Offset 174 +     : pixel data (ushort little-endian, one per pixel)
    /// Note: TiffWriter declares 11 IFD entries but writes 12 (includes ResolutionUnit).
    /// </summary>
    [Fact]
    public async Task TiffWriter_RoundTrip_PixelsMatch()
    {
        // Arrange
        const int rows = 32;
        const int cols = 32;
        // 12 entries actually written (11 declared + ResolutionUnit)
        const int pixelDataOffset = 8 + 2 + (12 * 12) + 4 + 8 + 8; // = 174
        var frame = CreateCounterFrame(rows, cols);
        var writer = new TiffWriter();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await writer.SaveAsync(tempFile, frame);

            // Assert
            var bytes = await File.ReadAllBytesAsync(tempFile);
            bytes.Length.Should().BeGreaterThanOrEqualTo(pixelDataOffset + rows * cols * 2,
                "TIFF file must contain header, IFD, rationals, and pixel data");

            var readPixels = new ushort[rows * cols];
            for (int i = 0; i < readPixels.Length; i++)
            {
                int byteOffset = pixelDataOffset + i * 2;
                readPixels[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(byteOffset, 2));
            }

            readPixels.Should().Equal(frame.Pixels,
                "all pixel values must survive the TIFF write/read round-trip unchanged");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    // ------------------------------------------------------------------
    // Test 4: HostSimulator direct frame pass-through
    // ------------------------------------------------------------------

    /// <summary>
    /// Verifies that HostSimulator.Process(FrameData) returns the identical FrameData
    /// object when called with a direct frame (no UDP reassembly path).
    /// </summary>
    [Fact]
    public void HostSimulator_DirectFrameProcess_ReturnsIdenticalFrame()
    {
        // Arrange
        var simulator = new CoreHostSimulator();
        var config = new HostConfig
        {
            ListenPort = 8000,
            PacketTimeoutMs = 5000,
            ReceiveThreads = 1,
            OutputDirectory = null,
            SaveTiff = false,
            SaveRaw = false
        };
        simulator.Initialize(config);

        var pixels = new ushort[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = (ushort)(i & 0xFFFF);

        var frame = new FrameData(frameNumber: 42, width: 32, height: 32, pixels: pixels);

        // Act
        var result = simulator.Process(frame);

        // Assert
        result.Should().BeOfType<FrameData>("Process must return a FrameData for direct frame input");
        var resultFrame = (FrameData)result;
        resultFrame.Should().BeSameAs(frame,
            "HostSimulator passes FrameData through directly without copying or transforming it");
    }
}
