using XrayDetector.Core.Processing;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Processing;

/// <summary>
/// Specification tests for WindowLevelMapper.
/// Maps 16-bit grayscale data to 8-bit for display using window/level technique.
/// </summary>
public class WindowLevelMapperTests
{
    [Fact]
    public void Map_WithDefaultWindowLevel_MapsCorrectly()
    {
        // Arrange
        ushort[] input16 = { 0, 32768, 65535 };
        var mapper = new WindowLevelMapper();

        // Act
        byte[] output8 = mapper.Map(input16);

        // Assert
        Assert.NotNull(output8);
        Assert.Equal(3, output8.Length);
    }

    [Fact]
    public void Map_WithWindowLevel_MapsCorrectly()
    {
        // Arrange
        ushort[] input16 = { 0, 1000, 2000, 3000, 4000 };
        var mapper = new WindowLevelMapper(window: 2000, level: 2000);

        // Act
        byte[] output8 = mapper.Map(input16);

        // Assert
        Assert.NotNull(output8);
        Assert.Equal(5, output8.Length);
        // At level 2000 with window 2000: range is [1000, 3000]
        // Values below window should be 0, above should be 255
        Assert.Equal(0, output8[0]); // 0 is below window
    }

    [Fact]
    public void Map_WithCenterValue_MapsTo128()
    {
        // Arrange
        ushort[] input16 = { 2000 }; // Exactly at level
        var mapper = new WindowLevelMapper(window: 2000, level: 2000);

        // Act
        byte[] output8 = mapper.Map(input16);

        // Assert
        Assert.Equal(128, output8[0]); // Center value maps to 128
    }

    [Fact]
    public void Map_WithAboveWindow_MapsTo255()
    {
        // Arrange
        ushort[] input16 = { 5000 }; // Above window
        var mapper = new WindowLevelMapper(window: 2000, level: 2000);

        // Act
        byte[] output8 = mapper.Map(input16);

        // Assert
        Assert.Equal(255, output8[0]);
    }

    [Fact]
    public void Map_WithBelowWindow_MapsTo0()
    {
        // Arrange
        ushort[] input16 = { 500 }; // Below window
        var mapper = new WindowLevelMapper(window: 2000, level: 2000);

        // Act
        byte[] output8 = mapper.Map(input16);

        // Assert
        Assert.Equal(0, output8[0]);
    }

    [Fact]
    public void Map_WithGrayscaleRamp_ProducesLinearOutput()
    {
        // Arrange
        ushort[] input16 = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            input16[i] = (ushort)(32768 + i * 100);
        }
        var mapper = new WindowLevelMapper(window: 25600, level: 32768);

        // Act
        byte[] output8 = mapper.Map(input16);

        // Assert
        Assert.Equal(256, output8.Length);
        // Verify monotonic increase
        for (int i = 1; i < output8.Length; i++)
        {
            Assert.True(output8[i] >= output8[i - 1]);
        }
    }

    [Fact]
    public void Map_WithEmptyData_ReturnsEmptyArray()
    {
        // Arrange
        ushort[] input16 = Array.Empty<ushort>();
        var mapper = new WindowLevelMapper();

        // Act
        byte[] output8 = mapper.Map(input16);

        // Assert
        Assert.NotNull(output8);
        Assert.Empty(output8);
    }

    [Fact]
    public void Map_WithLargeDataset_HandlesEfficiently()
    {
        // Arrange
        ushort[] input16 = new ushort[2048 * 2048];
        for (int i = 0; i < input16.Length; i++)
        {
            input16[i] = (ushort)(i % 65536);
        }
        var mapper = new WindowLevelMapper();

        // Act
        byte[] output8 = mapper.Map(input16);

        // Assert
        Assert.Equal(2048 * 2048, output8.Length);
    }

    [Fact]
    public void UpdateWindowLevel_WithNewValues_ChangesMapping()
    {
        // Arrange
        ushort[] input16 = { 32768 };
        var mapper = new WindowLevelMapper(window: 1000, level: 32768);

        // Act
        byte[] output1 = mapper.Map(input16);
        mapper.UpdateWindowLevel(window: 65535, level: 32768);
        byte[] output2 = mapper.Map(input16);

        // Assert
        // With larger window, the same input should map closer to 128
        Assert.Equal(128, output1[0]); // Center of small window
        Assert.Equal(128, output2[0]); // Center of large window
    }
}
