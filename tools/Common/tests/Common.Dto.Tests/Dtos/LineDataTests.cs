using System.Text.Json;
using Common.Dto.Dtos;
using FluentAssertions;
using Xunit;

namespace Common.Dto.Tests.Dtos;

/// <summary>
/// Tests for LineData DTO specification.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including LineData.
/// </summary>
public class LineDataTests
{
    [Fact]
    public void LineData_shall_be_immutable_record()
    {
        // Arrange & Act
        var lineData = new LineData(1, 0, new ushort[1024]);

        // Assert
        lineData.Should().NotBeNull();
        lineData.GetType().IsClass.Should().BeTrue();
        lineData.GetType().IsValueType.Should().BeFalse();
        lineData.GetType().Name.Should().Be("LineData");
    }

    [Fact]
    public void LineData_should_have_required_properties()
    {
        // Arrange
        var expectedFrameNumber = 1;
        var expectedLineNumber = 100;
        var expectedPixels = new ushort[1024];

        // Act
        var lineData = new LineData(expectedFrameNumber, expectedLineNumber, expectedPixels);

        // Assert
        lineData.FrameNumber.Should().Be(expectedFrameNumber);
        lineData.LineNumber.Should().Be(expectedLineNumber);
        lineData.Pixels.Should().BeSameAs(expectedPixels);
    }

    [Fact]
    public void LineData_should_validate_line_number_is_non_negative()
    {
        // Arrange
        var pixels = new ushort[1024];

        // Act
        var act = () => new LineData(1, -1, pixels);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("lineNumber");
    }

    [Fact]
    public void LineData_should_validate_pixels_is_not_null()
    {
        // Act
        var act = () => new LineData(1, 0, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pixels");
    }

    [Fact]
    public void LineData_should_validate_pixels_is_not_empty()
    {
        // Arrange
        var pixels = Array.Empty<ushort>();

        // Act
        var act = () => new LineData(1, 0, pixels);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("pixels");
    }

    [Fact]
    public void LineData_should_be_serializable_to_json()
    {
        // Arrange
        var lineData = new LineData(1, 100, new ushort[] { 1, 2, 3, 4 });

        // Act
        var json = JsonSerializer.Serialize(lineData);
        var deserialized = JsonSerializer.Deserialize<LineData>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.FrameNumber.Should().Be(lineData.FrameNumber);
        deserialized.LineNumber.Should().Be(lineData.LineNumber);
        deserialized.Pixels.Should().BeEquivalentTo(lineData.Pixels);
    }

    [Fact]
    public void LineData_should_override_ToString()
    {
        // Arrange
        var lineData = new LineData(42, 100, new ushort[] { 1, 2, 3, 4 });

        // Act
        var result = lineData.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("FrameNumber");
        result.Should().Contain("42");
        result.Should().Contain("LineNumber");
        result.Should().Contain("100");
    }

    [Fact]
    public void LineData_should_implement_value_equality()
    {
        // Arrange
        var pixels = new ushort[] { 1, 2, 3, 4 };
        var line1 = new LineData(1, 0, pixels);
        var line2 = new LineData(1, 0, pixels);
        var line3 = new LineData(1, 1, pixels);

        // Act & Assert
        line1.Should().Be(line2);
        line1.Should().NotBe(line3);
        (line1 == line2).Should().BeTrue();
        (line1 == line3).Should().BeFalse();
    }

    [Fact]
    public void LineData_should_support_with_expression()
    {
        // Arrange
        var original = new LineData(1, 0, new ushort[] { 1, 2, 3, 4 });

        // Act
        var modified = original with { LineNumber = 10 };

        // Assert
        modified.LineNumber.Should().Be(10);
        modified.FrameNumber.Should().Be(original.FrameNumber);
        modified.Pixels.Should().BeSameAs(original.Pixels);
    }
}
