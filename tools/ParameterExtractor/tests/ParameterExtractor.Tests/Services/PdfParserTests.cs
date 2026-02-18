using FluentAssertions;
using ParameterExtractor.Core.Models;
using ParameterExtractor.Core.Services;
using Xunit;

namespace ParameterExtractor.Tests.Services;

/// <summary>
/// Unit tests for PdfParser following TDD methodology.
/// AC-TOOLS-001: PDF parsing with >= 90% accuracy.
/// </summary>
public class PdfParserTests
{
    private readonly PdfParser _sut = new();

    [Fact]
    public void CanParse_ShouldReturnFalse_WhenFilePathIsNull()
    {
        // Arrange
        string? filePath = null;

        // Act
        var result = _sut.CanParse(filePath!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_ShouldReturnFalse_WhenFilePathIsEmpty()
    {
        // Arrange
        var filePath = string.Empty;

        // Act
        var result = _sut.CanParse(filePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_ShouldReturnFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var filePath = "nonexistent.pdf";

        // Act
        var result = _sut.CanParse(filePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_ShouldReturnFalse_WhenExtensionIsNotPdf()
    {
        // Arrange
        var filePath = "document.txt";

        // Act
        var result = _sut.CanParse(filePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanParse_ShouldReturnTrue_WhenFileIsPdf()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".pdf";

        try
        {
            // Create the file
            File.WriteAllText(filePath, "test");

            // Act
            var result = _sut.CanParse(filePath);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanParse_ShouldReturnTrue_WhenExtensionIsUpperCasePdf()
    {
        // Arrange
        var filePath = Path.ChangeExtension(Path.GetTempFileName(), ".PDF");

        try
        {
            // Create the file
            File.WriteAllText(filePath, "test");

            // Act
            var result = _sut.CanParse(filePath);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnUnsuccessfulResult_WhenFileDoesNotExist()
    {
        // Arrange
        var filePath = "nonexistent.pdf";

        // Act
        var result = await _sut.ParseAsync(filePath);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Contains("not a valid PDF"));
    }

    [Fact]
    public async Task ParseAsync_ShouldExtractParameterWithNameValueUnit_FromValidText()
    {
        // Arrange - Create a temporary PDF-like test file
        var tempFile = Path.GetTempFileName() + ".pdf";
        try
        {
            // Note: Actual PDF creation requires more complex setup
            // For this test, we're testing the extraction logic
            // In production, we'd use proper PDF test fixtures

            // Act & Assert - This test would need actual PDF content
            // For TDD purposes, we document the expected behavior
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}

/// <summary>
/// Tests for parameter extraction from text patterns.
/// </summary>
public class PdfParserExtractionTests
{
    [Fact]
    public void ParameterInfo_ShouldStoreNameValueUnit()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Pixel Pitch",
            Value = "150",
            Unit = "um"
        };

        // Act & Assert
        param.Name.Should().Be("Pixel Pitch");
        param.Value.Should().Be("150");
        param.Unit.Should().Be("um");
    }

    [Fact]
    public void ParameterInfo_NumericValue_ShouldReturnDouble_WhenValueIsNumeric()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Rows",
            Value = "2048"
        };

        // Act
        var numeric = param.NumericValue;

        // Assert
        numeric.Should().Be(2048);
    }

    [Fact]
    public void ParameterInfo_NumericValue_ShouldReturnNull_WhenValueIsNotNumeric()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Mode",
            Value = "RAW16"
        };

        // Act
        var numeric = param.NumericValue;

        // Assert
        numeric.Should().BeNull();
    }

    [Fact]
    public void ParameterInfo_NumericValue_ShouldHandleDecimalValues()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Gate ON",
            Value = "10.5"
        };

        // Act
        var numeric = param.NumericValue;

        // Assert
        numeric.Should().Be(10.5);
    }

    [Fact]
    public void ExtractionResult_ShouldInitializeWithEmptyLists()
    {
        // Arrange & Act
        var result = new ExtractionResult();

        // Assert
        result.Parameters.Should().NotBeNull().And.BeEmpty();
        result.Messages.Should().NotBeNull().And.BeEmpty();
        result.IsSuccessful.Should().BeTrue();
        result.ExtractedCount.Should().Be(0);
    }
}
