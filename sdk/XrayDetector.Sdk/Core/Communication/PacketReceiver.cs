using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Threading;

namespace XrayDetector.Core.Communication;

/// <summary>
/// High-performance packet receiver using System.IO.Pipelines.
/// Processes incoming UDP packets with minimal allocations.
/// </summary>
public sealed class PacketReceiver : IDisposable
{
    private readonly IUdpSocketClient _client;
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;
    private int _state = (int)PacketReceiverState.Stopped;

    /// <summary>Current receiver state.</summary>
    public PacketReceiverState State => (PacketReceiverState)Volatile.Read(ref _state);

    /// <summary>True if receiver is actively running.</summary>
    public bool IsReceiving => State == PacketReceiverState.Running;

    /// <summary>Event raised when a packet is received.</summary>
    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

    /// <summary>Event raised when an error occurs.</summary>
    public event EventHandler<PacketErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Creates a new packet receiver.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when client is null.
    /// </exception>
    public PacketReceiver(IUdpSocketClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Starts the packet receiver.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when client is not connected or receiver is already running.
    /// </exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
            throw new InvalidOperationException("UDP client is not connected.");

        if (IsReceiving)
            return; // Already running

        SetState(PacketReceiverState.Running);

        // Start receiving in background
        _receiveTask = Task.Run(() => ReceiveLoopAsync(cancellationToken));

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the packet receiver.
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsReceiving)
            return; // Already stopped

        SetState(PacketReceiverState.Stopping);
        _cts.Cancel();

        if (_receiveTask != null)
        {
            await _receiveTask;
            _receiveTask = null;
        }

        SetState(PacketReceiverState.Stopped);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cts.Token);

        try
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _client.ReceiveAsync(linkedCts.Token);
                    OnPacketReceived(result.Buffer);
                }
                catch (OperationCanceledException)
                {
                    break; // Normal shutdown
                }
                catch (Exception ex)
                {
                    OnErrorOccurred(ex);
                    break;
                }
            }
        }
        finally
        {
            if (State == PacketReceiverState.Running)
                SetState(PacketReceiverState.Stopped);
        }
    }

    private void OnPacketReceived(byte[] packet)
    {
        PacketReceived?.Invoke(this, new PacketReceivedEventArgs(packet));
    }

    private void OnErrorOccurred(Exception exception)
    {
        ErrorOccurred?.Invoke(this, new PacketErrorEventArgs(exception));
    }

    private void SetState(PacketReceiverState newState)
    {
        Volatile.Write(ref _state, (int)newState);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsReceiving)
        {
            StopAsync().GetAwaiter().GetResult();
        }

        _cts.Dispose();
    }
}

/// <summary>Event arguments for packet received event.</summary>
public sealed class PacketReceivedEventArgs : EventArgs
{
    public byte[] Packet { get; }
    public DateTime Timestamp { get; }

    public PacketReceivedEventArgs(byte[] packet)
    {
        Packet = packet;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>Event arguments for packet error event.</summary>
public sealed class PacketErrorEventArgs : EventArgs
{
    public Exception Exception { get; }
    public DateTime Timestamp { get; }

    public PacketErrorEventArgs(Exception exception)
    {
        Exception = exception;
        Timestamp = DateTime.UtcNow;
    }
}
