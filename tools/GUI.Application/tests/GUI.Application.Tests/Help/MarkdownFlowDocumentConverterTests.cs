using System.Windows.Documents;
using XrayDetector.Gui.Help;

namespace XrayDetector.Gui.Tests.Help;

/// <summary>
/// TDD tests for MarkdownFlowDocumentConverter (SPEC-HELP-001 Wave 2).
/// RED phase: Tests written before implementation.
/// </summary>
public class MarkdownFlowDocumentConverterTests
{
    private readonly MarkdownFlowDocumentConverter _sut;

    public MarkdownFlowDocumentConverterTests()
    {
        _sut = new MarkdownFlowDocumentConverter();
    }

    [Fact]
    public void Convert_WithEmptyString_ShouldReturnFlowDocument()
    {
        // Act
        var result = _sut.Convert(string.Empty);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FlowDocument>();
    }

    [Fact]
    public void Convert_WithNullString_ShouldReturnFlowDocument()
    {
        // Act
        var result = _sut.Convert(null!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FlowDocument>();
    }

    [Fact]
    public void Convert_WithMarkdownContent_ShouldReturnFlowDocument()
    {
        // Arrange
        var markdown = "# Test Heading\n\nSome paragraph text.";

        // Act
        var result = _sut.Convert(markdown);

        // Assert
        result.Should().NotBeNull();
        result.Blocks.Should().NotBeEmpty("converted markdown should produce blocks");
    }

    [Fact]
    public void Convert_WithHeading_ShouldProduceBlocks()
    {
        // Arrange
        var markdown = "# System Overview\n\nThis is content.";

        // Act
        var result = _sut.Convert(markdown);

        // Assert
        result.Blocks.Count.Should().BeGreaterThan(0, "heading and paragraph should produce blocks");
    }

    [Fact]
    public void Convert_WithCodeBlock_ShouldProduceBlocks()
    {
        // Arrange
        var markdown = "Some text\n\n```\ncode here\n```\n\nMore text";

        // Act
        var result = _sut.Convert(markdown);

        // Assert
        result.Blocks.Should().NotBeEmpty();
    }

    [Fact]
    public void Convert_WithTableContent_ShouldProduceBlocks()
    {
        // Arrange
        var markdown = "| Header1 | Header2 |\n|---------|----------|\n| Cell1 | Cell2 |";

        // Act
        var result = _sut.Convert(markdown);

        // Assert
        result.Blocks.Should().NotBeEmpty("table should produce at least one block");
    }

    [Fact]
    public void Convert_WithRegularText_ShouldProduceParagraph()
    {
        // Arrange
        var markdown = "This is a regular paragraph of text.";

        // Act
        var result = _sut.Convert(markdown);

        // Assert
        result.Blocks.Should().NotBeEmpty();
        result.Blocks.Should().ContainItemsAssignableTo<Paragraph>();
    }
}
