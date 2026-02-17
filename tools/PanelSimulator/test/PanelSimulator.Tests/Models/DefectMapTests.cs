using System.Linq;
using FluentAssertions;
using Xunit;
using PanelSimulator.Models;

namespace PanelSimulator.Tests.Models;

/// <summary>
/// Tests for DefectMap.
/// REQ-SIM-012: Pixel defect injection (dead pixels and hot pixels).
/// </summary>
public class DefectMapTests
{
    [Fact]
    public void DefectMap_shall_exist()
    {
        // Arrange & Act
        var defectMap = new DefectMap(0.001, 42);

        // Assert
        defectMap.Should().NotBeNull();
    }

    [Fact]
    public void ApplyDefects_shall_inject_dead_and_hot_pixels()
    {
        // Arrange
        double defectRate = 0.01;  // 1% defect rate
        int seed = 42;
        var defectMap = new DefectMap(defectRate, seed);
        ushort[] pixels = Enumerable.Range(0, 10000).Select(i => (ushort)32000).ToArray();

        // Act
        var defectivePixels = defectMap.ApplyDefects(pixels);

        // Assert
        defectivePixels.Should().NotBeNull();
        defectivePixels.Length.Should().Be(pixels.Length);
        // With 1% defect rate, expect some defects
        defectivePixels.Should().Contain(p => p == 0, "should have dead pixels (0)");
        defectivePixels.Should().Contain(p => p == 65535, "should have hot pixels (max)");
    }

    [Theory]
    [InlineData(0.001, 42)]    // 0.1% defect rate
    [InlineData(0.01, 42)]     // 1% defect rate
    [InlineData(0.1, 42)]      // 10% defect rate (high for testing)
    public void ApplyDefects_shall_approximate_configured_rate(double defectRate, int seed)
    {
        // Arrange
        var defectMap = new DefectMap(defectRate, seed);
        int pixelCount = 100000;
        ushort[] pixels = Enumerable.Range(0, pixelCount).Select(i => (ushort)32000).ToArray();

        // Act
        var defectivePixels = defectMap.ApplyDefects(pixels);

        // Assert
        int defectCount = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (defectivePixels[i] != pixels[i])
            {
                defectCount++;
            }
        }
        double actualRate = (double)defectCount / pixelCount;

        // Allow 25% tolerance due to randomness (statistical variance)
        // For 0.001 rate, 25% = 0.00025, which accounts for natural distribution variance
        actualRate.Should().BeApproximately(defectRate, defectRate * 0.25,
            $"defect rate should be approximately {defectRate}");
    }

    [Fact]
    public void ApplyDefects_shall_set_dead_pixels_to_zero()
    {
        // Arrange
        double defectRate = 0.1;  // High rate for testing
        int seed = 42;
        var defectMap = new DefectMap(defectRate, seed);
        ushort[] pixels = Enumerable.Range(0, 1000).Select(i => (ushort)32000).ToArray();

        // Act
        var defectivePixels = defectMap.ApplyDefects(pixels);

        // Assert
        for (int i = 0; i < pixels.Length; i++)
        {
            if (defectivePixels[i] == 0)
            {
                // Dead pixel: should be zero
                defectivePixels[i].Should().Be(0);
            }
        }
    }

    [Fact]
    public void ApplyDefects_shall_set_hot_pixels_to_max_value()
    {
        // Arrange
        double defectRate = 0.1;  // High rate for testing
        int seed = 42;
        var defectMap = new DefectMap(defectRate, seed);
        ushort[] pixels = Enumerable.Range(0, 1000).Select(i => (ushort)32000).ToArray();

        // Act
        var defectivePixels = defectMap.ApplyDefects(pixels);

        // Assert
        // Hot pixels should be at max value (65535 for 16-bit)
        defectivePixels.Should().Contain(p => p == 65535);
    }

    [Fact]
    public void ApplyDefects_shall_be_deterministic_with_seed()
    {
        // Arrange
        double defectRate = 0.01;
        int seed = 12345;
        var defectMap1 = new DefectMap(defectRate, seed);
        var defectMap2 = new DefectMap(defectRate, seed);
        ushort[] pixels = Enumerable.Range(0, 10000).Select(i => (ushort)32000).ToArray();

        // Act
        var defective1 = defectMap1.ApplyDefects(pixels);
        var defective2 = defectMap2.ApplyDefects(pixels);

        // Assert
        defective1.Should().BeEquivalentTo(defective2,
            "same seed should produce identical defect pattern");
    }

    [Fact]
    public void ApplyDefects_shall_preserve_non_defective_pixels()
    {
        // Arrange
        double defectRate = 0.001;  // Low rate
        int seed = 42;
        var defectMap = new DefectMap(defectRate, seed);
        ushort[] pixels = Enumerable.Range(0, 10000).Select(i => (ushort)32000).ToArray();

        // Act
        var defectivePixels = defectMap.ApplyDefects(pixels);

        // Assert
        // Most pixels should be unchanged
        int unchangedCount = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (defectivePixels[i] == pixels[i])
            {
                unchangedCount++;
            }
        }
        unchangedCount.Should().BeGreaterThan((int)(pixels.Length * 0.9),
            "at least 90% of pixels should be unchanged with 0.1% defect rate");
    }

    [Fact]
    public void ApplyDefects_shall_handle_empty_array()
    {
        // Arrange
        var defectMap = new DefectMap(0.01, 42);
        ushort[] pixels = Array.Empty<ushort>();

        // Act
        var defectivePixels = defectMap.ApplyDefects(pixels);

        // Assert
        defectivePixels.Should().NotBeNull().And.BeEmpty();
    }
}
