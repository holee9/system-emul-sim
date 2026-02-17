using FluentAssertions;
using Xunit;
using PanelSimulator.Generators;

namespace PanelSimulator.Tests.Generators;

/// <summary>
/// Tests for CounterPatternGenerator.
/// AC-SIM-001: PanelSimulator Counter Pattern verification.
/// </summary>
public class CounterPatternGeneratorTests
{
    [Fact]
    public void CounterPatternGenerator_shall_exist()
    {
        // Arrange & Act
        var generator = new CounterPatternGenerator();

        // Assert
        generator.Should().NotBeNull();
    }

    [Fact]
    public void Generate_shall_create_counter_pattern_for_frame()
    {
        // Arrange
        var generator = new CounterPatternGenerator();
        int width = 1024;
        int height = 1024;
        int bitDepth = 16;
        int frameNumber = 0;

        // Act
        var pixels = generator.Generate(width, height, bitDepth, frameNumber);

        // Assert
        pixels.Should().NotBeNull();
        pixels.Length.Should().Be(width * height);
    }

    [Theory]
    [InlineData(1024, 1024, 16, 0)]   // Full frame
    [InlineData(512, 512, 14, 1)]     // Half frame
    [InlineData(2048, 2048, 16, 5)]   // Larger frame
    public void Generate_shall_produce_correct_counter_values(int width, int height, int bitDepth, int frameNumber)
    {
        // Arrange
        var generator = new CounterPatternGenerator();

        // Act
        var pixels = generator.Generate(width, height, bitDepth, frameNumber);

        // Assert
        // AC-SIM-001: pixel[r][c] == (r * cols + c) % 2^bit_depth
        int maxValue = (1 << bitDepth) - 1;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                int expectedValue = (row * width + col) % (maxValue + 1);
                int actualValue = pixels[row * width + col];
                actualValue.Should().Be(expectedValue,
                    $"pixel at [{row}][{col}] should be {expectedValue} but was {actualValue}");
            }
        }
    }

    [Fact]
    public void Generate_shall_clamp_to_bit_depth()
    {
        // Arrange
        var generator = new CounterPatternGenerator();
        int width = 256;
        int height = 256;
        int bitDepth = 14;  // Max value = 16383

        // Act
        var pixels = generator.Generate(width, height, bitDepth, 0);

        // Assert
        int maxValue = (1 << bitDepth) - 1;
        pixels.Should().OnlyContain(v => v <= maxValue);
    }

    [Fact]
    public void Generate_shall_be_deterministic_with_same_input()
    {
        // Arrange
        var generator = new CounterPatternGenerator();
        int width = 512;
        int height = 512;
        int bitDepth = 16;
        int frameNumber = 42;

        // Act
        var pixels1 = generator.Generate(width, height, bitDepth, frameNumber);
        var pixels2 = generator.Generate(width, height, bitDepth, frameNumber);

        // Assert
        pixels1.Should().BeEquivalentTo(pixels2);
    }
}
