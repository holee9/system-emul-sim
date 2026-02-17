using HostSimulator.Core.Storage;
using Xunit;
using FluentAssertions;
using System.Buffers.Binary;
using Common.Dto.Dtos;

namespace HostSimulator.Tests.Storage;

/// <summary>
/// Tests for RawWriter class.
/// REQ-SIM-043: Save frames in RAW format (flat binary).
/// </summary>
public class RawWriterTests
{
    [Fact]
    public async Task WriteAsync_ShouldCreateValidRawFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var frame = CreateTestFrame(width: 100, height: 100);
        var writer = new RawWriter();

        try
        {
            // Act
            await writer.WriteAsync(tempFile, frame);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var fileInfo = new FileInfo(tempFile);
            int expectedSize = 100 * 100 * 2; // 100x100 pixels, 2 bytes per pixel
            fileInfo.Length.Should().Be(expectedSize);
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
        var pixels = new ushort[] { 100, 200, 300, 400 };
        var frame = new FrameData(frameNumber: 1, width: 2, height: 2, pixels: pixels);
        var writer = new RawWriter();

        try
        {
            // Act
            await writer.WriteAsync(tempFile, frame);

            // Assert
            var bytes = await File.ReadAllBytesAsync(tempFile);
            bytes.Length.Should().Be(8); // 4 pixels * 2 bytes

            // Verify pixel data (little-endian)
            var readPixels = new ushort[4];
            for (int i = 0; i < 4; i++)
            {
                readPixels[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2, 2));
            }

            readPixels.Should().Equal(pixels);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldWriteLittleEndian()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var pixels = new ushort[] { 0x1234 }; // 4660 in decimal
        var frame = new FrameData(frameNumber: 1, width: 1, height: 1, pixels: pixels);
        var writer = new RawWriter();

        try
        {
            // Act
            await writer.WriteAsync(tempFile, frame);

            // Assert
            var bytes = await File.ReadAllBytesAsync(tempFile);
            bytes[0].Should().Be(0x34); // Low byte
            bytes[1].Should().Be(0x12); // High byte
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldHandleLargeFrames()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var frame = CreateTestFrame(width: 1024, height: 1024);
        var writer = new RawWriter();

        try
        {
            // Act
            await writer.WriteAsync(tempFile, frame);

            // Assert
            var fileInfo = new FileInfo(tempFile);
            int expectedSize = 1024 * 1024 * 2; // 2MB
            fileInfo.Length.Should().Be(expectedSize);
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
        var writer = new RawWriter();

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
        var writer = new RawWriter();

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
