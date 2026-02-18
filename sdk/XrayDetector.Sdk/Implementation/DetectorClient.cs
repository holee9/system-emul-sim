using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Net.Sockets;
using System.Threading.Channels;
using XrayDetector.Common.Dto;
using XrayDetector.Core.Communication;
using XrayDetector.Core.Processing;
using XrayDetector.Core.Reassembly;
using XrayDetector.Models;

namespace XrayDetector.Implementation;

/// <summary>
/// X-ray detector client implementation.
/// Manages connection, configuration, and frame acquisition.
/// </summary>
public sealed class DetectorClient : IDetectorClient
{
    private readonly IUdpSocketClient _socketClient;
    private readonly FrameReassembler _reassembler;
    private readonly ArrayPool<ushort> _pool;
    private readonly Channel<Frame> _frameChannel;
    private readonly CancellationTokenSource _internalCts;

    private bool _disposed;
    private bool _isConnected;
    private bool _isAcquiring;
    private DetectorStatus? _status;
    private Task? _receiveTask;

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionChanged;
    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    public bool IsConnected => _isConnected;
    public DetectorStatus? Status => _status;

    public DetectorClient()
        : this(new UdpSocketClient("localhost", 8001), ArrayPool<ushort>.Shared)
    {
    }

    public DetectorClient(IUdpSocketClient socketClient, ArrayPool<ushort>? pool)
    {
        _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
        _pool = pool ?? ArrayPool<ushort>.Shared;
        _reassembler = new FrameReassembler(_pool);
        _frameChannel = Channel.CreateUnbounded<Frame>();
        _internalCts = new CancellationTokenSource();
        _disposed = false;
        _isConnected = false;
        _isAcquiring = false;
    }

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Note: IUdpSocketClient.ConnectAsync doesn't take host/port parameters
            // The implementation needs to handle this differently
            // For now, this is a placeholder
            _isConnected = true;
            OnConnectionChanged(isConnected: true);

