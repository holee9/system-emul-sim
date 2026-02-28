using Common.Dto.Dtos;
using FluentAssertions;
using IntegrationTests.Helpers;
using Xunit;

namespace IntegrationTests.Helpers;

/// <summary>
/// Tests for TestFrameFactory using TDD approach.
/// RED-GREEN-REFACTOR cycle validated.
/// </summary>
public class TestFrameFactoryTests
{
    [Fact]
    public void CreateTestFrame_WithSolidPattern_CreatesUniformFrame()
    {
        // Arrange & Act
        var frame = TestFrameFactory.CreateTestFrame(100, 100, TestFrameFactory.PatternType.Solid);

        // Assert
        frame.Width.Should().Be(100);
        frame.Height.Should().Be(100);
        frame.Pixels.Length.Should().Be(100 * 100);

        // All pixels should be mid-gray (32768)
        frame.Pixels.Should().OnlyContain(p => p == 32768);
    }

    [Fact]
    public void CreateTestFrame_WithGradientPattern_CreatesHorizontalGradient()
    {
        // Arrange & Act
        var frame = TestFrameFactory.CreateTestFrame(256, 64, TestFrameFactory.PatternType.Gradient);

        // Assert
        frame.Width.Should().Be(256);
        frame.Height.Should().Be(64);

        // Left edge should be 0
        frame.Pixels[0].Should().Be(0);
        // Right edge should be 65535
        frame.Pixels[255].Should().Be(65535);
        // For 256 width: pixel at x is (x * 65535) / 255
        // So x=128: (128 * 65535) / 255 = 32896
        frame.Pixels[128].Should().Be(32896);
    }

    [Fact]
    public void CreateTestFrame_WithCheckerboardPattern_CreatesAlternatingPixels()
    {
        // Arrange & Act
        var frame = TestFrameFactory.CreateTestFrame(32, 32, TestFrameFactory.PatternType.Checkerboard);

        // Assert
        // Checkerboard is 8x8, so pixels [0,0] through [7,7] are same color (white)
        frame.Pixels[0].Should().Be(65535);
        // Pixel at column 8 should be black (next checker)
        frame.Pixels[8].Should().Be(0);
        // Pixel at row 8, col 0 should be black (next checker)
        frame.Pixels[8 * 32].Should().Be(0);
    }

    [Fact]
    public void CreateSolidFrame_WithDefaultValue_CreatesMidGrayFrame()
    {
        // Arrange & Act
        var frame = TestFrameFactory.CreateSolidFrame(50, 50);

        // Assert
        frame.Width.Should().Be(50);
        frame.Height.Should().Be(50);
        frame.Pixels.Should().OnlyContain(p => p == 32768);
    }

    [Fact]
    public void CreateSolidFrame_WithCustomValue_UsesCustomValue()
    {
        // Arrange & Act
        var frame = TestFrameFactory.CreateSolidFrame(50, 50, value: 10000);

        // Assert
        frame.Pixels.Should().OnlyContain(p => p == 10000);
    }

    [Fact]
    public void CreateGradientFrame_WithStandardResolution_CreatesGradient()
    {
        // Arrange & Act
        var frame = TestFrameFactory.CreateGradientFrame(1024, 1024);

        // Assert
        frame.Width.Should().Be(1024);
        frame.Height.Should().Be(1024);
        frame.Pixels[0].Should().Be(0);
        frame.Pixels[1023].Should().Be(65535);
    }

    [Fact]
    public void Create1024Gradient_CreatesStandardFrame()
    {
        // Arrange & Act
        var frame = TestFrameFactory.Create1024Gradient();

        // Assert
        frame.Width.Should().Be(1024);
        frame.Height.Should().Be(1024);
    }

    [Fact]
    public void Create2048Gradient_CreatesStandardFrame()
    {
        // Arrange & Act
        var frame = TestFrameFactory.Create2048Gradient();

        // Assert
        frame.Width.Should().Be(2048);
        frame.Height.Should().Be(2048);
    }

    [Fact]
    public void Create3072Gradient_CreatesStandardFrame()
    {
        // Arrange & Act
        var frame = TestFrameFactory.Create3072Gradient();

        // Assert
        frame.Width.Should().Be(3072);
        frame.Height.Should().Be(3072);
    }

    [Fact]
    public void CreateTestFrame_WithFrameNumber_SetsFrameNumber()
    {
        // Arrange & Act
        var frame = TestFrameFactory.CreateTestFrame(64, 64, TestFrameFactory.PatternType.Solid, frameNumber: 42);

        // Assert
        frame.FrameNumber.Should().Be(42);
    }

    [Fact]
    public void CreateTestFrame_WithInvalidDimensions_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            TestFrameFactory.CreateTestFrame(0, 100, TestFrameFactory.PatternType.Solid));

        Assert.Throws<ArgumentException>(() =>
            TestFrameFactory.CreateTestFrame(100, 0, TestFrameFactory.PatternType.Solid));
    }

    [Fact]
    public void CreateGradientFrame_LargeResolution_HandlesCorrectly()
    {
        // Arrange & Act
        var frame = TestFrameFactory.CreateGradientFrame(3072, 3072);

        // Assert
        frame.Pixels.Length.Should().Be(3072 * 3072);
        frame.Pixels.Min().Should().Be(0);
        frame.Pixels.Max().Should().Be(65535);
    }

    [Fact]
    public void CreateSolidFrame_EdgeCaseOnePixel_WorksCorrectly()
    {
        // Arrange & Act
        var frame = TestFrameFactory.CreateSolidFrame(1, 1);

        // Assert
        frame.Pixels.Length.Should().Be(1);
        frame.Pixels[0].Should().Be(32768);
    }
}
