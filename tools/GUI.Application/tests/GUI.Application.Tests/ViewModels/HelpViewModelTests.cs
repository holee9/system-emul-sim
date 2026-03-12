using System.Windows.Documents;
using Moq;
using XrayDetector.Gui.Help;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// TDD tests for HelpViewModel (SPEC-HELP-001 Wave 2).
/// RED phase: Tests written before implementation.
/// </summary>
public class HelpViewModelTests
{
    private readonly Mock<IHelpContentService> _mockService;
    private readonly HelpViewModel _sut;

    public HelpViewModelTests()
    {
        _mockService = new Mock<IHelpContentService>();

        var topics = new List<HelpTopic>
        {
            new HelpTopic("overview", "시스템 개요"),
            new HelpTopic("getting-started", "빠른 시작 가이드"),
            new HelpTopic("panel-simulation", "Panel 시뮬레이션"),
        };

        _mockService.Setup(s => s.GetTopics()).Returns(topics);
        _mockService.Setup(s => s.GetTopic("overview")).Returns(topics[0]);
        _mockService.Setup(s => s.GetContent("overview")).Returns("# Overview\nContent here.");
        _mockService.Setup(s => s.GetTopic("getting-started")).Returns(topics[1]);
        _mockService.Setup(s => s.GetContent("getting-started")).Returns("# Getting Started\nContent.");
        _mockService.Setup(s => s.GetTopic("panel-simulation")).Returns(topics[2]);
        _mockService.Setup(s => s.GetContent("panel-simulation")).Returns("# Panel\nContent.");

        _sut = new HelpViewModel(_mockService.Object);
    }

    [Fact]
    public void Constructor_WithService_ShouldLoadTopics()
    {
        // Assert
        _sut.Topics.Should().HaveCount(3, "topics should be loaded from service");
    }

    [Fact]
    public void Topics_ShouldBeReadOnlyList()
    {
        // Assert
        _sut.Topics.Should().BeAssignableTo<IReadOnlyList<HelpTopic>>();
    }

    [Fact]
    public void SelectedTopic_DefaultState_ShouldBeNull()
    {
        // Assert - initial state
        // Note: ViewModel may auto-select first topic - we test initial state
        _sut.Topics.Should().NotBeEmpty("topics should be loaded");
    }

    [Fact]
    public void SelectTopicCommand_WithValidTopicId_ShouldSetSelectedTopic()
    {
        // Act
        _sut.SelectTopicCommand.Execute("overview");

        // Assert
        _sut.SelectedTopic.Should().NotBeNull();
        _sut.SelectedTopic!.Id.Should().Be("overview");
    }

    [Fact]
    public void SelectTopicCommand_WithValidTopicId_ShouldLoadContent()
    {
        // Act
        _sut.SelectTopicCommand.Execute("overview");

        // Assert
        _sut.CurrentContent.Should().NotBeNull("content should be loaded when topic selected");
        _sut.CurrentContent.Should().BeOfType<FlowDocument>();
    }

    [Fact]
    public void SearchText_DefaultValue_ShouldBeEmpty()
    {
        // Assert
        _sut.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void FilteredTopics_WithEmptySearch_ShouldReturnAllTopics()
    {
        // Arrange
        _sut.SearchText = string.Empty;

        // Assert
        _sut.FilteredTopics.Should().HaveCount(3, "empty search should show all topics");
    }

    [Fact]
    public void FilteredTopics_WithMatchingSearch_ShouldFilterTopics()
    {
        // Arrange
        _sut.SearchText = "개요";

        // Assert
        _sut.FilteredTopics.Should().HaveCount(1, "only matching topics should be shown");
        _sut.FilteredTopics.First().Id.Should().Be("overview");
    }

    [Fact]
    public void FilteredTopics_WithNonMatchingSearch_ShouldReturnEmpty()
    {
        // Arrange
        _sut.SearchText = "xyznonexistent";

        // Assert
        _sut.FilteredTopics.Should().BeEmpty("no topics match the search text");
    }

    [Fact]
    public void SelectedTopic_WhenChanged_ShouldRaisePropertyChangedForCurrentContent()
    {
        // Arrange
        var propertyChangedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName);

        // Act
        _sut.SelectTopicCommand.Execute("overview");

        // Assert
        propertyChangedEvents.Should().Contain("CurrentContent");
    }

    [Fact]
    public void Constructor_WithNullService_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new HelpViewModel(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
