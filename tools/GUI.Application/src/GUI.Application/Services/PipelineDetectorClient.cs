using System;
using System.Threading;
using System.Threading.Tasks;
using Common.Dto.Dtos;
using IntegrationRunner.Core;
using IntegrationRunner.Core.Models;
using IntegrationRunner.Core.Network;
using XrayDetector.Common.Dto;
using XrayDetector.Core.Processing;
using XrayDetector.Implementation;
using XrayDetector.Models;

namespace XrayDetector.Gui.Services;

/// <summary>
/// IDetectorClient implementation that wraps SimulatorPipeline for in-memory pipeline emulation.
/// REQ-UI-011: Implements IDetectorClient interface, wraps SimulatorPipeline, background frame generation.
/// </summary>
public sealed class PipelineDetectorClient : IDetectorClient
{
    private const int DefaultFps = 10;
    private const decimal DefaultTemperature = 25.0m;
    private const ushort DefaultBitDepth = 16;

    private readonly SimulatorPipeline _pipeline;
    private readonly object _lock = new();

    private bool _isConnected;
    private bool _isAcquiring;
    private bool _disposed;
    private CancellationTokenSource? _acquisitionCts;
    private Task? _acquisitionTask;
    private DetectorConfig? _config;
    private uint _frameNumber;

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionChanged;
    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    public bool IsConnected => _isConnected;
    public DetectorStatus? Status { get; private set; }

    /// <summary>
    /// Creates a new PipelineDetectorClient.
    /// </summary>
    public PipelineDetectorClient()
    {
        _pipeline = new SimulatorPipeline();
    }

