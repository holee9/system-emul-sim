using Moq;
using XrayDetector.Gui.Core;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// Tests for AboutViewModel.
/// Covers DEC-003, DEC-004 decisions.
/// </summary>
public class AboutViewModelTests
{
    private readonly Mock<IClipboardService> _mockClipboard = new();
    private readonly AboutViewModel _sut;

    public AboutViewModelTests()
    {
        _sut = new AboutViewModel(_mockClipboard.Object);
    }

    [Fact]
    public void AppName_ShouldNotBeNullOrEmpty()
    {
        _sut.AppName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Version_ShouldNotBeNullOrEmpty()
    {
        _sut.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BuildDate_ShouldNotBeNullOrEmpty()
    {
        _sut.BuildDate.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DotNetVersion_ShouldNotBeNullOrEmpty()
    {
        _sut.DotNetVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void OSVersion_ShouldNotBeNullOrEmpty()
    {
        _sut.OSVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProcessorCount_ShouldBePositive()
    {
        _sut.ProcessorCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AvailableMemoryMB_ShouldBePositive()
    {
        _sut.AvailableMemoryMB.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CopyToClipboardCommand_ShouldNotBeNull()
    {
        _sut.CopyToClipboardCommand.Should().NotBeNull();
    }

    [Fact]
    public void CopyToClipboardCommand_WhenExecuted_ShouldCallClipboardService()
    {
        // Arrange
        _mockClipboard.Setup(x => x.SetText(It.IsAny<string>())).Verifiable();

        // Act
        _sut.CopyToClipboardCommand.Execute(null);

        // Assert
        _mockClipboard.Verify(x => x.SetText(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void CopyToClipboardCommand_WhenExecuted_ShouldPassTextContainingVersion()
    {
        // Arrange
        string? capturedText = null;
        _mockClipboard.Setup(x => x.SetText(It.IsAny<string>()))
            .Callback<string>(text => capturedText = text);

        // Act
        _sut.CopyToClipboardCommand.Execute(null);

        // Assert
        capturedText.Should().NotBeNull();
        capturedText.Should().Contain(_sut.Version, "clipboard text should contain version");
    }

    [Fact]
    public void CopyToClipboardCommand_WhenExecuted_ShouldPassTextContainingOSInfo()
    {
        // Arrange
        string? capturedText = null;
        _mockClipboard.Setup(x => x.SetText(It.IsAny<string>()))
            .Callback<string>(text => capturedText = text);

        // Act
        _sut.CopyToClipboardCommand.Execute(null);

        // Assert
        capturedText.Should().Contain(_sut.OSVersion, "clipboard text should contain OS info");
    }

    [Fact]
    public void OpenGitHubCommand_ShouldNotBeNull()
    {
        _sut.OpenGitHubCommand.Should().NotBeNull();
    }

    [Fact]
    public void PipelineStatusItems_ShouldContainFourComponents()
    {
        _sut.PipelineStatusItems.Should().HaveCount(4, "Panel, FPGA, MCU, Host");
    }

    [Fact]
    public void PipelineStatusItems_ShouldContainExpectedComponents()
    {
        var names = _sut.PipelineStatusItems.Select(x => x.Name).ToList();
        names.Should().Contain("Panel");
        names.Should().Contain("FPGA");
        names.Should().Contain("MCU");
        names.Should().Contain("Host");
    }
}
