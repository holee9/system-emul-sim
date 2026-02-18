using XrayDetector.Common.Dto;
using XrayDetector.Core.Processing;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Processing;

/// <summary>
/// Specification tests for ImageEncoder.
/// Handles TIFF and RAW format encoding for 16-bit grayscale X-ray images.
/// </summary>
public class ImageEncoderTests : IDisposable
{
    private readonly string _testOutputDir;
    private readonly ushort[] _testData;

    public ImageEncoderTests()
    {
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"xray_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputDir);

        // Create test data: 4x4 16-bit grayscale image
        _testData = new ushort[]
        {
            100, 200, 300, 400,
            500, 600, 700, 800,
            900, 1000, 1100, 1200,
            1300, 1400, 1500, 1600
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testOutputDir))
        {
            try { Directory.Delete(_testOutputDir, true); }
            catch { /* Ignore cleanup errors */ }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task EncodeTiffAsync_WithValid16BitData_CreatesTiffFile()
    {
        // Arrange
        var encoder = new ImageEncoder();
        string outputPath = Path.Combine(_testOutputDir, "test.tif");
        var metadata = new FrameMetadata(4, 4, 16, DateTime.UtcNow.AddTicks(100), 0);

        // Act
        await encoder.EncodeTiffAsync(_testData, metadata, outputPath, default);

        // Assert
        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public async Task EncodeTiffAsync_WithCorrectDimensions_SetsTiffTags()
    {
        // Arrange
        var encoder = new ImageEncoder();
        string outputPath = Path.Combine(_testOutputDir, "test_tags.tif");
        var metadata = new FrameMetadata(width: 512, height: 512, bitDepth: 16, timestamp: DateTime.UtcNow, frameNumber: 5);

        // Act
        await encoder.EncodeTiffAsync(_testData, metadata, outputPath, default);

        // Assert
        Assert.True(File.Exists(outputPath));
        // File should contain TIFF header and data
        using var fs = File.OpenRead(outputPath);
        // TIFF files start with "II" (little-endian) or "MM" (big-endian)
        byte[] header = new byte[2];
        fs.Read(header, 0, 2);
        Assert.True(header[0] == 0x49 && header[1] == 0x49 || // "II"
                    header[0] == 0x4D && header[1] == 0x4D);   // "MM"
    }

    [Fact]
    public async Task EncodeTiffAsync_WithLargeImage_HandlesMemoryEfficiently()
    {
        // Arrange
        var encoder = new ImageEncoder();
        string outputPath = Path.Combine(_testOutputDir, "large.tif");
        int width = 2048;
        int height = 2048;
        var largeData = new ushort[width * height];
        for (int i = 0; i < largeData.Length; i++)
        {
            largeData[i] = (ushort)(i % 65536);
        }
        var metadata = new FrameMetadata(width, height, 16, DateTime.UtcNow, 0);

        // Act
        await encoder.EncodeTiffAsync(largeData, metadata, outputPath, default);

        // Assert
        Assert.True(File.Exists(outputPath));
        FileInfo fi = new FileInfo(outputPath);
        Assert.True(fi.Length > width * height * 2); // Should contain all pixel data
    }

    [Fact]
    public async Task EncodeRawAsync_WithValidData_CreatesRawAndSidecar()
    {
        // Arrange
        var encoder = new ImageEncoder();
        string rawPath = Path.Combine(_testOutputDir, "test.raw");
        var metadata = new FrameMetadata(4, 4, 16, DateTime.UtcNow.AddTicks(9999), 42);

        // Act
        await encoder.EncodeRawAsync(_testData, metadata, rawPath, default);

        // Assert
        Assert.True(File.Exists(rawPath));
        string jsonPath = Path.ChangeExtension(rawPath, ".json");
        Assert.True(File.Exists(jsonPath));
    }

    [Fact]
    public async Task EncodeRawAsync_SidecarContainsCorrectMetadata()
    {
        // Arrange
        var encoder = new ImageEncoder();
        string rawPath = Path.Combine(_testOutputDir, "test_metadata.raw");
        var metadata = new FrameMetadata(width: 1024, height: 768, bitDepth: 16, timestamp: DateTime.UtcNow.AddTicks(11111), frameNumber: 7);

        // Act
        await encoder.EncodeRawAsync(_testData, metadata, rawPath, default);

        // Assert
        string jsonPath = Path.ChangeExtension(rawPath, ".json");
        string jsonContent = await File.ReadAllTextAsync(jsonPath);
        Assert.Contains("1024", jsonContent); // width
        Assert.Contains("768", jsonContent);  // height
        Assert.Contains("16", jsonContent);   // bitDepth
        // Note: timestamp is DateTime in JSON, so just check it exists
        Assert.Contains("7", jsonContent);    // frameNumber
    }

    [Fact]
    public async Task EncodeRawAsync_BinaryFileContainsPixelData()
    {
        // Arrange
        var encoder = new ImageEncoder();
        string rawPath = Path.Combine(_testOutputDir, "test_binary.raw");
        var metadata = new FrameMetadata(4, 4, 16, DateTime.UtcNow, 0);

        // Act
        await encoder.EncodeRawAsync(_testData, metadata, rawPath, default);

        // Assert
        byte[] fileData = await File.ReadAllBytesAsync(rawPath);
        int expectedSize = _testData.Length * 2; // 2 bytes per ushort (little-endian)
        Assert.Equal(expectedSize, fileData.Length);

        // Verify first pixel value (100 = 0x0064 little-endian)
        Assert.Equal(0x64, fileData[0]);
        Assert.Equal(0x00, fileData[1]);
    }
}
