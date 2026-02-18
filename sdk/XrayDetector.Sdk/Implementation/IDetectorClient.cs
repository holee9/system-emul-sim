using XrayDetector.Common.Dto;
using XrayDetector.Models;

namespace XrayDetector.Implementation;

/// <summary>
/// Client interface for X-ray detector communication.
/// Provides methods for connecting, configuring, and acquiring frames.
/// </summary>
public interface IDetectorClient : IDisposable
{
    /// <summary>Connection state changed event.</summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionChanged;

    /// <summary>Frame received event during streaming.</summary>
    event EventHandler<FrameReceivedEventArgs>? FrameReceived;

    /// <summary>Error occurred event.</summary>
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    /// <summary>Current connection state.</summary>
    bool IsConnected { get; }

    /// <summary>Current detector status.</summary>
    DetectorStatus? Status { get; }

    /// <summary>
    /// Connects to the detector at the specified endpoint.
    /// </summary>
    Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the detector.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures the detector with the specified parameters.
    /// </summary>
    Task ConfigureAsync(DetectorConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts frame acquisition.
    /// </summary>
    Task StartAcquisitionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops frame acquisition.
    /// </summary>
    Task StopAcquisitionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures a single frame synchronously.
    /// </summary>
    Task<Frame> CaptureFrameAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams frames asynchronously as they are received.
    /// </summary>
    IAsyncEnumerable<Frame> StreamFramesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a frame to disk in the specified format.
    /// </summary>
    Task SaveFrameAsync(Frame frame, string path, ImageFormat format, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current detector status.
    /// </summary>
    Task<DetectorStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Connection state changed event arguments.
/// </summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    public bool IsConnected { get; }
    public DateTime Timestamp { get; }

    public ConnectionStateChangedEventArgs(bool isConnected, DateTime timestamp)
    {
        IsConnected = isConnected;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Frame received event arguments.
/// </summary>
public sealed class FrameReceivedEventArgs : EventArgs
{
    public Frame Frame { get; }
    public DateTime Timestamp { get; }

    public FrameReceivedEventArgs(Frame frame, DateTime timestamp)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Timestamp = timestamp;
    }
}

/// <summary>
/// Error occurred event arguments.
/// </summary>
public sealed class ErrorOccurredEventArgs : EventArgs
{
    public string Message { get; }
    public Exception? Exception { get; }
    public DateTime Timestamp { get; }

    public ErrorOccurredEventArgs(string message, Exception? exception, DateTime timestamp)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Exception = exception;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Detector configuration parameters.
/// </summary>
public sealed class DetectorConfiguration
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int BitDepth { get; set; }
    public int FrameRate { get; set; }
    public uint Gain { get; set; }
    public uint Offset { get; set; }

    public DetectorConfiguration(int width, int height, int bitDepth, int frameRate, uint gain, uint offset)
    {
        Width = width;
        Height = height;
        BitDepth = bitDepth;
        FrameRate = frameRate;
        Gain = gain;
        Offset = offset;
    }
}

/// <summary>
/// Image format for saving frames.
/// </summary>
public enum ImageFormat
{
    Tiff,
    Raw,
    Jpeg,
    Png
}
