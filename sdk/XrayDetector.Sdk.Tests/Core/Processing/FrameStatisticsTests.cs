using XrayDetector.Core.Processing;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Processing;

/// <summary>
/// Specification tests for FrameStatistics.
/// Provides lazy computation of min, max, mean values for image data.
/// </summary>
public class FrameStatisticsTests
{
    [Fact]
    public void Create_WithPixelData_CreatesStatistics()
    {
        // Arrange
        ushort[] data = { 100, 200, 300, 400, 500 };

        // Act
        var stats = new FrameStatistics(data);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(5, stats.PixelCount);
    }

    [Fact]
    public void ComputeMin_WithData_ReturnsMinimum()
    {
        // Arrange
        ushort[] data = { 1000, 500, 3000, 2500, 100 };
        var stats = new FrameStatistics(data);

        // Act
        ushort min = stats.Min;

        // Assert
        Assert.Equal((ushort)100, min);
    }

    [Fact]
    public void ComputeMax_WithData_ReturnsMaximum()
    {
        // Arrange
        ushort[] data = { 1000, 500, 3000, 2500, 100 };
        var stats = new FrameStatistics(data);

        // Act
        ushort max = stats.Max;

        // Assert
        Assert.Equal((ushort)3000, max);
    }

    [Fact]
    public void ComputeMean_WithData_ReturnsAverage()
    {
        // Arrange
        ushort[] data = { 100, 200, 300, 400, 500 };
        var stats = new FrameStatistics(data);

        // Act
        double mean = stats.Mean;

        // Assert
        Assert.Equal(300.0, mean);
    }

    [Fact]
    public void ComputeAll_WithData_CalculatesAllStatistics()
    {
        // Arrange
        ushort[] data = { 100, 200, 300, 400, 500 };
        var stats = new FrameStatistics(data);

        // Act
        ushort min = stats.Min;
        ushort max = stats.Max;
        double mean = stats.Mean;

        // Assert
        Assert.Equal((ushort)100, min);
        Assert.Equal((ushort)500, max);
        Assert.Equal(300.0, mean);
    }

    [Fact]
    public void ComputeAll_WithSinglePixel_ReturnsSameValue()
    {
        // Arrange
        ushort[] data = { 12345 };
        var stats = new FrameStatistics(data);

        // Act
        ushort min = stats.Min;
        ushort max = stats.Max;
        double mean = stats.Mean;

        // Assert
        Assert.Equal((ushort)12345, min);
        Assert.Equal((ushort)12345, max);
        Assert.Equal(12345.0, mean);
    }

    [Fact]
    public void ComputeAll_WithUniformData_ReturnsSameValue()
    {
        // Arrange
        ushort[] data = { 500, 500, 500, 500, 500 };
        var stats = new FrameStatistics(data);

        // Act
        ushort min = stats.Min;
        ushort max = stats.Max;
        double mean = stats.Mean;

        // Assert
        Assert.Equal((ushort)500, min);
        Assert.Equal((ushort)500, max);
        Assert.Equal(500.0, mean);
    }

    [Fact]
    public void ComputeMin_WithFullDynamicRange_ReturnsZero()
    {
        // Arrange
        ushort[] data = { 0, 1000, 30000, 65535 };
        var stats = new FrameStatistics(data);

        // Act
        ushort min = stats.Min;

        // Assert
        Assert.Equal((ushort)0, min);
    }

    [Fact]
    public void ComputeMax_WithFullDynamicRange_ReturnsMaxValue()
    {
        // Arrange
        ushort[] data = { 0, 1000, 30000, 65535 };
        var stats = new FrameStatistics(data);

        // Act
        ushort max = stats.Max;

        // Assert
        Assert.Equal((ushort)65535, max);
    }

    [Fact]
    public void ComputeMean_WithLargeDataset_CalculatesAccurately()
    {
        // Arrange
        ushort[] data = new ushort[1000];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (ushort)i;
        }
        var stats = new FrameStatistics(data);

        // Act
        double mean = stats.Mean;

        // Assert
        // Mean of 0 to 999 is 499.5
        Assert.Equal(499.5, mean);
    }
}
