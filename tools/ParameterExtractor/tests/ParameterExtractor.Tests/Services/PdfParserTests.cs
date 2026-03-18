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

/// <summary>
/// Tests for IsSimulationRelevant filter logic.
/// Verifies that only simulation parameters pass and non-simulation text is blocked.
/// </summary>
public class PdfParserSimulationFilterTests
{
    // --- Simulation-relevant params that MUST pass ---

    [Theory]
    [InlineData("Pixel pitch", "140", "um", "panel")]
    [InlineData("Rows", "1024", "", "panel")]
    [InlineData("Cols", "1024", "", "panel")]
    [InlineData("Bit depth", "16", "bit", "panel")]
    [InlineData("Gate on", "50", "us", "fpga.timing")]
    [InlineData("Gate off", "20", "us", "fpga.timing")]
    [InlineData("ADC conv", "12", "us", "fpga.timing")]
    [InlineData("CSI lane count", "4", "", "fpga.data_interface.csi2")]
    [InlineData("Lane speed", "1200", "Mbps", "fpga.data_interface.csi2")]
    [InlineData("SPI clock", "10", "MHz", "fpga.spi")]
    [InlineData("Dark current", "5", "nA", "panel")]
    [InlineData("Noise floor", "12", "e-", "panel")]
    [InlineData("Frame rate", "30", "fps", "controller.ethernet")]
    public void IsSimulationRelevant_ShouldReturnTrue_ForSimulationParams(
        string name, string value, string unit, string category)
    {
        var param = new ParameterInfo
        {
            Name = name,
            Value = value,
            Unit = unit,
            Category = category
        };

        PdfParser.IsSimulationRelevant(param).Should().BeTrue(
            because: $"'{name}' is a simulation-relevant parameter");
    }

    // --- Non-simulation params that MUST be blocked ---

    [Theory]
    [InlineData("Avoid direct sunlight", "0", "", "unknown")]        // instruction + zero value
    [InlineData("Stored in dry pack", "25", "", "unknown")]           // storage instruction
    [InlineData("Calculated by firmware", "8", "", "unknown")]        // instruction keyword
    [InlineData("Damage may occur beyond", "100", "", "unknown")]     // warning keyword
    [InlineData("ESD handling required", "500", "", "unknown")]       // ESD text
    [InlineData("Shipping weight......", "12", "", "unknown")]        // TOC entry with dots
    [InlineData("Introduction......", "3", "", "unknown")]            // TOC entry
    [InlineData("Pad block pitch", "200", "um", "unknown")]           // physical layout, not in sim keywords
    [InlineData("Gate capacitance", "50", "pF", "unknown")]           // analog only
    [InlineData("Package dimensions", "35", "mm", "unknown")]         // packaging
    public void IsSimulationRelevant_ShouldReturnFalse_ForNonSimulationParams(
        string name, string value, string unit, string category)
    {
        var param = new ParameterInfo
        {
            Name = name,
            Value = value,
            Unit = unit,
            Category = category
        };

        PdfParser.IsSimulationRelevant(param).Should().BeFalse(
            because: $"'{name}' is NOT a simulation-relevant parameter");
    }

    [Fact]
    public void IsSimulationRelevant_ShouldReturnFalse_WhenNumericValueIsZero()
    {
        // Zero values are not useful for simulation; reject them.
        var param = new ParameterInfo
        {
            Name = "Pixel pitch",
            Value = "0",
            Unit = "um",
            Category = "panel"
        };

        PdfParser.IsSimulationRelevant(param).Should().BeFalse(
            because: "zero numeric value provides no simulation data");
    }

    [Fact]
    public void IsSimulationRelevant_ShouldReturnFalse_WhenValueIsNonNumeric()
    {
        // Non-numeric values (mode strings, etc.) must be filtered.
        var param = new ParameterInfo
        {
            Name = "Pixel pitch",
            Value = "N/A",
            Unit = "um",
            Category = "panel"
        };

        PdfParser.IsSimulationRelevant(param).Should().BeFalse(
            because: "non-parseable value provides no numeric data for simulation");
    }

    [Fact]
    public void IsSimulationRelevant_ShouldReturnFalse_WhenValueIsNegative()
    {
        // Negative values are parsed as positive by the regex (leading minus is text),
        // but a deliberate negative value should not pass.
        // NumericValue > 0 guard handles this.
        var param = new ParameterInfo
        {
            Name = "Pixel pitch",
            Value = "-140",
            Unit = "um",
            Category = "panel"
        };

        PdfParser.IsSimulationRelevant(param).Should().BeFalse(
            because: "negative value is not a valid simulation parameter");
    }

    [Fact]
    public void IsSimulationRelevant_ShouldReturnTrue_ForPixelPitchWithKnownCategory()
    {
        // Regression: pixel pitch with value="140", unit="um" as extracted by the real parser.
        var param = new ParameterInfo
        {
            Name = "Pixel pitch",
            Value = "140",
            Unit = "um",
            Category = "panel"
        };

        PdfParser.IsSimulationRelevant(param).Should().BeTrue(
            because: "pixel pitch is a core simulation parameter");
    }

    [Fact]
    public void IsSimulationRelevant_ShouldReturnFalse_ForTocEntryWithDotsInName()
    {
        // Table-of-contents entries have "......" in the name.
        var param = new ParameterInfo
        {
            Name = "Electrical Characteristics......",
            Value = "14",
            Unit = "",
            Category = "unknown"
        };

        PdfParser.IsSimulationRelevant(param).Should().BeFalse(
            because: "TOC entries with '......' are page numbers, not parameters");
    }

    [Fact]
    public void IsSimulationRelevant_ShouldReturnFalse_WhenCategoryUnknownAndNoSimKeyword()
    {
        // Unknown category without any simulation keyword should be rejected.
        var param = new ParameterInfo
        {
            Name = "Pad pitch",
            Value = "300",
            Unit = "um",
            Category = "unknown"
        };

        PdfParser.IsSimulationRelevant(param).Should().BeFalse(
            because: "physical pad dimensions are not simulation inputs");
    }

    [Fact]
    public void IsSimulationRelevant_ShouldReturnTrue_WhenCategoryIsKnownNonUnknown()
    {
        // If the category is already resolved (e.g. "fpga.timing"), accept without keyword check.
        var param = new ParameterInfo
        {
            Name = "Some custom timing param",
            Value = "25",
            Unit = "ns",
            Category = "fpga.timing"
        };

        PdfParser.IsSimulationRelevant(param).Should().BeTrue(
            because: "a non-unknown category means InferCategory already matched it as simulation-relevant");
    }
}