    /// <summary>
    /// Updates the pipeline configuration (for UI parameter binding).
    /// Initializes the underlying SimulatorPipeline with the new configuration.
    /// </summary>
    /// <param name="config">Detector configuration containing panel, FPGA, SoC, and simulation settings.</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
    public void UpdateConfig(DetectorConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _pipeline.Initialize(_config);
    }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isConnected)
            return Task.CompletedTask;

        _isConnected = true;
        Status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            temperature: DefaultTemperature,
            timestamp: DateTime.UtcNow);

        ConnectionChanged?.Invoke(this, new ConnectionStateChangedEventArgs(true, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_isConnected)
            return Task.CompletedTask;

        // Stop acquisition if running
        StopAcquisitionAsync(cancellationToken).GetAwaiter().GetResult();

        _isConnected = false;
        Status = null;
        ConnectionChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public Task ConfigureAsync(DetectorConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Map SDK configuration to internal config
        if (_config == null)
        {
            _config = new DetectorConfig
            {
                Panel = new PanelConfig
                {
                    Rows = configuration.Height,
                    Cols = configuration.Width,
                    BitDepth = configuration.BitDepth
                },
                Simulation = new SimulationConfig
                {
                    MaxFrames = 10 // Default for unit tests
                }
            };
            _pipeline.Initialize(_config);
        }

        return Task.CompletedTask;
    }

    public Task StartAcquisitionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isAcquiring)
            return Task.CompletedTask;

        if (!_isConnected)
            throw new InvalidOperationException("Cannot start acquisition: not connected. Call ConnectAsync first.");

        if (_config == null)
            throw new InvalidOperationException("Cannot start acquisition: configuration not set. Call UpdateConfig or ConfigureAsync first.");

        _isAcquiring = true;
        _acquisitionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acquisitionTask = Task.Run(() => RunAcquisitionLoop(_acquisitionCts.Token));

        Status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Acquiring,
            temperature: DefaultTemperature,
            timestamp: DateTime.UtcNow);

        return Task.CompletedTask;
    }

    public async Task StopAcquisitionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_isAcquiring)
            return;

        _isAcquiring = false;
        _acquisitionCts?.Cancel();

        if (_acquisitionTask != null)
        {
            try { await _acquisitionTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { } // Expected on cancellation
        }

        _acquisitionTask = null;
        _acquisitionCts?.Dispose();
        _acquisitionCts = null;

        Status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            temperature: DefaultTemperature,
            timestamp: DateTime.UtcNow);
    }

    public Task<Frame> CaptureFrameAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_config == null)
            throw new InvalidOperationException("Configuration not set. Call UpdateConfig or ConfigureAsync first.");

        var frameData = _pipeline.ProcessFrame();
        if (frameData == null)
            throw new InvalidOperationException("Pipeline failed to generate frame");

        var frame = ConvertToFrame(frameData);
        return Task.FromResult(frame);
    }

    public async IAsyncEnumerable<Frame> StreamFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        while (!cancellationToken.IsCancellationRequested && _isAcquiring)
        {
            var frame = await CaptureFrameAsync(cancellationToken);
            yield return frame;
            await Task.Delay(1000 / DefaultFps, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Saves a frame as TIFF or RAW file using ImageEncoder (SPEC-GUI-001 MVP-4).
    /// </summary>
    public async Task SaveFrameAsync(Frame frame, string path, ImageFormat format,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (frame == null) throw new ArgumentNullException(nameof(frame));
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path cannot be empty", nameof(path));

        var metadata = new FrameMetadata(frame.Width, frame.Height, frame.BitDepth, frame.Timestamp, frame.FrameNumber);
        var encoder = new ImageEncoder();

        switch (format)
        {
            case ImageFormat.Tiff:
                await encoder.EncodeTiffAsync(frame.PixelData, metadata, path, cancellationToken);
                break;
            case ImageFormat.Raw:
                await encoder.EncodeRawAsync(frame.PixelData, metadata, path, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Image format {format} is not supported. Use Tiff or Raw.");
        }
    }

    public Task<DetectorStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var status = Status ?? new DetectorStatus(
            ConnectionState.Disconnected,
            AcquisitionState.Idle,
            temperature: 0m,
            timestamp: DateTime.UtcNow);

        return Task.FromResult(status);
    }

    /// <summary>
    /// Returns a snapshot of the underlying pipeline statistics (SPEC-GUI-001 MVP-2).
    /// </summary>
    public PipelineStatistics GetStatistics() => _pipeline.GetStatistics();

    /// <summary>
    /// Updates network impairment parameters on the running pipeline (SPEC-GUI-002 Ethernet tab).
    /// </summary>
    public void UpdateNetworkConfig(double lossRate, double reorderRate, double corruptionRate,
        int minDelayMs = 0, int maxDelayMs = 0)
    {
        _pipeline.SetPacketLossRate(lossRate);
        _pipeline.SetPacketReorderRate(reorderRate);
        _pipeline.SetDelayRange(minDelayMs, maxDelayMs);
    }

    /// <summary>
    /// Runs the acquisition loop in a background task.
    /// Generates frames at the configured FPS and fires FrameReceived events.
    /// </summary>
    private async Task RunAcquisitionLoop(CancellationToken ct)
    {
        int intervalMs = 1000 / DefaultFps;

        while (!ct.IsCancellationRequested && _isAcquiring)
        {
            try
            {
                var frameData = _pipeline.ProcessFrame();
                if (frameData != null)
                {
                    var frame = ConvertToFrame(frameData);
                    FrameReceived?.Invoke(this, new FrameReceivedEventArgs(frame, DateTime.UtcNow));
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                    $"Frame generation error: {ex.Message}", ex, DateTime.UtcNow));
            }

            await Task.Delay(intervalMs, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Converts Pipeline FrameData to SDK Frame format.
    /// </summary>
    private Frame ConvertToFrame(FrameData frameData)
    {
        var metadata = new FrameMetadata(
            width: frameData.Width,
            height: frameData.Height,
            bitDepth: DefaultBitDepth,
            timestamp: DateTime.UtcNow,
            frameNumber: _frameNumber++);

        // FrameData already has ushort[] Pixels
        return new Frame(frameData.Pixels, metadata);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PipelineDetectorClient));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _acquisitionCts?.Cancel();
        _acquisitionCts?.Dispose();
        _isAcquiring = false;
    }
}
