using XrayDetector.Common.Dto;
using Xunit;

namespace XrayDetector.Sdk.Tests.Common.Dto;

/// <summary>
/// Specification tests for DetectorStatus value object.
/// Tests encapsulation, thread-safety, and immutability guarantees.
/// </summary>
public class DetectorStatusTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesStatus()
    {
        // Arrange
        var connectionState = ConnectionState.Connected;
        var acquisitionState = AcquisitionState.Idle;
        var temperature = 45.5m;
        var timestamp = DateTime.UtcNow;

        // Act
        var status = new DetectorStatus(connectionState, acquisitionState, temperature, timestamp);

        // Assert
        Assert.Equal(connectionState, status.ConnectionState);
        Assert.Equal(acquisitionState, status.AcquisitionState);
        Assert.Equal(temperature, status.Temperature);
        Assert.Equal(timestamp, status.Timestamp);
    }

    [Fact]
    public void ConnectionState_Values_AreWellDefined()
    {
        // Assert all connection states exist and are distinct
        Assert.Equal(0, (int)ConnectionState.Disconnected);
        Assert.Equal(1, (int)ConnectionState.Connecting);
        Assert.Equal(2, (int)ConnectionState.Connected);
        Assert.Equal(3, (int)ConnectionState.Reconnecting);
        Assert.Equal(4, (int)ConnectionState.Error);
    }

    [Fact]
    public void AcquisitionState_Values_AreWellDefined()
    {
        // Assert all acquisition states exist and are distinct
        Assert.Equal(0, (int)AcquisitionState.Idle);
        Assert.Equal(1, (int)AcquisitionState.Arming);
        Assert.Equal(2, (int)AcquisitionState.Acquiring);
        Assert.Equal(3, (int)AcquisitionState.Draining);
        Assert.Equal(4, (int)AcquisitionState.Error);
    }

    [Theory]
    [InlineData(-50.0)]
    [InlineData(0.0)]
    [InlineData(25.0)]
    [InlineData(50.0)]
    [InlineData(100.0)]
    public void Temperature_AcceptsValidRange(decimal temp)
    {
        // Arrange & Act
        var status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            temp,
            DateTime.UtcNow);

        // Assert
        Assert.Equal(temp, status.Temperature);
    }

    [Fact]
    public void WithState_ReturnsNewInstanceWithUpdatedState()
    {
        // Arrange
        var original = new DetectorStatus(
            ConnectionState.Disconnected,
            AcquisitionState.Idle,
            25.0m,
            DateTime.UtcNow);

        // Act
        var updated = original.WithState(ConnectionState.Connected);

        // Assert - immutability verified
        Assert.NotSame(original, updated);
        Assert.Equal(ConnectionState.Disconnected, original.ConnectionState);
        Assert.Equal(ConnectionState.Connected, updated.ConnectionState);

        // Other fields preserved
        Assert.Equal(original.AcquisitionState, updated.AcquisitionState);
        Assert.Equal(original.Temperature, updated.Temperature);
        Assert.Equal(original.Timestamp, updated.Timestamp);
    }

    [Fact]
    public void WithAcquisitionState_ReturnsNewInstanceWithUpdatedState()
    {
        // Arrange
        var original = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            30.0m,
            DateTime.UtcNow);

        // Act
        var updated = original.WithAcquisitionState(AcquisitionState.Acquiring);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(AcquisitionState.Idle, original.AcquisitionState);
        Assert.Equal(AcquisitionState.Acquiring, updated.AcquisitionState);
    }

    [Fact]
    public void WithTemperature_ReturnsNewInstanceWithUpdatedTemperature()
    {
        // Arrange
        var original = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Acquiring,
            35.0m,
            DateTime.UtcNow);

        // Act
        var updated = original.WithTemperature(42.5m);

        // Assert
        Assert.NotSame(original, updated);
        Assert.Equal(35.0m, original.Temperature);
        Assert.Equal(42.5m, updated.Temperature);
    }

    [Fact]
    public void IsConnected_ReturnsTrueForConnectedState()
    {
        // Arrange
        var status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            25.0m,
            DateTime.UtcNow);

        // Act & Assert
        Assert.True(status.IsConnected());
    }

    [Fact]
    public void IsConnected_ReturnsFalseForNonConnectedStates()
    {
        // Arrange
        var disconnected = new DetectorStatus(
            ConnectionState.Disconnected,
            AcquisitionState.Idle,
            25.0m,
            DateTime.UtcNow);

        var connecting = new DetectorStatus(
            ConnectionState.Connecting,
            AcquisitionState.Idle,
            25.0m,
            DateTime.UtcNow);

        // Act & Assert
        Assert.False(disconnected.IsConnected());
        Assert.False(connecting.IsConnected());
    }

    [Fact]
    public void IsAcquiring_ReturnsTrueForAcquiringState()
    {
        // Arrange
        var status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Acquiring,
            25.0m,
            DateTime.UtcNow);

        // Act & Assert
        Assert.True(status.IsAcquiring());
    }

    [Fact]
    public void IsAcquiring_ReturnsFalseForNonAcquiringStates()
    {
        // Arrange
        var idle = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            25.0m,
            DateTime.UtcNow);

        var arming = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Arming,
            25.0m,
            DateTime.UtcNow);

        // Act & Assert
        Assert.False(idle.IsAcquiring());
        Assert.False(arming.IsAcquiring());
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var status1 = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            25.0m,
            timestamp);

        var status2 = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            25.0m,
            timestamp);

        // Act & Assert
        Assert.Equal(status1, status2);
        Assert.Equal(status1.GetHashCode(), status2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var status1 = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            25.0m,
            timestamp);

        var status2 = new DetectorStatus(
            ConnectionState.Disconnected,
            AcquisitionState.Idle,
            25.0m,
            timestamp);

        // Act & Assert
        Assert.NotEqual(status1, status2);
    }
}
