using XrayDetector.Common.Dto;
using Xunit;

namespace XrayDetector.Sdk.Tests.Common.Dto;

/// <summary>
/// Specification tests for FrameMetadata value object.
/// Tests frame metadata structure, validation, and immutability.
/// </summary>
public class FrameMetadataTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesMetadata()
    {
        // Arrange
        const int width = 1024;
        const int height = 1024;
        const int bitDepth = 16;
        var timestamp = DateTime.UtcNow;
        const uint frameNumber = 42;

        // Act
        var metadata = new FrameMetadata(width, height, bitDepth, timestamp, frameNumber);

        // Assert
        Assert.Equal(width, metadata.Width);
        Assert.Equal(height, metadata.Height);
        Assert.Equal(bitDepth, metadata.BitDepth);
        Assert.Equal(timestamp, metadata.Timestamp);
        Assert.Equal(frameNumber, metadata.FrameNumber);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1024, 1024)]
    [InlineData(2048, 2048)]
    [InlineData(3072, 3072)]
    public void Constructor_AcceptsValidDimensions(int width, int height)
    {
        // Act
        var metadata = new FrameMetadata(width, height, 16, DateTime.UtcNow, 1);

        // Assert
        Assert.Equal(width, metadata.Width);
        Assert.Equal(height, metadata.Height);
    }

    [Theory]
    [InlineData(0, 1024)]
    [InlineData(1024, 0)]
    [InlineData(-1, 1024)]
    [InlineData(1024, -1)]
    public void Constructor_InvalidDimensions_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FrameMetadata(width, height, 16, DateTime.UtcNow, 1));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(16)]
    public void Constructor_AcceptsValidBitDepths(int bitDepth)
    {
        // Act
        var metadata = new FrameMetadata(1024, 1024, bitDepth, DateTime.UtcNow, 1);

        // Assert
        Assert.Equal(bitDepth, metadata.BitDepth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(32)]
    public void Constructor_InvalidBitDepth_ThrowsArgumentOutOfRangeException(int bitDepth)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FrameMetadata(1024, 1024, bitDepth, DateTime.UtcNow, 1));
    }

    [Fact]
    public void FrameNumber_WrapsAtUintMaxValue()
    {
        // Arrange
        const uint maxFrame = uint.MaxValue;
        const uint expectedNext = 0;

        // Act
        var metadata1 = new FrameMetadata(1024, 1024, 16, DateTime.UtcNow, maxFrame);
        var metadata2 = metadata1.WithNextFrameNumber();

        // Assert
        Assert.Equal(maxFrame, metadata1.FrameNumber);
        Assert.Equal(expectedNext, metadata2.FrameNumber);
    }

    [Fact]
    public void WithNextFrameNumber_IncrementsFrameNumber()
    {
        // Arrange
        var original = new FrameMetadata(1024, 1024, 16, DateTime.UtcNow, 42);

        // Act
        var updated = original.WithNextFrameNumber();

        // Assert - immutability verified
        Assert.NotSame(original, updated);
        Assert.Equal(42u, original.FrameNumber);
        Assert.Equal(43u, updated.FrameNumber);

        // Other fields preserved
        Assert.Equal(original.Width, updated.Width);
        Assert.Equal(original.Height, updated.Height);
        Assert.Equal(original.BitDepth, updated.BitDepth);
        Assert.Equal(original.Timestamp, updated.Timestamp);
    }

    [Fact]
    public void PixelCount_ReturnsTotalPixels()
    {
        // Arrange
        var metadata = new FrameMetadata(1024, 768, 16, DateTime.UtcNow, 1);

        // Act
        var pixelCount = metadata.PixelCount;

        // Assert
        Assert.Equal(1024 * 768, pixelCount);
    }

    [Fact]
    public void BytesPerFrame_ReturnsCorrectSizeFor16Bit()
    {
        // Arrange
        var metadata = new FrameMetadata(1024, 1024, 16, DateTime.UtcNow, 1);

        // Act
        var bytesPerFrame = metadata.BytesPerFrame;

        // Assert
        Assert.Equal(1024 * 1024 * 2, bytesPerFrame); // 16-bit = 2 bytes per pixel
    }

    [Fact]
    public void BytesPerFrame_ReturnsCorrectSizeFor14Bit()
    {
        // Arrange
        var metadata = new FrameMetadata(512, 512, 14, DateTime.UtcNow, 1);

        // Act
        var bytesPerFrame = metadata.BytesPerFrame;

        // Assert
        Assert.Equal(512 * 512 * 2, bytesPerFrame); // 14-bit stored in 2 bytes
    }

    [Fact]
    public void BytesPerFrame_ReturnsCorrectSizeFor8Bit()
    {
        // Arrange
        var metadata = new FrameMetadata(640, 480, 8, DateTime.UtcNow, 1);

        // Act
        var bytesPerFrame = metadata.BytesPerFrame;

        // Assert
        Assert.Equal(640 * 480 * 1, bytesPerFrame); // 8-bit = 1 byte per pixel
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var metadata1 = new FrameMetadata(1024, 1024, 16, timestamp, 42);
        var metadata2 = new FrameMetadata(1024, 1024, 16, timestamp, 42);

        // Act & Assert
        Assert.Equal(metadata1, metadata2);
        Assert.Equal(metadata1.GetHashCode(), metadata2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentFrameNumber_ReturnsFalse()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var metadata1 = new FrameMetadata(1024, 1024, 16, timestamp, 42);
        var metadata2 = new FrameMetadata(1024, 1024, 16, timestamp, 43);

        // Act & Assert
        Assert.NotEqual(metadata1, metadata2);
    }

    [Fact]
    public void ToString_ReturnsHumanReadableString()
    {
        // Arrange
        var timestamp = new DateTime(2026, 2, 18, 12, 30, 45, DateTimeKind.Utc);
        var metadata = new FrameMetadata(1024, 768, 16, timestamp, 42);

        // Act
        var result = metadata.ToString();

        // Assert
        Assert.Contains("1024x768", result);
        Assert.Contains("16-bit", result);
        Assert.Contains("42", result);
    }
}
