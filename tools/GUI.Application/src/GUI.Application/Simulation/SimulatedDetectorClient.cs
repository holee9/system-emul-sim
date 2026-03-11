using System.IO;
using XrayDetector.Common.Dto;
using XrayDetector.Implementation;
using XrayDetector.Models;

namespace XrayDetector.Gui.Simulation;

/// <summary>
/// Simulated detector client for demo/development mode.
/// Generates animated X-ray-like frames locally without network hardware.
/// Fires FrameReceived at ~10fps with changing gradient+noise patterns.
/// </summary>
public sealed class SimulatedDetectorClient : IDetectorClient
{
    private const int FrameWidth = 256;
    private const int FrameHeight = 256;
    private const int BitDepth = 16;
    private const int TargetFps = 10;

    private bool _isConnected;
    private bool _isAcquiring;
    private CancellationTokenSource? _acquisitionCts;
    private Task? _acquisitionTask;
    private uint _frameNumber;
    private DetectorStatus? _status;
    private readonly Random _rng = new(42);
    private bool _disposed;

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionChanged;
    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;
#pragma warning disable CS0067 // ErrorOccurred is required by IDetectorClient interface
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;
#pragma warning restore CS0067

    public bool IsConnected => _isConnected;
    public DetectorStatus? Status => _status;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        _isConnected = true;
        _status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            temperature: 25.0m,
            timestamp: DateTime.UtcNow);

        ConnectionChanged?.Invoke(this, new ConnectionStateChangedEventArgs(true, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        StopAcquisitionAsync(cancellationToken).GetAwaiter().GetResult();
        _isConnected = false;
        _status = null;
        ConnectionChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public Task ConfigureAsync(DetectorConfiguration configuration, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task StartAcquisitionAsync(CancellationToken cancellationToken = default)
    {
        if (_isAcquiring) return Task.CompletedTask;

        _isAcquiring = true;
        _acquisitionCts = new CancellationTokenSource();
        _acquisitionTask = Task.Run(() => RunAcquisitionLoop(_acquisitionCts.Token));

        _status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Acquiring,
            temperature: 25.0m,
            timestamp: DateTime.UtcNow);

        return Task.CompletedTask;
    }

    public async Task StopAcquisitionAsync(CancellationToken cancellationToken = default)
    {
        if (!_isAcquiring) return;

        _isAcquiring = false;
        _acquisitionCts?.Cancel();

        if (_acquisitionTask != null)
            await _acquisitionTask.ConfigureAwait(false);

        _acquisitionTask = null;
        _status = new DetectorStatus(
            ConnectionState.Connected,
            AcquisitionState.Idle,
            temperature: 25.0m,
            timestamp: DateTime.UtcNow);
    }

    public Task<Frame> CaptureFrameAsync(CancellationToken cancellationToken = default)
    {
        var frame = GenerateFrame(_frameNumber++);
        return Task.FromResult(frame);
    }

    public async IAsyncEnumerable<Frame> StreamFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return GenerateFrame(_frameNumber++);
            await Task.Delay(1000 / TargetFps, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task SaveFrameAsync(Frame frame, string path, ImageFormat format,
        CancellationToken cancellationToken = default)
    {
        // Write raw 16-bit data with XFRA header (same as PanelSimulator)
        using var fs = File.OpenWrite(path);
        using var bw = new BinaryWriter(fs);
        bw.Write(new byte[] { 0x58, 0x46, 0x52, 0x41 }); // XFRA magic
        bw.Write(1);                                        // version
        bw.Write((ushort)frame.Width);
        bw.Write((ushort)frame.Height);
        foreach (var pixel in frame.PixelData)
            bw.Write(pixel);
        return Task.CompletedTask;
    }

    public Task<DetectorStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = _status ?? new DetectorStatus(
            ConnectionState.Disconnected,
            AcquisitionState.Idle,
            temperature: 0m,
            timestamp: DateTime.UtcNow);
        return Task.FromResult(status);
    }

    private async Task RunAcquisitionLoop(CancellationToken ct)
    {
        int intervalMs = 1000 / TargetFps;
        while (!ct.IsCancellationRequested)
        {
            var frame = GenerateFrame(_frameNumber++);
            FrameReceived?.Invoke(this, new FrameReceivedEventArgs(frame, DateTime.UtcNow));
            try { await Task.Delay(intervalMs, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Generates an animated X-ray-like frame:
    /// - Diagonal gradient that shifts each frame (simulates detector drift)
    /// - Gaussian noise overlay (realistic detector noise)
    /// - Central bright spot that pulses (simulates X-ray beam)
    /// </summary>
    private Frame GenerateFrame(uint frameNumber)
    {
        var pixels = new ushort[FrameWidth * FrameHeight];
        double phase = frameNumber * 0.15;            // animation phase
        double pulseIntensity = 0.5 + 0.5 * Math.Sin(phase * 0.5); // 0..1

        int cx = FrameWidth / 2;
        int cy = FrameHeight / 2;
        double maxDist = Math.Sqrt(cx * cx + cy * cy);

        for (int y = 0; y < FrameHeight; y++)
        {
            for (int x = 0; x < FrameWidth; x++)
            {
                // Diagonal gradient (shifts with phase)
                double gradient = ((x + y + phase * 8) % (FrameWidth + FrameHeight))
                                  / (double)(FrameWidth + FrameHeight);

                // Radial beam pattern (bright center)
                double dx = x - cx;
                double dy = y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                double beam = Math.Exp(-dist * dist / (2.0 * (FrameWidth * 0.15) * (FrameWidth * 0.15)));
                beam *= pulseIntensity;

                // Combine: background gradient + beam
                double signal = 0.3 * gradient + 0.6 * beam + 0.1;
                signal = Math.Clamp(signal, 0.0, 1.0);

                // Scale to 16-bit with Gaussian noise (σ ≈ 150 counts)
                double noise = NextGaussian() * 150.0;
                double rawValue = signal * 58000.0 + noise + 3000.0;
                pixels[y * FrameWidth + x] = (ushort)Math.Clamp(rawValue, 0, 65535);
            }
        }

        var metadata = new FrameMetadata(FrameWidth, FrameHeight, BitDepth, DateTime.UtcNow, frameNumber);
        return new Frame(pixels, metadata);
    }

    private double NextGaussian()
    {
        // Box-Muller transform
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _acquisitionCts?.Cancel();
        _acquisitionCts?.Dispose();
    }
}
