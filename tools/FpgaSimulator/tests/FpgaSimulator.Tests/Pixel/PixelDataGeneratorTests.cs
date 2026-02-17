namespace FpgaSimulator.Tests.Pixel;

using FluentAssertions;
using FpgaSimulator.Core.Pixel;
using Xunit;

public class PixelDataGeneratorTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var generator = new PixelDataGenerator();

        // Assert
        generator.BitDepth.Should().Be(16);
        generator.PatternMode.Should().Be(PatternMode.Counter);
    }

    [Fact]
    public void Constructor_WithParameters_ShouldSetProperties()
    {
        // Arrange & Act
        var generator = new PixelDataGenerator(bitDepth: 14, patternMode: PatternMode.Constant);

        // Assert
        generator.BitDepth.Should().Be(14);
        generator.PatternMode.Should().Be(PatternMode.Constant);
    }

    [Fact]
    public void SetSeed_ShouldMakeOutputDeterministic()
    {
        // Arrange
        var gen1 = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Random);
        var gen2 = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Random);

        // Act
        gen1.SetSeed(42);
        gen2.SetSeed(42);

        var frame1 = gen1.GenerateFrame(10, 10);
        var frame2 = gen2.GenerateFrame(10, 10);

        // Assert
        frame1.Should().BeEquivalentTo(frame2);
    }

    [Fact]
    public void GenerateFrame_CounterPattern_ShouldProduceSequentialValues()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Counter);

        // Act
        var frame = generator.GenerateFrame(rows: 4, cols: 4);

        // Assert
        frame[0, 0].Should().Be(0);
        frame[0, 1].Should().Be(1);
        frame[0, 2].Should().Be(2);
        frame[1, 0].Should().Be(4);  // row 1 * cols 4 + col 0 = 4
        frame[3, 3].Should().Be(15); // row 3 * cols 4 + col 3 = 15
    }

    [Fact]
    public void GenerateFrame_ConstantPattern_ShouldProduceUniformValues()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Constant);
        generator.SetConstantValue(0x8000);

        // Act
        var frame = generator.GenerateFrame(rows: 10, cols: 10);

        // Assert
        for (int row = 0; row < 10; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                frame[row, col].Should().Be(0x8000);
            }
        }
    }

    [Fact]
    public void GenerateFrame_RandomPattern_ShouldProduceVaryingValues()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Random);
        generator.SetSeed(12345);

        // Act
        var frame = generator.GenerateFrame(rows: 100, cols: 100);

        // Assert - Check that not all values are the same
        var firstValue = frame[0, 0];
        var hasDifferentValue = false;
        for (int row = 0; row < 100; row++)
        {
            for (int col = 0; col < 100; col++)
            {
                if (frame[row, col] != firstValue)
                {
                    hasDifferentValue = true;
                    break;
                }
            }
            if (hasDifferentValue) break;
        }
        hasDifferentValue.Should().BeTrue();
    }

    [Fact]
    public void GenerateFrame_CheckerboardPattern_ShouldAlternateValues()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Checkerboard);

        // Act
        var frame = generator.GenerateFrame(rows: 4, cols: 4);

        // Assert
        frame[0, 0].Should().Be(0);       // Even position = 0
        frame[0, 1].Should().Be(0xFFFF);  // Odd position = max
        frame[0, 2].Should().Be(0);
        frame[1, 0].Should().Be(0xFFFF);  // Odd row, first = max (alternating rows)
        frame[1, 1].Should().Be(0);
    }

    [Fact]
    public void GenerateFrame_14BitDepth_ShouldClampValues()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 14, patternMode: PatternMode.Checkerboard);
        var maxValue = (ushort)((1 << 14) - 1); // 16383

        // Act
        var frame = generator.GenerateFrame(rows: 2, cols: 2);

        // Assert
        frame[0, 0].Should().Be(0);
        frame[0, 1].Should().Be(maxValue);
        frame[1, 0].Should().Be(maxValue);
        frame[1, 1].Should().Be(0);
    }

    [Fact]
    public void GenerateFrame_LargeFrame_ShouldSucceed()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Counter);

        // Act
        var frame = generator.GenerateFrame(rows: 2048, cols: 2048);

        // Assert
        frame.GetLength(0).Should().Be(2048);
        frame.GetLength(1).Should().Be(2048);
    }

    [Fact]
    public void GenerateFrame_MaximumSize_ShouldSucceed()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Counter);

        // Act
        var frame = generator.GenerateFrame(rows: 3072, cols: 3072);

        // Assert
        frame.GetLength(0).Should().Be(3072);
        frame.GetLength(1).Should().Be(3072);
        frame[3071, 3071].Should().Be((3071 * 3072 + 3071) % 65536); // Counter wrap check
    }

    [Fact]
    public void TwoSequentialFrames_CounterPattern_ShouldIncrement()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Counter);

        // Act
        var frame1 = generator.GenerateFrame(rows: 2, cols: 2);
        var frame2 = generator.GenerateFrame(rows: 2, cols: 2);

        // Assert - Frames should be identical (counter resets each frame)
        frame1.Should().BeEquivalentTo(frame2);
    }

    [Fact]
    public void SetConstantValue_ShouldUpdateAllPixels()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Constant);
        generator.SetConstantValue(0x1234);

        // Act
        var frame = generator.GenerateFrame(rows: 5, cols: 5);

        // Assert
        frame[0, 0].Should().Be(0x1234);
        frame[4, 4].Should().Be(0x1234);
    }

    [Fact]
    public void CounterPattern_16BitWrap_ShouldHandleOverflow()
    {
        // Arrange
        var generator = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Counter);

        // Act
        var frame = generator.GenerateFrame(rows: 256, cols: 256);

        // Assert
        // At position [255, 255], value = 255*256 + 255 = 65535 (max 16-bit)
        frame[255, 255].Should().Be(65535);
        // At position [0, 0], value = 0
        frame[0, 0].Should().Be(0);
    }

    [Fact]
    public void GenerateFrame_DifferentSeeds_ShouldProduceDifferentRandomFrames()
    {
        // Arrange
        var gen1 = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Random);
        var gen2 = new PixelDataGenerator(bitDepth: 16, patternMode: PatternMode.Random);

        // Act
        gen1.SetSeed(100);
        gen2.SetSeed(200);

        var frame1 = gen1.GenerateFrame(rows: 10, cols: 10);
        var frame2 = gen2.GenerateFrame(rows: 10, cols: 10);

        // Assert
        frame1.Should().NotBeEquivalentTo(frame2);
    }
}
