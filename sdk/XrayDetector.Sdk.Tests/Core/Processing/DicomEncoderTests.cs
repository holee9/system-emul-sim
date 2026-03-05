using XrayDetector.Common.Dto;
using XrayDetector.Core.Processing;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Processing;

/// <summary>
/// Specification tests for DicomEncoder (REQ-SDK-041, optional).
/// DICOM format export for medical image integration.
/// </summary>
public class DicomEncoderTests : IDisposable
{
    private readonly DicomEncoder _encoder;
    private readonly string _testOutputDir;

    public DicomEncoderTests()
    {
        _encoder = new DicomEncoder();
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"dicom_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testOutputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testOutputDir))
        {
            try
            {
                Directory.Delete(_testOutputDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Create_WithDefaultParameters_CreatesEncoder()
    {
        // Arrange & Act
        var encoder = new DicomEncoder();

        // Assert
        Assert.NotNull(encoder);
    }

    [Fact]
    public void Create_WithCustomParameters_CreatesEncoder()
    {
        // Arrange & Act
        var encoder = new DicomEncoder(
            manufacturer: "Test Manufacturer",
            institutionName: "Test Institution",
            softwareVersion: "2.0.0");

        // Assert
        Assert.NotNull(encoder);
    }

    [Fact]
    public async Task EncodeDicomAsync_WithBasicData_CreatesFile()
    {
        // Arrange
        ushort[] pixelData = { 100, 200, 300, 400 };
        var metadata = new FrameMetadata(2, 2, 16, DateTime.UtcNow, 1);
        string outputPath = Path.Combine(_testOutputDir, "test.dcm");

        // Act
        await _encoder.EncodeDicomAsync(pixelData, metadata, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public async Task EncodeDicomAsync_WithPatientInfo_IncludesMetadata()
    {
        // Arrange
        ushort[] pixelData = { 100, 200, 300, 400 };
        var metadata = new FrameMetadata(2, 2, 16, DateTime.UtcNow, 1);
        string outputPath = Path.Combine(_testOutputDir, "test_patient.dcm");
        var patientInfo = new DicomPatientInfo
        {
            PatientName = "Doe^John",
            PatientId = "12345",
            BirthDate = "19800101",
            Sex = "M"
        };

        // Act
        await _encoder.EncodeDicomAsync(pixelData, metadata, outputPath, patientInfo);

        // Assert
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task EncodeDicomAsync_WithStudyInfo_IncludesMetadata()
    {
        // Arrange
        ushort[] pixelData = { 100, 200, 300, 400 };
        var metadata = new FrameMetadata(2, 2, 16, DateTime.UtcNow, 1);
        string outputPath = Path.Combine(_testOutputDir, "test_study.dcm");
        var studyInfo = new DicomStudyInfo
        {
            StudyDescription = "Test Study",
            AccessionNumber = "ACC001",
            SeriesNumber = "5"
        };

        // Act
        await _encoder.EncodeDicomAsync(pixelData, metadata, outputPath, studyInfo: studyInfo);

        // Assert
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task EncodeDicomAsync_WithLargeFrame_HandlesCorrectly()
    {
        // Arrange
        ushort[] pixelData = new ushort[512 * 512];
        for (int i = 0; i < pixelData.Length; i++)
        {
            pixelData[i] = (ushort)(i % 65536);
        }
        var metadata = new FrameMetadata(512, 512, 16, DateTime.UtcNow, 1);
        string outputPath = Path.Combine(_testOutputDir, "test_large.dcm");

        // Act
        await _encoder.EncodeDicomAsync(pixelData, metadata, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        FileInfo fi = new FileInfo(outputPath);
        Assert.True(fi.Length > pixelData.Length * 2); // At least pixel data + headers
    }

    [Fact]
    public async Task EncodeDicomAsync_WithNullPixelData_ThrowsArgumentNullException()
    {
        // Arrange
        var metadata = new FrameMetadata(2, 2, 16, DateTime.UtcNow, 1);
        string outputPath = Path.Combine(_testOutputDir, "test.dcm");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _encoder.EncodeDicomAsync(null!, metadata, outputPath));
    }

    [Fact]
    public async Task EncodeDicomAsync_WithNullMetadata_ThrowsArgumentNullException()
    {
        // Arrange
        ushort[] pixelData = { 100, 200 };
        string outputPath = Path.Combine(_testOutputDir, "test.dcm");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _encoder.EncodeDicomAsync(pixelData, null!, outputPath));
    }

    [Fact]
    public async Task EncodeDicomAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        ushort[] pixelData = { 100, 200 };
        var metadata = new FrameMetadata(2, 2, 16, DateTime.UtcNow, 1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _encoder.EncodeDicomAsync(pixelData, metadata, string.Empty));
    }

    [Fact]
    public void DicomPatientInfo_WithDefaultValues_HasDefaults()
    {
        // Arrange & Act
        var info = new DicomPatientInfo();

        // Assert
        Assert.Equal("Anonymous^Anonymous", info.PatientName);
        Assert.Equal("UNKNOWN", info.PatientId);
        Assert.Equal("O", info.Sex);
    }

    [Fact]
    public void DicomStudyInfo_WithDefaultConstructor_GeneratesUids()
    {
        // Arrange & Act
        var info = new DicomStudyInfo();

        // Assert
        Assert.NotEmpty(info.StudyInstanceUid);
        Assert.NotEmpty(info.SeriesInstanceUid);
        Assert.NotEqual(info.StudyInstanceUid, info.SeriesInstanceUid);
    }

    [Fact]
    public void DicomStudyInfo_WithCustomUids_UsesProvidedUids()
    {
        // Arrange & Act
        var info = new DicomStudyInfo("study-uid-123", "series-uid-456");

        // Assert
        Assert.Equal("study-uid-123", info.StudyInstanceUid);
        Assert.Equal("series-uid-456", info.SeriesInstanceUid);
    }
}
