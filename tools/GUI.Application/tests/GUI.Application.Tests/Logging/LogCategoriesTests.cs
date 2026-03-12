using XrayDetector.Gui.Logging;

namespace XrayDetector.Gui.Tests.Logging;

/// <summary>
/// Tests for LogCategories constants.
/// </summary>
public class LogCategoriesTests
{
    [Fact]
    public void Pipeline_ShouldHaveExpectedValue()
    {
        LogCategories.Pipeline.Should().Be("XrayDetector.Gui.Pipeline");
    }

    [Fact]
    public void UI_ShouldHaveExpectedValue()
    {
        LogCategories.UI.Should().Be("XrayDetector.Gui.UI");
    }

    [Fact]
    public void Performance_ShouldHaveExpectedValue()
    {
        LogCategories.Performance.Should().Be("XrayDetector.Gui.Performance");
    }

    [Fact]
    public void UserAction_ShouldHaveExpectedValue()
    {
        LogCategories.UserAction.Should().Be("XrayDetector.Gui.UserAction");
    }

    [Fact]
    public void Help_ShouldHaveExpectedValue()
    {
        LogCategories.Help.Should().Be("XrayDetector.Gui.Help");
    }

    [Fact]
    public void App_ShouldHaveExpectedValue()
    {
        LogCategories.App.Should().Be("XrayDetector.Gui.App");
    }
}
