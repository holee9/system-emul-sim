using FluentAssertions;
using Xunit;
using PanelSimulator.Generators;

namespace PanelSimulator.Tests.Generators;

/// <summary>
/// Tests for CheckerboardPatternGenerator.
/// REQ-SIM-014: Checkerboard test pattern (alternating max/zero).
/// </summary>
public class CheckerboardPatternGeneratorTests
{
    [Fact]
    public void CheckerboardPatternGenerator_shall_exist()
    {
        // Arrange & Act
        var generator = new CheckerboardPatternGenerator();

        // Assert
        generator.Should().NotBeNull();
    }

    [Fact]
    public void Generate_shall_create_checkerboard_pattern()
    {
        // Arrange
        var generator = new CheckerboardPatternGenerator();
        int width = 1024;
        int height = 1024;
        int bitDepth = 16;

        // Act
        var pixels = generator.Generate(width, height, bitDepth, 0);

        // Assert
        pixels.Should().NotBeNull();
        pixels.Length.Should().Be(width * height);
    }

    [Theory]
    [InlineData(1024, 1024, 16)]
    [InlineData(512, 512, 14)]
    [InlineData(128, 128, 16)]
    public void Generate_shall_alternate_max_and_zero(int width, int height, int bitDepth)
    {
        // Arrange
        var generator = new CheckerboardPatternGenerator();

        // Act
        var pixels = generator.Generate(width, height, bitDepth, 0);

        // Assert
        // REQ-SIM-014: Even pixels = 0, odd pixels = max value. Pattern inverts every other row.
        int maxValue = (1 << bitDepth) - 1;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                int expectedValue;
                bool isEvenPixel = (row % 2 == 0) ? (col % 2 == 0) : (col % 2 == 1);
                expectedValue = isEvenPixel ? 0 : maxValue;

                int actualValue = pixels[row * width + col];
                actualValue.Should().Be(expectedValue,
                    $"pixel at [{row}][{col}] should be {expectedValue} but was {actualValue}");
            }
        }
    }

    [Fact]
    public void Generate_shall_be_deterministic()
    {
        // Arrange
        var generator = new CheckerboardPatternGenerator();
        int width = 512;
        int height = 512;
        int bitDepth = 16;

        // Act
        var pixels1 = generator.Generate(width, height, bitDepth, 0);
        var pixels2 = generator.Generate(width, height, bitDepth, 0);

        // Assert
        pixels1.Should().BeEquivalentTo(pixels2);
    }
}
