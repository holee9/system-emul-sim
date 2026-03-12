using Moq;
using XrayDetector.Implementation;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// Tests for new commands and properties added to MainViewModel in SPEC-HELP-001 Wave 1.
/// </summary>
public class MainViewModelNewCommandsTests
{
    private readonly Mock<IDetectorClient> _mockClient = new(MockBehavior.Loose);
    private readonly MainViewModel _sut;

    public MainViewModelNewCommandsTests()
    {
        _sut = new MainViewModel(_mockClient.Object);
    }

    [Fact]
    public void IsStatusBarVisible_DefaultsToTrue()
    {
        _sut.IsStatusBarVisible.Should().BeTrue("status bar should be visible by default");
    }

    [Fact]
    public void IsFullScreen_DefaultsToFalse()
    {
        _sut.IsFullScreen.Should().BeFalse("full screen should be off by default");
    }

    [Fact]
    public void AppVersion_ShouldNotBeEmpty()
    {
        _sut.AppVersion.Should().NotBeNullOrEmpty("app version should come from ApplicationInfo");
    }

    [Fact]
    public void ExitCommand_ShouldNotBeNull()
    {
        _sut.ExitCommand.Should().NotBeNull();
    }

    [Fact]
    public void ShowAboutCommand_ShouldNotBeNull()
    {
        _sut.ShowAboutCommand.Should().NotBeNull();
    }

    [Fact]
    public void ToggleStatusBarCommand_ShouldNotBeNull()
    {
        _sut.ToggleStatusBarCommand.Should().NotBeNull();
    }

    [Fact]
    public void ToggleFullScreenCommand_ShouldNotBeNull()
    {
        _sut.ToggleFullScreenCommand.Should().NotBeNull();
    }

    [Fact]
    public void ToggleStatusBarCommand_WhenExecuted_ShouldToggleIsStatusBarVisible()
    {
        // Arrange
        _sut.IsStatusBarVisible.Should().BeTrue();

        // Act
        _sut.ToggleStatusBarCommand.Execute(null);

        // Assert
        _sut.IsStatusBarVisible.Should().BeFalse("should toggle to false");

        // Act again
        _sut.ToggleStatusBarCommand.Execute(null);

        // Assert
        _sut.IsStatusBarVisible.Should().BeTrue("should toggle back to true");
    }

    [Fact]
    public void ToggleStatusBarCommand_WhenExecuted_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertiesChanged = new List<string?>();
        _sut.PropertyChanged += (_, e) => propertiesChanged.Add(e.PropertyName);

        // Act
        _sut.ToggleStatusBarCommand.Execute(null);

        // Assert
        propertiesChanged.Should().Contain(nameof(MainViewModel.IsStatusBarVisible));
    }

    [Fact]
    public void ToggleFullScreenCommand_WhenExecuted_ShouldToggleIsFullScreen()
    {
        // Arrange
        _sut.IsFullScreen.Should().BeFalse();

        // Act
        _sut.ToggleFullScreenCommand.Execute(null);

        // Assert
        _sut.IsFullScreen.Should().BeTrue("should toggle to true");
    }

    [Fact]
    public void ToggleFullScreenCommand_WhenExecuted_ShouldRaiseFullScreenRequestedEvent()
    {
        // Arrange
        bool? receivedValue = null;
        _sut.FullScreenRequested += (isFullScreen) => receivedValue = isFullScreen;

        // Act
        _sut.ToggleFullScreenCommand.Execute(null);

        // Assert
        receivedValue.Should().BeTrue("event should fire with true when going full screen");
    }
}