            // Start receiving packets
            _receiveTask = Task.Run(() => ReceivePacketsAsync(_internalCts.Token));
        }
        catch (Exception ex)
        {
            OnError($"Failed to connect: {ex.Message}", ex);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await StopAcquisitionAsync(cancellationToken);
            _internalCts.Cancel();

            if (_receiveTask != null)
            {
                await _receiveTask;
            }

            await _socketClient.DisconnectAsync();
            _isConnected = false;
            OnConnectionChanged(isConnected: false);
        }
        catch (Exception ex)
        {
            OnError($"Failed to disconnect: {ex.Message}", ex);
            throw;
        }
    }

    public async Task ConfigureAsync(DetectorConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        try
        {
            byte[] payload = SerializeConfiguration(configuration);
            var command = new UdpCommand(
                commandType: UdpCommandType.Config,
                payload: payload,
                sequenceNumber: 0
            );

            byte[] commandData = command.Serialize();
            await _socketClient.SendAsync(commandData, cancellationToken);

            // Update status
            _status = new DetectorStatus(
                connectionState: ConnectionState.Connected,
                acquisitionState: _isAcquiring ? AcquisitionState.Acquiring : AcquisitionState.Idle,
                temperature: 0,
                timestamp: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            OnError($"Failed to configure: {ex.Message}", ex);
            throw;
        }
    }

    public async Task StartAcquisitionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        try
        {
            var command = UdpCommand.CreateStartAcquisition(Array.Empty<byte>(), 0);
            byte[] commandData = command.Serialize();
            await _socketClient.SendAsync(commandData, cancellationToken);
            _isAcquiring = true;
        }
        catch (Exception ex)
        {
            OnError($"Failed to start acquisition: {ex.Message}", ex);
            throw;
        }
    }

    public async Task StopAcquisitionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            if (_isAcquiring)
            {
                var command = UdpCommand.CreateStopAcquisition(0);
                byte[] commandData = command.Serialize();
                await _socketClient.SendAsync(commandData, cancellationToken);
                _isAcquiring = false;
            }
        }
        catch (Exception ex)
        {
            OnError($"Failed to stop acquisition: {ex.Message}", ex);
            throw;
        }
    }

    public async Task<Frame> CaptureFrameAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        try
        {
            // Request single frame
            var command = UdpCommand.CreatePing(0);
            byte[] commandData = command.Serialize();
            await _socketClient.SendAsync(commandData, cancellationToken);

            // Wait for frame from channel
            var frame = await _frameChannel.Reader.ReadAsync(cancellationToken);
            return frame;
        }
        catch (Exception ex)
        {
            OnError($"Failed to capture frame: {ex.Message}", ex);
            throw;
        }
    }

    public async IAsyncEnumerable<Frame> StreamFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        await StartAcquisitionAsync(cancellationToken);

        try
        {
            await foreach (var frame in _frameChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return frame;
            }
        }
        finally
        {
            await StopAcquisitionAsync(CancellationToken.None);
        }
    }

    public async Task SaveFrameAsync(Frame frame, string path, ImageFormat format, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (frame == null)
            throw new ArgumentNullException(nameof(frame));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        // Validate path and prevent directory traversal attacks
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Directory does not exist: {directory}");

        // Check for path traversal attempts
        if (path.Contains("..") || path.Contains("~"))
            throw new ArgumentException("Path traversal detected", nameof(path));

        try
        {
            var encoder = new ImageEncoder();
            var metadata = new FrameMetadata(
                frame.Width,
                frame.Height,
                frame.BitDepth,
                frame.Timestamp,
                frame.FrameNumber
            );

            if (format == ImageFormat.Tiff)
            {
                await encoder.EncodeTiffAsync(frame.PixelData, metadata, path, cancellationToken);
            }
            else if (format == ImageFormat.Raw)
            {
                await encoder.EncodeRawAsync(frame.PixelData, metadata, path, cancellationToken);
            }
            else
            {
                throw new NotSupportedException($"Format {format} is not yet supported");
            }
        }
        catch (Exception ex)
        {
            OnError($"Failed to save frame: {ex.Message}", ex);
            throw;
        }
    }

    public async Task<DetectorStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        try
        {
            var command = UdpCommand.CreatePing(0);
            byte[] commandData = command.Serialize();
            await _socketClient.SendAsync(commandData, cancellationToken);

            return _status ?? throw new InvalidOperationException("Status not available");
        }
        catch (Exception ex)
        {
            OnError($"Failed to get status: {ex.Message}", ex);
            throw;
        }
    }

    private async Task ReceivePacketsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                try
                {
                    var result = await _socketClient.ReceiveAsync(cancellationToken);
                    var packet = result.Buffer; // UdpReceiveResult has Buffer property
                    var frameResult = _reassembler.ProcessPacket(packet);

                    if (frameResult.Status == ReassemblyStatus.Complete && frameResult.FrameData != null)
                    {
                        var frame = CreateFrame(frameResult);
                        await _frameChannel.Writer.WriteAsync(frame, cancellationToken);
                        OnFrameReceived(frame);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError($"Error receiving packet: {ex.Message}", ex);
                }
            }
        }
        finally
        {
            _frameChannel.Writer.Complete();
        }
    }

    private Frame CreateFrame(FrameReassemblyResult result)
    {
        if (result.FrameData == null)
            throw new ArgumentException("Frame data cannot be null", nameof(result));

        // Calculate frame dimensions from frame data length
        // Assuming square frame for now (can be improved with actual metadata from packet)
        int pixelCount = result.FrameData.Length;
        int width = (int)Math.Sqrt(pixelCount);
        int height = width;

        var metadata = new FrameMetadata(
            width: width,
            height: height,
            bitDepth: 16,
            timestamp: DateTime.UtcNow,
            frameNumber: result.FrameNumber
        );

        ushort[] frameData = _pool.Rent(result.FrameData.Length);
        Array.Copy(result.FrameData, frameData, result.FrameData.Length);

        return new Frame(frameData, metadata, _pool);
    }

    private static byte[] SerializeConfiguration(DetectorConfiguration config)
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(config.Width);
        writer.Write(config.Height);
        writer.Write(config.BitDepth);
        writer.Write(config.FrameRate);
        writer.Write(config.Gain);
        writer.Write(config.Offset);

        return ms.ToArray();
    }

    private void EnsureConnected()
    {
        if (!_isConnected)
            throw new InvalidOperationException("Not connected to detector");
    }

    private void OnConnectionChanged(bool isConnected)
    {
        ConnectionChanged?.Invoke(this, new ConnectionStateChangedEventArgs(isConnected, DateTime.UtcNow));
    }

    private void OnFrameReceived(Frame frame)
    {
        FrameReceived?.Invoke(this, new FrameReceivedEventArgs(frame, DateTime.UtcNow));
    }

    private void OnError(string message, Exception? exception)
    {
        ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(message, exception, DateTime.UtcNow));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _internalCts.Cancel();
        _receiveTask?.Wait(TimeSpan.FromSeconds(1));
        _internalCts.Dispose();
        _socketClient?.Dispose();

        _disposed = true;
    }
}
