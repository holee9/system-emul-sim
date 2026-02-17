using System.Text.Json;
using Common.Dto.Dtos;
using FluentAssertions;
using Xunit;

namespace Common.Dto.Tests.Dtos;

/// <summary>
/// Tests for FrameData DTO specification.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including FrameData.
/// </summary>
public class FrameDataTests
{
    [Fact]
    public void FrameData_shall_be_immutable_record()
    {
        // Arrange & Act
        var frameData = new FrameData(1, 1024, 2048, new ushort[1024 * 2048]);

        // Assert
        frameData.Should().NotBeNull();
        frameData.GetType().IsClass.Should().BeTrue();
        frameData.GetType().IsValueType.Should().BeFalse();
        // Records in C# are reference types with value semantics
        frameData.GetType().Name.Should().Be("FrameData");
    }

    [Fact]
    public void FrameData_should_have_required_properties()
    {
        // Arrange
        var expectedFrameNumber = 1;
        var expectedWidth = 1024;
        var expectedHeight = 2048;
        var expectedPixels = new ushort[expectedWidth * expectedHeight];

        // Act
        var frameData = new FrameData(expectedFrameNumber, expectedWidth, expectedHeight, expectedPixels);

        // Assert
        frameData.FrameNumber.Should().Be(expectedFrameNumber);
        frameData.Width.Should().Be(expectedWidth);
        frameData.Height.Should().Be(expectedHeight);
        frameData.Pixels.Should().BeSameAs(expectedPixels);
    }

    [Fact]
    public void FrameData_should_validate_width_is_positive()
    {
        // Arrange
        var pixels = new ushort[1024];

        // Act
        var act = () => new FrameData(1, 0, 1024, pixels);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("width");
    }

    [Fact]
    public void FrameData_should_validate_height_is_positive()
    {
        // Arrange
        var pixels = new ushort[1024];

        // Act
        var act = () => new FrameData(1, 1024, 0, pixels);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("height");
    }

    [Fact]
    public void FrameData_should_validate_pixels_array_size()
    {
        // Arrange
        var pixels = new ushort[100];

        // Act
        var act = () => new FrameData(1, 1024, 2048, pixels);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("pixels");
    }

    [Fact]
    public void FrameData_should_validate_pixels_is_not_null()
    {
        // Act
        var act = () => new FrameData(1, 1024, 2048, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pixels");
    }

    [Fact]
    public void FrameData_should_be_serializable_to_json()
    {
        // Arrange
        var frameData = new FrameData(1, 1024, 2048, new ushort[1024 * 2048]);

        // Act
        var json = JsonSerializer.Serialize(frameData);
        var deserialized = JsonSerializer.Deserialize<FrameData>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.FrameNumber.Should().Be(frameData.FrameNumber);
        deserialized.Width.Should().Be(frameData.Width);
        deserialized.Height.Should().Be(frameData.Height);
        deserialized.Pixels.Should().HaveCount(frameData.Pixels.Length);
    }

    [Fact]
    public void FrameData_should_override_ToString()
    {
        // Arrange
        var frameData = new FrameData(42, 1024, 2048, new ushort[1024 * 2048]);

        // Act
        var result = frameData.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("FrameNumber");
        result.Should().Contain("42");
        result.Should().Contain("1024");
        result.Should().Contain("2048");
    }

    [Fact]
    public void FrameData_should_implement_value_equality()
    {
        // Arrange
        var pixels = new ushort[] { 1, 2, 3, 4 };
        var frame1 = new FrameData(1, 2, 2, pixels);
        var frame2 = new FrameData(1, 2, 2, pixels);
        var frame3 = new FrameData(2, 2, 2, pixels);

        // Act & Assert
        frame1.Should().Be(frame2);
        frame1.Should().NotBe(frame3);
        (frame1 == frame2).Should().BeTrue();
        (frame1 == frame3).Should().BeFalse();
    }

    [Fact]
    public void FrameData_should_support_with_expression()
    {
        // Arrange
        var original = new FrameData(1, 1024, 2048, new ushort[1024 * 2048]);

        // Act
        var modified = original with { FrameNumber = 2 };

        // Assert
        modified.FrameNumber.Should().Be(2);
        modified.Width.Should().Be(original.Width);
        modified.Height.Should().Be(original.Height);
        modified.Pixels.Should().BeSameAs(original.Pixels);
    }
}
