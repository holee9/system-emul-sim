using XrayDetector.Gui.Help;

namespace XrayDetector.Gui.Tests.Help;

/// <summary>
/// TDD tests for EmbeddedHelpContentService (SPEC-HELP-001 Wave 2).
/// RED phase: Tests written before implementation.
/// </summary>
public class EmbeddedHelpContentServiceTests
{
    private readonly EmbeddedHelpContentService _sut;

    public EmbeddedHelpContentServiceTests()
    {
        _sut = new EmbeddedHelpContentService();
    }

    [Fact]
    public void GetTopics_ShouldReturnNineTopics()
    {
        // Act
        var topics = _sut.GetTopics();

        // Assert
        topics.Should().HaveCount(9, "there are 9 help topics defined");
    }

    [Theory]
    [InlineData("overview", "시스템 개요")]
    [InlineData("getting-started", "빠른 시작 가이드")]
    [InlineData("panel-simulation", "Panel 시뮬레이션")]
    [InlineData("fpga-csi2", "FPGA/CSI-2 처리")]
    [InlineData("mcu-udp", "MCU/UDP 통신")]
    [InlineData("host-pipeline", "Host 파이프라인")]
    [InlineData("parameters-ref", "파라미터 레퍼런스")]
    [InlineData("keyboard-shortcuts", "키보드 단축키")]
    [InlineData("troubleshooting", "문제 해결")]
    public void GetTopics_ShouldContainExpectedTopic(string topicId, string expectedTitle)
    {
        // Act
        var topics = _sut.GetTopics();

        // Assert
        topics.Should().ContainSingle(t => t.Id == topicId && t.Title == expectedTitle,
            $"topic '{topicId}' with title '{expectedTitle}' should exist");
    }

    [Fact]
    public void GetTopic_WithValidId_ShouldReturnTopic()
    {
        // Act
        var topic = _sut.GetTopic("overview");

        // Assert
        topic.Should().NotBeNull();
        topic!.Id.Should().Be("overview");
        topic.Title.Should().Be("시스템 개요");
    }

    [Fact]
    public void GetTopic_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var topic = _sut.GetTopic("nonexistent-topic-id");

        // Assert
        topic.Should().BeNull();
    }

    [Fact]
    public void GetContent_WithValidId_ShouldReturnNonEmptyContent()
    {
        // Act
        var content = _sut.GetContent("overview");

        // Assert
        content.Should().NotBeNullOrWhiteSpace("embedded resource should have content");
    }

    [Fact]
    public void GetContent_WithInvalidId_ShouldReturnEmptyString()
    {
        // Act
        var content = _sut.GetContent("nonexistent-topic-id");

        // Assert
        content.Should().BeEmpty("fallback for missing resource should be empty string");
    }

    [Theory]
    [InlineData("overview")]
    [InlineData("getting-started")]
    [InlineData("panel-simulation")]
    [InlineData("fpga-csi2")]
    [InlineData("mcu-udp")]
    [InlineData("host-pipeline")]
    [InlineData("parameters-ref")]
    [InlineData("keyboard-shortcuts")]
    [InlineData("troubleshooting")]
    public void GetContent_AllTopics_ShouldReturnNonEmptyContent(string topicId)
    {
        // Act
        var content = _sut.GetContent(topicId);

        // Assert
        content.Should().NotBeNullOrWhiteSpace($"topic '{topicId}' should have embedded markdown content");
    }

    [Fact]
    public void GetTopics_ShouldReturnReadOnlyList()
    {
        // Act
        var topics = _sut.GetTopics();

        // Assert
        topics.Should().BeAssignableTo<IReadOnlyList<HelpTopic>>();
    }
}
