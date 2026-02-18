namespace XrayDetector.Common.Dto;

/// <summary>
/// Represents the current state of the detector connection.
/// </summary>
public enum ConnectionState
{
    /// <summary>No active connection.</summary>
    Disconnected = 0,

    /// <summary>Attempting to establish connection.</summary>
    Connecting = 1,

    /// <summary>Connection established and active.</summary>
    Connected = 2,

    /// <summary>Connection interrupted, attempting reconnect.</summary>
    Reconnecting = 3,

    /// <summary>Connection error occurred.</summary>
    Error = 4
}

/// <summary>
/// Represents the current acquisition state of the detector.
/// </summary>
public enum AcquisitionState
{
    /// <summary>Not acquiring frames.</summary>
    Idle = 0,

    /// <summary>Preparing for acquisition.</summary>
    Arming = 1,

    /// <summary>Actively acquiring frames.</summary>
    Acquiring = 2,

    /// <summary>Draining remaining frames after stop.</summary>
    Draining = 3,

    /// <summary>Acquisition error occurred.</summary>
    Error = 4
}

/// <summary>
/// Value object representing the detector status at a point in time.
/// Immutable - use With* methods to create updated instances.
/// </summary>
public sealed class DetectorStatus : IEquatable<DetectorStatus>
{
    /// <summary>Current connection state.</summary>
    public ConnectionState ConnectionState { get; }

    /// <summary>Current acquisition state.</summary>
    public AcquisitionState AcquisitionState { get; }

    /// <summary>Detector temperature in Celsius.</summary>
    public decimal Temperature { get; }

    /// <summary>Timestamp when status was captured.</summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates a new DetectorStatus instance.
    /// </summary>
    public DetectorStatus(
        ConnectionState connectionState,
        AcquisitionState acquisitionState,
        decimal temperature,
        DateTime timestamp)
    {
        ConnectionState = connectionState;
        AcquisitionState = acquisitionState;
        Temperature = temperature;
        Timestamp = timestamp;
    }

    /// <summary>Creates new instance with updated connection state.</summary>
    public DetectorStatus WithState(ConnectionState connectionState) =>
        new(connectionState, AcquisitionState, Temperature, Timestamp);

    /// <summary>Creates new instance with updated acquisition state.</summary>
    public DetectorStatus WithAcquisitionState(AcquisitionState acquisitionState) =>
        new(ConnectionState, acquisitionState, Temperature, Timestamp);

    /// <summary>Creates new instance with updated temperature.</summary>
    public DetectorStatus WithTemperature(decimal temperature) =>
        new(ConnectionState, AcquisitionState, temperature, Timestamp);

    /// <summary>Returns true if connection state is Connected.</summary>
    public bool IsConnected() => ConnectionState == ConnectionState.Connected;

    /// <summary>Returns true if acquisition state is Acquiring.</summary>
    public bool IsAcquiring() => AcquisitionState == AcquisitionState.Acquiring;

    /// <inheritdoc />
    public bool Equals(DetectorStatus? other) =>
        other != null &&
        ConnectionState == other.ConnectionState &&
        AcquisitionState == other.AcquisitionState &&
        Temperature == other.Temperature &&
        Timestamp == other.Timestamp;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DetectorStatus);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(ConnectionState, AcquisitionState, Temperature, Timestamp);
}
