using FluentAssertions;
using XrayDetector.Common.Dto;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// TDD tests for StatusViewModel (RED phase).
/// Tests status dashboard per REQ-TOOLS-041, REQ-TOOLS-045.
/// </summary>
public class StatusViewModelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var viewModel = new StatusViewModel();

        // Assert - REQ-TOOLS-045: Initial status display
        viewModel.ConnectionState.Should().Be("Disconnected");
        viewModel.AcquisitionState.Should().Be("Idle");
        viewModel.FramesReceived.Should().Be(0);
        viewModel.DroppedFrames.Should().Be(0);
        viewModel.ThroughputGbps.Should().BeApproximately(0.0, 0.001);
        viewModel.Temperature.Should().BeApproximately(0.0, 0.1);
        viewModel.IsConnected.Should().BeFalse();
        viewModel.IsAcquiring.Should().BeFalse();
    }

    [Fact]
    public void Update_WithConnectedStatus_ShouldUpdateAllProperties()
    {
        // Arrange
        var viewModel = new StatusViewModel();
        var status = new DetectorStatus(
            connectionState: ConnectionState.Connected,
            acquisitionState: AcquisitionState.Acquiring,
            temperature: 42.5m,
            timestamp: DateTime.UtcNow
        );

        // Act
        viewModel.Update(status, framesReceived: 1000, droppedFrames: 5, throughputGbps: 1.5);

        // Assert - REQ-TOOLS-045: Status dashboard update
        viewModel.ConnectionState.Should().Be("Connected");
        viewModel.AcquisitionState.Should().Be("Acquiring");
        viewModel.FramesReceived.Should().Be(1000);
        viewModel.DroppedFrames.Should().Be(5);
        viewModel.ThroughputGbps.Should().BeApproximately(1.5, 0.01);
        viewModel.Temperature.Should().BeApproximately(42.5, 0.1);
        viewModel.IsConnected.Should().BeTrue();
        viewModel.IsAcquiring.Should().BeTrue();
    }

    [Fact]
    public void Update_WithDisconnectedStatus_ShouldSetDisconnectedStates()
    {
        // Arrange
        var viewModel = new StatusViewModel();
        var status = new DetectorStatus(
            connectionState: ConnectionState.Disconnected,
            acquisitionState: AcquisitionState.Idle,
            temperature: 0m,
            timestamp: DateTime.UtcNow
        );

        // Act
        viewModel.Update(status, framesReceived: 0, droppedFrames: 0, throughputGbps: 0);

        // Assert
        viewModel.ConnectionState.Should().Be("Disconnected");
        viewModel.AcquisitionState.Should().Be("Idle");
        viewModel.IsConnected.Should().BeFalse();
        viewModel.IsAcquiring.Should().BeFalse();
    }

    [Fact]
    public void Update_WithReconnectingStatus_ShouldShowReconnectingState()
    {
        // Arrange
        var viewModel = new StatusViewModel();
        var status = new DetectorStatus(
            connectionState: ConnectionState.Reconnecting,
            acquisitionState: AcquisitionState.Idle,
            temperature: 0m,
            timestamp: DateTime.UtcNow
        );

        // Act
        viewModel.Update(status, framesReceived: 0, droppedFrames: 0, throughputGbps: 0);

        // Assert - REQ-SDK-025: Auto-reconnect status
        viewModel.ConnectionState.Should().Be("Reconnecting...");
        viewModel.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Update_WithAcquisitionStates_ShouldMapCorrectly()
    {
        // Arrange
        var viewModel = new StatusViewModel();

        // Test Arming state
        var armingStatus = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Arming,
            0m,
            DateTime.UtcNow
        );
        viewModel.Update(armingStatus, 0, 0, 0);
        viewModel.AcquisitionState.Should().Be("Arming...");

        // Test Draining state
        var drainingStatus = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Draining,
            0m,
            DateTime.UtcNow
        );
        viewModel.Update(drainingStatus, 0, 0, 0);
        viewModel.AcquisitionState.Should().Be("Draining...");

        // Test Error state
        var errorStatus = new DetectorStatus(
            ConnectionState.Error,
            AcquisitionState.Error,
            0m,
            DateTime.UtcNow
        );
        viewModel.Update(errorStatus, 0, 0, 0);
        viewModel.AcquisitionState.Should().Be("Acquisition Error");
        viewModel.ConnectionState.Should().Be("Connection Error");
    }

    [Fact]
    public void Update_ShouldRaisePropertyChangedForAllProperties()
    {
        // Arrange
        var viewModel = new StatusViewModel();
        var propertiesChanged = new List<string>();

        viewModel.PropertyChanged += (s, e) => propertiesChanged.Add(e.PropertyName!);

        var status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Acquiring,
            42.0m,
            DateTime.UtcNow
        );

        // Act
        viewModel.Update(status, 100, 1, 1.0);

        // Assert
        propertiesChanged.Should().Contain(nameof(StatusViewModel.ConnectionState));
        propertiesChanged.Should().Contain(nameof(StatusViewModel.AcquisitionState));
        propertiesChanged.Should().Contain(nameof(StatusViewModel.FramesReceived));
        propertiesChanged.Should().Contain(nameof(StatusViewModel.DroppedFrames));
        propertiesChanged.Should().Contain(nameof(StatusViewModel.ThroughputGbps));
        propertiesChanged.Should().Contain(nameof(StatusViewModel.Temperature));
        propertiesChanged.Should().Contain(nameof(StatusViewModel.IsConnected));
        propertiesChanged.Should().Contain(nameof(StatusViewModel.IsAcquiring));
    }

    [Fact]
    public void Reset_ShouldRestoreDefaultValues()
    {
        // Arrange
        var viewModel = new StatusViewModel();
        var status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Acquiring,
            42.0m,
            DateTime.UtcNow
        );
        viewModel.Update(status, 1000, 10, 2.0);

        // Act
        viewModel.Reset();

        // Assert
        viewModel.ConnectionState.Should().Be("Disconnected");
        viewModel.AcquisitionState.Should().Be("Idle");
        viewModel.FramesReceived.Should().Be(0);
        viewModel.DroppedFrames.Should().Be(0);
        viewModel.ThroughputGbps.Should().BeApproximately(0.0, 0.001);
        viewModel.Temperature.Should().BeApproximately(0.0, 0.1);
        viewModel.IsConnected.Should().BeFalse();
        viewModel.IsAcquiring.Should().BeFalse();
    }

    [Fact]
    public void DropRatePercentage_ShouldCalculateCorrectly()
    {
        // Arrange
        var viewModel = new StatusViewModel();

        // Act
        viewModel.Update(
            new DetectorStatus(ConnectionState.Connected, AcquisitionState.Acquiring, 0m, DateTime.UtcNow),
            framesReceived: 950,
            droppedFrames: 50,
            throughputGbps: 0
        );

        // Assert
        viewModel.DropRatePercentage.Should().BeApproximately(5.0, 0.01);
    }

    [Fact]
    public void DropRatePercentage_WithNoFrames_ShouldBeZero()
    {
        // Arrange
        var viewModel = new StatusViewModel();

        // Act
        viewModel.Update(
            new DetectorStatus(ConnectionState.Connected, AcquisitionState.Idle, 0m, DateTime.UtcNow),
            framesReceived: 0,
            droppedFrames: 0,
            throughputGbps: 0
        );

        // Assert
        viewModel.DropRatePercentage.Should().BeApproximately(0.0, 0.01);
    }
}
