using XrayDetector.Gui.Help;

namespace XrayDetector.Gui.Tests.Help;

/// <summary>
/// TDD tests for HelpTopic record (SPEC-HELP-001 Wave 2).
/// RED phase: Tests written before implementation.
/// </summary>
public class HelpTopicTests
{
    [Fact]
    public void HelpTopic_WithIdAndTitle_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var topic = new HelpTopic("overview", "시스템 개요");

        // Assert
        topic.Id.Should().Be("overview");
        topic.Title.Should().Be("시스템 개요");
        topic.ParentId.Should().BeNull();
        topic.Children.Should().BeEmpty();
    }

    [Fact]
    public void HelpTopic_WithParentId_ShouldSetParentId()
    {
        // Arrange & Act
        var topic = new HelpTopic("child", "Child Topic", "parent");

        // Assert
        topic.ParentId.Should().Be("parent");
    }

    [Fact]
    public void HelpTopic_WithChildren_ShouldContainChildren()
    {
        // Arrange
        var parent = new HelpTopic("parent", "Parent");
        var child = new HelpTopic("child", "Child", "parent");

        // Act
        parent.Children.Add(child);

        // Assert
        parent.Children.Should().HaveCount(1);
        parent.Children[0].Id.Should().Be("child");
    }

    [Fact]
    public void HelpTopic_SameIdAndTitle_ShouldHaveSameValues()
    {
        // Arrange
        var topic1 = new HelpTopic("overview", "시스템 개요");
        var topic2 = new HelpTopic("overview", "시스템 개요");

        // Assert - Records with same primary constructor args have same Id/Title
        // Note: Children is a mutable List so record equality compares by reference
        topic1.Id.Should().Be(topic2.Id);
        topic1.Title.Should().Be(topic2.Title);
        topic1.ParentId.Should().Be(topic2.ParentId);
    }

    [Fact]
    public void HelpTopic_DifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var topic1 = new HelpTopic("overview", "시스템 개요");
        var topic2 = new HelpTopic("getting-started", "시스템 개요");

        // Assert
        topic1.Should().NotBe(topic2);
    }
}
