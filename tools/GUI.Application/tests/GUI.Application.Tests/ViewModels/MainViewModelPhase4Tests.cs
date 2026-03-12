using Moq;
using XrayDetector.Gui.ViewModels;
using XrayDetector.Implementation;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// TDD tests for MainViewModel Phase 4 commands (SPEC-HELP-001 Wave 2).
/// RED phase: Tests written before implementation.
/// Tests for SwitchTabCommand, ShowShortcutOverlayCommand, ShowHelpCommand, SelectedTabIndex.
/// </summary>
public class MainViewModelPhase4Tests
{
    private readonly Mock<IDetectorClient> _mockClient;
    private readonly MainViewModel _sut;

    public MainViewModelPhase4Tests()
    {
        _mockClient = new Mock<IDetectorClient>(MockBehavior.Strict);
        _sut = new MainViewModel(_mockClient.Object);
    }

    [Fact]
    public void SelectedTabIndex_DefaultValue_ShouldBeZero()
    {
        // Assert
        _sut.SelectedTabIndex.Should().Be(0, "default tab index should be 0 (Status Dashboard)");
    }

    [Fact]
    public void SwitchTabCommand_ShouldNotBeNull()
    {
        // Assert
        _sut.SwitchTabCommand.Should().NotBeNull("SwitchTabCommand should be initialized");
    }

    [Fact]
    public void SwitchTabCommand_WithTabIndex_ShouldUpdateSelectedTabIndex()
    {
        // Act
        _sut.SwitchTabCommand.Execute("2");

        // Assert
        _sut.SelectedTabIndex.Should().Be(2, "tab should switch to index 2");
    }

    [Fact]
    public void SwitchTabCommand_WithTabIndexZero_ShouldSetZero()
    {
        // Arrange
        _sut.SwitchTabCommand.Execute("3"); // go to 3 first

        // Act
        _sut.SwitchTabCommand.Execute("0");

        // Assert
        _sut.SelectedTabIndex.Should().Be(0);
    }

    [Fact]
    public void ShowShortcutOverlayCommand_ShouldNotBeNull()
    {
        // Assert
        _sut.ShowShortcutOverlayCommand.Should().NotBeNull("ShowShortcutOverlayCommand should be initialized");
    }

    [Fact]
    public void IsShortcutOverlayVisible_DefaultValue_ShouldBeFalse()
    {
        // Assert
        _sut.IsShortcutOverlayVisible.Should().BeFalse("shortcut overlay should be hidden by default");
    }

    [Fact]
    public void ShowShortcutOverlayCommand_ShouldToggleVisibility()
    {
        // Act
        _sut.ShowShortcutOverlayCommand.Execute(null);

        // Assert
        _sut.IsShortcutOverlayVisible.Should().BeTrue("overlay should be visible after first toggle");
    }

    [Fact]
    public void ShowShortcutOverlayCommand_WhenExecutedTwice_ShouldHideOverlay()
    {
        // Act
        _sut.ShowShortcutOverlayCommand.Execute(null);
        _sut.ShowShortcutOverlayCommand.Execute(null);

        // Assert
        _sut.IsShortcutOverlayVisible.Should().BeFalse("overlay should be hidden after second toggle");
    }

    [Fact]
    public void ShowHelpCommand_ShouldNotBeNull()
    {
        // Assert
        _sut.ShowHelpCommand.Should().NotBeNull("ShowHelpCommand should be initialized");
    }

    [Fact]
    public void SelectedTabIndex_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName);

        // Act
        _sut.SwitchTabCommand.Execute("1");

        // Assert
        propertyChangedEvents.Should().Contain("SelectedTabIndex");
    }

    [Fact]
    public void IsShortcutOverlayVisible_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName);

        // Act
        _sut.ShowShortcutOverlayCommand.Execute(null);

        // Assert
        propertyChangedEvents.Should().Contain("IsShortcutOverlayVisible");
    }
}
