using FluentAssertions;
using ParameterExtractor.Core.Models;
using ParameterExtractor.Core.Services;
using Xunit;

namespace ParameterExtractor.Tests.Services;

/// <summary>
/// Unit tests for RuleValidator following TDD methodology.
/// AC-TOOLS-002: Rule engine for validating extracted parameters.
/// </summary>
public class RuleValidatorTests
{
    private readonly RuleValidator _sut = new();

    [Fact]
    public void Validate_ShouldReturnValid_ForParameterWithNoConstraints()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Test Parameter",
            Value = "100"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Valid);
        result.ValidationMessage.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenValueBelowMin()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Pixel Pitch",
            Value = "0",
            Min = 1
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Error);
        result.ValidationMessage.Should().Contain("below minimum");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenValueExceedsMax()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Rows",
            Value = "5000",
            Max = 4096
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Error);
        result.ValidationMessage.Should().Contain("exceeds maximum");
    }

    [Fact]
    public void Validate_ShouldValidateBitDepth_OnlyAllows14Or16()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Bit Depth",
            Value = "12"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Error);
        result.ValidationMessage.Should().Contain("must be 14 or 16");
    }

    [Fact]
    public void Validate_ShouldAcceptBitDepth14()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "ADC Bit Depth",
            Value = "14"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Valid);
    }

    [Fact]
    public void Validate_ShouldAcceptBitDepth16()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "ADC Bit Depth",
            Value = "16"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Valid);
    }

    [Fact]
    public void Validate_ShouldValidatePixelPitchGreaterThanZero()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Pixel Pitch",
            Value = "0"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Error);
        result.ValidationMessage.Should().Contain("greater than 0");
    }

    [Fact]
    public void Validate_ShouldWarnForPixelPitchExceedingTypical()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Pixel Pitch",
            Value = "600"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Warning);
        result.ValidationMessage.Should().Contain("exceeds typical range");
    }

    [Fact]
    public void Validate_ShouldValidateRowsInRange()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Rows",
            Value = "100"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Error);
        result.ValidationMessage.Should().Contain("between 256 and 4096");
    }

    [Fact]
    public void Validate_ShouldValidateColsInRange()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Columns",
            Value = "5000"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Error);
        result.ValidationMessage.Should().Contain("between 256 and 4096");
    }

    [Fact]
    public void Validate_ShouldValidateGateTimingGreaterThanZero()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Gate ON Time",
            Value = "0"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Error);
        result.ValidationMessage.Should().Contain("must be greater than 0");
    }

    [Fact]
    public void Validate_ShouldWarnForGateTimingExceeding1000us()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Gate ON",
            Value = "1500"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Warning);
        result.ValidationMessage.Should().Contain("exceeds 1000 us");
    }

    [Fact]
    public void Validate_ShouldValidateLaneCount()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Lane Count",
            Value = "3"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Error);
        result.ValidationMessage.Should().Contain("must be 1, 2, or 4");
    }

    [Fact]
    public void Validate_ShouldWarnForLaneSpeedOutsideVerifiedRange()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Speed Mbps",
            Value = "1500"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Warning);
        result.ValidationMessage.Should().Contain("outside verified range");
    }

    [Fact]
    public void ValidateMany_ShouldReturnAllValidationStatuses()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "Rows", Value = "2048" },
            new() { Name = "Pixel Pitch", Value = "150" },
            new() { Name = "Bit Depth", Value = "16" },
            new() { Name = "Invalid", Value = "100", Max = 50 }
        };

        // Act
        var results = _sut.ValidateMany(parameters);

        // Assert
        results.Should().HaveCount(4);
        results["Rows"].Should().Be(ValidationStatus.Valid);
        results["Pixel Pitch"].Should().Be(ValidationStatus.Valid);
        results["Bit Depth"].Should().Be(ValidationStatus.Valid);
        results["Invalid"].Should().Be(ValidationStatus.Error);
    }

    [Fact]
    public void AddRule_ShouldAllowCustomRuleRegistration()
    {
        // Arrange
        var validator = new RuleValidator();
        var customParam = new ParameterInfo
        {
            Name = "CustomParameter",
            Value = "50"
        };

        validator.AddRule("CustomParameter", p =>
        {
            if (p.NumericValue == 50)
                return (ValidationStatus.Error, "50 is not allowed");

            return (ValidationStatus.Valid, string.Empty);
        });

        // Act
        var result = validator.Validate(customParam);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Error);
        result.ValidationMessage.Should().Be("50 is not allowed");
    }

    [Fact]
    public void Validate_ShouldHandleNonNumericValue()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Mode",
            Value = "RAW16"
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Valid);
        result.ValidationMessage.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldHandleEmptyValue()
    {
        // Arrange
        var param = new ParameterInfo
        {
            Name = "Empty Param",
            Value = ""
        };

        // Act
        var result = _sut.Validate(param);

        // Assert
        result.ValidationStatus.Should().Be(ValidationStatus.Valid);
    }
}
